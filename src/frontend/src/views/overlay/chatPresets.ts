import type { Component } from "vue";
import ChatPresetCompact from "./presets/ChatPresetCompact.vue";
import ChatPresetDefault from "./presets/ChatPresetDefault.vue";
import ChatPresetMemberCardEmbed from "./presets/ChatPresetMemberCardEmbed.vue";

export interface ChatOverlayPreset {
  id: string;
  label: string;
  description: string;
  component: Component;
}

export const chatOverlayPresets: ChatOverlayPreset[] = [
  {
    id: "vulperonex-default",
    label: "Vulperonex default",
    description: "Stacked list with display name and message segments.",
    component: ChatPresetDefault
  },
  {
    id: "compact-line",
    label: "Compact line",
    description: "Single-line name and message rows for dense chat overlays.",
    component: ChatPresetCompact
  },
  {
    id: "member-card-inline",
    label: "Member card inline",
    description: "Chat rows with an optional member loyalty chip.",
    component: ChatPresetMemberCardEmbed
  }
];

export const defaultChatOverlayPresetId = "vulperonex-default";

export function findChatOverlayPreset(id: string | null | undefined): ChatOverlayPreset {
  if (!id) {
    return chatOverlayPresets[0];
  }

  return chatOverlayPresets.find((preset) => preset.id === id) ?? chatOverlayPresets[0];
}
