using Vulperonex.Application.Workflows.Timers;
using Vulperonex.Web.Errors;

namespace Vulperonex.Web.Endpoints;

public static class WorkflowTimerEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowTimerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/timers");

        group.MapGet("/", async (IWorkflowTimerRepository repository, CancellationToken cancellationToken) =>
        {
            var timers = await repository.ListAsync(cancellationToken);
            return Results.Ok(timers.Select(ToDto));
        });

        group.MapGet("/{id}", async (string id, IWorkflowTimerRepository repository, HttpResponse response, CancellationToken cancellationToken) =>
        {
            var timer = await repository.GetAsync(id, cancellationToken);
            if (timer is null)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowTimerNotFound, StatusCodes.Status404NotFound);
            }

            response.Headers.ETag = $"\"{timer.Version}\"";
            return Results.Ok(ToDto(timer));
        });

        group.MapPost("/", async (
            WorkflowTimerUpsertRequest request,
            IWorkflowTimerRepository repository,
            CancellationToken cancellationToken) =>
        {
            var validationError = Validate(request);
            if (validationError is not null)
            {
                return ApiErrors.ToResult(validationError, ErrorCodeStatusMap.GetStatusCode(validationError));
            }

            var id = Guid.NewGuid().ToString("N");
            var timer = ToTimer(request, id);
            await repository.AddAsync(timer, cancellationToken);
            return Results.Created($"/api/timers/{id}", ToDto(timer));
        });

        group.MapPut("/{id}", async (
            string id,
            WorkflowTimerUpsertRequest request,
            HttpContext httpContext,
            IWorkflowTimerRepository repository,
            CancellationToken cancellationToken) =>
        {
            var validationError = Validate(request);
            if (validationError is not null)
            {
                return ApiErrors.ToResult(validationError, ErrorCodeStatusMap.GetStatusCode(validationError));
            }

            if (!TryParseIfMatchVersion(httpContext.Request.Headers.IfMatch.ToString(), out var expectedVersion))
            {
                return ApiErrors.ToResult(ErrorCodes.PreconditionRequired, StatusCodes.Status428PreconditionRequired);
            }

            var timer = ToTimer(request, id);
            try
            {
                await repository.UpdateAsync(timer, expectedVersion, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowTimerNotFound, StatusCodes.Status404NotFound);
            }
            catch (WorkflowTimerConcurrencyException)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowTimerConflict, StatusCodes.Status409Conflict);
            }

            return Results.Ok(ToDto(timer with { Version = expectedVersion + 1 }));
        });

        group.MapDelete("/{id}", async (string id, IWorkflowTimerRepository repository, CancellationToken cancellationToken) =>
        {
            try
            {
                await repository.DeleteAsync(id, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowTimerNotFound, StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        });

        return endpoints;
    }

    private static bool TryParseIfMatchVersion(string? ifMatch, out int version)
    {
        version = 0;
        var value = ifMatch?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
        {
            value = value[1..^1];
        }

        return int.TryParse(value, out version) && version >= 0;
    }

    private static WorkflowTimerDto ToDto(WorkflowTimer timer)
    {
        return new WorkflowTimerDto(timer.Id, timer.RuleId, timer.IntervalSeconds, timer.IsEnabled, timer.NextFireAt, timer.Version);
    }

    private static WorkflowTimer ToTimer(WorkflowTimerUpsertRequest request, string id)
    {
        return new WorkflowTimer
        {
            Id = id,
            RuleId = request.RuleId,
            IntervalSeconds = request.IntervalSeconds,
            IsEnabled = request.IsEnabled,
            NextFireAt = request.NextFireAt,
        };
    }

    private static string? Validate(WorkflowTimerUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RuleId))
        {
            return ErrorCodes.TimerRuleIdRequired;
        }

        if (request.IntervalSeconds <= 0)
        {
            return ErrorCodes.TimerIntervalInvalid;
        }

        return null;
    }
}

public sealed record WorkflowTimerDto(
    string Id,
    string RuleId,
    int IntervalSeconds,
    bool IsEnabled,
    DateTimeOffset NextFireAt,
    int Version);

public sealed record WorkflowTimerUpsertRequest(
    string RuleId,
    int IntervalSeconds,
    bool IsEnabled,
    DateTimeOffset NextFireAt);
