using System.Net;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed record OAuthCallbackRequest(IPAddress RemoteIpAddress, string Host, string Path, string State, string Code);
