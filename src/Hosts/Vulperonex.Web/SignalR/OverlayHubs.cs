namespace Vulperonex.Web.SignalR;

public sealed class EventsHub : Microsoft.AspNetCore.SignalR.Hub;

public sealed class OverlayChatHub : Microsoft.AspNetCore.SignalR.Hub;

public sealed class OverlayAlertsHub : Microsoft.AspNetCore.SignalR.Hub;

public sealed class OverlayMemberHub : Microsoft.AspNetCore.SignalR.Hub;

public static class OverlayHubEndpoints
{
    public static IEndpointRouteBuilder MapOverlayHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<EventsHub>("/hubs/events");
        endpoints.MapHub<OverlayChatHub>("/hubs/overlay/chat");
        endpoints.MapHub<OverlayAlertsHub>("/hubs/overlay/alerts");
        endpoints.MapHub<OverlayMemberHub>("/hubs/overlay/member");
        return endpoints;
    }
}
