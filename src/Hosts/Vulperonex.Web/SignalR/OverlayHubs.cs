using Microsoft.AspNetCore.SignalR;
using Vulperonex.Application.Overlay;
using Vulperonex.Application.Overlay.Dtos;

namespace Vulperonex.Web.SignalR;

public sealed class EventsHub : Hub;

public sealed class OverlayChatHub(IOverlayHistoryService<OverlayChatPayload> history) : Hub
{
    public override async Task OnConnectedAsync()
    {
        foreach (var payload in await history.GetRecentAsync(Context.ConnectionAborted))
        {
            await Clients.Caller.SendAsync("event", payload, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}

public sealed class OverlayAlertsHub(IOverlayHistoryService<OverlayAlertPayload> history) : Hub
{
    public override async Task OnConnectedAsync()
    {
        foreach (var payload in await history.GetRecentAsync(Context.ConnectionAborted))
        {
            await Clients.Caller.SendAsync("event", payload with { Replayed = true }, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}

public sealed class OverlayMemberHub(IOverlayHistoryService<OverlayMemberPayload> history) : Hub
{
    public override async Task OnConnectedAsync()
    {
        foreach (var payload in await history.GetRecentAsync(Context.ConnectionAborted))
        {
            await Clients.Caller.SendAsync("event", payload, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}

public sealed class OverlayEffectsHub : Hub;

public sealed class OverlayWidgetsHub(IOverlayHistoryService<OverlayWidgetPayload> history) : Hub
{
    public override async Task OnConnectedAsync()
    {
        foreach (var payload in await history.GetRecentAsync(Context.ConnectionAborted))
        {
            await Clients.Caller.SendAsync("event", payload, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}

public static class OverlayHubEndpoints
{
    public static IEndpointRouteBuilder MapOverlayHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<EventsHub>("/hubs/events");
        endpoints.MapHub<OverlayChatHub>("/hubs/overlay/chat");
        endpoints.MapHub<OverlayAlertsHub>("/hubs/overlay/alerts");
        endpoints.MapHub<OverlayMemberHub>("/hubs/overlay/member");
        endpoints.MapHub<OverlayEffectsHub>("/hubs/overlay/effects");
        endpoints.MapHub<OverlayWidgetsHub>("/hubs/overlay/widgets");
        return endpoints;
    }
}
