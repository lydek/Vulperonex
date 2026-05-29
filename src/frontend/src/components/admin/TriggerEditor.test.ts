import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import TriggerEditor from "./TriggerEditor.vue";

const triggerMetadataResponse = [
  {
    key: "user.message",
    displayName: "User Message",
    description: "Chat message",
    filterFields: [
      {
        key: "CommandName",
        label: "Command Name",
        type: "string",
        options: null,
        help: "Command without prefix",
        required: false
      },
      {
        key: "Prefix",
        label: "Prefix",
        type: "string",
        options: null,
        help: null,
        required: false
      }
    ],
    validVariables: ["MessageText"]
  },
  {
    key: "user.donated",
    displayName: "User Donated",
    description: "Donation",
    filterFields: [
      {
        key: "MinAmount",
        label: "Minimum Amount",
        type: "number",
        options: null,
        help: null,
        required: false
      }
    ],
    validVariables: ["EventId"]
  }
];

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function stubFetch(): void {
  vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify(triggerMetadataResponse), {
    headers: { "Content-Type": "application/json" },
    status: 200
  })));
}

function mountTriggerEditor(props: {
  eventTypeKey: string;
  filter: Record<string, string>;
  matchCondition: string;
}, options: { stubVariablePicker?: boolean } = {}) {
  return mount(TriggerEditor, {
    props,
    global: {
      plugins: [buildI18n(), createPinia()],
      stubs: {
        EventTypeKeyDropdown: {
          props: ["modelValue"],
          emits: ["update:modelValue"],
          template:
            '<select data-testid="event-type-select" :value="modelValue" @change="$emit(\'update:modelValue\', $event.target.value)"><option value="user.message">message</option><option value="user.donated">donated</option></select>'
        },
        ...(options.stubVariablePicker === false ? {} : { VariablePicker: true })
      }
    }
  });
}

describe("TriggerEditor", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    stubFetch();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("should render metadata-defined fields for user.message", async () => {
    const wrapper = mountTriggerEditor({
      eventTypeKey: "user.message",
      filter: {},
      matchCondition: ""
    }, { stubVariablePicker: false });

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="trigger-filter-field-CommandName"]').exists()).toBe(true);
    });

    expect(wrapper.find('[data-testid="trigger-filter-field-Prefix"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="trigger-filter-add"]').exists()).toBe(false);
    expect(wrapper.findAll(".rule-filter-row")).toHaveLength(0);
    expect(wrapper.text()).toContain("{Trigger.MessageText}");
    expect(wrapper.text()).not.toContain("{Trigger.IsTest}");
  });

  it("should emit typed trigger filter and match condition updates", async () => {
    const wrapper = mountTriggerEditor({
      eventTypeKey: "user.message",
      filter: {},
      matchCondition: ""
    });

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="trigger-filter-input-CommandName"]').exists()).toBe(true);
    });

    await wrapper.find('[data-testid="event-type-select"]').setValue("user.donated");
    await wrapper.find('[data-testid="trigger-filter-input-CommandName"]').setValue("checkin");
    await wrapper.find('[data-testid="rule-editor-match-condition-raw-toggle"]').trigger("click");
    await wrapper.find('[data-testid="rule-editor-match-condition"]').setValue("Trigger.Message == '!go'");

    expect(wrapper.emitted("update:eventTypeKey")?.[0]).toEqual(["user.donated"]);
    expect(wrapper.emitted("update:filter")?.at(-1)?.[0]).toEqual({ CommandName: "checkin" });
    expect(wrapper.emitted("update:matchCondition")?.[0]).toEqual(["Trigger.Message == '!go'"]);
  });

  it("should switch to the donated number field and prune legacy filter keys", async () => {
    const wrapper = mountTriggerEditor({
      eventTypeKey: "user.message",
      filter: { CommandName: "checkin", platform: "twitch" },
      matchCondition: ""
    });

    await vi.waitFor(() => {
      expect(wrapper.emitted("update:filter")?.at(-1)?.[0]).toEqual({ CommandName: "checkin" });
    });

    await wrapper.setProps({
      eventTypeKey: "user.donated",
      filter: { CommandName: "checkin", MinAmount: "100" }
    });

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="trigger-filter-field-MinAmount"]').exists()).toBe(true);
    });

    const minAmount = wrapper.find('[data-testid="trigger-filter-field-MinAmount"] input');
    expect(minAmount.attributes("type")).toBe("number");
    expect(wrapper.find('[data-testid="trigger-filter-field-CommandName"]').exists()).toBe(false);
    expect(wrapper.emitted("update:filter")?.at(-1)?.[0]).toEqual({ MinAmount: "100" });
  });

  it("should update variable picker entries when the event type changes", async () => {
    const wrapper = mountTriggerEditor({
      eventTypeKey: "user.message",
      filter: {},
      matchCondition: ""
    }, { stubVariablePicker: false });

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain("{Trigger.MessageText}");
    });

    await wrapper.setProps({
      eventTypeKey: "user.donated",
      filter: { MinAmount: "100" }
    });

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="trigger-filter-field-MinAmount"]').exists()).toBe(true);
    });

    expect(wrapper.text()).toContain("Trigger.EventId");
    expect(wrapper.text()).not.toContain("{Trigger.MessageText}");
  });
});
