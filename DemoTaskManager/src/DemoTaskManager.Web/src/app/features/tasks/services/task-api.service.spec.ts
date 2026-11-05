import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { AppConfigService } from '../../../core/config/app-config.service';
import { apiBaseUrlInterceptor } from '../../../core/interceptors/api-base-url.interceptor';
import type { PagedResultDto, TaskItemDto } from '../../../core/models';
import { TaskApiService } from './task-api.service';

function createTask(overrides: Partial<TaskItemDto> = {}): TaskItemDto {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    title: 'Write tests',
    description: null,
    status: 'Todo',
    dueDateUtc: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: null,
    isReminderSent: false,
    isOverdue: false,
    ...overrides,
  };
}

describe('TaskApiService', () => {
  let service: TaskApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    TestBed.inject(AppConfigService).set({ apiBaseUrl: '/api' });
    service = TestBed.inject(TaskApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('requests the paged list with status, paging and search parameters', () => {
    let received: PagedResultDto<TaskItemDto> | undefined;

    service
      .list({ status: 'InProgress', pageNumber: 2, pageSize: 25, search: 'demo' })
      .subscribe((result) => (received = result));

    const request = httpMock.expectOne(
      (req) => req.url === '/api/tasks' && req.method === 'GET',
    );

    expect(request.request.params.get('status')).toBe('InProgress');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('search')).toBe('demo');

    const payload: PagedResultDto<TaskItemDto> = {
      items: [createTask()],
      pageNumber: 2,
      pageSize: 25,
      totalCount: 26,
      totalPages: 2,
      hasPreviousPage: true,
      hasNextPage: false,
    };
    request.flush(payload);

    expect(received?.items.length).toBe(1);
    httpMock.verify();
  });

  it('omits empty filter values from the query string', () => {
    service.list({ status: null, pageNumber: 1, pageSize: 10, search: null }).subscribe();

    const request = httpMock.expectOne((req) => req.url === '/api/tasks');
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.has('search')).toBe(false);

    request.flush({
      items: [],
      pageNumber: 1,
      pageSize: 10,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    });
    httpMock.verify();
  });

  it('posts create requests to /api/tasks', () => {
    service
      .create({ title: 'New', description: null, status: 'Todo', dueDateUtc: null })
      .subscribe();

    const request = httpMock.expectOne('/api/tasks');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      title: 'New',
      description: null,
      status: 'Todo',
      dueDateUtc: null,
    });

    request.flush(createTask({ title: 'New' }));
    httpMock.verify();
  });

  it('puts update requests to the task resource', () => {
    service
      .update('11111111-1111-1111-1111-111111111111', {
        title: 'Changed',
        description: 'x',
        status: 'Completed',
        dueDateUtc: null,
      })
      .subscribe();

    const request = httpMock.expectOne('/api/tasks/11111111-1111-1111-1111-111111111111');
    expect(request.request.method).toBe('PUT');
    request.flush(createTask({ title: 'Changed', status: 'Completed' }));
    httpMock.verify();
  });

  it('deletes the task resource', () => {
    service.remove('11111111-1111-1111-1111-111111111111').subscribe();

    const request = httpMock.expectOne('/api/tasks/11111111-1111-1111-1111-111111111111');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    httpMock.verify();
  });

  it('submits a Service Bus processing request', () => {
    service
      .requestProcessing('11111111-1111-1111-1111-111111111111', {
        targetStatus: 'InProgress',
        reason: 'manual',
      })
      .subscribe();

    const request = httpMock.expectOne(
      '/api/tasks/11111111-1111-1111-1111-111111111111/process',
    );
    expect(request.request.method).toBe('POST');
    request.flush({
      taskId: '11111111-1111-1111-1111-111111111111',
      targetStatus: 'InProgress',
      reason: 'manual',
      correlationId: 'abc',
      requestedAtUtc: '2026-01-01T00:00:00Z',
    });
    httpMock.verify();
  });

  it('honours a runtime-configured absolute API base URL', () => {
    TestBed.inject(AppConfigService).set({ apiBaseUrl: 'https://api.contoso.example/v1' });

    service.getById('11111111-1111-1111-1111-111111111111').subscribe();

    const request = httpMock.expectOne(
      'https://api.contoso.example/v1/tasks/11111111-1111-1111-1111-111111111111',
    );
    request.flush(createTask());
    httpMock.verify();
  });
});
