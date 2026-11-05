import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { TaskItemDto } from '../../../../core/models';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';

@Component({
  selector: 'app-task-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, StatusBadge],
  template: `
    <table class="table" data-testid="task-table">
      <caption class="visually-hidden">
        Tasks, newest first
      </caption>
      <thead>
        <tr>
          <th scope="col">Title</th>
          <th scope="col">Status</th>
          <th scope="col">Due (UTC)</th>
          <th scope="col">Created (UTC)</th>
          <th scope="col"><span class="visually-hidden">Actions</span></th>
        </tr>
      </thead>
      <tbody>
        @for (task of tasks(); track task.id) {
          <tr [class.table__row--overdue]="task.isOverdue" data-testid="task-row">
            <td>
              <a [routerLink]="['/tasks', task.id]">{{ task.title }}</a>
              @if (task.isOverdue) {
                <span class="table__flag">Overdue</span>
              }
            </td>
            <td><app-status-badge [status]="task.status" /></td>
            <td>{{ task.dueDateUtc ? (task.dueDateUtc | date: 'yyyy-MM-dd HH:mm' : 'UTC') : '—' }}</td>
            <td>{{ task.createdAtUtc | date: 'yyyy-MM-dd HH:mm' : 'UTC' }}</td>
            <td class="table__actions">
              <a class="button button--small" [routerLink]="['/tasks', task.id, 'edit']">Edit</a>
              <button
                type="button"
                class="button button--small button--danger"
                data-testid="delete-task"
                (click)="deleteRequested.emit(task)"
              >
                Delete
              </button>
            </td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    .table {
      width: 100%;
      border-collapse: collapse;
    }

    .table th,
    .table td {
      padding: 0.6rem 0.5rem;
      text-align: left;
      border-bottom: 1px solid var(--color-border);
      vertical-align: middle;
    }

    .table th {
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--color-muted);
    }

    .table__row--overdue td:first-child {
      border-left: 3px solid var(--color-danger);
      padding-left: 0.4rem;
    }

    .table__flag {
      margin-left: 0.5rem;
      font-size: 0.75rem;
      color: var(--color-danger);
      font-weight: 600;
    }

    .table__actions {
      display: flex;
      gap: 0.4rem;
      justify-content: flex-end;
    }
  `,
})
export class TaskTable {
  readonly tasks = input.required<readonly TaskItemDto[]>();
  readonly deleteRequested = output<TaskItemDto>();
}
