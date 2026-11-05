import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface RuntimeConfig {
  readonly apiBaseUrl: string;
  readonly environmentName: string;
  readonly applicationInsightsConnectionString: string | null;
}

const FALLBACK_CONFIG: RuntimeConfig = {
  apiBaseUrl: environment.defaultApiBaseUrl,
  environmentName: environment.production ? 'Production' : 'Development',
  applicationInsightsConnectionString: null,
};

/**
 * Holds configuration fetched from the server at startup so that the same compiled
 * bundle can be promoted between App Service environments without a rebuild.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly config = signal<RuntimeConfig>(FALLBACK_CONFIG);

  readonly current = this.config.asReadonly();

  get apiBaseUrl(): string {
    return this.config().apiBaseUrl;
  }

  get environmentName(): string {
    return this.config().environmentName;
  }

  set(config: Partial<RuntimeConfig>): void {
    this.config.set({ ...FALLBACK_CONFIG, ...config });
  }
}
