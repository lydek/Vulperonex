using System.Net;

namespace Vulperonex.Web.TwitchAuth;

public sealed class TwitchTokenExchangeException(HttpStatusCode statusCode) : Exception
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
