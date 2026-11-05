/** Mirrors DemoTaskManager.Contracts.Common.PagedResultDto&lt;T&gt;. */
export interface PagedResultDto<T> {
  readonly items: readonly T[];
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasPreviousPage: boolean;
  readonly hasNextPage: boolean;
}

export function emptyPagedResult<T>(pageNumber = 1, pageSize = 10): PagedResultDto<T> {
  return {
    items: [],
    pageNumber,
    pageSize,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  };
}
