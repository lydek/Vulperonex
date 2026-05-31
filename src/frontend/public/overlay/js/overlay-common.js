/**
 * =========================================================
 * OmniCommander Overlay Common JS
 * =========================================================
 * SignalR infrastructure for connecting static HTML overlays.
 * Supports Vanilla JS or Vue-based overlays.
 */

const OverlayCommon = {
    /**
     * Initializes the SignalR Hub connection
     * @param {string} hubUrl - Relative URL of the Hub (e.g., '/chathub', '/memberhub')
     * @param {object} eventHandlers - Event handlers to register (Key=event name, Value=callback function)
     * @returns {signalR.HubConnection|null} - The connection object, or null if failed
     */
    initSignalRConnection: function (hubUrl, eventHandlers) {
        if (typeof signalR === 'undefined') {
            console.error("[OverlayCommon] SignalR library is missing. Make sure to include the SignalR client JS in HTML.");
            return null;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();

        // Register event handlers
        if (eventHandlers && typeof eventHandlers === 'object') {
            for (const [eventName, handler] of Object.entries(eventHandlers)) {
                connection.on(eventName, handler);
            }
        }

        // --- Graceful Disconnect ---
        // Close connection gracefully on page unload to prevent orphaned WebSocket connections
        window.addEventListener('beforeunload', () => {
            if (connection) {
                console.log(`[OverlayCommon] Closing connection to ${hubUrl}...`);
                connection.stop();
            }
        });

        // --- Development & Hot-Reload Helpers ---

        // Force a page reload when triggered by the server
        connection.on("Reload", () => {
            console.log("[OverlayCommon] Reload signal received from server. Reloading page...");
            this.forceReload();
        });

        // When reconnecting (e.g., after dotnet run restarts), reload the page to clear the cache (important for OBS cache)
        connection.onreconnected(() => {
            console.log(`[OverlayCommon] Reconnected to ${hubUrl}. Reloading page to clear cache...`);
            setTimeout(() => this.forceReload(), 500);
        });

        // Connection closed fallback: if connection is completely disconnected, retry loading the page
        connection.onclose(async () => {
            console.log(`[OverlayCommon] Connection closed for ${hubUrl}. Retrying in 5 seconds...`);
            setTimeout(() => window.location.reload(), 5000);
        });

        // Start connection
        connection.start()
            .then(() => console.log(`[OverlayCommon] SignalR connection established to ${hubUrl}`))
            .catch(err => console.error(`[OverlayCommon] SignalR connection failed for ${hubUrl}: `, err));

        return connection;
    },

    /**
     * Appends a time-based query parameter (cache buster) and reloads the page.
     * This ensures OBS browser sources reload the latest layout instead of using cached assets.
     */
    forceReload: function() {
        const url = new URL(window.location.href);
        url.searchParams.set('t', new Date().getTime());
        window.location.href = url.toString();
    },

    /**
     * Generates a deterministic pseudo-random number based on a seed string.
     * Useful for rendering consistent element positions (e.g., stamp cards or bubbles)
     * across page reloads without storing state.
     * @param {string} seedStr
     * @returns {number} 0 ~ 1
     */
    getDeterministicRandom: function(seedStr) {
        let hash = 5381;
        for (let i = 0; i < seedStr.length; i++) {
            hash = (hash * 33) ^ seedStr.charCodeAt(i);
        }
        hash = Math.abs(hash) || 1;
        // Linear Congruential Generator
        hash = (hash * 9301 + 49297) % 233280;
        return hash / 233280.0;
    },

    /**
     * Initializes a postMessage listener for preview-mode background colour changes.
     * The admin panel sends { type: 'set-bg', style: { backgroundColor: '...' } }
     * and this handler applies it to the document body so the iframe respects
     * the chosen preview background.
     */
    initPreviewBridge: function () {
        window.addEventListener('message', function (e) {
            if (!e.data || e.data.type !== 'set-bg') return;
            var style = e.data.style;
            if (!style || typeof style !== 'object') return;

            // Reset previous background properties before applying new ones
            document.body.style.backgroundColor = '';
            document.body.style.backgroundImage = '';
            document.body.style.backgroundSize = '';
            document.body.style.backgroundPosition = '';

            for (var key in style) {
                if (style.hasOwnProperty(key)) {
                    document.body.style[key] = style[key];
                }
            }
        });
    }
};

// Auto-init preview bridge when loaded inside an iframe with ?preview=1
if (window.location.search.includes('preview=1')) {
    OverlayCommon.initPreviewBridge();
}

window.OverlayCommon = OverlayCommon;

