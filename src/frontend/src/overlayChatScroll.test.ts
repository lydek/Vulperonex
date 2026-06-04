import { readFileSync } from "node:fs";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const scriptPath = join(process.cwd(), "public", "overlay", "js", "chat-overlay.js");
const scriptSource = readFileSync(scriptPath, "utf8");

describe("chat overlay scrolling", () => {
  let overlayHandlers: { event: (data: unknown) => void } | undefined;

  beforeEach(() => {
    vi.restoreAllMocks();
    overlayHandlers = undefined;
    document.body.innerHTML = '<div id="chat-container" class="chat-container"></div>';
    window.history.replaceState(null, "", "/?preset=compact-line&preview=1&showMemberCard=false");

    Object.defineProperty(window, "requestAnimationFrame", {
      configurable: true,
      value: (callback: FrameRequestCallback) => {
        callback(0);
        return 1;
      }
    });

    Object.defineProperty(window, "OverlayCommon", {
      configurable: true,
      value: {
        getDeterministicRandom: () => 0.5,
        initSignalRConnection: vi.fn((_url: string, handlers: { event: (data: unknown) => void }) => {
          overlayHandlers = handlers;
        })
      }
    });
  });

  afterEach(() => {
    document.body.innerHTML = "";
    window.history.replaceState(null, "", "/");
  });

  it("keeps the latest compact-line chat message scrolled into view", () => {
    window.eval(scriptSource);
    document.dispatchEvent(new Event("DOMContentLoaded"));

    const chatContainer = document.getElementById("chat-container") as HTMLDivElement;
    Object.defineProperty(chatContainer, "scrollHeight", {
      configurable: true,
      get: () => 960
    });

    overlayHandlers?.event({
      displayName: "Sim User",
      htmlMessage: "!Test",
      colorHex: "#ffd700"
    });

    expect(chatContainer.scrollTop).toBe(960);
    expect(chatContainer.lastElementChild?.textContent).toContain("!Test");
  });
});
