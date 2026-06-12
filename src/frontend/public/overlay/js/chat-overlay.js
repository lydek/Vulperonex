/**
 * =========================================================
 * Vulperonex Chat Overlay Controller (Vanilla JS Edition)
 * =========================================================
 * 100% Pure Vanilla JS DOM operation to comply with CSP script-src 'self'
 * WITHOUT triggering unsafe-eval errors (no Vue compiler used).
 */

(function () {
    const urlParams = new URLSearchParams(window.location.search);
    const preset = urlParams.get('preset') || 'vulperonex-default';
    const isPreview = urlParams.get('preview') === '1';

    // Apply preset and preview class to body for theme CSS selection
    document.addEventListener('DOMContentLoaded', () => {
        document.body.classList.add(`preset-${preset}`);
        if (isPreview) {
            document.body.classList.add('preview-mode');
        }

        const chatContainer = document.getElementById('chat-container');
        if (!chatContainer) return;

        chatContainer.classList.add(`preset-${preset}`);

        const MAX_MESSAGES = 15;
        const showMemberCard = urlParams.get('showMemberCard') !== 'false';
        let cardBgUrl = '';
        let cardStampUrl = '';

        async function fetchGlobalCardSettings() {
            try {
                const bgResponse = await fetch('/api/config/overlay.member.background_url');
                const stampResponse = await fetch('/api/config/overlay.member.stamp_url');
                if (bgResponse.ok) {
                    const bgData = await bgResponse.json();
                    if (bgData.value) cardBgUrl = bgData.value;
                }
                if (stampResponse.ok) {
                    const stampData = await stampResponse.json();
                    if (stampData.value) cardStampUrl = stampData.value;
                }
            } catch (error) {}
        }

        // Shared history syncing flag
        let isSyncingHistory = true;
        setTimeout(() => {
            isSyncingHistory = false;
            scrollToLatestMessage();
            console.log("[OverlayChat] History sync complete. Live overlay rendering active.");
        }, 1500);

        // Track already-rendered dedicated check-in card events. Older builds
        // deduped by "displayName + count", which incorrectly dropped valid
        // rapid repeats when cooldown/test-mode kept the count unchanged.
        const renderedCheckInEventIds = new Set();
        function markCheckInRendered(eventId) {
            if (!eventId) {
                return;
            }

            renderedCheckInEventIds.add(eventId);
            if (renderedCheckInEventIds.size > 200) {
                const firstKey = renderedCheckInEventIds.values().next().value;
                renderedCheckInEventIds.delete(firstKey);
            }
        }
        function isCheckInAlreadyRendered(eventId) {
            return !!eventId && renderedCheckInEventIds.has(eventId);
        }

        // Escapes viewer-controlled values before they enter an HTML template
        // string; covers both element text and quoted attribute contexts.
        function escapeHtml(value) {
            return String(value ?? '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        // Shared Member Stamp Card HTML generator
        function buildMemberCardHtml(rawDisplayName, total, rawAvatarUrl, isHistory = false) {
            const displayName = escapeHtml(rawDisplayName);
            const avatarUrl = rawAvatarUrl ? escapeHtml(rawAvatarUrl) : null;
            const stamps = (total % 10 === 0) ? 10 : (total % 10);
            const currentRound = Math.max(1, Math.ceil(total / 10));

            // Build deterministic random layout for stamp slots
            let stampsHtml = '';
            for (let i = 1; i <= 10; i++) {
                // In history replays we render the final stamp static immediately to avoid clashing animations
                const isStamped = isHistory ? (i <= stamps) : (i < stamps);
                const seedPref = displayName + "_R" + currentRound + "_S" + i;
                const rot = (OverlayCommon.getDeterministicRandom(seedPref + "_rot") * 50) - 25;
                const dx = (OverlayCommon.getDeterministicRandom(seedPref + "_x") * 1.5) - 0.75;
                const dy = (OverlayCommon.getDeterministicRandom(seedPref + "_y") * 1.5) - 0.75;
                const scale = 0.95 + (OverlayCommon.getDeterministicRandom(seedPref + "_s") * 0.1);

                stampsHtml += `
                <div class="stamp-slot ${isStamped ? 'stamped' : ''}" 
                     style="--rot: ${rot}deg; --dx: ${dx}px; --dy: ${dy}px; --scale: ${scale};">
                     ${isStamped ? '' : i}
                </div>`;
            }

            // CSS Overrides for background configuration
            const customBgStyle = cardBgUrl ? `background-image: url('${cardBgUrl}');` : '';
            const overlayOpacity = cardBgUrl ? '1' : '0.6';
            const customStampVar = cardStampUrl ? `--stamp-image: url('${cardStampUrl}');` : '';

            return `
            <div class="chat-checkin-wrapper" style="width: 420px; max-width: 100%; height: 210px; position: relative; margin-top: 10px; overflow: hidden; border-radius: 16px; box-shadow: 0 8px 16px rgba(0,0,0,0.4); ${customStampVar}">
                <div class="loyalty-card" style="width: 600px; height: 300px; transform: scale(0.7); transform-origin: top left; ${customBgStyle}">
                    <div class="card-inner-bg">
                        <div class="card-overlay" style="opacity: ${overlayOpacity};"></div>
                        <div class="card-bg-pattern"></div>
                        <div class="card-left">
                            <div class="user-avatar-wrap">
                                <img class="user-avatar" src="${avatarUrl || "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><circle cx='50' cy='50' r='50' fill='%23666'/></svg>"}" />
                            </div>
                            <div class="user-name-container">
                                <div class="user-name">${displayName}</div>
                            </div>
                            <div class="vip-badge">Channel Member</div>
                        </div>
                        <div class="card-right">
                            <div class="card-header">
                                <div class="card-title">Member Stamp Card</div>
                            </div>
                            <div class="stamps-grid">
                                ${stampsHtml}
                            </div>
                        </div>
                    </div>
                </div>
            </div>`;
        }

        function appendChatLine(chatLine) {
            chatContainer.appendChild(chatLine);
            bindMediaScrollRefresh(chatLine);
            scrollToLatestMessage();

            setTimeout(() => {
                chatLine.classList.remove('enter');
                scrollToLatestMessage();
            }, 300);

            if (chatContainer.children.length > MAX_MESSAGES) {
                const oldestLine = chatContainer.firstElementChild;
                if (oldestLine) {
                    oldestLine.classList.add('exit');
                    setTimeout(() => {
                        oldestLine.remove();
                        scrollToLatestMessage();
                    }, 300);
                }
            }
        }

        function scrollToLatestMessage() {
            const updateScroll = () => {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            };

            if (typeof window.requestAnimationFrame === 'function') {
                window.requestAnimationFrame(updateScroll);
            } else {
                setTimeout(updateScroll, 0);
            }
        }

        function bindMediaScrollRefresh(chatLine) {
            chatLine.querySelectorAll('img').forEach(img => {
                if (img.complete) {
                    return;
                }

                img.addEventListener('load', scrollToLatestMessage, { once: true });
                img.addEventListener('error', scrollToLatestMessage, { once: true });
            });
        }

        function renderCheckInCardMessage(data) {
            const snap = data.memberSnapshot;
            if (!snap) return;

            const displayName = snap.displayName || data.displayName || "Unknown User";
            const total = snap.checkInCount || 1;
            const avatarUrl = snap.avatarUrl || "";
            if (isCheckInAlreadyRendered(data.eventId)) {
                return;
            }

            markCheckInRendered(data.eventId);

            const stamps = (total % 10 === 0) ? 10 : (total % 10);
            const chatLine = document.createElement('div');
            chatLine.className = 'chat-line enter checkin-message';
            chatLine.setAttribute('role', 'listitem');

            const title = document.createElement('div');
            title.className = 'chat-username';
            title.style.color = data.colorHex || '#ffd700';
            title.style.display = 'block';
            title.style.marginBottom = '8px';
            title.textContent = data.displayName || '打卡系統';
            chatLine.appendChild(title);

            const cardContainer = document.createElement('div');
            cardContainer.innerHTML = buildMemberCardHtml(displayName, total, avatarUrl, isSyncingHistory);
            chatLine.appendChild(cardContainer);
            appendChatLine(chatLine);

            if (!isSyncingHistory) {
                setTimeout(() => {
                    const stampsGrid = cardContainer.querySelector('.stamps-grid');
                    if (stampsGrid) {
                        const targetSlot = stampsGrid.children[stamps - 1];
                        if (targetSlot) {
                            targetSlot.classList.add('stamped', 'animate-stamp');
                            targetSlot.textContent = '';
                            const wrapper = cardContainer.querySelector('.chat-checkin-wrapper');
                            if (wrapper) {
                                wrapper.classList.add('shake');
                                setTimeout(() => wrapper.classList.remove('shake'), 400);
                            }
                        }
                    }
                }, 800);
            }
        }

        // Unified Chat Line Renderer (Vanilla JS)
        function renderChatMessage(data, isCheckIn = false) {
            // 1. Create message wrapper
            const chatLine = document.createElement('div');
            chatLine.className = 'chat-line enter';
            if (isCheckIn) {
                chatLine.classList.add('checkin-message');
            }
            chatLine.setAttribute('role', 'listitem');

            // 2. Add badges
            if (data.badges && Array.isArray(data.badges)) {
                data.badges.forEach(badgeUrl => {
                    const img = document.createElement('img');
                    img.src = badgeUrl;
                    img.className = 'chat-badge';
                    img.alt = 'badge';
                    img.onerror = () => img.style.display = 'none';
                    chatLine.appendChild(img);
                });
            }

            // 3. Add username
            const usernameSpan = document.createElement('span');
            usernameSpan.className = 'chat-username';
            usernameSpan.style.color = data.colorHex || '';
            usernameSpan.textContent = data.displayName || "Unknown User";
            chatLine.appendChild(usernameSpan);

            // 4. Add colon
            const colonSpan = document.createElement('span');
            colonSpan.className = 'chat-colon';
            colonSpan.textContent = ': ';
            chatLine.appendChild(colonSpan);

            // 5. Add content (segments support text/emote)
            const contentSpan = document.createElement('span');
            contentSpan.className = 'chat-content';

            // Chat text is viewer-controlled input: render via the segments path
            // (createTextNode / explicit <img>) so it can never be interpreted as
            // HTML. The legacy htmlMessage fallback is rendered as plain text —
            // never innerHTML — so a payload without segments cannot become XSS.
            if (data.segments && Array.isArray(data.segments)) {
                data.segments.forEach(seg => {
                    const type = seg.type || seg.kind || 'text';
                    const value = seg.value || seg.text || '';
                    if (type === 'emote') {
                        const emoteImg = document.createElement('img');
                        emoteImg.src = value;
                        emoteImg.className = 'chat-emote';
                        emoteImg.alt = 'emote';
                        contentSpan.appendChild(emoteImg);
                    } else {
                        const textNode = document.createTextNode(value);
                        contentSpan.appendChild(textNode);
                    }
                });
            } else {
                contentSpan.textContent = data.htmlMessage || data.HtmlMessage || "";
            }
            chatLine.appendChild(contentSpan);

            // 6. Add Member Loyalty Chip
            if (data.memberSnapshot) {
                const chip = document.createElement('span');
                chip.className = 'member-chip';
                chip.setAttribute('data-testid', 'chat-member-chip');

                if (data.memberSnapshot.avatarUrl) {
                    const chipAvatar = document.createElement('img');
                    chipAvatar.src = data.memberSnapshot.avatarUrl;
                    chipAvatar.className = 'member-chip__avatar';
                    chipAvatar.alt = '';
                    chip.appendChild(chipAvatar);
                }

                const chipLabel = document.createElement('span');
                chipLabel.className = 'member-chip__label';
                chipLabel.textContent = data.memberSnapshot.displayName || 'Member';
                chip.appendChild(chipLabel);

                const chipCount = document.createElement('span');
                chipCount.className = 'member-chip__count';
                chipCount.textContent = `#${data.memberSnapshot.checkInCount || 1}`;
                chip.appendChild(chipCount);

                chatLine.appendChild(chip);
            }

            // 7. Append to container and handle max messages limit with animation
            appendChatLine(chatLine);
        }

        // Connect to SignalR /hubs/overlay/chat for normal messages
        OverlayCommon.initSignalRConnection('/hubs/overlay/chat', {
            event: (data) => {
                console.log("[DEBUG] SignalR chat event payload received: ", data);
                if (data.variant === 'checkin-card') {
                    renderCheckInCardMessage(data);
                    return;
                }
                renderChatMessage(data, false);
            },
            cleared: () => {
                console.log("🧹 Clear chat command received");
                chatContainer.innerHTML = '';
            }
        });

        if (showMemberCard) {
            fetchGlobalCardSettings();
            setInterval(fetchGlobalCardSettings, 20000);
        }
    });
})();
