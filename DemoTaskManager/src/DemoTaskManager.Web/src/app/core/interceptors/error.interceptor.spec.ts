import { HttpErrorResponse, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { AppConfigService } from '../config/app-config.service';
import type { AppError } from '../models/api-error.model';
import { NotificationService } from '../services/notification.service';
import { apiBaseUrlInterceptor } from './api-base-url.interceptor';
import { errorInterceptor, toAppError } from './error.interceptor';
import { ApiClient } from '../api/api-client';

describe('errorInterceptor', () => {
  let api: ApiClient;
  let httpMock: HttpTestingController;
  let notifications: NotificationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiBaseUrlInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    TestBed.inject(AppConfigService).set({ apiBaseUrl: '/api' });
    api = TestBed.inject(ApiClient);
    httpMock = TestBed.inject(HttpTestingController);
    notifications = TestBed.inject(NotificationService);
  });

  it('maps ProblemDetails responses to an AppError and notifies for non-400 failures', () => {
    let error: AppError | undefined;

    api.get('tasks').subscribe({ error: (e: AppError) => (error = e) });

    httpMock.expectOne('/api/tasks').flush(
      {
        title: 'Resource not found.',
        status: 404,
        detail: "TaskItem 'x' was not found.",
        correlationId: 'corr-1',
      },
      { status: 404, statusText: 'Not Found' },
    );

    expect(error?.status).toBe(404);
    expect(error?.correlationId).toBe('corr-1');
    expect(notifications.notifications().length).toBe(1);
    expect(notifications.notifications()[0]?.level).toBe('error');
  });

  it('keeps validation errors inline instead of raising a toast', () => {
    let error: AppError | undefined;

    api.post('tasks', {}).subscribe({ error: (e: AppError) => (error = e) });

    httpMock.expectOne('/api/tasks').flush(
      {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: { Title: ['Title is required.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(error?.fieldErrors['Title']).toEqual(['Title is required.']);
    expect(notifications.notifications().length).toBe(0);
  });

  it('reports a friendly message when the server is unreachable', () => {
    let error: AppError | undefined;

    api.get('tasks').subscribe({ error: (e: AppError) => (error = e) });

    httpMock.expectOne('/api/tasks').error(new ProgressEvent('error'), { status: 0 });

    expect(error?.status).toBe(0);
    expect(error?.title).toBe('Network error');
  });

  it('falls back to the correlation id header when the body has none', () => {
    const response = new HttpErrorResponse({
      status: 500,
      statusText: 'Internal Server Error',
      headers: new HttpHeaders({ 'X-Correlation-Id': 'header-corr' }),
      error: 'boom',
    });

    expect(toAppError(response).correlationId).toBe('header-corr');
  });
});
