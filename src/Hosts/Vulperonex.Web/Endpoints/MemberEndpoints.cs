using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;
using Vulperonex.Application.Members;
using Vulperonex.Web.Errors;

namespace Vulperonex.Web.Endpoints;

public static class MemberEndpoints
{
    private const int MaxOffset = 1_000_000;

    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/members");

        group.MapGet("/", async (
            string? platform,
            int? limit,
            int? offset,
            IMemberQueryService queryService,
            IMemberAdminService adminService,
            CancellationToken cancellationToken) =>
        {
            var resolvedLimit = limit ?? 50;
            var resolvedOffset = offset ?? 0;
            if (resolvedLimit is < 1 or > 200 || resolvedOffset is < 0 or > MaxOffset)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            var members = await queryService.ListAsync(platform, resolvedLimit, resolvedOffset, cancellationToken);
            var enriched = new System.Collections.Generic.List<MemberReadModel>();
            foreach (var m in members)
            {
                var etagVal = await adminService.GetETagAsync(m.MemberId, m.UpdatedAtTicks, cancellationToken);
                enriched.Add(m with { ETag = etagVal });
            }
            return Results.Ok(enriched);
        });

        group.MapGet("/{id}", async (
            string id,
            IMemberQueryService queryService,
            IMemberAdminService adminService,
            HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            var member = await queryService.FindByMemberIdAsync(id, cancellationToken);
            if (member is null)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound);
            }

            var etag = await adminService.GetETagAsync(member.MemberId, member.UpdatedAtTicks, cancellationToken);
            response.Headers.ETag = $"\"{etag}\"";
            return Results.Ok(member with { ETag = etag });
        });

        group.MapGet("/{id}/audit", async (
            string id,
            int? limit,
            int? offset,
            IMemberQueryService queryService,
            IMemberAuditLogRepository auditLogRepository,
            CancellationToken cancellationToken) =>
        {
            var member = await queryService.FindByMemberIdAsync(id, cancellationToken);
            if (member is null)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound);
            }

            var resolvedLimit = limit ?? 50;
            var resolvedOffset = offset ?? 0;
            if (resolvedLimit is < 1 or > 200 || resolvedOffset is < 0 or > MaxOffset)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            var logs = await auditLogRepository.QueryAsync(id, resolvedLimit, resolvedOffset, cancellationToken);
            return Results.Ok(logs);
        });

        group.MapPatch("/{id}/loyalty", async (
            string id,
            AdjustLoyaltyRequest request,
            HttpContext httpContext,
            IMemberAdminService adminService,
            CancellationToken cancellationToken) =>
        {
            var ifMatch = httpContext.Request.Headers.IfMatch.ToString()?.Trim();
            if (string.IsNullOrEmpty(ifMatch))
            {
                return ApiErrors.ToResult(ErrorCodes.PreconditionRequired, StatusCodes.Status428PreconditionRequired);
            }

            if (ifMatch.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                ifMatch = ifMatch.Substring(2);
            }
            if (ifMatch.StartsWith("\"") && ifMatch.EndsWith("\""))
            {
                ifMatch = ifMatch.Substring(1, ifMatch.Length - 2);
            }

            try
            {
                await adminService.AdjustLoyaltyAsync(
                    id,
                    request.TotalLoyalty,
                    request.CheckInCount,
                    request.Reason,
                    ifMatch,
                    cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound);
            }
            catch (ArgumentException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }
            catch (MemberConcurrencyException)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberConcurrencyConflict, StatusCodes.Status409Conflict);
            }

            return Results.NoContent();
        });

        group.MapPost("/{id}/reset", async (
            string id,
            ResetMemberRequest request,
            HttpContext httpContext,
            IMemberAdminService adminService,
            CancellationToken cancellationToken) =>
        {
            var ifMatch = httpContext.Request.Headers.IfMatch.ToString()?.Trim();
            if (string.IsNullOrEmpty(ifMatch))
            {
                return ApiErrors.ToResult(ErrorCodes.PreconditionRequired, StatusCodes.Status428PreconditionRequired);
            }

            if (ifMatch.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                ifMatch = ifMatch.Substring(2);
            }
            if (ifMatch.StartsWith("\"") && ifMatch.EndsWith("\""))
            {
                ifMatch = ifMatch.Substring(1, ifMatch.Length - 2);
            }

            try
            {
                await adminService.ResetAsync(
                    id,
                    request.ResetLoyalty,
                    request.ResetCheckIn,
                    request.Reason,
                    ifMatch,
                    cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound);
            }
            catch (ArgumentException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }
            catch (MemberConcurrencyException)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberConcurrencyConflict, StatusCodes.Status409Conflict);
            }

            return Results.NoContent();
        });

        group.MapPost("/{id}/delete-token", async (
            string id,
            HttpContext httpContext,
            IMemberAdminService adminService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var token = await adminService.GenerateDeleteTokenAsync(id, cancellationToken);
                return Results.Ok(new { token });
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound);
            }
        });

        group.MapDelete("/{id}", async (
            string id,
            HttpContext httpContext,
            IMemberAdminService adminService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!httpContext.Request.HasJsonContentType())
                {
                    return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
                }

                var request = await httpContext.Request.ReadFromJsonAsync<DeleteMemberRequest>(cancellationToken: cancellationToken);
                if (request is null
                    || string.IsNullOrWhiteSpace(request.Token)
                    || string.IsNullOrWhiteSpace(request.Reason))
                {
                    return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
                }

                await adminService.DeleteWithTokenAsync(id, request.Token, request.Reason, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.MemberNotFound, StatusCodes.Status404NotFound);
            }
            catch (ArgumentException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }
            catch (JsonException)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            return Results.NoContent();
        });

        return endpoints;
    }


}

public sealed record AdjustLoyaltyRequest(int? TotalLoyalty, int? CheckInCount, string Reason);
public sealed record ResetMemberRequest(bool ResetLoyalty, bool ResetCheckIn, string Reason);
public sealed record DeleteMemberRequest(string? Token, string? Reason);
