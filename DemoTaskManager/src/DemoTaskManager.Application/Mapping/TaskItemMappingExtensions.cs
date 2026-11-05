using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;

namespace DemoTaskManager.Application.Mapping;

/// <summary>
/// Explicit, allocation-friendly mapping. No reflection-based mapper is used so the
/// projection stays visible to reviewers and to the EF Core query translator.
/// </summary>
public static class TaskItemMappingExtensions
{
    public static TaskItemDto ToDto(this TaskItem entity, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new TaskItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Status = entity.Status,
            DueDateUtc = entity.DueDateUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            IsReminderSent = entity.IsReminderSent,
            IsOverdue = entity.IsOverdue(utcNow)
        };
    }

    public static IReadOnlyList<TaskItemDto> ToDtoList(this IEnumerable<TaskItem> entities, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(entities);

        return [.. entities.Select(e => e.ToDto(utcNow))];
    }

    public static TaskItem ToEntity(this CreateTaskItemRequest request, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TaskItem.Create(
            request.Title,
            request.Description,
            request.Status,
            request.DueDateUtc,
            utcNow);
    }

    public static void ApplyTo(this UpdateTaskItemRequest request, TaskItem entity, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entity);

        entity.Update(
            request.Title,
            request.Description,
            request.Status,
            request.DueDateUtc,
            utcNow);
    }

    public static ServiceBusTaskMessageDto ToMessage(
        this ProcessTaskItemRequest request,
        Guid taskId,
        string correlationId,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ServiceBusTaskMessageDto
        {
            TaskId = taskId,
            TargetStatus = request.TargetStatus,
            Reason = request.Reason,
            CorrelationId = correlationId,
            RequestedAtUtc = utcNow
        };
    }
}
