import { createRouter, createWebHistory } from "vue-router";
import AdminStatusView from "@/views/admin/AdminStatusView.vue";
import ChatOutboxView from "@/views/admin/ChatOutboxView.vue";
import EventMonitorView from "@/views/admin/EventMonitorView.vue";
import MembersView from "@/views/admin/MembersView.vue";
import OverlayPresetsView from "@/views/admin/OverlayPresetsView.vue";
import RuleEditorView from "@/views/admin/RuleEditorView.vue";
import RulesView from "@/views/admin/RulesView.vue";
import SettingsView from "@/views/admin/SettingsView.vue";
import SimulateView from "@/views/admin/SimulateView.vue";
import TimersView from "@/views/admin/TimersView.vue";
import TwitchAuthView from "@/views/admin/TwitchAuthView.vue";
import MonitorDashboardView from "@/views/admin/MonitorDashboardView.vue";
import AlertOverlayView from "@/views/overlay/AlertOverlayView.vue";
import ChatOverlayView from "@/views/overlay/ChatOverlayView.vue";
import MemberOverlayView from "@/views/overlay/MemberOverlayView.vue";

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/", redirect: "/monitor" },
    { path: "/monitor", name: "monitor", component: MonitorDashboardView },
    { path: "/status", name: "status", component: AdminStatusView },
    { path: "/simulate", name: "simulate", component: SimulateView },
    { path: "/events", name: "event-monitor", component: EventMonitorView },
    { path: "/members", name: "members", component: MembersView },
    { path: "/overlay-presets", name: "overlay-presets", component: OverlayPresetsView },
    { path: "/settings", name: "settings", component: SettingsView },
    { path: "/rules", name: "rules", component: RulesView },
    { path: "/timers", name: "timers", component: TimersView },
    { path: "/chat-outbox", name: "chat-outbox", component: ChatOutboxView },
    { path: "/rules/new", name: "rule-create", component: RuleEditorView },
    { path: "/twitch", name: "twitch-auth", component: TwitchAuthView },
    { path: "/overlay/chat", name: "overlay-chat", component: ChatOverlayView },
    { path: "/overlay/alerts", name: "overlay-alerts", component: AlertOverlayView },
    { path: "/overlay/member", name: "overlay-member", component: MemberOverlayView }
  ]
});
