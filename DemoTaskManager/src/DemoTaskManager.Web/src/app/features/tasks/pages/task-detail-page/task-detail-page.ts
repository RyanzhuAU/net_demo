import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { of } from 'rxjs';
import { catchError, map, startWith, switchMap } from 'rxjs/operators';
import type { ProcessTaskItemRequest, TaskItemDto, TaskStatus } from '../../../../core/models';
import { NotificationService } from '../../../../core/services/notification.service';
import { ConfirmDialog } from '../../../../shared/components/confirm-dialog/confirm-dialog';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { LoadingSpinner } from '../../../../shared/components/loading-spinner/loading-spinner';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';
import { TaskApiService } from '../../services/task-api.service';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';

interface DetailState {
  readonly task: TaskItemDto | null;
  readonly loading: boolean;
  readonly failed: boolean;
}

const INITIAL_STATE: DetailState = { task: null, loading: true, failed: false };

@Component({
  selector: 'app-task-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, ConfirmDialog, EmptyState, LoadingSpinner, StatusBadge],
  templateUrl: './task-detail-page.html',
  styleUrl: './task-detail-page.scss',
})
export class TaskDetailPage {
  /** Bound from the route parameter via `withComponentInputBinding()`. */
  readonly id = input.required<string>();

  private readonly taskApi = inject(TaskApiService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  private readonly reloadToken = signal(0);

  protected readonly confirmingDelete = signal(false);
  protected readonly processing = signal(false);

  private readonly state = toSignal(
    toObservable(computed(() => ({ id: this.id(), token: this.reloadToken() }))).pipe(
      switchMap(({ id }) =>
        this.taskApi.getById(id).pipe(
          map((task): DetailState => ({ task, loading: false, failed: false })),
          catchError(() => of<DetailState>({ task: null, loading: false, failed: true })),
          startWith(INITIAL_STATE),
        ),
      ),
    ),
    { initialValue: INITIAL_STATE },
  );

  protected readonly task = computed(() => this.state().task);
  protected readonly loading = computed(() => this.state().loading);
  protected readonly failed = computed(() => this.state().failed);

  protected readonly nextStatus = computed<TaskStatus>(() =>
    this.task()?.status === 'InProgress' ? 'Completed' : 'InProgress',
  );

  reload(): void {
    this.reloadToken.update((token) => token + 1);
  }

  onProcess(): void {
    const task = this.task();
    if (!task || this.processing()) {
      return;
    }

    const request: ProcessTaskItemRequest = {
      targetStatus: this.nextStatus(),
      reason: 'Submitted from the task detail page',
    };

    this.processing.set(true);
    this.taskApi.requestProcessing(task.id, request).subscribe({
      next: (message) => {
        this.processing.set(false);
        this.notifications.success(
          `Processing queued on Service Bus (correlation ${message.correlationId}).`,
        );
      },
      error: () => this.processing.set(false),
    });
  }

  onDeleteConfirmed(): void {
    const task = this.task();
    if (!task) {
      return;
    }

    this.confirmingDelete.set(false);
    this.taskApi.remove(task.id).subscribe({
      next: () => {
        this.notifications.success(`Task “${task.title}” was deleted.`);
        void this.router.navigate(['/tasks']);
      },
      error: () => {
        // Handled centrally by the error interceptor.
      },
    });
  }
}
