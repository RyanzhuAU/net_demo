using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Application.Common.Validation;
using DemoTaskManager.Contracts.Messaging;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DemoTaskManager.Application.Services;

public enum TaskProcessingOutcome
{
    Applied,
    AlreadyInTargetState,
    TaskNotFound
}

public interface ITaskMessageProcessor
{
    Task<TaskProcessingOutcome> ProcessAsync(ServiceBusTaskMessageDto message, CancellationToken cancellationToken);
}

/// <summary>
/// Applies a queued task-processing command. Processing is idempotent: replaying the same
/// message is a no-op once the task already sits in the requested state, which is what
/// Service Bus at-least-once delivery requires.
/// </summary>
public sealed class TaskMessageProcessor(
    ITaskRepository repository,
    IRequestValidator validator,
    IDateTimeProvider clock,
    ILogger<TaskMessageProcessor> logger) : ITaskMessageProcessor
{
    public async Task<TaskProcessingOutcome> ProcessAsync(
        ServiceBusTaskMessageDto message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var validation = await validator.ValidateAsync(message, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            // Poison message: fail loudly so Service Bus dead-letters after max delivery count.
            throw new ValidationException(validation.Errors);
        }

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["TaskId"] = message.TaskId
        });

        var entity = await repository.GetByIdAsync(message.TaskId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            logger.LogWarning("Task {TaskId} no longer exists; message ignored", message.TaskId);
            return TaskProcessingOutcome.TaskNotFound;
        }

        if (entity.Status == message.TargetStatus)
        {
            logger.LogInformation("Task {TaskId} already in status {Status}; nothing to do", entity.Id, entity.Status);
            return TaskProcessingOutcome.AlreadyInTargetState;
        }

        entity.ChangeStatus(message.TargetStatus, clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Task {TaskId} moved to status {Status}", entity.Id, message.TargetStatus);
        return TaskProcessingOutcome.Applied;
    }
}
