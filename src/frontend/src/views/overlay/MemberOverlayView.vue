<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useOverlayHub } from "@/composables/useOverlayHub";
import { useStreamEvents } from "@/composables/useStreamEvents";
import { getDeterministicRandom } from "@/utils/deterministicRandom";
import { cssUrl, sanitizeAssetUrl } from "@/utils/overlayAssetUrl";

const MAX_STAMPS = 10;
const MAX_QUEUE_SIZE = 10;
const MEMBER_BACKGROUND_KEY = "overlay.member.background_url";
const MEMBER_STAMP_KEY = "overlay.member.stamp_url";
const FALLBACK_AVATAR =
  "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><circle cx='50' cy='50' r='50' fill='%23666'/><text x='50' y='55' font-family='Arial' font-size='40' font-weight='bold' fill='%23fff' text-anchor='middle' dominant-baseline='middle'>?</text></svg>";

interface QueueItem {
  name: string;
  avatarUrl: string;
  targetStamp: number;
  totalStamps: number;
  round: number;
}

const { t } = useI18n();
const route = useRoute();
const { events, start, state, lastEventAt, error, clear } = useOverlayHub("member");
const { events: systemEvents, start: startSystemEvents } = useStreamEvents();
const isPreview = computed(() => route.query.preview === "1");
const confirmOpen = ref(false);
const clearing = ref(false);
const showCard = ref(false);
const isAnimating = ref(false);
const checkInQueue = ref<QueueItem[]>([]);
const cardData = ref<QueueItem>({
  name: t("memberOverlay.loadingNickname"),
  avatarUrl: "",
  targetStamp: 0,
  totalStamps: 0,
  round: 1
});
const currentStamps = ref(0);
const isFullStamps = ref(false);
const isShaking = ref(false);
const animateStampIndex = ref<number | null>(null);
const customBgUrl = ref("");
const customStampUrl = ref("");

let settingsPollHandle: ReturnType<typeof setInterval> | null = null;
const isSyncingHistory = ref(true);
let syncTimeout: ReturnType<typeof setTimeout> | null = null;

onMounted(() => {
  void start().then(() => {
    // Open a 1.5s sync window to absorb and suppress initial historical events
    syncTimeout = setTimeout(() => {
      isSyncingHistory.value = false;
    }, 1500);
  });
  void startSystemEvents();
  void fetchSettings();
  settingsPollHandle = setInterval(() => {
    void fetchSettings();
  }, 10000);
});

onUnmounted(() => {
  if (syncTimeout !== null) {
    clearTimeout(syncTimeout);
    syncTimeout = null;
  }
  if (settingsPollHandle !== null) {
    clearInterval(settingsPollHandle);
    settingsPollHandle = null;
  }
});

watch(
  () => systemEvents.value[0]?.eventId,
  () => {
    const latest = systemEvents.value[0];
    if (!latest || latest.type !== "system.config_changed") {
      return;
    }

    if (latest.key === MEMBER_BACKGROUND_KEY || latest.key === MEMBER_STAMP_KEY) {
      void fetchSettings();
    }
  }
);

const lastProcessedEventId = ref<string | null>(null);

watch(
  () => events.value,
  (newEvents) => {
    if (newEvents.length === 0) {
      return;
    }

    const newest = newEvents[0];
    if (!newest || !newest.eventId) {
      return;
    }

    // During history synchronization window, update card statically without triggering animations
    if (isSyncingHistory.value) {
      const total = newest.checkInCount || 1;
      const displayStamps = total % MAX_STAMPS === 0 ? MAX_STAMPS : total % MAX_STAMPS;
      const round = Math.max(1, Math.ceil(total / MAX_STAMPS));

      cardData.value = {
        name: newest.displayName || t("memberOverlay.unknownUser"),
        avatarUrl: newest.avatarUrl || "",
        targetStamp: displayStamps,
        totalStamps: total,
        round
      };
      currentStamps.value = displayStamps;
      isFullStamps.value = displayStamps === MAX_STAMPS;
      lastProcessedEventId.value = newest.eventId;
      return;
    }

    if (newest.eventId === lastProcessedEventId.value) {
      return;
    }

    lastProcessedEventId.value = newest.eventId;

    const total = newest.checkInCount || 1;
    const displayStamps = total % MAX_STAMPS === 0 ? MAX_STAMPS : total % MAX_STAMPS;
    const round = Math.max(1, Math.ceil(total / MAX_STAMPS));

    if (checkInQueue.value.length >= MAX_QUEUE_SIZE) {
      checkInQueue.value.shift();
    }

    checkInQueue.value.push({
      name: newest.displayName || t("memberOverlay.unknownUser"),
      avatarUrl: newest.avatarUrl || "",
      targetStamp: displayStamps,
      totalStamps: total,
      round
    });

    void processQueue();
  },
  { deep: true }
);

async function fetchSettings() {
  try {
    const bgResponse = await fetch(`/api/config/${encodeURIComponent(MEMBER_BACKGROUND_KEY)}`);
    const stampResponse = await fetch(`/api/config/${encodeURIComponent(MEMBER_STAMP_KEY)}`);
    if (bgResponse.ok) {
      const bgData = await bgResponse.json();
      customBgUrl.value = sanitizeAssetUrl(bgData.value);
    }
    if (stampResponse.ok) {
      const stampData = await stampResponse.json();
      customStampUrl.value = sanitizeAssetUrl(stampData.value);
    }
  } catch {
  }
}

async function processQueue() {
  if (isAnimating.value || checkInQueue.value.length === 0) {
    return;
  }

  isAnimating.value = true;
  const task = checkInQueue.value.shift();
  if (task) {
    await renderAndShowCard(task);
  }
  isAnimating.value = false;

  setTimeout(() => {
    void processQueue();
  }, 500);
}

function renderAndShowCard(task: QueueItem): Promise<void> {
  return new Promise<void>((resolve) => {
    cardData.value = task;
    isFullStamps.value = false;
    isShaking.value = false;
    animateStampIndex.value = null;
    currentStamps.value = Math.max(0, task.targetStamp - 1);
    showCard.value = true;

    setTimeout(() => {
      currentStamps.value = task.targetStamp;
      animateStampIndex.value = task.targetStamp - 1;
      isShaking.value = true;

      setTimeout(() => {
        isShaking.value = false;
        if (task.targetStamp === MAX_STAMPS) {
          isFullStamps.value = true;
        }
      }, 400);

      setTimeout(() => {
        showCard.value = false;
        setTimeout(resolve, 800);
      }, 7000);
    }, 800);
  });
}

function getStampStyle(index: number) {
  const seedPref = `${cardData.value.name}_R${cardData.value.round}_S${index + 1}`;
  const rot = (getDeterministicRandom(`${seedPref}_rot`) * 50) - 25;
  const dx = (getDeterministicRandom(`${seedPref}_x`) * 1.5) - 0.75;
  const dy = (getDeterministicRandom(`${seedPref}_y`) * 1.5) - 0.75;
  const scale = 0.95 + (getDeterministicRandom(`${seedPref}_s`) * 0.1);

  return {
    "--rot": `${rot}deg`,
    "--dx": `${dx}px`,
    "--dy": `${dy}px`,
    "--scale": scale
  };
}

function onAvatarError(event: Event): void {
  (event.target as HTMLImageElement).src = FALLBACK_AVATAR;
}

async function onConfirm(): Promise<void> {
  clearing.value = true;
  try {
    await clear();
  } finally {
    clearing.value = false;
    confirmOpen.value = false;
  }
}
</script>

<template>
  <section
    class="overlay-panel"
    :class="{ 'overlay-panel--preview member-overlay--preview': isPreview }"
    aria-labelledby="member-overlay-title"
  >
    <header v-if="!isPreview" class="page-header visually-hidden-obs">
      <h1 id="member-overlay-title" class="page-title">{{ t("overlay.member.title") }}</h1>
      <div class="overlay-toolbar">
        <HubStatusChip :state="state" :last-event-at="lastEventAt" :error="error" />
        <button
          type="button"
          class="icon-button"
          :aria-label="t('overlay.clearAriaLabel', { hub: t('overlay.member.title') })"
          @click="confirmOpen = true"
        >
          {{ t("overlay.clear") }}
        </button>
      </div>
    </header>

    <div class="card-container" :class="{ show: showCard }">
      <div class="loyalty-card" :class="{ 'full-stamps': isFullStamps, shake: isShaking }">
        <div
          class="card-inner-bg"
          :style="{
            backgroundImage: cssUrl(customBgUrl),
            '--stamp-image': cssUrl(customStampUrl)
          }"
        >
          <div class="card-overlay" :style="{ opacity: customBgUrl ? '1' : '0.6' }"></div>
          <div class="card-bg-pattern"></div>

          <div class="card-left">
            <div class="user-avatar-wrap">
              <img
                class="user-avatar"
                :src="cardData.avatarUrl || FALLBACK_AVATAR"
                @error="onAvatarError"
              />
            </div>
            <div class="user-name-container">
              <div class="user-name">{{ cardData.name }}</div>
            </div>
            <div class="vip-badge">{{ t("memberOverlay.exclusiveStampCard") }}</div>
          </div>

          <div class="card-right">
            <div class="card-header">
              <div class="card-title">{{ t("memberOverlay.checkInMilestone") }}</div>
            </div>

            <div class="stamps-grid">
              <div
                v-for="(_, index) in MAX_STAMPS"
                :key="index"
                class="stamp-slot"
                :class="{
                  stamped: index < currentStamps,
                  'animate-stamp': index === animateStampIndex
                }"
                :style="getStampStyle(index)"
              >
                {{ index < currentStamps ? "" : index + 1 }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <ConfirmDialog
      v-if="!isPreview"
      :open="confirmOpen"
      :title="t('overlay.clearConfirmTitle')"
      :message="t('overlay.clearConfirmMessage', { hub: t('overlay.member.title') })"
      :confirm-label="t('overlay.clearConfirmAction')"
      :cancel-label="t('common.cancel')"
      :busy="clearing"
      @confirm="onConfirm"
      @cancel="confirmOpen = false"
    />
  </section>
</template>

<style>
:root {
  --stamp-image: url('data:image/svg+xml;utf8,<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg"><circle cx="50" cy="50" r="45" fill="none" stroke="%23ff3366" stroke-width="6" stroke-dasharray="8 4"/><circle cx="50" cy="50" r="38" fill="none" stroke="%23ff3366" stroke-width="2"/><g transform="rotate(-15 50 50) translate(8, 12) scale(0.85)" fill="%23ff3366"><path d="M 50,45 C 75,45 85,60 85,75 C 85,90 65,95 50,90 C 35,95 15,90 15,75 C 15,60 25,45 50,45 Z"/><ellipse cx="25" cy="35" rx="10" ry="15" transform="rotate(-30 25 35)"/><ellipse cx="40" cy="20" rx="10" ry="15"/><ellipse cx="60" cy="20" rx="10" ry="15"/><ellipse cx="75" cy="35" rx="10" ry="15" transform="rotate(30 75 35)"/></g></svg>');
  --gold-primary: #ffd700;
  --gold-secondary: #ffb700;
  --gold-light: #fff7a0;
  --card-bg-image: linear-gradient(135deg, rgba(20, 20, 25, 0.85) 0%, rgba(45, 45, 50, 0.95) 100%);
}

@media screen and (max-width: 100px), screen and (max-height: 100px) {
  .visually-hidden-obs {
    display: none !important;
  }
}
</style>

<style scoped>
.overlay-panel--preview {
  background: transparent;
  border: none;
  padding: 0;
  min-height: 100vh;
  overflow: hidden;
  position: relative;
}

.card-container {
  position: absolute;
  top: 50px;
  left: -1200px;
  transition: left 0.8s cubic-bezier(0.175, 0.885, 0.32, 1.275);
  transform: scale(1.5);
  transform-origin: top left;
  z-index: 1000;
}

.card-container.show {
  left: 50px;
}

.member-overlay--preview .card-container {
  inset: 0;
  left: 0;
  top: 0;
  transform: none;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
}

.member-overlay--preview .card-container.show {
  left: 0;
}

.member-overlay--preview .loyalty-card {
  width: min(600px, calc(100vw - 48px));
  height: auto;
  aspect-ratio: 2 / 1;
}

.loyalty-card {
  width: 600px;
  height: 300px;
  border-radius: 24px;
  position: relative;
  background: linear-gradient(135deg, var(--gold-primary), var(--gold-secondary), var(--gold-primary));
  padding: 2px;
  box-sizing: border-box;
  transition: box-shadow 0.8s ease, transform 0.4s ease;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.25);
}

.loyalty-card.full-stamps {
  box-shadow: 0 0 25px rgba(255, 215, 0, 0.8), inset 0 0 60px rgba(255, 215, 0, 0.5);
}

.card-inner-bg {
  position: relative;
  width: 100%;
  height: 100%;
  border-radius: 21px;
  background: var(--card-bg-image);
  background-size: cover;
  background-position: center;
  background-repeat: no-repeat;
  overflow: hidden;
  display: flex;
  box-sizing: border-box;
  color: #fff;
}

.card-overlay,
.card-bg-pattern {
  position: absolute;
  inset: 0;
  pointer-events: none;
}

.card-overlay {
  background: linear-gradient(135deg, rgba(0, 0, 0, 0.5) 0%, rgba(0, 0, 0, 0.8) 100%);
}

.card-bg-pattern {
  background-image: radial-gradient(rgba(255, 255, 255, 0.05) 1px, transparent 1px);
  background-size: 15px 15px;
}

.card-left,
.card-right {
  position: relative;
  z-index: 1;
}

.card-left {
  width: 220px;
  padding: 30px 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.user-avatar-wrap,
.user-avatar {
  width: 100px;
  height: 100px;
}

.user-avatar {
  border-radius: 50%;
  border: 3px solid #ffd700;
  object-fit: cover;
  background-color: #222;
}

.user-name-container {
  min-height: 50px;
  display: flex;
  align-items: center;
}

.user-name {
  font-size: 20px;
  font-weight: 700;
  text-align: center;
  line-height: 1.2;
  text-shadow: 0 2px 8px rgba(0, 0, 0, 0.9);
}

.vip-badge {
  margin-top: 10px;
  background: linear-gradient(135deg, #c59b27 0%, #ecd06f 50%, #c59b27 100%);
  color: #111;
  font-size: 14px;
  font-weight: 900;
  padding: 4px 16px;
  border-radius: 14px;
}

.card-right {
  flex: 1;
  padding: 25px 30px;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.card-header {
  margin-bottom: 15px;
  border-bottom: 1px solid rgba(255, 215, 0, 0.3);
  padding-bottom: 10px;
}

.card-title {
  font-size: 22px;
  font-weight: 900;
  color: #ffd700;
  letter-spacing: 2px;
}

.stamps-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 12px;
}

.stamp-slot {
  width: 50px;
  height: 50px;
  border-radius: 50%;
  border: 1px solid rgba(255, 215, 0, 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  background: rgba(0, 0, 0, 0.5);
  font-size: 18px;
  color: rgba(255, 215, 0, 0.2);
  font-weight: bold;
}

.stamp-slot.stamped::after {
  content: "";
  position: absolute;
  top: 50%;
  left: 50%;
  width: 54px;
  height: 54px;
  background-image: var(--stamp-image);
  background-size: contain;
  background-repeat: no-repeat;
  background-position: center;
  transform: translate(calc(-50% + var(--dx, 0px)), calc(-50% + var(--dy, 0px))) rotate(var(--rot, -15deg)) scale(var(--scale, 1));
}

.stamp-slot.animate-stamp::after {
  animation: stampInWithVars 0.5s cubic-bezier(0.175, 0.885, 0.32, 1.275) forwards;
}

.shake {
  animation: cardShake 0.4s ease-in-out;
}

@keyframes stampInWithVars {
  0% {
    transform: translate(calc(-50% + var(--dx, 0px)), calc(-50% + var(--dy, 0px))) scale(3) rotate(calc(var(--rot, -15deg) + 30deg));
    opacity: 0;
  }

  100% {
    transform: translate(calc(-50% + var(--dx, 0px)), calc(-50% + var(--dy, 0px))) scale(var(--scale, 1)) rotate(var(--rot, -15deg));
    opacity: 0.95;
  }
}

@keyframes cardShake {
  0% { transform: translate(0, 0) rotate(0); }
  20% { transform: translate(-3px, 2px) rotate(-1deg); }
  40% { transform: translate(3px, -2px) rotate(1deg); }
  60% { transform: translate(-2px, 1px) rotate(-0.5deg); }
  80% { transform: translate(2px, -1px) rotate(0.5deg); }
  100% { transform: translate(0, 0) rotate(0); }
}
</style>
