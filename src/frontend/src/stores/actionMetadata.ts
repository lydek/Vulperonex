import { computed, readonly, ref } from "vue";
import { defineStore } from "pinia";
import {
  getActionMetadata,
  type ActionMetadataEntry,
  type ActionParameterMetadata
} from "@/api/client";
import {
  fallbackActionDefinitions,
  type ActionDefinition,
  type FieldDefinition,
  type FieldKind,
  type JsonRecord
} from "@/components/admin/workflowEditor";

const outputVariableOverrides: Record<string, string[]> = {
  triggerCheckIn: ["CheckInCount", "TotalLoyalty", "DisplayName", "RoundIndex", "StampSlotInRound"],
  randomPicker: ["Picked", "Index"],
  updateCounter: ["Value"],
  lookupTwitchUser: ["Login", "DisplayName", "UserId", "IsFound"],
  addLotteryTickets: ["Value"]
};

export const useActionMetadataStore = defineStore("actionMetadata", () => {
  const entries = ref<ActionMetadataEntry[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const loadAttempted = ref(false);
  let pendingLoad: Promise<void> | null = null;

  const definitions = computed<ActionDefinition[]>(() => {
    if (entries.value.length === 0) {
      return fallbackActionDefinitions;
    }

    return entries.value.map(toActionDefinition);
  });

  const fallbackActive = computed(() => loadAttempted.value && entries.value.length === 0);

  async function load(signal?: AbortSignal): Promise<void> {
    if (entries.value.length > 0) {
      return;
    }

    if (pendingLoad) {
      return pendingLoad;
    }

    loading.value = true;
    error.value = null;
    pendingLoad = getActionMetadata(signal)
      .then(result => {
        entries.value = result;
      })
      .catch(err => {
        error.value = err instanceof Error ? err.message : String(err);
      })
      .finally(() => {
        loading.value = false;
        loadAttempted.value = true;
        pendingLoad = null;
      });

    return pendingLoad;
  }

  function findDefinition(type: string): ActionDefinition | undefined {
    return definitions.value.find(definition => definition.type === type);
  }

  return {
    entries: readonly(entries),
    definitions,
    loading: readonly(loading),
    error: readonly(error),
    fallbackActive,
    load,
    findDefinition
  };
});

function toActionDefinition(entry: ActionMetadataEntry): ActionDefinition {
  return {
    type: entry.type,
    label: entry.displayName,
    description: entry.description,
    fields: entry.parameters.map(toFieldDefinition),
    outputVariables: outputVariableOverrides[entry.type],
    create: () => createActionRecord(entry)
  };
}

function toFieldDefinition(parameter: ActionParameterMetadata): FieldDefinition {
  return {
    key: lowerFirst(parameter.key),
    label: parameter.label,
    kind: fieldKindFor(parameter),
    placeholder: parameter.help ?? undefined,
    advanced: parameter.advanced ?? false
  };
}

function fieldKindFor(parameter: ActionParameterMetadata): FieldKind {
  const key = lowerFirst(parameter.key).toLowerCase();
  const type = parameter.type.toLowerCase();
  if (type === "number") {
    return "number";
  }
  if (type === "boolean") {
    return "checkbox";
  }
  if (type === "array") {
    return key.includes("weight") ? "number-list" : "string-list";
  }
  if (type === "dictionary") {
    return key === "params" ? "json-object" : "string-map";
  }
  if (key === "template" || key === "displaytext") {
    return "textarea";
  }
  return "text";
}

function createActionRecord(entry: ActionMetadataEntry): JsonRecord {
  const record: JsonRecord = { type: entry.type };
  for (const parameter of entry.parameters) {
    if (!parameter.required) {
      continue;
    }

    const key = lowerFirst(parameter.key);
    switch (fieldKindFor(parameter)) {
      case "checkbox":
        record[key] = false;
        break;
      case "number":
        record[key] = 0;
        break;
      case "string-list":
      case "number-list":
        record[key] = [];
        break;
      case "string-map":
      case "json-object":
        record[key] = {};
        break;
      default:
        record[key] = "";
    }
  }
  return record;
}

function lowerFirst(value: string): string {
  return value.length === 0 ? value : value[0].toLowerCase() + value.slice(1);
}
