import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import type { AppError } from '../../../../core/models/api-error.model';
import {
  TASK_STATUSES,
  TASK_STATUS_LABELS,
  type CreateTaskItemRequest,
  type TaskItemDto,
  type TaskStatus,
  type UpdateTaskItemRequest,
} from '../../../../core/models';
import { NotificationService } from '../../../../core/services/notification.service';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { FieldError } from '../../../../shared/components/field-error/field-error';
import { LoadingSpinner } from '../../../../shared/components/loading-spinner/loading-spinner';
import { TaskApiService } from '../../services/task-api.service';

const TITLE_MAX_LENGTH = 200;
const DESCRIPTION_MAX_LENGTH = 2000;

/** Converts an ISO UTC instant to the value shape required by `datetime-local`. */
function toLocalInputValue(isoUtc: string | null): string {
  if (!isoUtc) {
    return '';
  }
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  const pad = (value: number): string => value.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function toIsoUtc(localValue: string): string | null {
  if (!localValue) {
    return null;
  }
  const date = new Date(localValue);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

@Component({
  selector: 'app-task-form-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, EmptyState, FieldError, LoadingSpinner],
  templateUrl: './task-form-page.html',
  styleUrl: './task-form-page.scss',
})
export class TaskFormPage {
  /** Present only on the edit route; bound from the route parameter. */
  readonly id = input<string | undefined>(undefined);

  private readonly formBuilder = inject(FormBuilder);
  private readonly taskApi = inject(TaskApiService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly statuses = TASK_STATUSES;
  protected readonly statusLabels = TASK_STATUS_LABELS;
  protected readonly titleMaxLength = TITLE_MAX_LENGTH;
  protected readonly descriptionMaxLength = DESCRIPTION_MAX_LENGTH;

  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly serverErrors = signal<Readonly<Record<string, readonly string[]>>>({});

  protected readonly isEditMode = computed(() => !!this.id());
  protected readonly heading = computed(() => (this.isEditMode() ? 'Edit task' : 'New task'));

  protected readonly form = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(TITLE_MAX_LENGTH)]],
    description: ['', [Validators.maxLength(DESCRIPTION_MAX_LENGTH)]],
    status: ['Todo' as TaskStatus, [Validators.required]],
    dueDateLocal: [''],
  });

  constructor() {
    effect(() => {
      const id = this.id();
      if (!id) {
        return;
      }

      this.loading.set(true);
      this.loadFailed.set(false);
      this.taskApi.getById(id).subscribe({
        next: (task) => {
          this.patchFrom(task);
          this.loading.set(false);
        },
        error: () => {
          this.loadFailed.set(true);
          this.loading.set(false);
        },
      });
    });
  }

  /** Used by the functional `canDeactivate` guard. */
  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved();
  }

  serverErrorsFor(field: string): readonly string[] {
    const errors = this.serverErrors();
    return errors[field] ?? errors[field.charAt(0).toUpperCase() + field.slice(1)] ?? [];
  }

  onSubmit(): void {
    this.serverErrors.set({});

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const payload: CreateTaskItemRequest & UpdateTaskItemRequest = {
      title: value.title.trim(),
      description: value.description.trim() === '' ? null : value.description.trim(),
      status: value.status,
      dueDateUtc: toIsoUtc(value.dueDateLocal),
    };

    this.saving.set(true);
    const id = this.id();
    const request$ = id ? this.taskApi.update(id, payload) : this.taskApi.create(payload);

    request$.subscribe({
      next: (task) => {
        this.saving.set(false);
        this.saved.set(true);
        this.notifications.success(id ? 'Task updated.' : 'Task created.');
        void this.router.navigate(['/tasks', task.id]);
      },
      error: (error: AppError) => {
        this.saving.set(false);
        if (error.status === 400) {
          this.serverErrors.set(error.fieldErrors);
          this.notifications.error(error.detail, error.correlationId);
        }
      },
    });
  }

  private patchFrom(task: TaskItemDto): void {
    this.form.reset({
      title: task.title,
      description: task.description ?? '',
      status: task.status,
      dueDateLocal: toLocalInputValue(task.dueDateUtc),
    });
  }
}
