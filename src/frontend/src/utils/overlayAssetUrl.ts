const ALLOWED_SCHEMES = /^(https?:|data:image\/(png|jpe?g|gif|svg\+xml|webp);)/i;

export function sanitizeAssetUrl(raw: string | null | undefined): string {
  if (!raw) {
    return "";
  }

  const trimmed = raw.trim();
  if (!ALLOWED_SCHEMES.test(trimmed)) {
    return "";
  }

  return /["')(\\]/.test(trimmed) ? "" : trimmed;
}

export function cssUrl(safeUrl: string): string | undefined {
  return safeUrl ? `url("${safeUrl}")` : undefined;
}
