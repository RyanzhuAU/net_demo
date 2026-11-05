using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DemoTaskManager.Infrastructure.Messaging;

public sealed class ServiceBusTaskMessagePublisher(
    ServiceBusClient client,
    IOptions<ServiceBusOptions> options,
    ILogger<ServiceBusTaskMessagePublisher> logger) : ITaskMessagePublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusOptions _options = options.Value;
    private ServiceBusSender? _sender;

    public async Task PublishAsync(ServiceBusTaskMessageDto message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var sender = _sender ??= client.CreateSender(_options.QueueName);

        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message, SerializerOptions))
        {
            ContentType = "application/json",
            Subject = nameof(ServiceBusTaskMessageDto),
            CorrelationId = message.CorrelationId,
            // Enables Service Bus duplicate detection when the namespace/queue has it enabled.
            MessageId = $"{message.TaskId:N}-{message.CorrelationId}"
        };

        await sender.SendMessageAsync(serviceBusMessage, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Published task-processing message for {TaskId} to queue {QueueName} (correlation {CorrelationId})",
            message.TaskId,
            _options.QueueName,
            message.CorrelationId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null)
        {
            await _sender.DisposeAsync().ConfigureAwait(false);
            _sender = null;
        }
    }
}

/// <summary>
/// Used when Service Bus is disabled (offline demo / unit tests) so the API still works.
/// </summary>
public sealed class NullTaskMessagePublisher(ILogger<NullTaskMessagePublisher> logger) : ITaskMessagePublisher
{
    public Task PublishAsync(ServiceBusTaskMessageDto message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        logger.LogWarning(
            "Service Bus is disabled; dropping task-processing message for {TaskId} (correlation {CorrelationId})",
            message.TaskId,
            message.CorrelationId);

        return Task.CompletedTask;
    }
}
