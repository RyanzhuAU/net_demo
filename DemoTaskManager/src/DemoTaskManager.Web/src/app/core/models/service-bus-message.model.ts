import type { TaskStatus } from './task-item.model';

/** Mirrors DemoTaskManager.Contracts.Messaging.ServiceBusTaskMessageDto. */
export interface ServiceBusTaskMessageDto {
  readonly taskId: string;
  readonly targetStatus: TaskStatus;
  readonly reason: string | null;
  readonly correlationId: string;
  readonly requestedAtUtc: string;
}
