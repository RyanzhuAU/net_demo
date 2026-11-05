import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { AbstractControl } from '@angular/forms';

/** Renders accessible, human-readable messages for a reactive form control. */
@Component({
  selector: 'app-field-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (message(); as text) {
      <p class="field-error" role="alert">{{ text }}</p>
    }
  `,
  styles: `
    .field-error {
      margin: 0.25rem 0 0;
      color: var(--color-danger);
      font-size: 0.85rem;
    }
  `,
})
export class FieldError {
  readonly control = input.required<AbstractControl>();
  readonly label = input('This field');
  readonly serverErrors = input<readonly string[]>([]);

  message(): string | null {
    const serverMessages = this.serverErrors();
    if (serverMessages.length > 0) {
      return serverMessages.join(' ');
    }

    const control = this.control();
    if (!control.touched && !control.dirty) {
      return null;
    }

    const errors = control.errors;
    if (!errors) {
      return null;
    }

    if (errors['required']) {
      return `${this.label()} is required.`;
    }
    if (errors['maxlength']) {
      const detail = errors['maxlength'] as { requiredLength: number };
      return `${this.label()} must be ${detail.requiredLength} characters or fewer.`;
    }
    if (errors['minlength']) {
      const detail = errors['minlength'] as { requiredLength: number };
      return `${this.label()} must be at least ${detail.requiredLength} characters.`;
    }
    if (errors['invalidDate']) {
      return `${this.label()} is not a valid date.`;
    }

    return `${this.label()} is invalid.`;
  }
}
