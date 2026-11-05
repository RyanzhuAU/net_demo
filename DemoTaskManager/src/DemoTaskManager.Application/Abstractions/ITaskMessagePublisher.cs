using DemoTaskManager.Contracts.Messaging;

namespace DemoTaskManager.Application.Abstractions;

/// <summary>
/// Publishes task-processing commands to the <c>task-processing</c> queue.
/// </summary>
public interface ITaskMessagePublisher
{
    Task PublishAsync(ServiceBusTaskMessageDto message, CancellationToken cancellationToken);
}
