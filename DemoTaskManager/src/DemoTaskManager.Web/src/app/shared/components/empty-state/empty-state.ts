import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty" role="status">
      <h2 class="empty__title">{{ title() }}</h2>
      <p class="empty__message">{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 3rem 1rem;
      text-align: center;
      border: 1px dashed var(--color-border);
      border-radius: 0.75rem;
      background: var(--color-surface);
    }

    .empty__title {
      margin: 0;
      font-size: 1.1rem;
    }

    .empty__message {
      margin: 0;
      color: var(--color-muted);
    }
  `,
})
export class EmptyState {
  readonly title = input('Nothing here yet');
  readonly message = input('There is no data to display.');
}
