using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Application.Common.Exceptions;
using DemoTaskManager.Application.Common.Validation;
using DemoTaskManager.Application.Mapping;
using DemoTaskManager.Contracts.Common;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Contracts.Tasks;
using Microsoft.Extensions.Logging;

namespace DemoTaskManager.Application.Services;

public sealed class TaskService(
    ITaskRepository repository,
    ITaskMessagePublisher publisher,
    IRequestValidator validator,
    IDateTimeProvider clock,
    ILogger<TaskService> logger) : ITaskService
{
    public async Task<PagedResultDto<TaskItemDto>> GetPagedAsync(
        TaskQueryParameters query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, totalCount) = await repository.GetPagedAsync(query, cancellationToken).ConfigureAwait(false);

        return PagedResultDto<TaskItemDto>.Create(
            items.ToDtoList(clock.UtcNow),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<TaskItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return entity?.ToDto(clock.UtcNow);
    }

    public async Task<TaskItemDto> CreateAsync(CreateTaskItemRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        var now = clock.UtcNow;
        var entity = request.ToEntity(now);

        repository.Add(entity);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Created task {TaskId}", entity.Id);
        return entity.ToDto(now);
    }

    public async Task<TaskItemDto> UpdateAsync(
        Guid id,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException(nameof(Domain.Entities.TaskItem), id);

        var now = clock.UtcNow;
        request.ApplyTo(entity, now);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Updated task {TaskId}", id);
        return entity.ToDto(now);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException(nameof(Domain.Entities.TaskItem), id);

        repository.Remove(entity);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Deleted task {TaskId}", id);
    }

    public async Task<ServiceBusTaskMessageDto> RequestProcessingAsync(
        Guid id,
        ProcessTaskItemRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);

        var entity = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException(nameof(Domain.Entities.TaskItem), id);

        var message = request.ToMessage(entity.Id, correlationId, clock.UtcNow);
        await publisher.PublishAsync(message, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Queued processing for task {TaskId} with correlation {CorrelationId}",
            entity.Id,
            correlationId);

        return message;
    }
}
