import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useExponentialPollingFallback } from "./useExponentialPollingFallback";

describe("useExponentialPollingFallback (II22)", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("should start with base delay and never schedule at 0 ms", () => {
    const fallback = useExponentialPollingFallback({
      poll: vi.fn(async () => {}),
      baseDelayMs: 30_000
    });

    fallback.start();
    expect(fallback.nextDelayMs).toBe(30_000);
    expect(fallback.armed).toBe(true);
  });

  it("should multiply delay by the multiplier on each consecutive failure up to the ceiling", async () => {
    const poll = vi.fn(async () => {
      throw new Error("boom");
    });
    const fallback = useExponentialPollingFallback({
      poll,
      baseDelayMs: 30_000,
      multiplier: 2,
      maxDelayMs: 300_000
    });

    fallback.start();
    expect(fallback.nextDelayMs).toBe(30_000);

    await vi.advanceTimersByTimeAsync(30_000);
    expect(poll).toHaveBeenCalledTimes(1);
    expect(fallback.nextDelayMs).toBe(60_000);

    await vi.advanceTimersByTimeAsync(60_000);
    expect(poll).toHaveBeenCalledTimes(2);
    expect(fallback.nextDelayMs).toBe(120_000);

    await vi.advanceTimersByTimeAsync(120_000);
    expect(poll).toHaveBeenCalledTimes(3);
    expect(fallback.nextDelayMs).toBe(240_000);

    await vi.advanceTimersByTimeAsync(240_000);
    expect(poll).toHaveBeenCalledTimes(4);
    expect(fallback.nextDelayMs).toBe(300_000);

    // Already at ceiling; further failures stay at 300_000.
    await vi.advanceTimersByTimeAsync(300_000);
    expect(poll).toHaveBeenCalledTimes(5);
    expect(fallback.nextDelayMs).toBe(300_000);

    fallback.stop();
  });

  it("should reset to base delay on successful poll", async () => {
    let shouldFail = true;
    const fallback = useExponentialPollingFallback({
      poll: async () => {
        if (shouldFail) throw new Error("boom");
      },
      baseDelayMs: 30_000,
      multiplier: 2,
      maxDelayMs: 300_000
    });

    fallback.start();
    await vi.advanceTimersByTimeAsync(30_000);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(fallback.nextDelayMs).toBe(120_000);

    shouldFail = false;
    await vi.advanceTimersByTimeAsync(120_000);
    expect(fallback.nextDelayMs).toBe(30_000);

    fallback.stop();
  });

  it("should clear the pending timer immediately when stop() is called", async () => {
    const poll = vi.fn(async () => {});
    const fallback = useExponentialPollingFallback({ poll, baseDelayMs: 30_000 });

    fallback.start();
    expect(fallback.armed).toBe(true);

    fallback.stop();
    expect(fallback.armed).toBe(false);

    await vi.advanceTimersByTimeAsync(60_000);
    expect(poll).not.toHaveBeenCalled();
  });

  it("should be idempotent: calling start() while armed does not stack timers", async () => {
    const poll = vi.fn(async () => {});
    const fallback = useExponentialPollingFallback({ poll, baseDelayMs: 30_000 });

    fallback.start();
    fallback.start();
    fallback.start();

    await vi.advanceTimersByTimeAsync(30_000);
    expect(poll).toHaveBeenCalledTimes(1);
    fallback.stop();
  });
});
