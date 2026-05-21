using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Web.Ports;
using Vulperonex.Web.Endpoints;
using Vulperonex.Web;
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
        if (configureDefaultLoopbackPorts)
        {
            ConfigureDefaultLoopbackPorts(builder);
        }

        return builder;
    }

    public static WebApplication Build(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapOpenApi("/openapi/v1.json");
        app.MapHealthEndpoints();
        app.MapWorkflowRuleEndpoints();
        app.MapEventTypeEndpoints();
        app.MapConfigEndpoints();
        app.MapMemberEndpoints();
        app.MapSimulateEndpoints();
        app.MapTwitchAuthEndpoints();
        app.MapOverlayHistoryEndpoints();
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
            || path.Equals("/health");
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
