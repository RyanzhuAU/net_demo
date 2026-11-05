using System.Text.Json.Serialization;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Contracts.Messaging;

/// <summary>
/// Contract exchanged over the <c>task-processing</c> Azure Service Bus queue.
/// Deliberately carries no personal or secret data so it is safe to trace.
/// </summary>
public sealed record ServiceBusTaskMessageDto
{
    public Guid TaskId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<TaskStatus>))]
    public TaskStatus TargetStatus { get; init; } = TaskStatus.InProgress;

    public string? Reason { get; init; }

    /// <summary>Stable id used for idempotency and for correlating logs end to end.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    public DateTime RequestedAtUtc { get; init; }
}
