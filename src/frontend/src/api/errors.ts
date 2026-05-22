import { ApiError } from "./client";
import { ERROR_CODES, resolveErrorCode } from "@/i18n/errorCodes";

/**
 * Normalize a caught value into a stable error code suitable for i18n lookup.
 *
 * - ApiError with a JSON error envelope returns the backend code.
 * - ApiError with a 5xx status and no parsed code returns INTERNAL_ERROR and
 *   logs the raw payload to console.error per the Phase 6 error contract.
 * - ApiError with a 4xx status and no parsed code returns HTTP_<status>.
 * - Any other thrown value returns NETWORK_ERROR.
 */
export function describeApiError(caught: unknown): string {
  if (caught instanceof ApiError) {
    if (caught.status >= 500) {
      console.error("API 5xx response", { status: caught.status, body: caught.body });
    }
    return resolveErrorCode(caught.status, caught.errorCode);
  }
  if (caught instanceof Error) {
    return ERROR_CODES.NetworkError;
  }
  return ERROR_CODES.NetworkError;
}
