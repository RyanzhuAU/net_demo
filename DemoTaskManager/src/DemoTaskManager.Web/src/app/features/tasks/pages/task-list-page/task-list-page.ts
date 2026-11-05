import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, startWith, switchMap } from 'rxjs/operators';
import {
  emptyPagedResult,
  type PagedResultDto,
  type TaskItemDto,
  type TaskQueryParameters,
  type TaskStatus,
} from '../../../../core/models';
import { NotificationService } from '../../../../core/services/notification.service';
import { ConfirmDialog } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { LoadingSpinner } from '../../../../shared/components/loading-spinner/loading-spinner';
import { Pagination } from '../../../../shared/components/pagination/pagination';
import { TaskFilter } from '../../components/task-filter/task-filter';
import { TaskTable } from '../../components/task-table/task-table';
import { TaskApiService } from '../../services/task-api.service';

interface ListState {
  readonly result: PagedResultDto<TaskItemDto>;
  readonly loading: boolean;
  readonly failed: boolean;
}

const INITIAL_STATE: ListState = {
  result: emptyPagedResult<TaskItemDto>(),
  loading: true,
  failed: false,
};

@Component({
  selector: 'app-task-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ConfirmDialog, EmptyState, LoadingSpinner, Pagination, TaskFilter, TaskTable],
  templateUrl: './task-list-page.html',
  styleUrl: './task-list-page.scss',
})
export class TaskListPage {
  private readonly taskApi = inject(TaskApiService);
  private readonly notifications = inject(NotificationService);

  private readonly reloadToken = signal(0);

  protected readonly status = signal<TaskStatus | null>(null);
  protected readonly search = signal<string | null>(null);
  protected readonly pageNumber = signal(1);
  protected readonly pageSize = signal(10);
  protected readonly pendingDeletion = signal<TaskItemDto | null>(null);

  private readonly query = computed<TaskQueryParameters & { readonly token: number }>(() => ({
    status: this.status(),
    search: this.search(),
    pageNumber: this.pageNumber(),
    pageSize: this.pageSize(),
    token: this.reloadToken(),
  }));

  private readonly state = toSignal(
    toObservable(this.query).pipe(
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      switchMap((query) =>
        this.taskApi.list(query).pipe(
          map((result): ListState => ({ result, loading: false, failed: false })),
          catchError(() => of<ListState>({ result: emptyPagedResult<TaskItemDto>(), loading: false, failed: true })),
          startWith<ListState>({ ...INITIAL_STATE, loading: true }),
        ),
      ),
    ),
    { initialValue: INITIAL_STATE },
  );

  protected readonly result = computed(() => this.state().result);
  protected readonly loading = computed(() => this.state().loading);
  protected readonly failed = computed(() => this.state().failed);
  protected readonly isEmpty = computed(() => !this.loading() && this.result().items.length === 0);
  protected readonly hasFilters = computed(() => this.status() !== null || !!this.search());

  /** Typing is debounced here rather than in the query pipeline so status and paging
      changes still issue a request immediately. */
  private readonly searchInput = new Subject<string | null>();

  constructor() {
    this.searchInput
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((search) => {
        this.search.set(search);
        this.pageNumber.set(1);
      });
  }

  onStatusChange(status: TaskStatus | null): void {
    this.status.set(status);
    this.pageNumber.set(1);
  }

  onSearchChange(search: string | null): void {
    this.searchInput.next(search);
  }

  onResetFilters(): void {
    this.status.set(null);
    this.search.set(null);
    this.pageNumber.set(1);
  }

  onPageChange(pageNumber: number): void {
    this.pageNumber.set(Math.max(1, pageNumber));
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize.set(pageSize);
    this.pageNumber.set(1);
  }

  onDeleteRequested(task: TaskItemDto): void {
    this.pendingDeletion.set(task);
  }

  onDeleteCancelled(): void {
    this.pendingDeletion.set(null);
  }

  onDeleteConfirmed(): void {
    const task = this.pendingDeletion();
    if (!task) {
      return;
    }

    this.pendingDeletion.set(null);
    this.taskApi.remove(task.id).subscribe({
      next: () => {
        this.notifications.success(`Task “${task.title}” was deleted.`);
        this.reload();
      },
      error: () => {
        // The error interceptor already surfaced a message to the user.
      },
    });
  }

  reload(): void {
    this.reloadToken.update((token) => token + 1);
  }
}
