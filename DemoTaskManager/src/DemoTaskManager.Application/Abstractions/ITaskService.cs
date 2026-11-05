using DemoTaskManager.Contracts.Common;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Contracts.Tasks;

namespace DemoTaskManager.Application.Abstractions;

public interface ITaskService
{
    Task<PagedResultDto<TaskItemDto>> GetPagedAsync(
        TaskQueryParameters query,
        CancellationToken cancellationToken);

    Task<TaskItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<TaskItemDto> CreateAsync(CreateTaskItemRequest request, CancellationToken cancellationToken);

    Task<TaskItemDto> UpdateAsync(Guid id, UpdateTaskItemRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<ServiceBusTaskMessageDto> RequestProcessingAsync(
        Guid id,
        ProcessTaskItemRequest request,
        string correlationId,
        CancellationToken cancellationToken);
}
