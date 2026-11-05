export interface AppEnvironment {
  readonly production: boolean;
  /** Fallback used when /config.json cannot be reached (e.g. offline dev server). */
  readonly defaultApiBaseUrl: string;
  readonly runtimeConfigUrl: string;
}
