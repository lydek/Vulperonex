<script setup lang="ts">
defineProps<{
  displayName: string;
  avatarUrl?: string | null;
  checkInCount: number;
}>();
</script>

<template>
  <span class="member-chip" data-testid="chat-member-chip">
    <img v-if="avatarUrl" :src="avatarUrl" class="member-chip__avatar" alt="" />
    <span class="member-chip__label">{{ displayName }}</span>
    <span class="member-chip__count">#{{ checkInCount }}</span>
  </span>
</template>

<style scoped>
.member-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-left: 8px;
  padding: 2px 8px;
  border: 1px solid rgba(189, 232, 232, 0.45);
  border-radius: 999px;
  background: rgba(8, 20, 32, 0.78);
  color: #dff7f7;
  font-size: 0.78rem;
  line-height: 1;
  vertical-align: middle;
}

.member-chip__avatar {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  object-fit: cover;
}

.member-chip__label {
  font-weight: 700;
}

.member-chip__count {
  color: #97d9d9;
  font-weight: 600;
}
</style>
