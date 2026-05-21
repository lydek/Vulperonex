import { createRouter, createWebHistory } from "vue-router";
import AdminStatusView from "@/views/admin/AdminStatusView.vue";
import SimulateView from "@/views/admin/SimulateView.vue";
import AlertOverlayView from "@/views/overlay/AlertOverlayView.vue";
import ChatOverlayView from "@/views/overlay/ChatOverlayView.vue";
import MemberOverlayView from "@/views/overlay/MemberOverlayView.vue";

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/", name: "status", component: AdminStatusView },
    { path: "/simulate", name: "simulate", component: SimulateView },
    { path: "/overlay/chat", name: "overlay-chat", component: ChatOverlayView },
    { path: "/overlay/alerts", name: "overlay-alerts", component: AlertOverlayView },
    { path: "/overlay/member", name: "overlay-member", component: MemberOverlayView }
  ]
});
