import { asString, type JsonRecord } from "@/components/admin/workflowEditor";

export interface RoleMigrationSuggestion {
  /** Legacy NCalc token detected, e.g. "Member.IsModerator". */
  token: string;
  /** Role the token maps to in a UserRoleCondition. */
  role: string;
  /** Where the token was found ("matchCondition" or a condition index). */
  source: string;
  /** Drop-in UserRoleCondition the operator can add by hand. */
  replacement: { type: "userRole"; mode: "HasAny"; roles: string };
}

interface LegacyRoleToken {
  token: string;
  role: string;
  pattern: RegExp;
}

const legacyRoleTokens: readonly LegacyRoleToken[] = [
  { token: "Member.IsBroadcaster", role: "Broadcaster", pattern: /Member\.IsBroadcaster\b/i },
  { token: "Member.IsModerator", role: "Moderator", pattern: /Member\.IsModerator\b/i },
  { token: "Member.IsSubscriber", role: "Subscriber", pattern: /Member\.IsSubscriber\b/i },
  { token: "Member.IsVip", role: "Vip", pattern: /Member\.IsVip\b/i }
];

function collectStringValues(record: JsonRecord): string[] {
  const values: string[] = [];
  for (const value of Object.values(record)) {
    if (typeof value === "string") {
      values.push(value);
    }
  }
  return values;
}

/**
 * Scans existing conditions and a rule's MatchCondition for legacy NCalc role
 * expressions that could be expressed as a typed UserRoleCondition instead.
 * Pure detection — it never mutates input or auto-applies a replacement.
 */
export function detectLegacyRoleExpressions(
  conditions: JsonRecord[],
  matchCondition?: string | null
): RoleMigrationSuggestion[] {
  const sources: Array<{ text: string; source: string }> = [];

  conditions.forEach((condition, index) => {
    // userRole conditions are already migrated — skip them.
    if (asString(condition.type) === "userRole") {
      return;
    }
    for (const text of collectStringValues(condition)) {
      sources.push({ text, source: `condition #${index + 1}` });
    }
  });

  if (matchCondition && matchCondition.trim().length > 0) {
    sources.push({ text: matchCondition, source: "matchCondition" });
  }

  const suggestions: RoleMigrationSuggestion[] = [];
  const seen = new Set<string>();

  for (const { text, source } of sources) {
    for (const entry of legacyRoleTokens) {
      if (!entry.pattern.test(text)) {
        continue;
      }

      const dedupeKey = `${source}|${entry.token}`;
      if (seen.has(dedupeKey)) {
        continue;
      }
      seen.add(dedupeKey);

      suggestions.push({
        token: entry.token,
        role: entry.role,
        source,
        replacement: { type: "userRole", mode: "HasAny", roles: entry.role }
      });
    }
  }

  return suggestions;
}
