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

        group.MapGet("/{id}", async (string id, IWorkflowTimerRepository repository, CancellationToken cancellationToken) =>
        {
            var timer = await repository.GetAsync(id, cancellationToken);
            return timer is null
                ? ApiErrors.ToResult(ErrorCodes.WorkflowTimerNotFound, StatusCodes.Status404NotFound)
                : Results.Ok(ToDto(timer));
        });

        group.MapPost("/", async (
            WorkflowTimerUpsertRequest request,
            IWorkflowTimerRepository repository,
            CancellationToken cancellationToken) =>
        {
            var validationError = Validate(request);
            if (validationError is not null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            var id = Guid.NewGuid().ToString("N");
            var timer = ToTimer(request, id);
            await repository.AddAsync(timer, cancellationToken);
            return Results.Created($"/api/timers/{id}", ToDto(timer));
        });

        group.MapPut("/{id}", async (
            string id,
            WorkflowTimerUpsertRequest request,
            IWorkflowTimerRepository repository,
            CancellationToken cancellationToken) =>
        {
            var validationError = Validate(request);
            if (validationError is not null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            var timer = ToTimer(request, id);
            try
            {
                await repository.UpdateAsync(timer, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowTimerNotFound, StatusCodes.Status404NotFound);
            }

            return Results.Ok(ToDto(timer));
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

    private static WorkflowTimerDto ToDto(WorkflowTimer timer)
    {
        return new WorkflowTimerDto(timer.Id, timer.RuleId, timer.IntervalSeconds, timer.IsEnabled, timer.NextFireAt);
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
            return "RULE_ID_REQUIRED";
        }

        if (request.IntervalSeconds <= 0)
        {
            return "INTERVAL_SECONDS_INVALID";
        }

        return null;
    }
}

public sealed record WorkflowTimerDto(
    string Id,
    string RuleId,
    int IntervalSeconds,
    bool IsEnabled,
    DateTimeOffset NextFireAt);

public sealed record WorkflowTimerUpsertRequest(
    string RuleId,
    int IntervalSeconds,
    bool IsEnabled,
    DateTimeOffset NextFireAt);
