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
        builder.Services.AddSingleton<Vulperonex.Web.Security.OverlayLanAccessKeyProvider>();
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

        // Apply migrations before the host (and thus every hosted service) starts.
        // Several workers read SystemSettings via EF as soon as their ExecuteAsync
        // runs; running the migration here removes any dependency on hosted-service
        // registration order.
        MigrateDatabase(app);

        var levelSwitch = app.Services.GetRequiredService<Serilog.Core.LoggingLevelSwitch>();
        var broker = app.Services.GetRequiredService<Vulperonex.Infrastructure.Settings.SystemSettingsBroker>();
        var hotReload = SerilogConfigurator.BindHotReload(levelSwitch, broker);
        app.Lifetime.ApplicationStopping.Register(hotReload.Dispose);

        EnsureOverlayLanAccessKey(app);

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
                        context.Response.Headers["Content-Security-Policy"] = BuildContentSecurityPolicy(isOverlay, context.Request.Host);
                        
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
        app.MapTwitchRewardsEndpoints();
        app.MapOAuthCallbackEndpoints();
        app.MapOverlayHistoryEndpoints();
        app.MapOverlayPresetEndpoints();
        app.MapChatOutboxEndpoints();
        app.MapOverlayLanEndpoints();
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

    private static string BuildContentSecurityPolicy(bool allowSameOriginFraming, HostString host)
    {
        var frameAncestors = allowSameOriginFraming ? "'self'" : "'none'";

        // SignalR connects to the same host the page was loaded from. For cross-machine OBS that host
        // is a LAN address, so derive the ws/wss source from the request Host rather than hard-coding
        // localhost — otherwise the overlay's hub connection is blocked by CSP.
        var hostWs = host.HasValue
            ? $" ws://{host.Value} wss://{host.Value}"
            : string.Empty;

        return $"default-src 'self'; connect-src 'self' ws://localhost:* wss://localhost:* ws://127.0.0.1:* wss://127.0.0.1:*{hostWs}; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; img-src 'self' data: https://static-cdn.jtvnw.net; font-src 'self' data: https://fonts.gstatic.com; frame-ancestors {frameAncestors}; object-src 'none'; base-uri 'self';";
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

    private static void MigrateDatabase(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<Vulperonex.Infrastructure.Data.DatabaseBootstrapper>();
        bootstrapper.MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void EnsureOverlayLanAccessKey(WebApplication app)
    {
        // Only seed/persist the key when cross-machine overlay access is enabled. When disabled the
        // provider stays empty and the middleware rejects every non-loopback request regardless.
        if (!app.Configuration.GetValue("Overlay:Lan:Enabled", false))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var provider = app.Services.GetRequiredService<Vulperonex.Web.Security.OverlayLanAccessKeyProvider>();

        var key = settings.GetAsync<string?>(SystemSettingKey.OverlayLanAccessKey, null).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Vulperonex.Web.Security.OverlayLanAccessKeyProvider.GenerateKey();
            settings.SetAsync(SystemSettingKey.OverlayLanAccessKey, key, "overlay-lan").GetAwaiter().GetResult();
        }

        provider.SetKey(key);
    }

    private static void ConfigureDefaultLoopbackPorts(WebApplicationBuilder builder)
    {
        var options = new PortAllocationOptions();
        var ports = new PortPairAllocator(new SocketPortAvailabilityProbe(), options).TryAllocate()
            ?? throw new PortExhaustedException(options);

        // Expose the allocated ports so the admin UI can render the OBS overlay URL.
        builder.Services.AddSingleton(ports);

        var lanEnabled = builder.Configuration.GetValue("Overlay:Lan:Enabled", false);
        var lanBindAddress = builder.Configuration["Overlay:Lan:BindAddress"] ?? "0.0.0.0";

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // API port is always loopback-only — admin/mutating/auth surface never reaches the LAN.
            kestrel.Listen(IPAddress.Loopback, ports.ApiPort);
            kestrel.Listen(IPAddress.IPv6Loopback, ports.ApiPort);

            // Overlay port: always loopback (admin preview), plus optional LAN bind for cross-machine OBS.
            kestrel.Listen(IPAddress.Loopback, ports.OverlayPort);
            kestrel.Listen(IPAddress.IPv6Loopback, ports.OverlayPort);

            if (lanEnabled && TryParseBindAddress(lanBindAddress, out var bindIp)
                && !IPAddress.IsLoopback(bindIp))
            {
                kestrel.Listen(bindIp, ports.OverlayPort);
            }
        });
    }

    private static bool TryParseBindAddress(string value, out IPAddress address)
    {
        if (string.Equals(value.Trim(), "0.0.0.0", StringComparison.Ordinal))
        {
            address = IPAddress.Any;
            return true;
        }

        return IPAddress.TryParse(value.Trim(), out address!);
    }
}
