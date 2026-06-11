using System.Net;

namespace Vulperonex.Adapters.Twitch.Auth;

public sealed class TwitchTokenExchangeException(HttpStatusCode statusCode) : Exception
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
