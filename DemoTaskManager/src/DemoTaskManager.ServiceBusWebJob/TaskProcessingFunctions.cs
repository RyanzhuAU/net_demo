using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DemoTaskManager.Application.Services;
using DemoTaskManager.Contracts.Messaging;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace DemoTaskManager.ServiceBusWebJob;

/// <summary>
/// Queue-triggered function bound to the <c>task-processing</c> queue.
/// Exceptions deliberately propagate so the WebJobs SDK abandons the message and
/// Service Bus applies its own retry / dead-letter policy.
/// </summary>
public sealed class TaskProcessingFunctions(
    ITaskMessageProcessor processor,
    ILogger<TaskProcessingFunctions> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [FunctionName(nameof(ProcessTaskMessageAsync))]
    public async Task ProcessTaskMessageAsync(
        [ServiceBusTrigger("%ServiceBus:QueueName%", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage receivedMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receivedMessage);

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["MessageId"] = receivedMessage.MessageId,
            ["CorrelationId"] = receivedMessage.CorrelationId ?? string.Empty,
            ["DeliveryCount"] = receivedMessage.DeliveryCount
        });

        var message = Deserialize(receivedMessage);

        // Only identifiers are logged; the payload body is never written to the log.
        logger.LogInformation(
            "Processing task-processing message for {TaskId} (delivery {DeliveryCount})",
            message.TaskId,
            receivedMessage.DeliveryCount);

        var outcome = await processor.ProcessAsync(message, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Message for {TaskId} completed with outcome {Outcome}", message.TaskId, outcome);
    }

    private static ServiceBusTaskMessageDto Deserialize(ServiceBusReceivedMessage receivedMessage)
    {
        try
        {
            return receivedMessage.Body.ToObjectFromJson<ServiceBusTaskMessageDto>(SerializerOptions)
                   ?? throw new InvalidOperationException("Message body deserialised to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Message '{receivedMessage.MessageId}' is not a valid {nameof(ServiceBusTaskMessageDto)} payload.", ex);
        }
    }
}
