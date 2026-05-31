import { describe, expect, it } from "vitest";
import { buildVariableGroups, getVariableInfo } from "./workflowEditor";

describe("workflowEditor variable contract", () => {
  it("matches the backend trigger context instead of exposing stale trigger user aliases", () => {
    expect(getVariableInfo("Trigger.Platform")?.type).toBe("string");
    expect(getVariableInfo("Trigger.OccurredAt")?.type).toBe("string");
    expect(getVariableInfo("Trigger.UserId")).toBeUndefined();
    expect(getVariableInfo("Trigger.Channel")).toBeUndefined();
  });

  it("exposes trigger user fields through the Member scope", () => {
    expect(getVariableInfo("Member.UserId")?.type).toBe("string");
    expect(getVariableInfo("Member.Platform")?.type).toBe("string");
    expect(getVariableInfo("Member.IsModerator")?.type).toBe("boolean");
  });

  it("keeps failure variables aligned with the runtime failure context", () => {
    expect(getVariableInfo("Failure.StepIndex")?.type).toBe("number");
    expect(getVariableInfo("Failure.ErrorMessage")?.type).toBe("string");
    expect(getVariableInfo("Failure.ErrorCode")).toBeUndefined();
  });

  it("builds trigger and member groups with the clarified labels", () => {
    const groups = buildVariableGroups([], false);

    expect(groups.find(group => group.key === "trigger")?.label).toBe("Trigger Event");
    expect(groups.find(group => group.key === "member")?.label).toBe("Trigger User");
  });
});
