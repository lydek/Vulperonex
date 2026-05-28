namespace Vulperonex.Application.Workflows.Actions;

using System.Text.Json.Serialization;
using Vulperonex.Application.Workflows.Metadata;

[ActionMetadata("Add Lottery Tickets", "Add lottery tickets for a user")]
public sealed record AddLotteryTicketsAction : WorkflowAction
{
    public const string ActionType = "addLotteryTickets";

    [JsonIgnore]
    public override string Type => ActionType;

    [ActionParam("User ID", "string", required: false, help: "The template expression for the user's ID")]
    public string UserId { get; init; } = "{Member.UserId}";

    [ActionParam("Amount", "number", required: false, help: "The amount of tickets to add")]
    public long Amount { get; init; } = 1;
}
