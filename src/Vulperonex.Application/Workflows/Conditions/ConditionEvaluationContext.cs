using Vulperonex.Domain.Events;

namespace Vulperonex.Application.Workflows.Conditions;

public sealed record ConditionEvaluationContext(IStreamEvent StreamEvent, string WorkflowRuleId);
