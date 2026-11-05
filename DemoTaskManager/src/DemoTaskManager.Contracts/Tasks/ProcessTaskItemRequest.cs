using System.Text.Json.Serialization;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Contracts.Tasks;

/// <summary>
/// Body of <c>POST /api/tasks/{id}/process</c>: asks the API to enqueue a
/// task-processing message on Azure Service Bus.
/// </summary>
public sealed record ProcessTaskItemRequest
{
    /// <summary>Status the background worker should apply once processing succeeds.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TaskStatus>))]
    public TaskStatus TargetStatus { get; init; } = TaskStatus.InProgress;

    /// <summary>Free-form, non-sensitive note recorded with the processing request.</summary>
    public string? Reason { get; init; }
}
