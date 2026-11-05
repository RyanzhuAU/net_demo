import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, viewChild } from '@angular/core';

/** Native <dialog>-based confirmation, which gives focus trapping and Esc for free. */
@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog class="confirm" (cancel)="onCancel($event)" aria-labelledby="confirm-title">
      <h2 id="confirm-title" class="confirm__title">{{ title() }}</h2>
      <p class="confirm__message">{{ message() }}</p>
      <div class="confirm__actions">
        <button type="button" class="button" data-testid="confirm-cancel" (click)="cancelled.emit()">
          Cancel
        </button>
        <button
          type="button"
          class="button button--danger"
          data-testid="confirm-accept"
          (click)="confirmed.emit()"
        >
          {{ confirmLabel() }}
        </button>
      </div>
    </dialog>
  `,
  styles: `
    .confirm {
      border: none;
      border-radius: 0.75rem;
      padding: 1.5rem;
      max-width: 26rem;
      box-shadow: 0 20px 45px rgb(15 23 42 / 25%);
    }

    .confirm::backdrop {
      background: rgb(15 23 42 / 45%);
    }

    .confirm__title {
      margin: 0 0 0.5rem;
      font-size: 1.15rem;
    }

    .confirm__message {
      margin: 0 0 1.25rem;
      color: var(--color-muted);
    }

    .confirm__actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
    }
  `,
})
export class ConfirmDialog {
  readonly open = input(false);
  readonly title = input('Are you sure?');
  readonly message = input('This action cannot be undone.');
  readonly confirmLabel = input('Confirm');

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly dialogRef = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const dialog = this.dialogRef().nativeElement;
      if (this.open()) {
        if (!dialog.open) {
          dialog.showModal();
        }
      } else if (dialog.open) {
        dialog.close();
      }
    });
  }

  onCancel(event: Event): void {
    event.preventDefault();
    this.cancelled.emit();
  }
}
