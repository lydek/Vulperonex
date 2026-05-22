export interface PollingFallbackOptions {
  poll: () => Promise<void> | void;
  baseDelayMs?: number;
  multiplier?: number;
  maxDelayMs?: number;
}

const DEFAULT_BASE_DELAY_MS = 30_000;
const DEFAULT_MULTIPLIER = 2;
const DEFAULT_MAX_DELAY_MS = 300_000;

export interface PollingFallback {
  start(): void;
  stop(): void;
  /** Next delay (ms) that will be used by the upcoming schedule call. Test-only. */
  readonly nextDelayMs: number;
  /** Whether a timer is currently armed. */
  readonly armed: boolean;
}

/**
 * Exponential-backoff polling fallback for SignalR transient drops (II22).
 *
 * Contract:
 * - Base delay 30s, multiplier 2, ceiling 300s by default.
 * - Each failed poll multiplies the delay until the ceiling.
 * - Each successful poll resets the delay back to base.
 * - `start()` is idempotent; calling it while armed is a no-op.
 * - `stop()` immediately clears the pending timer and resets state so the
 *   reconnect happy path does not double-fire.
 * - Never schedules with a zero delay -- the smallest delay is the base.
 */
export function useExponentialPollingFallback(options: PollingFallbackOptions): PollingFallback {
  const baseDelay = options.baseDelayMs ?? DEFAULT_BASE_DELAY_MS;
  const multiplier = options.multiplier ?? DEFAULT_MULTIPLIER;
  const maxDelay = options.maxDelayMs ?? DEFAULT_MAX_DELAY_MS;

  let timer: ReturnType<typeof setTimeout> | null = null;
  let nextDelay = baseDelay;

  function schedule(): void {
    timer = setTimeout(async () => {
      timer = null;
      try {
        await options.poll();
        nextDelay = baseDelay;
      } catch {
        nextDelay = Math.min(Math.max(nextDelay * multiplier, baseDelay), maxDelay);
      }
      // Only re-arm if stop() did not clear us while poll() was inflight.
      if (timer === null) {
        schedule();
      }
    }, nextDelay);
  }

  function start(): void {
    if (timer !== null) return;
    nextDelay = baseDelay;
    schedule();
  }

  function stop(): void {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
    nextDelay = baseDelay;
  }

  return {
    start,
    stop,
    get nextDelayMs() {
      return nextDelay;
    },
    get armed() {
      return timer !== null;
    }
  };
}
