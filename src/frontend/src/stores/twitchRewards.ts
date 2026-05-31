import { readonly, ref } from "vue";
import { defineStore } from "pinia";
import {
  ApiError,
  getTwitchRewards,
  refreshTwitchRewards,
  type TwitchRewardDescriptor
} from "@/api/client";

export const useTwitchRewardsStore = defineStore("twitchRewards", () => {
  const rewards = ref<TwitchRewardDescriptor[]>([]);
  const ready = ref(false);
  const lastRefreshedAt = ref<string | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);
  let pendingLoad: Promise<void> | null = null;

  async function load(signal?: AbortSignal): Promise<void> {
    if (ready.value) {
      return;
    }

    if (pendingLoad) {
      return pendingLoad;
    }

    loading.value = true;
    error.value = null;
    pendingLoad = getTwitchRewards(signal)
      .then(apply)
      .catch(captureError)
      .finally(() => {
        loading.value = false;
        pendingLoad = null;
      });

    return pendingLoad;
  }

  async function refresh(signal?: AbortSignal): Promise<void> {
    loading.value = true;
    error.value = null;
    try {
      apply(await refreshTwitchRewards(signal));
    } catch (caught) {
      captureError(caught);
    } finally {
      loading.value = false;
    }
  }

  function apply(payload: { ready: boolean; lastRefreshedAt?: string | null; rewards: TwitchRewardDescriptor[] }): void {
    ready.value = payload.ready;
    lastRefreshedAt.value = payload.lastRefreshedAt ?? null;
    rewards.value = payload.rewards;
  }

  function captureError(caught: unknown): void {
    error.value = caught instanceof ApiError
      ? caught.errorCode ?? `HTTP_${caught.status}`
      : caught instanceof Error
        ? caught.message
        : String(caught);
  }

  return {
    rewards: readonly(rewards),
    ready: readonly(ready),
    lastRefreshedAt: readonly(lastRefreshedAt),
    loading: readonly(loading),
    error: readonly(error),
    load,
    refresh
  };
});
