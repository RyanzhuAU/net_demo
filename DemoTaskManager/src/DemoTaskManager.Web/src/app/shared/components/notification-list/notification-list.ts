import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-notification-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" aria-live="assertive" aria-atomic="true">
      @for (notification of notifications.notifications(); track notification.id) {
        <div class="toast" [class]="'toast--' + notification.level" role="alert">
          <span class="toast__message">{{ notification.message }}</span>
          @if (notification.correlationId) {
            <span class="toast__correlation">Reference: {{ notification.correlationId }}</span>
          }
          <button
            type="button"
            class="toast__close"
            aria-label="Dismiss notification"
            (click)="notifications.dismiss(notification.id)"
          >
            ×
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed;
      right: 1rem;
      bottom: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      z-index: 50;
      max-width: min(28rem, 90vw);
    }

    .toast {
      position: relative;
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      padding: 0.75rem 2.25rem 0.75rem 0.9rem;
      border-radius: 0.5rem;
      color: #fff;
      box-shadow: 0 12px 24px rgb(15 23 42 / 25%);
    }

    .toast--error {
      background: #b91c1c;
    }

    .toast--success {
      background: #15803d;
    }

    .toast--info {
      background: #1d4ed8;
    }

    .toast__correlation {
      font-size: 0.75rem;
      opacity: 0.85;
    }

    .toast__close {
      position: absolute;
      top: 0.25rem;
      right: 0.4rem;
      border: none;
      background: transparent;
      color: inherit;
      font-size: 1.25rem;
      line-height: 1;
      cursor: pointer;
    }
  `,
})
export class NotificationList {
  protected readonly notifications = inject(NotificationService);
}
