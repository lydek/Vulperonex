# Vendored Libraries

These files are vendored into the repo intentionally. Do **not** replace with CDN.

## Why vendored

The `public/overlay/*.html` overlays are designed to run as standalone Vanilla HTML/CSS/JS overlays without going through the Vite build pipeline. Streamers extending overlays should be able to:

1. Drop a custom `.html` into `wwwroot/overlay/` and have OBS load it directly
2. Have it work offline (e.g., OBS running on an isolated streaming PC)
3. Not require Node.js / pnpm / Vite tooling to make small visual tweaks

Loading Vue and SignalR from CDN would break offline use and create a hard external dependency for every overlay refresh.

## Versions (pinned)

| File | Library | Version | Source |
|------|---------|---------|--------|
| `vue.global.js` | Vue 3 (global build) | 3.5.x | https://unpkg.com/vue@3/dist/vue.global.js |
| `signalr.min.js` | @microsoft/signalr | 8.0.x | https://unpkg.com/@microsoft/signalr@8/dist/browser/signalr.min.js |

## Upgrade procedure

1. Download new file from unpkg URL above
2. Verify file integrity (no minification corruption)
3. Run all overlay manual smoke tests in `docs/phases/phase-7b-*/manual-verification.md`
4. Commit version bump in same PR as any code change requiring the new version
