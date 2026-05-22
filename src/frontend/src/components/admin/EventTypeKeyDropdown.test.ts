import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import EventTypeKeyDropdown from "./EventTypeKeyDropdown.vue";

function buildI18n() {
  return createI18n({
    legacy: false,
    locale: "en-US",
    fallbackLocale: "en-US",
    missing: (_locale, key) => key,
    messages: { "en-US": enUS, "zh-TW": zhTW }
  });
}

function mountDropdown(modelValue = "") {
  return mount(EventTypeKeyDropdown, {
    props: { modelValue },
    global: { plugins: [buildI18n()] }
  });
}

const sampleEventTypes = [
  { key: "user.message", description: "Chat message", isSimulatable: true },
  { key: "user.followed", description: "Follower joined", isSimulatable: true },
  { key: "user.subscribed", description: "Subscription event", isSimulatable: true },
  { key: "user.donated", description: "Donation", isSimulatable: false },
  { key: "user.gifted_sub", description: "Gifted sub", isSimulatable: false }
];

describe("EventTypeKeyDropdown", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should render every event type returned by the api as a select option", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify(sampleEventTypes), { status: 200 }))
    );

    const wrapper = mountDropdown();
    await flushPromises();

    const options = wrapper.findAll('[data-testid="event-type-select"] option');
    const optionValues = options.map((option) => option.attributes("value"));
    expect(optionValues).toEqual([
      "",
      "user.message",
      "user.followed",
      "user.subscribed",
      "user.donated",
      "user.gifted_sub"
    ]);
  });

  it("should not render an option for platform.connection_changed because the api filters it server-side", async () => {
    // The /api/event-types endpoint already excludes IsSystemEvent=true entries
    // so the dropdown only sees workflow-eligible keys.
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify(sampleEventTypes), { status: 200 }))
    );

    const wrapper = mountDropdown();
    await flushPromises();

    expect(wrapper.html()).not.toContain("platform.connection_changed");
  });

  it("should badge only the three canonical simulatable keys", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify(sampleEventTypes), { status: 200 }))
    );

    const wrapper = mountDropdown();
    await flushPromises();

    const badges = wrapper.findAll('[data-testid="event-type-badge"]');
    expect(badges).toHaveLength(3);
    expect(badges.map((badge) => badge.text())).toEqual([
      "user.message",
      "user.followed",
      "user.subscribed"
    ]);
  });

  it("should mark non-simulatable options as data-simulatable=false", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify(sampleEventTypes), { status: 200 }))
    );

    const wrapper = mountDropdown();
    await flushPromises();

    const donated = wrapper.find('option[value="user.donated"]');
    expect(donated.attributes("data-simulatable")).toBe("false");
    expect(donated.text()).toContain("no simulator yet");

    const followed = wrapper.find('option[value="user.followed"]');
    expect(followed.attributes("data-simulatable")).toBe("true");
    expect(followed.text()).toContain("simulatable");
  });

  it("should emit update:modelValue when selection changes", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(JSON.stringify(sampleEventTypes), { status: 200 }))
    );

    const wrapper = mountDropdown();
    await flushPromises();

    await wrapper.find('[data-testid="event-type-select"]').setValue("user.followed");

    expect(wrapper.emitted("update:modelValue")?.[0]).toEqual(["user.followed"]);
  });
});
