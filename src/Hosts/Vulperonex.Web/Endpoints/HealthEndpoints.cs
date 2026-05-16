namespace Vulperonex.Web.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
            .WithName("Health");

        return endpoints;
    }

    private sealed record HealthResponse(string Status);
}
