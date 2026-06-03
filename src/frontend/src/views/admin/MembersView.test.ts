import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "vue-i18n";
import enUS from "@/i18n/en-US.json";
import zhTW from "@/i18n/zh-TW.json";
import MembersView from "./MembersView.vue";

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
  return mount(MembersView, {
    global: {
      plugins: [buildI18n(), createPinia()]
    }
  });
}

function createMembersFetchMock(detailResponse?: Response) {
  return vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input.toString();

    if (url === "/api/members/" || url.startsWith("/api/members/?")) {
      return new Response(JSON.stringify(sampleMembers), { status: 200 });
    }

    if (url === "/api/config/overlay.member.background_url" || url === "/api/config/overlay.member.stamp_url") {
      return new Response(JSON.stringify({ value: "" }), { status: 200 });
    }

    if (url === "/api/members/MBR-01" && detailResponse) {
      return detailResponse;
    }

    return new Response(null, { status: 404 });
  });
}

const sampleMembers = [
  {
    memberId: "MBR-01",
    identities: [
      { platform: "twitch", platformUserId: "tw-1" },
      { platform: "youtube", platformUserId: "yt-1" }
    ],
    loyalty: { totalLoyalty: 250, checkInCount: 12 }
  },
  {
    memberId: "MBR-02",
    identities: [{ platform: "twitch", platformUserId: "tw-2" }],
    loyalty: { totalLoyalty: 80, checkInCount: 3 }
  }
];

describe("MembersView", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("should render member rows when list endpoint returns members", async () => {
    vi.stubGlobal("fetch", createMembersFetchMock());

    const wrapper = mountView();
    await flushPromises();

    const rows = wrapper.findAll('[data-testid="members-row"]');
    expect(rows).toHaveLength(2);
    expect(rows[0].text()).toContain("MBR-01");
    expect(rows[1].text()).toContain("MBR-02");
  });

  it("should render translated table headers instead of raw i18n keys", async () => {
    vi.stubGlobal("fetch", createMembersFetchMock());

    const wrapper = mountView();
    await flushPromises();

    const headerText = wrapper.find('[data-testid="members-table"]').text();
    expect(headerText).toContain("Avatar");
    expect(headerText).toContain("Display name");
    expect(headerText).toContain("Twitch account");
    expect(headerText).toContain("Role");
    expect(headerText).not.toContain("members.col.");
  });

  it("should expose member edit actions on the editable members panel", async () => {
    const fetchMock = createMembersFetchMock(
      new Response(JSON.stringify({
        ...sampleMembers[0],
        updatedAtTicks: 123
      }), { status: 200 })
    );
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.findAll('[data-testid="members-row"]')[0].trigger("click");
    await flushPromises();

    const interactiveText = [
      ...wrapper.findAll("button"),
      ...wrapper.findAll("a"),
      ...wrapper.findAll("[role='button']")
    ]
      .map((node) => `${node.text()} ${node.attributes("aria-label") ?? ""}`)
      .join(" ");

    expect(interactiveText).toMatch(/refresh|刷新/i);
    expect(interactiveText).toMatch(/detail|詳情/i);
    expect(interactiveText).toMatch(/delete|刪除/i);
    expect(wrapper.findAll('form[data-testid="members-edit-form"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-testid="members-delete"]')).toHaveLength(0);
    expect(wrapper.findAll('[data-testid="members-seed"]')).toHaveLength(0);
  });

  it("should not open the detail modal when detail loading fails", async () => {
    const fetchMock = createMembersFetchMock(
      new Response(JSON.stringify({ errorCode: "MEMBER_NOT_FOUND" }), { status: 404 })
    );
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.findAll(".detail-btn-quaternary")[0].trigger("click");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledWith("/api/members/MBR-01", expect.any(Object));
    expect(wrapper.find(".detail-modal-card").exists()).toBe(false);
  });

  it("should render detail when getMember returns the member", async () => {
    const fetchMock = createMembersFetchMock(
      new Response(JSON.stringify({
        ...sampleMembers[0],
        updatedAtTicks: 123
      }), { status: 200 })
    );
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    await wrapper.findAll(".detail-btn-quaternary")[0].trigger("click");
    await flushPromises();

    const detail = wrapper.find(".detail-modal-card");
    expect(detail.exists()).toBe(true);
    expect(detail.text()).toContain("MBR-01");
    expect(detail.text()).toContain("Twitch ID");
    expect(detail.text()).toContain("tw-1");
  });
});
