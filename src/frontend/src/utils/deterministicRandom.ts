// Deterministic LCG seeded by a string.
// Use case: stamp visual placement that should be stable across reloads for
// the same (member, round, slot) tuple, but vary between tuples.
//
// Mirrored at: src/frontend/public/overlay/js/overlay-common.js (standalone HTML)
// Keep both implementations bit-identical.

export function getDeterministicRandom(seed: string): number {
    let hash = 5381;
    for (let i = 0; i < seed.length; i++) {
        hash = (hash * 33) ^ seed.charCodeAt(i);
    }
    hash = Math.abs(hash) || 1;
    hash = (hash * 9301 + 49297) % 233280;
    return hash / 233280.0;
}
