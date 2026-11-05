using System.Text.Json;
using Azure.Messaging.ServiceBus;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.ServiceBusWebJob;
using FluentAssertions;
using NSubstitute;
using DemoTaskManager.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.WebJobs.Tests;

public class ServiceBusMessageTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ServiceBusReceivedMessage CreateReceivedMessage(object body, string correlationId = "corr-1") =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(body, Json),
            messageId: Guid.NewGuid().ToString("N"),
            correlationId: correlationId,
            contentType: "application/json",
            deliveryCount: 1);

    [Fact]
    public void Message_round_trips_through_camel_case_json()
    {
        var original = new ServiceBusTaskMessageDto
        {
            TaskId = Guid.NewGuid(),
            TargetStatus = TaskStatus.Completed,
            Reason = "manual",
            CorrelationId = "corr-42",
            RequestedAtUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(original, Json);
        json.Should().Contain("\"taskId\"").And.Contain("\"Completed\"");

        var restored = JsonSerializer.Deserialize<ServiceBusTaskMessageDto>(json, Json);
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task Function_deserialises_the_payload_and_forwards_it_to_the_processor()
    {
        var processor = Substitute.For<ITaskMessageProcessor>();
        processor
            .ProcessAsync(Arg.Any<ServiceBusTaskMessageDto>(), Arg.Any<CancellationToken>())
            .Returns(TaskProcessingOutcome.Applied);

        var functions = new TaskProcessingFunctions(processor, NullLogger<TaskProcessingFunctions>.Instance);
        var taskId = Guid.NewGuid();

        await functions.ProcessTaskMessageAsync(
            CreateReceivedMessage(new
            {
                taskId,
                targetStatus = "InProgress",
                reason = "manual",
                correlationId = "corr-1",
                requestedAtUtc = "2026-03-01T08:00:00Z"
            }),
            CancellationToken.None);

        await processor.Received(1).ProcessAsync(
            Arg.Is<ServiceBusTaskMessageDto>(m => m.TaskId == taskId && m.TargetStatus == TaskStatus.InProgress),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Function_throws_for_a_malformed_payload_so_the_message_is_retried_then_dead_lettered()
    {
        var processor = Substitute.For<ITaskMessageProcessor>();
        var functions = new TaskProcessingFunctions(processor, NullLogger<TaskProcessingFunctions>.Instance);

        var poison = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("not json at all"),
            messageId: "poison-1",
            deliveryCount: 3);

        var act = async () => await functions.ProcessTaskMessageAsync(poison, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await processor.DidNotReceive().ProcessAsync(
            Arg.Any<ServiceBusTaskMessageDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Function_does_not_swallow_processor_failures()
    {
        var processor = Substitute.For<ITaskMessageProcessor>();
        processor
            .ProcessAsync(Arg.Any<ServiceBusTaskMessageDto>(), Arg.Any<CancellationToken>())
            .Returns<TaskProcessingOutcome>(_ => throw new TimeoutException("transient"));

        var functions = new TaskProcessingFunctions(processor, NullLogger<TaskProcessingFunctions>.Instance);

        var act = async () => await functions.ProcessTaskMessageAsync(
            CreateReceivedMessage(new
            {
                taskId = Guid.NewGuid(),
                targetStatus = "Completed",
                correlationId = "corr-2",
                requestedAtUtc = "2026-03-01T08:00:00Z"
            }),
            CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Function_passes_the_cancellation_token_through()
    {
        var processor = Substitute.For<ITaskMessageProcessor>();
        processor
            .ProcessAsync(Arg.Any<ServiceBusTaskMessageDto>(), Arg.Any<CancellationToken>())
            .Returns(TaskProcessingOutcome.AlreadyInTargetState);

        var functions = new TaskProcessingFunctions(processor, NullLogger<TaskProcessingFunctions>.Instance);
        using var cts = new CancellationTokenSource();

        await functions.ProcessTaskMessageAsync(
            CreateReceivedMessage(new
            {
                taskId = Guid.NewGuid(),
                targetStatus = "Completed",
                correlationId = "corr-3",
                requestedAtUtc = "2026-03-01T08:00:00Z"
            }),
            cts.Token);

        await processor.Received(1).ProcessAsync(Arg.Any<ServiceBusTaskMessageDto>(), cts.Token);
    }
}
