import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import TriggerEditor from "./TriggerEditor.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

describe("TriggerEditor", () => {
  it("should keep newly added blank filter rows visible until edited", async () => {
    const wrapper = mount(TriggerEditor, {
      props: {
        eventTypeKey: "user.message",
        filter: {},
        matchCondition: ""
      },
      global: {
        plugins: [buildI18n()],
        stubs: {
          EventTypeKeyDropdown: {
            props: ["modelValue"],
            emits: ["update:modelValue"],
            template: "<div />"
          }
        }
      }
    });

    expect(wrapper.findAll(".rule-filter-row")).toHaveLength(1);

    await wrapper.find('[data-testid="trigger-filter-add"]').trigger("click");

    expect(wrapper.findAll(".rule-filter-row")).toHaveLength(2);
    expect(wrapper.emitted("update:filter")).toBeUndefined();
  });

  it("should emit trigger filter and match condition updates", async () => {
    const wrapper = mount(TriggerEditor, {
      props: {
        eventTypeKey: "user.message",
        filter: { initial: "" },
        matchCondition: ""
      },
      global: {
        plugins: [buildI18n()],
        stubs: {
          EventTypeKeyDropdown: {
            props: ["modelValue"],
            emits: ["update:modelValue"],
            template:
              '<select data-testid="event-type-select" :value="modelValue" @change="$emit(\'update:modelValue\', $event.target.value)"><option value="user.followed">follow</option></select>'
          }
        }
      }
    });

    await wrapper.find('[data-testid="event-type-select"]').setValue("user.followed");
    const filterInputs = wrapper.findAll(".rule-filter-row input");
    await filterInputs[0].setValue("platform");
    await wrapper.findAll(".rule-filter-row input")[1].setValue("twitch");
    await wrapper.find('[data-testid="rule-editor-match-condition-raw-toggle"]').trigger("click");
    await wrapper.find('[data-testid="rule-editor-match-condition"]').setValue("Trigger.Message == '!go'");

    expect(wrapper.emitted("update:eventTypeKey")?.[0]).toEqual(["user.followed"]);
    expect(wrapper.emitted("update:filter")?.at(-1)?.[0]).toEqual({ platform: "twitch" });
    expect(wrapper.emitted("update:matchCondition")?.[0]).toEqual(["Trigger.Message == '!go'"]);
  });

  it("should drop blank keys from emitted filter payload", async () => {
    const wrapper = mount(TriggerEditor, {
      props: {
        eventTypeKey: "user.message",
        filter: { platform: "twitch" },
        matchCondition: ""
      },
      global: {
        plugins: [buildI18n()],
        stubs: {
          EventTypeKeyDropdown: {
            props: ["modelValue"],
            emits: ["update:modelValue"],
            template: "<div />"
          }
        }
      }
    });

    await wrapper.findAll(".rule-filter-row input")[0].setValue("");

    expect(wrapper.emitted("update:filter")?.at(-1)?.[0]).toEqual({});
  });
});
