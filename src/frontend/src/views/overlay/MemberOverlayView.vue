<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import ConfirmDialog from "@/components/admin/ConfirmDialog.vue";
import HubStatusChip from "@/components/admin/HubStatusChip.vue";
import { useOverlayHub } from "@/composables/useOverlayHub";
import { getDeterministicRandom } from "@/utils/deterministicRandom";

const MAX_STAMPS = 10;
const MAX_QUEUE_SIZE = 10;

const { t } = useI18n();
const { events, start, state, lastEventAt, error, clear } = useOverlayHub("member");
const confirmOpen = ref(false);
const clearing = ref(false);

// ??∪??急?嗥???const showCard = ref(false);
const isAnimating = ref(false);
const checkInQueue = ref<any[]>([]);

const cardData = ref<{
  name: string;
  avatarUrl: string;
  targetStamp: number;
  totalStamps: number;
  round: number;
}>({
  name: "?梁迂頛銝?,
  avatarUrl: "",
  targetStamp: 0,
  totalStamps: 0,
  round: 1
});

const currentStamps = ref<number>(0);
const isFullStamps = ref(false);
const isShaking = ref(false);
const animateStampIndex = ref<number | null>(null);

const customBgUrl = ref("");
const customStampUrl = ref("");

// ?迂??URL scheme嚗 CSS url() 瘜典
const ALLOWED_SCHEMES = /^(https?:|data:image\/(png|jpe?g|gif|svg\+xml|webp);)/i;

function sanitizeAssetUrl(raw: string | null | undefined): string {
  if (!raw) return "";
  const trimmed = raw.trim();
  if (!ALLOWED_SCHEMES.test(trimmed)) return "";
  // 蝳迫 CSS url() 頝喳摮?
  if (/["')(\\]/.test(trimmed)) return "";
  return trimmed;
}

function cssUrl(safeUrl: string): string | undefined {
  return safeUrl ? `url("${safeUrl}")` : undefined;
}

async function fetchSettings() {
  try {
    const bgResponse = await fetch("/api/config/overlay.member.background_url");
    const stampResponse = await fetch("/api/config/overlay.member.stamp_url");
    if (bgResponse.ok) {
      const bgData = await bgResponse.json();
      customBgUrl.value = sanitizeAssetUrl(bgData.value);
    }
    if (stampResponse.ok) {
      const stampData = await stampResponse.json();
      customStampUrl.value = sanitizeAssetUrl(stampData.value);
    }
  } catch {
    // ?脩戌閮剖?霈?仃??  }
}

let settingsPollHandle: ReturnType<typeof setInterval> | null = null;

onMounted(() => {
  void start();
  void fetchSettings();
  settingsPollHandle = setInterval(() => {
    void fetchSettings();
  }, 10000);
});

onUnmounted(() => {
  if (settingsPollHandle !== null) {
    clearInterval(settingsPollHandle);
    settingsPollHandle = null;
  }
});

// ?? SignalR ?唬?隞?watch(
  () => events.value.length,
  (newLength, oldLength) => {
    if (newLength > (oldLength || 0)) {
      const newest = events.value[0]; // ?踵??啁?銝蝑?      if (newest) {
        const total = newest.checkInCount || 1;
        const displayStamps = (total % MAX_STAMPS === 0) ? MAX_STAMPS : (total % MAX_STAMPS);
        const round = Math.max(1, Math.ceil(total / MAX_STAMPS));

        if (checkInQueue.value.length >= MAX_QUEUE_SIZE) {
          checkInQueue.value.shift();
        }

        checkInQueue.value.push({
          name: newest.displayName || "?芰雿輻??,
          avatarUrl: newest.avatarUrl || "",
          targetStamp: displayStamps,
          totalStamps: total,
          round: round
        });

        void processQueue();
      }
    }
  }
);

async function processQueue() {
  if (isAnimating.value || checkInQueue.value.length === 0) return;
  isAnimating.value = true;
  const task = checkInQueue.value.shift();
  await renderAndShowCard(task);
  isAnimating.value = false;
  setTimeout(() => {
    void processQueue();
  }, 500);
}

function renderAndShowCard(task: any): Promise<void> {
  return new Promise<void>((resolve) => {
    cardData.value = task;
    isFullStamps.value = false;
    isShaking.value = false;
    animateStampIndex.value = null;

    const initialStamps = Math.max(0, task.targetStamp - 1);
    currentStamps.value = initialStamps;

    // 皛憿舐內?∠?
    showCard.value = true;

    // 800ms 敺???啣蝡??怨??脣漲?湔
    setTimeout(() => {
      currentStamps.value = task.targetStamp;
      animateStampIndex.value = task.targetStamp - 1; // 1-based to 0-based
      isShaking.value = true;

      // 400ms 敺?甇Ｘ????澆?皛輻??寞?
      setTimeout(() => {
        isShaking.value = false;
        if (task.targetStamp === MAX_STAMPS) {
          isFullStamps.value = true;
        }
      }, 400);

      // 7蝘??嗉絲?∠?銝血???promise
      setTimeout(() => {
        showCard.value = false;
        setTimeout(resolve, 800); // 蝑??嗉絲 transition
      }, 7000);

    }, 800);
  });
}

function getStampStyle(index: number) {
  const seedPref = `${cardData.value.name}_R${cardData.value.round}_S${index + 1}`;
  const rot = (getDeterministicRandom(seedPref + "_rot") * 50) - 25;
  const dx = (getDeterministicRandom(seedPref + "_x") * 1.5) - 0.75;
  const dy = (getDeterministicRandom(seedPref + "_y") * 1.5) - 0.75;
  const scale = 0.95 + (getDeterministicRandom(seedPref + "_s") * 0.1);

  return {
    "--rot": `${rot}deg`,
    "--dx": `${dx}px`,
    "--dy": `${dy}px`,
    "--scale": scale
  };
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
  <section class="overlay-panel" aria-labelledby="member-overlay-title">
    <header class="page-header visually-hidden-obs">
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

    <!-- ?∠?銝駁?摰孵 -->
    <div class="card-container" :class="{ 'show': showCard }">
      <div class="loyalty-card" :class="{ 'full-stamps': isFullStamps, 'shake': isShaking }">
        <div
          class="card-inner-bg"
          :style="{
            backgroundImage: cssUrl(customBgUrl),
            '--stamp-image': cssUrl(customStampUrl)
          }"
        >
          <div class="card-overlay" :style="{ opacity: customBgUrl ? '1' : '0.6' }"></div>
          <div class="card-bg-pattern"></div>

          <!-- 撌血?憛?Avatar & ?梁迂 -->
          <div class="card-left">
            <div class="user-avatar-wrap">
              <img
                class="user-avatar"
                :src="cardData.avatarUrl || 'data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 100 100\'><circle cx=\'50\' cy=\'50\' r=\'50\' fill=\'%23666\'/><text x=\'50\' y=\'55\' font-family=\'Arial\' font-size=\'40\' font-weight=\'bold\' fill=\'%23fff\' text-anchor=\'middle\' dominant-baseline=\'middle\'>?</text></svg>'"
                @error="($event.target as HTMLImageElement).src = 'data:image/svg+xml;utf8,<svg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 100 100\'><circle cx=\'50\' cy=\'50\' r=\'50\' fill=\'%23666\'/><text x=\'50\' y=\'55\' font-family=\'Arial\' font-size=\'40\' font-weight=\'bold\' fill=\'%23fff\' text-anchor=\'middle\' dominant-baseline=\'middle\'>?</text></svg>'"
              />
            </div>
            <div class="user-name-container">
              <div class="user-name">{{ cardData.name }}</div>
            </div>
            <div class="vip-badge">?駁?撠惇????/div>
          </div>

          <!-- ?喳?憛???蝡 -->
          <div class="card-right">
            <div class="card-header">
              <div class="card-title">????蝣?/div>
            </div>

            <div class="stamps-grid">
              <div
                v-for="(_, index) in MAX_STAMPS"
                :key="index"
                class="stamp-slot"
                :class="{
                  'stamped': index < currentStamps,
                  'animate-stamp': index === animateStampIndex
                }"
                :style="getStampStyle(index)"
              >
                {{ index < currentStamps ? '' : (index + 1) }}
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>

    <ConfirmDialog
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
/* ?典?撘???詨?蝢?*/
:root {
  --stamp-image: url('data:image/svg+xml;utf8,<svg viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg"><circle cx="50" cy="50" r="45" fill="none" stroke="%23ff3366" stroke-width="6" stroke-dasharray="8 4"/><circle cx="50" cy="50" r="38" fill="none" stroke="%23ff3366" stroke-width="2"/><g transform="rotate(-15 50 50) translate(8, 12) scale(0.85)" fill="%23ff3366"><path d="M 50,45 C 75,45 85,60 85,75 C 85,90 65,95 50,90 C 35,95 15,90 15,75 C 15,60 25,45 50,45 Z"/><ellipse cx="25" cy="35" rx="10" ry="15" transform="rotate(-30 25 35)"/><ellipse cx="40" cy="20" rx="10" ry="15"/><ellipse cx="60" cy="20" rx="10" ry="15"/><ellipse cx="75" cy="35" rx="10" ry="15" transform="rotate(30 75 35)"/></g></svg>');
  --frame-color: #BDE8E8;
  --fox-blue: #1A3D6E;
  --ice-glint: #FFFFFF;
  --gold-primary: #ffd700;
  --gold-secondary: #ffb700;
  --gold-light: #fff7a0;
  --gold-glow: rgba(255, 215, 0, 0.8);
  --glass-bg: linear-gradient(135deg, rgba(255, 255, 255, 0.98) 0%, rgba(235, 250, 255, 0.9) 100%);
  --accent-glow: rgba(189, 232, 232, 0.5);
  --card-bg-image: linear-gradient(135deg, rgba(20, 20, 25, 0.85) 0%, rgba(45, 45, 50, 0.95) 100%);
}

/* OBS 銝??嗆?嚗憿舐內????*/
@media screen and (max-width: 100px), screen and (max-height: 100px) {
  .visually-hidden-obs {
    display: none !important;
  }
}
</style>

<style scoped>
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
  overflow: visible;
}

.loyalty-card::after {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  border-radius: 24px;
  pointer-events: none;
  z-index: -1;
  opacity: 0;
  transition: opacity 0.8s ease;
  box-shadow:
    0 0 4px #ffd700,
    0 0 12px rgba(255, 215, 0, 0.3);
}

.loyalty-card::before {
  content: '';
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  border-radius: 24px;
  padding: 3px;
  background: linear-gradient(45deg, var(--gold-primary), var(--gold-secondary), var(--gold-light), var(--gold-primary));
  background-size: 300% 300%;
  animation: gradientBorder 4s ease infinite;
  -webkit-mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
  mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
  -webkit-mask-composite: xor;
  mask-composite: exclude;
  pointer-events: none;
  z-index: 10;
}

.loyalty-card.full-stamps {
  box-shadow: 0 0 25px var(--gold-primary), inset 0 0 60px rgba(255, 215, 0, 0.5);
  animation: goldPulse 2s infinite alternate;
}

.loyalty-card.full-stamps::after {
  opacity: 1;
}

@keyframes goldPulse {
  0% { box-shadow: 0 0 15px rgba(255, 215, 0, 0.5), inset 0 0 30px rgba(255, 215, 0, 0.3); }
  100% { box-shadow: 0 0 35px var(--gold-primary), inset 0 0 60px rgba(255, 215, 0, 0.5); }
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
  box-shadow: inset 0 0 10px rgba(0, 0, 0, 0.8), inset 0 1px 0 rgba(255, 255, 255, 0.1);
  z-index: 1;
}

.card-overlay {
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  background: linear-gradient(135deg, rgba(0, 0, 0, 0.5) 0%, rgba(0, 0, 0, 0.8) 100%);
  z-index: 1;
  opacity: 0.6;
  pointer-events: none;
}

.card-bg-pattern {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-image: radial-gradient(rgba(255, 255, 255, 0.05) 1px, transparent 1px);
  background-size: 15px 15px;
  opacity: 1;
  z-index: 1;
  pointer-events: none;
}

.card-left {
  width: 220px;
  background: transparent;
  padding: 30px 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  z-index: 2;
  position: relative;
}

.card-left::after {
  content: '';
  position: absolute;
  right: 0;
  top: 15%; bottom: 15%;
  width: 1px;
  background: rgba(255, 215, 0, 0.3);
}

.user-avatar-wrap {
  position: relative;
  width: 100px;
  height: 100px;
  margin-bottom: 20px;
}

.user-avatar {
  width: 100px;
  height: 100px;
  border-radius: 50%;
  border: 3px solid #ffd700;
  box-shadow: 0 0 15px rgba(255, 215, 0, 0.4);
  object-fit: cover;
  background-color: #222;
  animation: avatarPulse 3s infinite alternate;
}

@keyframes avatarPulse {
  0% { box-shadow: 0 0 15px rgba(255, 215, 0, 0.3); border-color: #ffd700; }
  100% { box-shadow: 0 0 25px rgba(255, 215, 0, 0.7); border-color: #fff7a0; }
}

.user-name-container {
  width: 100%;
  height: 50px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 5px;
}

.user-name {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
  text-align: center;
  text-shadow: 0 2px 8px rgba(0, 0, 0, 0.9);
  width: 100%;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  word-wrap: break-word;
  word-break: break-all;
  line-height: 1.2;
  letter-spacing: 1px;
  color: #fff;
}

.vip-badge {
  margin-top: 10px;
  background: linear-gradient(135deg, #c59b27 0%, #ecd06f 50%, #c59b27 100%);
  color: #111;
  font-size: 14px;
  font-weight: 900;
  padding: 4px 16px;
  border-radius: 14px;
  text-transform: uppercase;
  letter-spacing: 1px;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.5), inset 0 1px 1px rgba(255, 255, 255, 0.6);
  border: 1px solid #7a5c13;
}

.card-right {
  flex: 1;
  padding: 25px 30px;
  display: flex;
  flex-direction: column;
  justify-content: center;
  position: relative;
  z-index: 2;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 15px;
  border-bottom: 1px solid rgba(255, 215, 0, 0.3);
  padding-bottom: 10px;
}

.card-title {
  font-size: 22px;
  font-weight: 900;
  color: #ffd700;
  text-shadow: 0 2px 5px rgba(0, 0, 0, 0.5);
  letter-spacing: 2px;
  margin: 0;
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
  box-shadow: inset 0 4px 8px rgba(0, 0, 0, 0.6), 0 1px 0 rgba(255, 255, 255, 0.05);
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  background: rgba(0, 0, 0, 0.5);
  font-size: 18px;
  color: rgba(255, 215, 0, 0.2);
  font-weight: bold;
}

/* ????蝡?憪?唳???*/
.stamp-slot.stamped::after {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  width: 54px;
  height: 54px;
  background-image: var(--stamp-image);
  background-size: contain;
  background-repeat: no-repeat;
  background-position: center;
  opacity: 0.95;
  transform: translate(calc(-50% + var(--dx, 0px)), calc(-50% + var(--dy, 0px))) rotate(var(--rot, -15deg)) scale(var(--scale, 1));
  filter: drop-shadow(0 0 5px rgba(255, 51, 102, 0.5));
}

.stamp-slot.animate-stamp::after {
  animation: stampInWithVars 0.5s cubic-bezier(0.175, 0.885, 0.32, 1.275) forwards;
}

@keyframes stampInWithVars {
  0% {
    transform: translate(calc(-50% + var(--dx, 0px)), calc(-50% + var(--dy, 0px))) scale(3) rotate(calc(var(--rot, -15deg) + 30deg));
    opacity: 0;
  }
  50% {
    transform: translate(calc(-50% + var(--dx, 0px)), calc(-50% + var(--dy, 0px))) scale(calc(var(--scale, 1) * 0.8)) rotate(calc(var(--rot, -15deg) - 10deg));
    opacity: 1;
    filter: drop-shadow(0 0 10px rgba(255, 51, 102, 0.8));
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

.shake {
  animation: cardShake 0.4s ease-in-out;
}

@keyframes gradientBorder {
  0% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}
</style>
