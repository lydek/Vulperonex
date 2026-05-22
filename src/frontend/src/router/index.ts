import { createRouter, createWebHistory } from "vue-router";
import AdminStatusView from "@/views/admin/AdminStatusView.vue";
import EventMonitorView from "@/views/admin/EventMonitorView.vue";
import MembersView from "@/views/admin/MembersView.vue";
import RulesView from "@/views/admin/RulesView.vue";
import SimulateView from "@/views/admin/SimulateView.vue";
import AlertOverlayView from "@/views/overlay/AlertOverlayView.vue";
import ChatOverlayView from "@/views/overlay/ChatOverlayView.vue";
import MemberOverlayView from "@/views/overlay/MemberOverlayView.vue";

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/", name: "status", component: AdminStatusView },
    { path: "/simulate", name: "simulate", component: SimulateView },
    { path: "/events", name: "event-monitor", component: EventMonitorView },
    { path: "/members", name: "members", component: MembersView },
    { path: "/rules", name: "rules", component: RulesView },
    { path: "/overlay/chat", name: "overlay-chat", component: ChatOverlayView },
    { path: "/overlay/alerts", name: "overlay-alerts", component: AlertOverlayView },
    { path: "/overlay/member", name: "overlay-member", component: MemberOverlayView }
  ]
});
