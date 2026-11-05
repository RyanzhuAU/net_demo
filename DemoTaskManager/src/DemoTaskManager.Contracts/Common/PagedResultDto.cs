namespace DemoTaskManager.Contracts.Common;

/// <summary>
/// Envelope for server-side paginated collections.
/// </summary>
public sealed record PagedResultDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int PageNumber { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResultDto<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount) =>
        new()
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };

    public static PagedResultDto<T> Empty(int pageNumber, int pageSize) =>
        Create([], pageNumber, pageSize, 0);
}
