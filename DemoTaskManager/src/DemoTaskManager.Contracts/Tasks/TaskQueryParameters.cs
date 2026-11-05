using System.ComponentModel.DataAnnotations;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Contracts.Tasks;

/// <summary>
/// Query string binding for <c>GET /api/tasks</c>. Values are clamped on assignment so a
/// hostile page size can never reach the database.
/// </summary>
public sealed record TaskQueryParameters
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 10;

    private readonly int _pageNumber = 1;
    private readonly int _pageSize = DefaultPageSize;

    public TaskStatus? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    [Range(1, MaxPageSize)]
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Optional case-insensitive title contains-filter.</summary>
    [StringLength(200)]
    public string? Search { get; init; }
}
