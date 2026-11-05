import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="spinner" role="status" aria-live="polite">
      <span class="spinner__circle" aria-hidden="true"></span>
      <span class="spinner__label">{{ label() }}</span>
    </div>
  `,
  styles: `
    .spinner {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 1.5rem 0;
      color: var(--color-muted);
    }

    .spinner__circle {
      width: 1.25rem;
      height: 1.25rem;
      border: 2px solid currentcolor;
      border-top-color: transparent;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .spinner__circle {
        animation-duration: 3s;
      }
    }
  `,
})
export class LoadingSpinner {
  readonly label = input('Loading…');
}
