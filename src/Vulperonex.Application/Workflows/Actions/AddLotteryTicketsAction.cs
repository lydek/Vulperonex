namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;

public sealed record AddLotteryTicketsAction : WorkflowAction
{
    public const string ActionType = "addLotteryTickets";

    [JsonIgnore]
    public override string Type => ActionType;

    public string UserId { get; init; } = "{Member.UserId}";

    public long Amount { get; init; } = 1;
}
