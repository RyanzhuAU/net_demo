using System.Text.Json.Serialization;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Contracts.Tasks;

/// <summary>
/// Read model returned by the API. Never expose the EF Core entity itself.
/// </summary>
public sealed record TaskItemDto
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<TaskStatus>))]
    public required TaskStatus Status { get; init; }

    public DateTime? DueDateUtc { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public required bool IsReminderSent { get; init; }

    public required bool IsOverdue { get; init; }
}
