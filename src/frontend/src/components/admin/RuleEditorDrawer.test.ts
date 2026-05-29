import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import RuleEditorDrawer from "./RuleEditorDrawer.vue";

const { getRuleMock, updateRuleMock } = vi.hoisted(() => ({
  getRuleMock: vi.fn(),
  updateRuleMock: vi.fn()
}));

vi.mock("@/api/client", async () => {
  const actual = await vi.importActual<typeof import("@/api/client")>("@/api/client");
  return {
    ...actual,
    getRule: getRuleMock,
    updateRule: updateRuleMock
  };
});

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

const ruleDetail = {
  id: "rule-a",
  name: "Check-in",
  eventTypeKey: "user.message",
  isEnabled: true,
  priority: 100,
  createdAt: "2026-05-22T00:00:00Z",
  version: 1,
  trigger: { filter: { CommandName: "!checkin" } },
  matchCondition: "",
  isSubWorkflow: false,
  conditions: [{ type: "userRole", mode: "HasAny", roles: "Subscriber" }],
  actions: [{ type: "sendChatMessage", template: "hi" }],
  onFailureSteps: [{ type: "sendChatMessage", template: "failed" }],
  executionMode: "Serial",
  maxParallelism: 1,
  throttle: {
    maxConcurrent: 0,
    cooldownSeconds: 0,
    perUserCooldown: false,
    perUserCooldownSeconds: 0
  },
  timeoutSeconds: 30
};

function mountDrawer() {
  return mount(RuleEditorDrawer, {
    attachTo: document.body,
    props: {
      open: true,
      ruleId: "rule-a"
    },
    global: {
      plugins: [buildI18n()],
      stubs: {
        TriggerEditor: {
          props: ["eventTypeKey", "filter", "matchCondition"],
          template: '<div data-testid="drawer-trigger-editor">{{ eventTypeKey }}</div>'
        },
        ThrottleEditor: {
          props: ["modelValue"],
          template: '<div data-testid="drawer-throttle-editor">{{ JSON.stringify(modelValue) }}</div>'
        },
        WorkflowConditionsEditor: {
          props: ["modelValue", "title", "emptyText", "testIdPrefix"],
          emits: ["update:modelValue"],
          template:
            '<textarea :data-testid="`${testIdPrefix}-editor`" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
        },
        WorkflowActionsEditor: {
          props: ["modelValue", "title", "emptyText", "testIdPrefix"],
          emits: ["update:modelValue"],
          template:
            '<textarea :data-testid="`${testIdPrefix}-editor`" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />'
        }
      }
    }
  });
}

describe("RuleEditorDrawer", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    document.body.innerHTML = "";
  });

  it("loads a rule, preserves form state across tabs, and saves", async () => {
    getRuleMock.mockResolvedValue(ruleDetail);
    updateRuleMock.mockResolvedValue({ ...ruleDetail, name: "Updated rule" });

    const wrapper = mountDrawer();
    await flushPromises();

    const name = document.body.querySelector<HTMLInputElement>('[data-testid="rule-drawer-name"]');
    expect(name?.value).toBe("Check-in");
    name!.value = "Updated rule";
    name!.dispatchEvent(new Event("input"));
    await wrapper.vm.$nextTick();

    document.body.querySelector<HTMLButtonElement>('[data-testid="rule-drawer-tab-actions"]')?.click();
    await flushPromises();

    document.body.querySelector<HTMLButtonElement>('[data-testid="rule-drawer-tab-basic"]')?.click();
    await wrapper.vm.$nextTick();
    expect(document.body.querySelector<HTMLInputElement>('[data-testid="rule-drawer-name"]')?.value)
      .toBe("Updated rule");

    document.body.querySelector<HTMLButtonElement>('[data-testid="rule-drawer-save"]')?.click();
    await flushPromises();

    expect(updateRuleMock).toHaveBeenCalledWith(
      "rule-a",
      expect.objectContaining({
        name: "Updated rule",
        actions: [{ type: "sendChatMessage", template: "hi" }]
      })
    );
    expect(wrapper.emitted("saved")?.[0]).toEqual(["rule-a"]);
    expect(wrapper.emitted("update:open")?.at(-1)).toEqual([false]);
  });

  it("adds and removes userRole conditions from role chips", async () => {
    getRuleMock.mockResolvedValue({ ...ruleDetail, conditions: [] });
    updateRuleMock.mockResolvedValue(ruleDetail);

    mountDrawer();
    await flushPromises();

    document.body.querySelector<HTMLButtonElement>('[data-testid="role-chip-Moderator"]')?.click();
    await flushPromises();

    document.body.querySelector<HTMLButtonElement>('[data-testid="rule-drawer-save"]')?.click();
    await flushPromises();

    expect(updateRuleMock).toHaveBeenLastCalledWith(
      "rule-a",
      expect.objectContaining({
        conditions: [{ type: "userRole", mode: "HasAny", roles: "Moderator" }]
      })
    );

    document.body.innerHTML = "";
    getRuleMock.mockResolvedValue({ ...ruleDetail, conditions: [{ type: "userRole", mode: "HasAny", roles: "Subscriber" }] });
    updateRuleMock.mockClear();

    mountDrawer();
    await flushPromises();

    document.body.querySelector<HTMLButtonElement>('[data-testid="role-chip-Subscriber"]')?.click();
    await flushPromises();

    document.body.querySelector<HTMLButtonElement>('[data-testid="rule-drawer-save"]')?.click();
    await flushPromises();

    expect(updateRuleMock).toHaveBeenLastCalledWith(
      "rule-a",
      expect.objectContaining({
        conditions: []
      })
    );
  });
});
