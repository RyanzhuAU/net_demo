using System.Text.Json.Serialization;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Contracts.Tasks;

public sealed record CreateTaskItemRequest
{
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<TaskStatus>))]
    public TaskStatus Status { get; init; } = TaskStatus.Todo;

    public DateTime? DueDateUtc { get; init; }
}
