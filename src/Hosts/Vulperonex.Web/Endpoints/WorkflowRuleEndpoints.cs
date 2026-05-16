namespace Vulperonex.Web.Endpoints;

using Vulperonex.Application.Workflows;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Validation;
using Vulperonex.Web.Workflows;

public static class WorkflowRuleEndpoints
{
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
            var error = validator.Validate(request);
            if (error is not null)
            {
                return ApiErrors.ToResult(error, ErrorCodeStatusMap.GetStatusCode(error));
            }

            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id;
            var rule = WorkflowRuleJsonMapper.ToRule(request, id);
            await repository.AddAsync(rule, cancellationToken);

            return Results.Created($"/api/rules/{id}", WorkflowRuleJsonMapper.ToDto(rule));
        });

        group.MapPut("/{id}", async (
            string id,
            WorkflowRuleUpsertRequest request,
            WorkflowRuleValidator validator,
            IWorkflowRuleRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(request.Id) && !string.Equals(id, request.Id, StringComparison.Ordinal))
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidRuleIdMismatch, StatusCodes.Status400BadRequest);
            }

            var error = validator.Validate(request);
            if (error is not null)
            {
                return ApiErrors.ToResult(error, ErrorCodeStatusMap.GetStatusCode(error));
            }

            var rule = WorkflowRuleJsonMapper.ToRule(request, id);
            try
            {
                await repository.UpdateAsync(rule, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return ApiErrors.ToResult(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound);
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

        group.MapPost("/{id}/enable", (string id, IWorkflowRuleRepository repository, CancellationToken cancellationToken) =>
            SetEnabledAsync(id, true, repository, cancellationToken));

        group.MapPost("/{id}/disable", (string id, IWorkflowRuleRepository repository, CancellationToken cancellationToken) =>
            SetEnabledAsync(id, false, repository, cancellationToken));

        return endpoints;
    }

    private static async Task<IResult> SetEnabledAsync(
        string id,
        bool isEnabled,
        IWorkflowRuleRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SetEnabledAsync(id, isEnabled, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return ApiErrors.ToResult(ErrorCodes.WorkflowRuleNotFound, StatusCodes.Status404NotFound);
        }

        return Results.NoContent();
    }
}
