import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="pager" aria-label="Pagination">
      <button
        type="button"
        class="pager__button"
        data-testid="previous-page"
        [disabled]="!hasPreviousPage()"
        (click)="pageChange.emit(pageNumber() - 1)"
      >
        Previous
      </button>

      <span class="pager__status" aria-live="polite" data-testid="pager-status">
        Page {{ pageNumber() }} of {{ safeTotalPages() }} · {{ totalCount() }} item(s)
      </span>

      <button
        type="button"
        class="pager__button"
        data-testid="next-page"
        [disabled]="!hasNextPage()"
        (click)="pageChange.emit(pageNumber() + 1)"
      >
        Next
      </button>

      <label class="pager__size">
        <span>Page size</span>
        <select
          data-testid="page-size"
          [value]="pageSize()"
          (change)="onPageSizeChange($event)"
          aria-label="Items per page"
        >
          @for (size of pageSizes; track size) {
            <option [value]="size">{{ size }}</option>
          }
        </select>
      </label>
    </nav>
  `,
  styles: `
    .pager {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.75rem;
      margin-top: 1rem;
    }

    .pager__button {
      padding: 0.4rem 0.9rem;
      border: 1px solid var(--color-border);
      border-radius: 0.4rem;
      background: var(--color-surface);
      cursor: pointer;
    }

    .pager__button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .pager__status {
      color: var(--color-muted);
      font-size: 0.9rem;
    }

    .pager__size {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      margin-left: auto;
      font-size: 0.9rem;
    }
  `,
})
export class Pagination {
  readonly pageNumber = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly hasPreviousPage = input.required<boolean>();
  readonly hasNextPage = input.required<boolean>();

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly pageSizes = [5, 10, 25, 50] as const;

  readonly safeTotalPages = computed(() => Math.max(1, this.totalPages()));

  onPageSizeChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (!Number.isNaN(value)) {
      this.pageSizeChange.emit(value);
    }
  }
}
