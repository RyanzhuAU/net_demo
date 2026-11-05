import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TASK_STATUS_LABELS, type TaskStatus } from '../../../core/models';

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="'badge--' + status().toLowerCase()">{{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 0.15rem 0.6rem;
      border-radius: 999px;
      font-size: 0.8rem;
      font-weight: 600;
      white-space: nowrap;
    }

    .badge--todo {
      background: #e2e8f0;
      color: #1e293b;
    }

    .badge--inprogress {
      background: #dbeafe;
      color: #1e40af;
    }

    .badge--completed {
      background: #dcfce7;
      color: #166534;
    }
  `,
})
export class StatusBadge {
  readonly status = input.required<TaskStatus>();

  label(): string {
    return TASK_STATUS_LABELS[this.status()];
  }
}
