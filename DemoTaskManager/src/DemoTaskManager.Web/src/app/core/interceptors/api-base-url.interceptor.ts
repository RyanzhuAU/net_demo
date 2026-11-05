import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AppConfigService } from '../config/app-config.service';

/** Marker prefix rewritten to the runtime-configured API base URL. */
export const API_URL_PREFIX = 'api://';

/**
 * Lets services request `api://tasks` and resolves the real host at request time,
 * which is what makes the runtime configuration approach possible.
 */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith(API_URL_PREFIX)) {
    return next(request);
  }

  const baseUrl = inject(AppConfigService).apiBaseUrl.replace(/\/+$/, '');
  const relativeUrl = request.url.slice(API_URL_PREFIX.length).replace(/^\/+/, '');

  return next(request.clone({ url: `${baseUrl}/${relativeUrl}` }));
};
