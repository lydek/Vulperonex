<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  displayName: string;
  avatarUrl?: string | null;
  checkInCount: number;
}>();

const slots = computed(() => {
  const total = Math.max(1, props.checkInCount);
  const stamped = total % 10 === 0 ? 10 : total % 10;
  return Array.from({ length: 10 }, (_, index) => ({
    index: index + 1,
    stamped: index < stamped
  }));
});

const progress = computed(() => `${props.checkInCount % 10 === 0 ? 10 : props.checkInCount % 10 || 1}/10`);
</script>

<template>
  <div class="checkin-card">
    <div class="checkin-card__left">
      <div class="checkin-card__avatar-wrap">
        <img v-if="avatarUrl" :src="avatarUrl" class="checkin-card__avatar" alt="" />
        <span v-else class="checkin-card__avatar-fallback">?</span>
      </div>
      <strong class="checkin-card__name">{{ displayName }}</strong>
      <span class="checkin-card__badge">頻道專屬收集卡</span>
    </div>

    <div class="checkin-card__right">
      <div class="checkin-card__header">
        <strong>會員收集里程碑</strong>
        <span>{{ progress }}</span>
      </div>
      <div class="checkin-card__track" />
      <div class="checkin-card__grid">
        <div
          v-for="slot in slots"
          :key="slot.index"
          class="checkin-card__slot"
          :class="{ 'is-stamped': slot.stamped }"
        >
          {{ slot.stamped ? "" : slot.index }}
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.checkin-card {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 18px;
  padding: 20px;
  border-radius: 20px;
  background:
    radial-gradient(circle at top right, rgba(255, 215, 0, 0.18), transparent 35%),
    linear-gradient(180deg, #26221d 0%, #171411 100%);
  border: 4px solid #ffcf2e;
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.35);
  color: #fff6cf;
}

.checkin-card__left {
  display: grid;
  justify-items: center;
  align-content: start;
  gap: 12px;
}

.checkin-card__avatar-wrap {
  display: grid;
  place-items: center;
  width: 82px;
  height: 82px;
  border-radius: 999px;
  border: 3px solid #fff06c;
  background: #6f7278;
  overflow: hidden;
}

.checkin-card__avatar {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.checkin-card__avatar-fallback {
  font-size: 32px;
  font-weight: 800;
  color: #ffffff;
}

.checkin-card__name {
  font-size: 20px;
}

.checkin-card__badge {
  padding: 6px 14px;
  border-radius: 999px;
  background: linear-gradient(180deg, #f1d15f, #c59a21);
  color: #241b06;
  font-size: 12px;
  font-weight: 800;
}

.checkin-card__right {
  display: grid;
  gap: 12px;
}

.checkin-card__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 22px;
  font-weight: 900;
  color: #ffd733;
}

.checkin-card__header span {
  padding: 4px 10px;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.65);
  border: 1px solid rgba(255, 215, 51, 0.5);
  font-size: 16px;
}

.checkin-card__track {
  height: 6px;
  border-radius: 999px;
  background: rgba(255, 215, 51, 0.18);
}

.checkin-card__grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 10px;
}

.checkin-card__slot {
  display: grid;
  place-items: center;
  width: 52px;
  height: 52px;
  border-radius: 999px;
  background: rgba(0, 0, 0, 0.55);
  border: 2px solid rgba(255, 204, 0, 0.18);
  color: #9d8752;
  font-weight: 800;
}

.checkin-card__slot.is-stamped {
  background: radial-gradient(circle at 35% 35%, #4f79ff, #203d7a 70%);
  border-color: #e4c145;
  box-shadow: inset 0 0 0 3px rgba(255, 215, 0, 0.32);
}
</style>
