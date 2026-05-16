using Vulperonex.Application.Members;
using Vulperonex.Web.Errors;

namespace Vulperonex.Web.Endpoints;

public static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/members");

        group.MapGet("/", async (
            string? platform,
            int? limit,
            int? offset,
            IMemberQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var resolvedLimit = limit ?? 50;
            var resolvedOffset = offset ?? 0;
            if (resolvedLimit is < 1 or > 200 || resolvedOffset < 0)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            var members = await queryService.ListAsync(platform, resolvedLimit, resolvedOffset, cancellationToken);
            return Results.Ok(members);
        });

        group.MapGet("/{id}", async (string id, IMemberQueryService queryService, CancellationToken cancellationToken) =>
        {
            var member = await queryService.FindByMemberIdAsync(id, cancellationToken);
            return member is null
                ? ApiErrors.ToResult(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound)
                : Results.Ok(member);
        });

        return endpoints;
    }
}
