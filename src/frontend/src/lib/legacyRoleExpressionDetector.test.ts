import { describe, expect, it } from "vitest";
import { detectLegacyRoleExpressions } from "./legacyRoleExpressionDetector";

describe("detectLegacyRoleExpressions", () => {
  it("flags Member.IsModerator inside a condition value", () => {
    const suggestions = detectLegacyRoleExpressions([
      { type: "expression", expression: "Member.IsModerator == true" }
    ]);

    expect(suggestions).toHaveLength(1);
    expect(suggestions[0]).toMatchObject({
      token: "Member.IsModerator",
      role: "Moderator",
      source: "condition #1",
      replacement: { type: "userRole", mode: "HasAny", roles: "Moderator" }
    });
  });

  it("flags expressions found in the rule MatchCondition", () => {
    const suggestions = detectLegacyRoleExpressions([], "Member.IsSubscriber || Member.IsVip");

    expect(suggestions.map((entry) => entry.role).sort()).toEqual(["Subscriber", "Vip"]);
    expect(suggestions.every((entry) => entry.source === "matchCondition")).toBe(true);
  });

  it("covers all four standard roles", () => {
    const suggestions = detectLegacyRoleExpressions(
      [],
      "Member.IsBroadcaster || Member.IsModerator || Member.IsSubscriber || Member.IsVip"
    );

    expect(suggestions.map((entry) => entry.role).sort()).toEqual([
      "Broadcaster",
      "Moderator",
      "Subscriber",
      "Vip"
    ]);
  });

  it("ignores userRole conditions that are already migrated", () => {
    const suggestions = detectLegacyRoleExpressions([
      { type: "userRole", mode: "HasAny", roles: "Member.IsModerator" }
    ]);

    expect(suggestions).toHaveLength(0);
  });

  it("returns nothing when no legacy role tokens are present", () => {
    const suggestions = detectLegacyRoleExpressions(
      [{ type: "messageContent", pattern: "!hello" }],
      "Trigger.MessageText == '!hi'"
    );

    expect(suggestions).toHaveLength(0);
  });

  it("dedupes a token repeated within the same source", () => {
    const suggestions = detectLegacyRoleExpressions([
      { type: "expression", expression: "Member.IsModerator", note: "Member.IsModerator again" }
    ]);

    expect(suggestions).toHaveLength(1);
  });
});
