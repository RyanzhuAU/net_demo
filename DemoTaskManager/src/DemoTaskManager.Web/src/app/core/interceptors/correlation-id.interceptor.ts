import type { HttpInterceptorFn } from '@angular/common/http';

export const CORRELATION_ID_HEADER = 'X-Correlation-Id';

function newCorrelationId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `web-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

/** Stamps every outbound API call so browser and server logs share one identifier. */
export const correlationIdInterceptor: HttpInterceptorFn = (request, next) =>
  next(request.clone({ setHeaders: { [CORRELATION_ID_HEADER]: newCorrelationId() } }));
