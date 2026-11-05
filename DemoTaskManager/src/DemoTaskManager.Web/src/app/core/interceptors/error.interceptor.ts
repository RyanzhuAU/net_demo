import { HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import type { ApiErrorDto, AppError } from '../models/api-error.model';
import { NotificationService } from '../services/notification.service';
import { CORRELATION_ID_HEADER } from './correlation-id.interceptor';

function isApiErrorDto(value: unknown): value is ApiErrorDto {
  return typeof value === 'object' && value !== null && 'title' in value;
}

export function toAppError(response: HttpErrorResponse): AppError {
  const correlationId = response.headers?.get(CORRELATION_ID_HEADER) ?? null;

  if (response.status === 0) {
    return {
      status: 0,
      title: 'Network error',
      detail: 'The server could not be reached. Check your connection and try again.',
      correlationId,
      fieldErrors: {},
    };
  }

  if (isApiErrorDto(response.error)) {
    const problem = response.error;
    return {
      status: problem.status || response.status,
      title: problem.title,
      detail: problem.detail ?? defaultDetailFor(response.status),
      correlationId: problem.correlationId ?? correlationId,
      fieldErrors: problem.errors ?? {},
    };
  }

  return {
    status: response.status,
    title: response.statusText || 'Request failed',
    detail: defaultDetailFor(response.status),
    correlationId,
    fieldErrors: {},
  };
}

function defaultDetailFor(status: number): string {
  switch (status) {
    case 400:
      return 'The request was rejected. Please review the highlighted fields.';
    case 404:
      return 'The requested item could not be found.';
    case 409:
      return 'The item was modified by someone else. Reload and try again.';
    default:
      return status >= 500
        ? 'Something went wrong on the server. Please try again shortly.'
        : 'The request could not be completed.';
  }
}

/** Single place where HTTP failures become user-facing messages. */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((response: unknown) => {
      if (!(response instanceof HttpErrorResponse)) {
        return throwError(() => response);
      }

      const appError = toAppError(response);

      // 400 responses are rendered inline next to the offending form controls.
      if (appError.status !== 400) {
        notifications.error(appError.detail, appError.correlationId);
      }

      return throwError(() => appError);
    }),
  );
};
