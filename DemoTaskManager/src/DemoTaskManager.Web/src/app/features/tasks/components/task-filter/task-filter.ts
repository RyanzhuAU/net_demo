import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TASK_STATUSES, TASK_STATUS_LABELS, type TaskStatus } from '../../../../core/models';

@Component({
  selector: 'app-task-filter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="filter" role="search" (submit)="$event.preventDefault()">
      <label class="filter__field">
        <span>Status</span>
        <select
          data-testid="status-filter"
          [value]="status() ?? ''"
          (change)="onStatusChange($event)"
          aria-label="Filter tasks by status"
        >
          <option value="">All statuses</option>
          @for (option of statuses; track option) {
            <option [value]="option">{{ labels[option] }}</option>
          }
        </select>
      </label>

      <label class="filter__field filter__field--grow">
        <span>Search</span>
        <input
          type="search"
          data-testid="search-filter"
          placeholder="Search by title"
          [value]="search() ?? ''"
          (input)="onSearchChange($event)"
          aria-label="Search tasks by title"
        />
      </label>

      <button type="button" class="button" data-testid="reset-filters" (click)="filtersReset.emit()">
        Reset
      </button>
    </form>
  `,
  styles: `
    .filter {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 0.75rem;
      margin-bottom: 1rem;
    }

    .filter__field {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.85rem;
      color: var(--color-muted);
    }

    .filter__field--grow {
      flex: 1 1 14rem;
    }
  `,
})
export class TaskFilter {
  readonly status = input<TaskStatus | null>(null);
  readonly search = input<string | null>(null);

  readonly statusChange = output<TaskStatus | null>();
  readonly searchChange = output<string | null>();
  readonly filtersReset = output<void>();

  readonly statuses = TASK_STATUSES;
  readonly labels = TASK_STATUS_LABELS;

  onStatusChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.statusChange.emit(value === '' ? null : (value as TaskStatus));
  }

  onSearchChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    this.searchChange.emit(value === '' ? null : value);
  }
}
