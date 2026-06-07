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

function createMembersFetchMock(detailResponse?: Response, list = sampleMembers) {
  return vi.fn(async (input: RequestInfo | URL) => {
    const url = typeof input === "string" ? input : input.toString();

    if (url === "/api/members/" || url.startsWith("/api/members/?")) {
      return new Response(JSON.stringify(list), { status: 200 });
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

const pagedMembers = Array.from({ length: 12 }, (_, index) => ({
  memberId: `MBR-${String(index + 1).padStart(2, "0")}`,
  identities: [{ platform: "twitch", platformUserId: `tw-${index + 1}`, displayName: `User ${index + 1}` }],
  loyalty: { totalLoyalty: index * 10, checkInCount: index }
}));

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

  it("should filter members with a platform combobox", async () => {
    const fetchMock = createMembersFetchMock();
    vi.stubGlobal("fetch", fetchMock);

    const wrapper = mountView();
    await flushPromises();

    const platformSelect = wrapper.get('[data-testid="members-platform-filter"]');
    expect(platformSelect.element.tagName).toBe("SELECT");
    expect(platformSelect.text()).toContain("All platforms");
    expect(platformSelect.text()).toContain("twitch");
    expect(platformSelect.text()).toContain("youtube");

    await platformSelect.setValue("twitch");
    await wrapper.get(".members-toolbar").trigger("submit");
    await flushPromises();

    expect(fetchMock).toHaveBeenCalledWith("/api/members/?platform=twitch", expect.any(Object));
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

  it("should page the check-in management table by the selected row count", async () => {
    vi.stubGlobal("fetch", createMembersFetchMock(undefined, pagedMembers));

    const wrapper = mountView();
    await flushPromises();

    await wrapper.findAll(".tab-btn")[1].trigger("click");
    await flushPromises();

    expect(wrapper.get('[data-testid="members-checkin-page-status"]').text()).toContain("1-10 of 12");
    expect(wrapper.findAll('[data-testid="members-row"]')).toHaveLength(10);

    await wrapper.get('[data-testid="members-checkin-next"]').trigger("click");
    await flushPromises();

    expect(wrapper.get('[data-testid="members-checkin-page-status"]').text()).toContain("11-12 of 12");
    expect(wrapper.findAll('[data-testid="members-row"]')).toHaveLength(2);

    await wrapper.get('[data-testid="members-checkin-page-size"]').setValue("20");
    await flushPromises();

    expect(wrapper.get('[data-testid="members-checkin-page-status"]').text()).toContain("1-12 of 12");
    expect(wrapper.findAll('[data-testid="members-row"]')).toHaveLength(12);
  });

  it("should sort the check-in management table by supported columns", async () => {
    vi.stubGlobal("fetch", createMembersFetchMock(undefined, pagedMembers));

    const wrapper = mountView();
    await flushPromises();

    await wrapper.findAll(".tab-btn")[1].trigger("click");
    await flushPromises();

    expect(wrapper.findAll('[data-testid="members-row"]')[0].text()).toContain("User 12");

    await wrapper.get('[data-testid="members-checkin-sort-checkins"]').trigger("click");
    await flushPromises();

    expect(wrapper.findAll('[data-testid="members-row"]')[0].text()).toContain("User 1");

    await wrapper.get('[data-testid="members-checkin-sort-user"]').trigger("click");
    await flushPromises();

    expect(wrapper.findAll('[data-testid="members-row"]')[0].text()).toContain("User 1");
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
