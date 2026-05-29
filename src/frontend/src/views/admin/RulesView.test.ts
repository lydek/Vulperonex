import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import RulesView from "./RulesView.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function mountView() {
  return mount(RulesView, {
    global: {
      plugins: [buildI18n(), createPinia()],
      stubs: {
        RuleEditorDrawer: {
          props: ["open", "ruleId"],
          emits: ["update:open", "saved"],
          template:
            '<div v-if="open" data-testid="rule-editor-drawer"><button data-testid="drawer-stub-save" @click="$emit(\'saved\', ruleId)">Save</button></div>'
        }
      }
    }
  });
}

const ruleA = {
  id: "rule-a",
  name: "Greeter",
  eventTypeKey: "user.message",
  isEnabled: true,
  priority: 100,
  createdAt: "2026-05-22T00:00:00Z",
  version: 3
};

const ruleADetail = {
  ...ruleA,
  conditions: [],
  actions: [{ type: "send_chat_message", message: "hi" }],
  executionMode: "Serial",
  maxParallelism: 1
};

describe("RulesView", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should render rule rows with version and priority columns", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify([ruleA]), { status: 200 }))
    );

    const wrapper = mountView();
    await flushPromises();

    const rows = wrapper.findAll('[data-testid="rules-row"]');
    expect(rows).toHaveLength(1);
    expect(rows[0].text()).toContain("Greeter");
    expect(rows[0].text()).toContain("100");
    expect(rows[0].text()).toContain("3");
  });

  it("should surface optimistic-lock notice when toggle returns 409", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([ruleA]), { status: 200 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "WORKFLOW_RULE_CONFLICT" }), { status: 409 })
      )
      .mockResolvedValueOnce(new Response(JSON.stringify([{ ...ruleA, version: 4 }]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="toggle-rule-a"]').trigger("click");
    await flushPromises();

    const notice = wrapper.find('[data-testid="rules-conflict"]');
    expect(notice.exists()).toBe(true);
    expect(notice.text()).toContain("Rule version was updated");
  });

  it("should require confirm dialog before issuing delete", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([ruleA]), { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="delete-rule-a"]').trigger("click");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const dialog = wrapper.find("[role='dialog']");
    expect(dialog.exists()).toBe(true);

    await wrapper.find(".danger-button").trigger("click");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[1][0]).toBe("/api/rules/rule-a");
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: "DELETE" });
    expect(wrapper.find('[data-testid="rules-empty"]').exists()).toBe(true);
  });

  it("should load detail JSON when row name cell is clicked", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([ruleA]), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(ruleADetail), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    const firstCell = wrapper.findAll('[data-testid="rules-row"] td.monitor-mono')[0];
    await firstCell.trigger("click");
    await flushPromises();

    const detail = wrapper.find('[data-testid="rules-detail"]');
    expect(detail.exists()).toBe(true);
    expect(detail.text()).toContain("send_chat_message");
  });

  it("should open the drawer editor from the row edit button", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify([ruleA]), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ ...ruleA, name: "Drawer Updated" }]), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ...ruleADetail, name: "Drawer Updated" }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.find('[data-testid="edit-rule-a"]').trigger("click");
    await flushPromises();

    expect(wrapper.find('[data-testid="rule-editor-drawer"]').exists()).toBe(true);
    await wrapper.find('[data-testid="drawer-stub-save"]').trigger("click");
    await flushPromises();

    expect(fetchMock.mock.calls[1][0]).toBe("/api/rules/");
    expect(fetchMock.mock.calls[2][0]).toBe("/api/rules/rule-a");
  });
});
