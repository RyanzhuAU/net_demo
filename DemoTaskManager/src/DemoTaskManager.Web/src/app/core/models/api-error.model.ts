/** Mirrors DemoTaskManager.Contracts.Common.ApiErrorDto (RFC 9457 ProblemDetails). */
export interface ApiErrorDto {
  readonly type?: string;
  readonly title: string;
  readonly status: number;
  readonly detail?: string;
  readonly instance?: string;
  readonly correlationId?: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

/** Normalised error surfaced to components by the HTTP error interceptor. */
export interface AppError {
  readonly status: number;
  readonly title: string;
  readonly detail: string;
  readonly correlationId: string | null;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
}
