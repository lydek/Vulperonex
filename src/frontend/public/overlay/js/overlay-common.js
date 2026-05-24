/**
 * =========================================================
 * OmniCommander Overlay Common JS
 * =========================================================
 * ?? SignalR ???撠???臬極?瑁??芸??脣翰???氬? * ?⊥??嗡?鞈湛??詨捆??Vanilla JS ??Vue 蝑憓? */

const OverlayCommon = {
    /**
     * ????SignalR Hub ?????     * @param {string} hubUrl - Hub ?撠楝敺?(靘?: '/chathub', '/memberhub')
     * @param {object} eventHandlers - 閬酉??鈭辣皜 (Key=鈭辣?迂, Value=??賣)
     * @returns {signalR.HubConnection} - 餈?撱箇?憟賜????撖阡? (?亙仃??? null)
     */
    initSignalRConnection: function (hubUrl, eventHandlers) {
        if (typeof signalR === 'undefined') {
            console.error("[OverlayCommon] ?芾???SignalR 摰Ｘ蝡舐?撘澈嚗?蝣箄? HTML 銝剜?撘 js");
            return null;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();

        // 蝬?憭?喳???虜撘?        if (eventHandlers && typeof eventHandlers === 'object') {
            for (const [eventName, handler] of Object.entries(eventHandlers)) {
                connection.on(eventName, handler);
            }
        }

        // --- ?典?皜??蝳行???---

        // ??貉??蜓?葉?琿??嚗甇?WebSocket 瘣拇?
        window.addEventListener('beforeunload', () => {
            if (connection) {
                console.log(`?? [OverlayCommon] 甇?銝剜 ${hubUrl} ???...`);
                connection.stop();
            }
        });

        // --- ?典??脫蝺???望?圈蝳?(Cache Buster) ---

        // ?交敺垢銝餃???渡??誘 (靘?敺?銵冽暺??瑟)
        connection.on("Reload", () => {
            console.log("?? ?嗅隡箸??刻?瘙蜓???唳????..");
            this.forceReload();
        });

        // ??dotnet run ??????????撘瑕????唾??瑟 (蝎? OBS 敹怠?)
        connection.onreconnected(() => {
            console.log(`?? ?菜葫?唬撩? (${hubUrl}) ??嚗迤?典撥?園??唳???Ｗ翰??..`);
            setTimeout(() => this.forceReload(), 500);
        });

        // 蝯扔?脩戌嚗?蜓璈????敺孵??琿?嚗?蝘??芸??隞仿?恍?⊥香
        connection.onclose(async () => {
            console.log(`???蜓璈?(${hubUrl}) ???撌脫??撠 5 蝘??芸??蝬脤?...`);
            setTimeout(() => window.location.reload(), 5000);
        });

        // ?瑁????
        connection.start()
            .then(() => console.log(`??撌脫?????蝡?SignalR 隡箸???(${hubUrl})`))
            .catch(err => console.error(`??SignalR ??憭望? (${hubUrl}): `, err));

        return connection;
    },

    /**
     * ????唾??撘瑕?頛?嚗?瘝?OBS ?汗?典翰??憿?     */
    forceReload: function() {
        const url = new URL(window.location.href);
        url.searchParams.set('t', new Date().getTime());
        window.location.href = url.toString();
    },

    /**
     * ????鈭?Ｙ???(Deterministic LCG)
     * ?冽??∪蝡???蝔梯??潭?Ｙ??箏??雿???     * @param {string} seedStr
     * @returns {number} 0 ~ 1 銋?????     */
    getDeterministicRandom: function(seedStr) {
        let hash = 5381;
        for (let i = 0; i < seedStr.length; i++) {
            hash = (hash * 33) ^ seedStr.charCodeAt(i);
        }
        hash = Math.abs(hash) || 1;
        // Linear Congruential Generator
        hash = (hash * 9301 + 49297) % 233280;
        return hash / 233280.0;
    }
};

window.OverlayCommon = OverlayCommon;
