import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { AppConfigService } from '../../../../core/config/app-config.service';
import { apiBaseUrlInterceptor } from '../../../../core/interceptors/api-base-url.interceptor';
import type { PagedResultDto, TaskItemDto } from '../../../../core/models';
import { TaskListPage } from './task-list-page';

function task(id: string, title: string, overrides: Partial<TaskItemDto> = {}): TaskItemDto {
  return {
    id,
    title,
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

function page(items: TaskItemDto[], overrides: Partial<PagedResultDto<TaskItemDto>> = {}): PagedResultDto<TaskItemDto> {
  return {
    items,
    pageNumber: 1,
    pageSize: 10,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
    hasPreviousPage: false,
    hasNextPage: false,
    ...overrides,
  };
}

describe('TaskListPage', () => {
  let fixture: ComponentFixture<TaskListPage>;
  let httpMock: HttpTestingController;

  const flushList = async (result: PagedResultDto<TaskItemDto>): Promise<void> => {
    await fixture.whenStable();
    const requests = httpMock.match((req) => req.url === '/api/tasks');
    requests[requests.length - 1]?.flush(result);
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [TaskListPage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    TestBed.inject(AppConfigService).set({ apiBaseUrl: '/api' });
    httpMock = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(TaskListPage);
    fixture.detectChanges();
  });

  it('shows the loading indicator before the first response arrives', () => {
    expect(fixture.nativeElement.querySelector('app-loading-spinner')).not.toBeNull();
  });

  it('renders one row per returned task', async () => {
    await flushList(page([task('11111111-1111-1111-1111-111111111111', 'Alpha'), task('22222222-2222-2222-2222-222222222222', 'Beta')]));

    const rows = fixture.nativeElement.querySelectorAll('[data-testid="task-row"]');
    expect(rows.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Alpha');
    expect(fixture.nativeElement.textContent).toContain('Beta');
  });

  it('shows the empty state when the API returns no items', async () => {
    await flushList(page([]));

    expect(fixture.nativeElement.querySelector('[data-testid="empty-state"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="task-table"]')).toBeNull();
  });

  it('re-queries with the selected status filter and resets to page one', async () => {
    await flushList(page([task('11111111-1111-1111-1111-111111111111', 'Alpha')]));

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('[data-testid="status-filter"]');
    select.value = 'Completed';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();

    const requests = httpMock.match((req) => req.url === '/api/tasks');
    const last = requests[requests.length - 1];
    expect(last?.request.params.get('status')).toBe('Completed');
    expect(last?.request.params.get('pageNumber')).toBe('1');
    last?.flush(page([]));
  });

  it('requests the next page when pagination advances', async () => {
    await flushList(
      page([task('11111111-1111-1111-1111-111111111111', 'Alpha')], {
        totalCount: 20,
        totalPages: 2,
        hasNextPage: true,
      }),
    );

    const next: HTMLButtonElement = fixture.nativeElement.querySelector('[data-testid="next-page"]');
    expect(next.disabled).toBe(false);
    next.click();
    fixture.detectChanges();
    await fixture.whenStable();

    const requests = httpMock.match((req) => req.url === '/api/tasks');
    const last = requests[requests.length - 1];
    expect(last?.request.params.get('pageNumber')).toBe('2');
    last?.flush(page([]));
  });

  it('shows an error state when the list request fails', async () => {
    await fixture.whenStable();
    const requests = httpMock.match((req) => req.url === '/api/tasks');
    requests[requests.length - 1]?.flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="error-state"]')).not.toBeNull();
  });
});
