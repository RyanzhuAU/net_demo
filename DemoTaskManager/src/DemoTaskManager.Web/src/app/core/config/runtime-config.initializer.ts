import { HttpBackend, HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, firstValueFrom, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppConfigService, type RuntimeConfig } from './app-config.service';

/**
 * Runs before the first route activates. HttpBackend is used directly so the request
 * bypasses interceptors that would otherwise need the configuration being loaded here.
 */
export function loadRuntimeConfig(): Promise<void> {
  const configService = inject(AppConfigService);
  const http = new HttpClient(inject(HttpBackend));

  return firstValueFrom(
    http
      .get<Partial<RuntimeConfig>>(environment.runtimeConfigUrl)
      .pipe(catchError(() => of({} as Partial<RuntimeConfig>))),
  ).then((config) => {
    configService.set(config);
  });
}
