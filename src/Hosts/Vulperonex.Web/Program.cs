using System.Net;
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
        return CreateBuilder(new WebApplicationOptions { Args = args });
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

        app.MapOpenApi("/openapi/v1.json");
        app.MapHealthEndpoints();
        app.MapWorkflowRuleEndpoints();
        app.MapEventTypeEndpoints();
        app.MapConfigEndpoints();
        app.MapMemberEndpoints();
        app.MapSimulateEndpoints();
        app.MapOverlayHubs();

        return app;
    }

    private static void ConfigureDefaultLoopbackPorts(WebApplicationBuilder builder)
    {
        var ports = new PortPairAllocator(new SocketPortAvailabilityProbe()).TryAllocate()
            ?? throw new PortExhaustedException();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, ports.ApiPort);
            options.Listen(IPAddress.IPv6Loopback, ports.ApiPort);
            options.Listen(IPAddress.Loopback, ports.OverlayPort);
            options.Listen(IPAddress.IPv6Loopback, ports.OverlayPort);
        });
    }
}
