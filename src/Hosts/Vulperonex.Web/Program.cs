using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Logging;
using Vulperonex.Web.Ports;
using Vulperonex.Web.Endpoints;
using Vulperonex.Web;
using Vulperonex.Web.Logging;
using Vulperonex.Web.SignalR;

var builder = VulperonexWebApplication.CreateBuilder(args);
var app = VulperonexWebApplication.Build(builder);

await app.RunAsync();

public static partial class VulperonexWebApplication
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        return CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = ResolveWebRootPath(),
        });
    }

    public static WebApplicationBuilder CreateBuilder(WebApplicationOptions options, bool configureDefaultLoopbackPorts = true)
    {
        var builder = WebApplication.CreateBuilder(options);

        builder.Services.AddVulperonexWeb();
        builder.Services.AddSingleton<Vulperonex.Web.Security.AdminCsrfTokenProvider>();
        if (configureDefaultLoopbackPorts)
        {
            ConfigureDefaultLoopbackPorts(builder);
        }

        return builder;
    }

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        ConfigureSerilog(builder);
        var app = builder.Build();

        var levelSwitch = app.Services.GetRequiredService<Serilog.Core.LoggingLevelSwitch>();
        var broker = app.Services.GetRequiredService<Vulperonex.Infrastructure.Settings.SystemSettingsBroker>();
        var hotReload = SerilogConfigurator.BindHotReload(levelSwitch, broker);
        app.Lifetime.ApplicationStopping.Register(hotReload.Dispose);

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseMiddleware<Vulperonex.Web.Middleware.AdminGuardMiddleware>();

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode != StatusCodes.Status302Found && 
                    context.Response.StatusCode != StatusCodes.Status301MovedPermanently)
                {
                    var contentType = context.Response.ContentType;
                    var isHtml = contentType != null && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);

                    var path = context.Request.Path.Value;
                    var isOverlay = path != null && path.StartsWith("/overlay", StringComparison.OrdinalIgnoreCase);

                    var isDevException = app.Environment.IsDevelopment() && context.Response.StatusCode >= 500;

                    if ((isHtml || isOverlay) && !isDevException)
                    {
                        context.Response.Headers["Content-Security-Policy"] = BuildContentSecurityPolicy(isOverlay);
                        
                        // Disable HTTP Cache for all HTML and Overlay pages to enforce instant OBS updates
                        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                        context.Response.Headers["Pragma"] = "no-cache";
                        context.Response.Headers["Expires"] = "0";
                    }
                }
                return Task.CompletedTask;
            });
            await next();
        });

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var path = ctx.Context.Request.Path.Value;
                if (path != null && path.StartsWith("/overlay", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                    ctx.Context.Response.Headers["Pragma"] = "no-cache";
                    ctx.Context.Response.Headers["Expires"] = "0";
                }
            }
        });

        // Retrieve admin CSRF token (restricted by loopback and host allow-list)
        // 【Security Architecture Decision & Trade-offs】:
        // 1. Every time Kestrel restarts, AdminCsrfTokenProvider generates a fresh random session token.
        //    This means any open browser tabs for admin pages must be reloaded to fetch the new token.
        // 2. This endpoint does not require further inter-process authentication; other trusted processes running
        //    on the same local machine can fetch this token. This is a known and accepted compromise
        //    of the security boundary for local desktop loopback applications.
        app.MapGet("/api/overlay/csrf-token", (Vulperonex.Web.Security.AdminCsrfTokenProvider tokenProvider) => 
        {
            return Results.Ok(new { token = tokenProvider.Token });
        });

        app.MapOpenApi("/openapi/v1.json");
        app.MapHealthEndpoints();
        app.MapWorkflowRuleEndpoints();
        app.MapWorkflowTimerEndpoints();
        app.MapEventTypeEndpoints();
        app.MapMetadataEndpoints();
        app.MapConfigEndpoints();
        app.MapMemberEndpoints();
        app.MapPluginModuleEndpoints();
        app.MapSimulateEndpoints();
        app.MapTwitchAuthEndpoints();
        app.MapTwitchBadgesEndpoints();
        app.MapOAuthCallbackEndpoints();
        app.MapOverlayHistoryEndpoints();
        app.MapOverlayPresetEndpoints();
        app.MapChatOutboxEndpoints();
        app.MapOverlayHubs();
        app.MapFallback(ServeSpaIndexAsync);

        return app;
    }

    private static async Task ServeSpaIndexAsync(HttpContext context)
    {
        if (IsBackendPath(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var indexFile = environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!indexFile.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexFile, context.RequestAborted);
    }

    private static bool IsBackendPath(PathString path)
    {
        return path.StartsWithSegments("/api")
            || path.StartsWithSegments("/hubs")
            || path.StartsWithSegments("/openapi")
            || path.StartsWithSegments("/auth")
            || path.Equals("/health");
    }

    private static string BuildContentSecurityPolicy(bool allowSameOriginFraming)
    {
        var frameAncestors = allowSameOriginFraming ? "'self'" : "'none'";

        return $"default-src 'self'; connect-src 'self' ws://localhost:* wss://localhost:* ws://127.0.0.1:* wss://127.0.0.1:*; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; img-src 'self' data: https://static-cdn.jtvnw.net; font-src 'self' data: https://fonts.gstatic.com; frame-ancestors {frameAncestors}; object-src 'none'; base-uri 'self';";
    }

    private static string ResolveWebRootPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Hosts", "Vulperonex.Web", "wwwroot"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static void ConfigureSerilog(WebApplicationBuilder builder)
    {
        var configuredLevel = builder.Configuration["Log:MinLevel"];
        var levelSwitch = SerilogConfigurator.CreateLevelSwitch(configuredLevel);
        builder.Services.AddSingleton(levelSwitch);

        builder.Host.UseSerilog((context, serviceProvider, loggerConfiguration) =>
        {
            var sink = serviceProvider.GetRequiredService<AppLogsSink>();
            var directory = SerilogConfigurator.ResolveLogDirectory();
            SerilogConfigurator.ConfigureLogger(loggerConfiguration, levelSwitch, sink, directory);
        });
    }

    private static void ConfigureDefaultLoopbackPorts(WebApplicationBuilder builder)
    {
        var options = new PortAllocationOptions();
        var ports = new PortPairAllocator(new SocketPortAvailabilityProbe(), options).TryAllocate()
            ?? throw new PortExhaustedException(options);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, ports.ApiPort);
            options.Listen(IPAddress.IPv6Loopback, ports.ApiPort);
            options.Listen(IPAddress.Loopback, ports.OverlayPort);
            options.Listen(IPAddress.IPv6Loopback, ports.OverlayPort);
        });
    }
}
