/**
 * =========================================================
 * Vulperonex Chat Overlay Controller
 * =========================================================
 */

const { createApp, ref, onMounted } = Vue;

const app = createApp({
    setup() {
        const messages = ref([]);
        let messageCounter = 0;

        // Restrict visible message count to 15. Excess messages trigger fade-out animations and leave the DOM.
        const MAX_MESSAGES = 15;

        onMounted(() => {
            OverlayCommon.initSignalRConnection('/hubs/overlay/chat', {
                event: (data) => {
                    console.log("[DEBUG] SignalR chat event payload received: ", data);

                    // Parse segments to safely render HTML and emotes
                    let messageHtml = "";
                    if (data.segments && Array.isArray(data.segments)) {
                        messageHtml = data.segments.map(seg => {
                            const type = seg.type || seg.kind || 'text';
                            const value = seg.value || seg.text || '';
                            if (type === 'emote') {
                                return `<img src="${value}" class="chat-emote" />`;
                            }
                            return value
                                .replace(/&/g, "&amp;")
                                .replace(/</g, "&lt;")
                                .replace(/>/g, "&gt;")
                                .replace(/"/g, "&quot;")
                                .replace(/'/g, "&#039;");
                        }).join("");
                    } else {
                        messageHtml = (data.htmlMessage || data.HtmlMessage || "").trim();
                    }

                    const newMsg = {
                        id: ++messageCounter,
                        username: data.displayName || data.DisplayName || "Unknown User",
                        nameColorHex: data.colorHex || data.ColorHex || '',
                        message: messageHtml,
                        badges: data.badges || data.Badges || []
                    };

                    messages.value.push(newMsg);

                    if (messages.value.length > MAX_MESSAGES) {
                        messages.value.shift();
                    }
                },
                cleared: () => {
                    console.log("🧹 Clear chat command received");
                    messages.value = [];
                }
            });
        });

        return { messages };
    }
});

app.mount('#app');
