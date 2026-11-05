/** Mirrors DemoTaskManager.Domain.Enums.TaskStatus (serialised as a string by the API). */
export const TASK_STATUSES = ['Todo', 'InProgress', 'Completed'] as const;

export type TaskStatus = (typeof TASK_STATUSES)[number];

export const TASK_STATUS_LABELS: Readonly<Record<TaskStatus, string>> = {
  Todo: 'To do',
  InProgress: 'In progress',
  Completed: 'Completed',
};

/** Mirrors DemoTaskManager.Contracts.Tasks.TaskItemDto. */
export interface TaskItemDto {
  readonly id: string;
  readonly title: string;
  readonly description: string | null;
  readonly status: TaskStatus;
  readonly dueDateUtc: string | null;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string | null;
  readonly isReminderSent: boolean;
  readonly isOverdue: boolean;
}

/** Mirrors DemoTaskManager.Contracts.Tasks.CreateTaskItemRequest. */
export interface CreateTaskItemRequest {
  readonly title: string;
  readonly description: string | null;
  readonly status: TaskStatus;
  readonly dueDateUtc: string | null;
}

/** Mirrors DemoTaskManager.Contracts.Tasks.UpdateTaskItemRequest. */
export interface UpdateTaskItemRequest {
  readonly title: string;
  readonly description: string | null;
  readonly status: TaskStatus;
  readonly dueDateUtc: string | null;
}

/** Mirrors DemoTaskManager.Contracts.Tasks.ProcessTaskItemRequest. */
export interface ProcessTaskItemRequest {
  readonly targetStatus: TaskStatus;
  readonly reason: string | null;
}

/** Mirrors DemoTaskManager.Contracts.Tasks.TaskQueryParameters. */
export interface TaskQueryParameters {
  readonly status: TaskStatus | null;
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly search: string | null;
}
