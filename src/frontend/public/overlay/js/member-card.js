/**
 * =========================================================
 * OmniCommander Member Card Controller (Vulperonex Ported)
 * =========================================================
 */

(function () {
    const MAX_STAMPS = 10;
    const MAX_QUEUE_SIZE = 10;
    let checkInQueue = [];
    let isAnimating = false;
    let hubConnection = null;
    const urlParams = new URLSearchParams(window.location.search);
    const isInline = urlParams.get('inline') === 'true';

    async function fetchSettings() {
        try {
            const bgResponse = await fetch('/api/config/overlay.member.background_url');
            const stampResponse = await fetch('/api/config/overlay.member.stamp_url');

            const card = document.getElementById('loyalty-card');
            const overlay = document.getElementById('card-overlay');
            if (card) {
                if (bgResponse.ok) {
                    const bgData = await bgResponse.json();
                    if (bgData.value) {
                        card.style.backgroundImage = `url('${bgData.value}')`;
                        if (overlay) overlay.style.opacity = '1';
                    } else {
                        card.style.backgroundImage = '';
                        if (overlay) overlay.style.opacity = '0.6';
                    }
                }

                if (stampResponse.ok) {
                    const stampData = await stampResponse.json();
                    if (stampData.value) {
                        document.documentElement.style.setProperty('--stamp-image', `url('${stampData.value}')`);
                    } else {
                        document.documentElement.style.setProperty('--stamp-image', '');
                    }
                }
            }
        } catch (error) {}
    }

    function logDebug(msg) {
        console.log("[Omni-Commander Hand-Stamped Gold Card]", msg);
    }

    async function processQueue() {
        if (document.hidden) {
            setTimeout(processQueue, 1000);
            return;
        }

        if (isAnimating || checkInQueue.length === 0) return;
        isAnimating = true;
        const task = checkInQueue.shift();
        await renderAndShowCard(task);
        isAnimating = false;
        setTimeout(processQueue, 500);
    }

    function renderAndShowCard(task) {
        return new Promise(resolve => {
            const cardContainer = document.getElementById('card-container');
            const userAvatar = document.getElementById('user-avatar');
            const userName = document.getElementById('user-name');
            const roundWatermark = document.getElementById('round-watermark');
            const stampsGrid = document.getElementById('stamps-grid');
            const progressBadge = document.getElementById('progress-badge');
            const progressBarFill = document.getElementById('progress-bar-fill');
            const cardElement = document.getElementById('loyalty-card');

            let currentRound = task.totalStamps ? Math.max(1, Math.ceil(task.totalStamps / MAX_STAMPS)) : (task.round || 1);

            cardElement.classList.remove('full-stamps');

            if (task.profileImage) {
                userAvatar.src = task.profileImage;
            } else {
                userAvatar.src = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><circle cx='50' cy='50' r='50' fill='%23666'/><text x='50' y='55' font-family='Arial' font-size='40' font-weight='bold' fill='%23fff' text-anchor='middle' dominant-baseline='middle'>?</text></svg>";
            }

            userAvatar.onerror = function () {
                this.src = "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><circle cx='50' cy='50' r='50' fill='%23666'/><text x='50' y='55' font-family='Arial' font-size='40' font-weight='bold' fill='%23fff' text-anchor='middle' dominant-baseline='middle'>?</text></svg>";
                this.onerror = null;
            };

            userName.textContent = task.name;
            if (roundWatermark) {
                roundWatermark.textContent = `Vol.${String(currentRound).padStart(2, '0')}`;
            }

            let initialStamps = Math.max(0, task.targetStamp - 1);
            if (progressBadge) {
                progressBadge.textContent = `${initialStamps} / ${MAX_STAMPS}`;
            }
            if (progressBarFill) {
                progressBarFill.style.width = ((initialStamps / MAX_STAMPS) * 100) + "%";
            }

            stampsGrid.innerHTML = '';
            for (let i = 1; i <= MAX_STAMPS; i++) {
                const slot = document.createElement('div');
                slot.className = 'stamp-slot';
                slot.textContent = i;

                let seedPref = task.name + "_R" + currentRound + "_S" + i;
                let rot = (OverlayCommon.getDeterministicRandom(seedPref + "_rot") * 50) - 25;
                let dx = (OverlayCommon.getDeterministicRandom(seedPref + "_x") * 1.5) - 0.75;
                let dy = (OverlayCommon.getDeterministicRandom(seedPref + "_y") * 1.5) - 0.75;
                let scale = 0.95 + (OverlayCommon.getDeterministicRandom(seedPref + "_s") * 0.1);

                slot.style.setProperty('--rot', rot + 'deg');
                slot.style.setProperty('--dx', dx + 'px');
                slot.style.setProperty('--dy', dy + 'px');
                slot.style.setProperty('--scale', scale);

                if (i <= initialStamps) {
                    slot.classList.add('stamped');
                    slot.textContent = '';
                }
                stampsGrid.appendChild(slot);
            }

            cardContainer.classList.add('show');

            setTimeout(() => {
                if (progressBadge) {
                    progressBadge.textContent = `${task.targetStamp} / ${MAX_STAMPS}`;
                }
                if (progressBarFill) {
                    progressBarFill.style.width = ((task.targetStamp / MAX_STAMPS) * 100) + "%";
                }

                const currentSlot = stampsGrid.children[task.targetStamp - 1];
                if (currentSlot) {
                    currentSlot.classList.add('stamped', 'animate-stamp');
                    currentSlot.textContent = '';

                    cardElement.classList.add('shake');
                    setTimeout(() => cardElement.classList.remove('shake'), 400);

                    if (task.targetStamp === MAX_STAMPS) {
                        setTimeout(() => {
                            cardElement.classList.add('full-stamps');
                        }, 400);
                    }
                }

                if (isInline) {
                    if (task.targetStamp === MAX_STAMPS) {
                        cardElement.classList.add('full-stamps');
                    }
                    resolve();
                } else {
                    setTimeout(() => {
                        cardContainer.classList.remove('show');
                        setTimeout(resolve, 800);
                    }, 7000);
                }
            }, 800);
        });
    }

    // Test Trigger: Press 'T' to simulate check-in
    let testCounter = 0;
    document.addEventListener('keydown', (e) => {
        if (e.key === 't' || e.key === 'T') {
            testCounter++;
            const isSecondCard = testCounter > 10;
            const visualStamp = isSecondCard ? (testCounter - 10) : testCounter;
            const round = isSecondCard ? 2 : 1;

            logDebug(`Simulate check-in: stamps ${visualStamp} (Vol.${round})`);
            checkInQueue.push({
                name: "Simulated User",
                profileImage: "https://api.dicebear.com/7.x/bottts/svg?seed=fox",
                targetStamp: visualStamp,
                round: round,
                isManualDisplay: false
            });
            processQueue();

            if (testCounter >= 20) testCounter = 0;
        }
    });

    // DOMContentLoaded initialization
    window.addEventListener('DOMContentLoaded', () => {
        const isPreview = urlParams.get('preview') === '1';
        if (isPreview) {
            document.body.classList.add('preview-mode');
        }

        fetchSettings();
        setInterval(fetchSettings, 10000);

        if (isInline) {
            const container = document.getElementById('card-container');
            container.style.transition = 'none';
            container.style.width = '600px';
            container.style.height = '300px';
            container.style.left = '50%';
            container.style.top = '50%';
            container.style.transformOrigin = 'center';

            const adjustScale = () => {
                const w = window.innerWidth;
                const h = window.innerHeight;
                const safePadding = 20;
                const scale = Math.min((w - safePadding) / 600, (h - safePadding) / 300);
                container.style.transform = `translate(-50%, -50%) scale(${scale})`;
            };
            window.addEventListener('resize', adjustScale);
            adjustScale();

            const task = {
                name: urlParams.get('name') || 'Guest User',
                profileImage: urlParams.get('avatar') || '',
                targetStamp: parseInt(urlParams.get('stamps')) || 1,
                totalStamps: parseInt(urlParams.get('totalStamps')) || 1,
                isManualDisplay: false
            };
            renderAndShowCard(task);

            document.body.style.backgroundColor = 'transparent';
            document.body.classList.add('inline-mode');
        } else {
            let isSyncingHistory = true;
            setTimeout(() => {
                isSyncingHistory = false;
                logDebug("History synchronization window closed.");
            }, 1500);

            // Connect to Vulperonex /hubs/overlay/member
            OverlayCommon.initSignalRConnection('/hubs/overlay/member', {
                event: function (userData) {
                    logDebug("Check-in event received: " + JSON.stringify(userData));

                    const total = userData.checkInCount || 1;
                    const displayStamps = (total % 10 === 0) ? 10 : (total % 10);
                    const displayName = userData.displayName || "Unknown User";
                    const avatarUrl = userData.avatarUrl || "";

                    if (isSyncingHistory) {
                        // Statically update the card data without pushing to queue or rendering animations
                        const userAvatar = document.getElementById('user-avatar');
                        const userName = document.getElementById('user-name');
                        const roundWatermark = document.getElementById('round-watermark');
                        const stampsGrid = document.getElementById('stamps-grid');
                        const progressBadge = document.getElementById('progress-badge');
                        const progressBarFill = document.getElementById('progress-bar-fill');
                        const cardElement = document.getElementById('loyalty-card');

                        let currentRound = Math.max(1, Math.ceil(total / MAX_STAMPS));

                        if (cardElement) cardElement.classList.remove('full-stamps', 'shake');

                        if (userAvatar) {
                            userAvatar.src = avatarUrl || "data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><circle cx='50' cy='50' r='50' fill='%23666'/><text x='50' y='55' font-family='Arial' font-size='40' font-weight='bold' fill='%23fff' text-anchor='middle' dominant-baseline='middle'>?</text></svg>";
                        }
                        if (userName) userName.textContent = displayName;
                        if (roundWatermark) {
                            roundWatermark.textContent = `Vol.${String(currentRound).padStart(2, '0')}`;
                        }
                        if (progressBadge) {
                            progressBadge.textContent = `${displayStamps} / ${MAX_STAMPS}`;
                        }
                        if (progressBarFill) {
                            progressBarFill.style.width = ((displayStamps / MAX_STAMPS) * 100) + "%";
                        }

                        if (stampsGrid) {
                            stampsGrid.innerHTML = '';
                            for (let i = 1; i <= MAX_STAMPS; i++) {
                                const slot = document.createElement('div');
                                slot.className = 'stamp-slot';
                                slot.textContent = i;

                                let seedPref = displayName + "_R" + currentRound + "_S" + i;
                                let rot = (OverlayCommon.getDeterministicRandom(seedPref + "_rot") * 50) - 25;
                                let dx = (OverlayCommon.getDeterministicRandom(seedPref + "_x") * 1.5) - 0.75;
                                let dy = (OverlayCommon.getDeterministicRandom(seedPref + "_y") * 1.5) - 0.75;
                                let scale = 0.95 + (OverlayCommon.getDeterministicRandom(seedPref + "_s") * 0.1);

                                slot.style.setProperty('--rot', rot + 'deg');
                                slot.style.setProperty('--dx', dx + 'px');
                                slot.style.setProperty('--dy', dy + 'px');
                                slot.style.setProperty('--scale', scale);

                                if (i <= displayStamps) {
                                    slot.classList.add('stamped');
                                    slot.textContent = '';
                                }
                                stampsGrid.appendChild(slot);
                            }
                        }

                        if (displayStamps === MAX_STAMPS && cardElement) {
                            cardElement.classList.add('full-stamps');
                        }

                        const cardContainer = document.getElementById('card-container');
                        if (cardContainer) {
                            cardContainer.classList.add('show');
                        }

                        logDebug("Statically updated member-card with replayed history.");
                        return;
                    }

                    if (checkInQueue.length >= MAX_QUEUE_SIZE) {
                        logDebug("Queue full. Dropping the oldest event.");
                        checkInQueue.shift();
                    }

                    checkInQueue.push({
                        name: displayName,
                        profileImage: avatarUrl,
                        targetStamp: displayStamps,
                        totalStamps: total,
                        isManualDisplay: false
                    });
                    processQueue();
                }
            });
        }
    });

})();
