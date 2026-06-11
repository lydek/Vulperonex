namespace Vulperonex.Web.Endpoints;

using System.Text.Json;
using Vulperonex.Application.Workflows;
using Vulperonex.Application.Workflows.Actions;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Validation;
using Vulperonex.Web.Workflows;

public static class WorkflowRuleEndpoints
{
    private const int MaxCycleWalkNodes = 1024;

    public static IEndpointRouteBuilder MapWorkflowRuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/rules");

        group.MapGet("/", async (IWorkflowRuleQueryService queryService, CancellationToken cancellationToken) =>
        {
            var rules = await queryService.ListAsync(cancellationToken);
            return Results.Ok(rules);
        });

        group.MapGet("/{id}", async (string id, IWorkflowRuleQueryService queryService, CancellationToken cancellationToken) =>
        {
            var rule = await queryService.GetAsync(id, cancellationToken);
            return rule is null
                ? ApiErrors.ToResult(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound)
                : Results.Ok(WorkflowRuleJsonMapper.ToDto(rule));
        });

        group.MapPost("/", async (
            WorkflowRuleUpsertRequest request,
            WorkflowRuleValidator validator,
            IWorkflowRuleRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(request.Id))
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowRuleIdNotAllowed, StatusCodes.Status400BadRequest);
            }

            var error = ValidateRequest(request, validator);
            if (error is not null)
            {
                return ApiErrors.ToResult(error, ErrorCodeStatusMap.GetStatusCode(error));
            }

            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id;
            var rule = ToRuleOrNull(request, id);
            if (rule is null)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidActionConfig, StatusCodes.Status400BadRequest);
            }

            await repository.AddAsync(rule, cancellationToken);

            return Results.Created($"/api/rules/{id}", WorkflowRuleJsonMapper.ToDto(rule));
        });

        group.MapPut("/{id}", async (
            string id,
            WorkflowRuleUpsertRequest request,
            WorkflowRuleValidator validator,
            IWorkflowRuleQueryService queryService,
            IWorkflowRuleRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(request.Id) && !string.Equals(id, request.Id, StringComparison.Ordinal))
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidRuleIdMismatch, StatusCodes.Status400BadRequest);
            }

            var existing = await queryService.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound);
            }

            var error = ValidateRequest(request, validator);
            if (error is not null)
            {
                return ApiErrors.ToResult(error, ErrorCodeStatusMap.GetStatusCode(error));
            }

            if (await WouldCreateCycleAsync(id, request.Actions ?? [], queryService, cancellationToken))
            {
                return ApiErrors.ToResult(ErrorCodes.CircularWorkflowReference, StatusCodes.Status400BadRequest);
            }

            var rule = ToRuleOrNull(request, id, existing.CreatedAt, existing.Version);
            if (rule is null)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidActionConfig, StatusCodes.Status400BadRequest);
            }

            try
            {
                await repository.UpdateAsync(rule, existing.Version, cancellationToken);
            }
            catch (WorkflowRuleConcurrencyException)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowRuleConflict, StatusCodes.Status409Conflict);
            }

            return Results.Ok(WorkflowRuleJsonMapper.ToDto(rule));
        });

        group.MapDelete("/{id}", async (string id, IWorkflowRuleRepository repository, CancellationToken cancellationToken) =>
        {
            try
            {
                await repository.DeleteAsync(id, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        });

        group.MapPut("/{id}/enable", (string id, IWorkflowRuleQueryService queryService, IWorkflowRuleRepository repository, CancellationToken cancellationToken) =>
            SetEnabledAsync(id, true, queryService, repository, cancellationToken));

        group.MapPut("/{id}/disable", (string id, IWorkflowRuleQueryService queryService, IWorkflowRuleRepository repository, CancellationToken cancellationToken) =>
            SetEnabledAsync(id, false, queryService, repository, cancellationToken));

        return endpoints;
    }

    private static async Task<IResult> SetEnabledAsync(
        string id,
        bool isEnabled,
        IWorkflowRuleQueryService queryService,
        IWorkflowRuleRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await queryService.GetAsync(id, cancellationToken);
            if (existing is null)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound);
            }

            await repository.SetEnabledAsync(id, isEnabled, existing.Version, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return ApiErrors.ToResult(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound);
        }
        catch (WorkflowRuleConcurrencyException)
        {
            return ApiErrors.ToResult(ErrorCodes.WorkflowRuleConflict, StatusCodes.Status409Conflict);
        }

        return Results.NoContent();
    }

    private static string? ValidateRequest(WorkflowRuleUpsertRequest request, WorkflowRuleValidator validator)
    {
        try
        {
            return validator.Validate(request);
        }
        catch (JsonException)
        {
            return ErrorCodes.InvalidActionConfig;
        }
        catch (InvalidOperationException)
        {
            return ErrorCodes.InvalidActionConfig;
        }
    }

    private static WorkflowRule? ToRuleOrNull(
        WorkflowRuleUpsertRequest request,
        string id,
        DateTimeOffset? createdAt = null,
        int version = 0)
    {
        try
        {
            return WorkflowRuleJsonMapper.ToRule(request, id, createdAt, version);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<bool> WouldCreateCycleAsync(
        string ruleId,
        IReadOnlyList<JsonElement> candidateActions,
        IWorkflowRuleQueryService queryService,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(ExtractInvokedWorkflowIds(candidateActions));

        while (pending.TryDequeue(out var nextId))
        {
            if (string.Equals(ruleId, nextId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!visited.Add(nextId))
            {
                continue;
            }

            if (visited.Count > MaxCycleWalkNodes)
            {
                return true;
            }

            var nextRule = await queryService.GetAsync(nextId, cancellationToken);
            if (nextRule is null)
            {
                continue;
            }

            foreach (var invokedId in nextRule.Actions.OfType<InvokeSubWorkflowAction>().Select(action => action.WorkflowId))
            {
                pending.Enqueue(invokedId);
            }
        }

        return false;
    }

    private static IEnumerable<string> ExtractInvokedWorkflowIds(IEnumerable<JsonElement> actions)
    {
        foreach (var action in actions)
        {
            if (!IsInvokeSubWorkflow(action))
            {
                continue;
            }

            if (action.TryGetProperty("workflowId", out var workflowId)
                && workflowId.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(workflowId.GetString()))
            {
                yield return workflowId.GetString()!;
            }
        }
    }

    private static bool IsInvokeSubWorkflow(JsonElement action)
    {
        return action.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && string.Equals(type.GetString(), InvokeSubWorkflowAction.ActionType, StringComparison.Ordinal);
    }
}
