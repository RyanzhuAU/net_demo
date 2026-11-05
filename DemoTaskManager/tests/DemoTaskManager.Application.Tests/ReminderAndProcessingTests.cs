using DemoTaskManager.Application.Options;
using DemoTaskManager.Application.Services;
using DemoTaskManager.Application.Tests.TestSupport;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Domain.Entities;
using DemoTaskManager.Infrastructure.Persistence;
using DemoTaskManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Application.Tests;

public class ReminderAndProcessingTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

    private SqliteDbFixture _fixture = null!;

    public async Task InitializeAsync() => _fixture = await SqliteDbFixture.CreateAsync();

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private ReminderService CreateReminderService(AppDbContext context, int batchSize = 100) =>
        new(new TaskRepository(context),
            new FixedDateTimeProvider(Now),
            Microsoft.Extensions.Options.Options.Create(new ReminderOptions { BatchSize = batchSize }),
            NullLogger<ReminderService>.Instance);

    private TaskMessageProcessor CreateProcessor(AppDbContext context) =>
        new(new TaskRepository(context),
            new TestRequestValidator(),
            new FixedDateTimeProvider(Now),
            NullLogger<TaskMessageProcessor>.Instance);

    private async Task SeedAsync(params TaskItem[] items)
    {
        await using var context = _fixture.CreateContext();
        context.Tasks.AddRange(items);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Scheduled_run_selects_only_overdue_unreminded_incomplete_tasks()
    {
        var overdue = TaskItem.Create("Overdue", null, TaskStatus.Todo, Now.AddHours(-1), Now.AddDays(-2));
        var future = TaskItem.Create("Future", null, TaskStatus.Todo, Now.AddHours(1), Now.AddDays(-2));
        var completed = TaskItem.Create("Completed", null, TaskStatus.Completed, Now.AddHours(-5), Now.AddDays(-3));
        var noDueDate = TaskItem.Create("No due date", null, TaskStatus.Todo, null, Now.AddDays(-4));
        await SeedAsync(overdue, future, completed, noDueDate);

        await using var context = _fixture.CreateContext();
        var result = await CreateReminderService(context).RunOnceAsync(CancellationToken.None);

        result.Candidates.Should().Be(1);
        result.RemindersSent.Should().Be(1);
    }

    [Fact]
    public async Task Scheduled_run_marks_the_reminder_as_processed()
    {
        var overdue = TaskItem.Create("Overdue", null, TaskStatus.InProgress, Now.AddHours(-2), Now.AddDays(-2));
        await SeedAsync(overdue);

        await using (var context = _fixture.CreateContext())
        {
            await CreateReminderService(context).RunOnceAsync(CancellationToken.None);
        }

        await using var verifyContext = _fixture.CreateContext();
        var reloaded = await verifyContext.Tasks.SingleAsync(t => t.Id == overdue.Id);
        reloaded.IsReminderSent.Should().BeTrue();
        reloaded.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Scheduled_run_is_idempotent_across_repeated_executions()
    {
        await SeedAsync(TaskItem.Create("Overdue", null, TaskStatus.Todo, Now.AddHours(-2), Now.AddDays(-2)));

        await using var context = _fixture.CreateContext();
        var service = CreateReminderService(context);

        var first = await service.RunOnceAsync(CancellationToken.None);
        var second = await service.RunOnceAsync(CancellationToken.None);

        first.RemindersSent.Should().Be(1);
        second.Candidates.Should().Be(0);
        second.RemindersSent.Should().Be(0);
    }

    [Fact]
    public async Task Reminder_claim_is_won_by_exactly_one_caller()
    {
        var task = TaskItem.Create("Contended", null, TaskStatus.Todo, Now.AddHours(-1), Now.AddDays(-1));
        await SeedAsync(task);

        await using var context = _fixture.CreateContext();
        var repository = new TaskRepository(context);

        var first = await repository.TryClaimReminderAsync(task.Id, Now, CancellationToken.None);
        var second = await repository.TryClaimReminderAsync(task.Id, Now, CancellationToken.None);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task Scheduled_run_honours_the_configured_batch_size()
    {
        var overdue = Enumerable.Range(1, 5)
            .Select(i => TaskItem.Create($"Overdue {i}", null, TaskStatus.Todo, Now.AddHours(-i), Now.AddDays(-2)))
            .ToArray();
        await SeedAsync(overdue);

        await using var context = _fixture.CreateContext();
        var result = await CreateReminderService(context, batchSize: 2).RunOnceAsync(CancellationToken.None);

        result.Candidates.Should().Be(2);
        result.RemindersSent.Should().Be(2);
    }

    [Fact]
    public async Task Processing_applies_the_requested_status()
    {
        var task = TaskItem.Create("Queue me", null, TaskStatus.Todo, null, Now.AddDays(-1));
        await SeedAsync(task);

        await using var context = _fixture.CreateContext();
        var outcome = await CreateProcessor(context).ProcessAsync(
            NewMessage(task.Id, TaskStatus.InProgress),
            CancellationToken.None);

        outcome.Should().Be(TaskProcessingOutcome.Applied);

        await using var verifyContext = _fixture.CreateContext();
        (await verifyContext.Tasks.SingleAsync(t => t.Id == task.Id)).Status.Should().Be(TaskStatus.InProgress);
    }

    [Fact]
    public async Task Processing_the_same_message_twice_is_a_no_op()
    {
        var task = TaskItem.Create("Queue me", null, TaskStatus.Todo, null, Now.AddDays(-1));
        await SeedAsync(task);

        var message = NewMessage(task.Id, TaskStatus.Completed);

        await using var context = _fixture.CreateContext();
        var processor = CreateProcessor(context);

        (await processor.ProcessAsync(message, CancellationToken.None))
            .Should().Be(TaskProcessingOutcome.Applied);
        (await processor.ProcessAsync(message, CancellationToken.None))
            .Should().Be(TaskProcessingOutcome.AlreadyInTargetState);
    }

    [Fact]
    public async Task Processing_an_unknown_task_reports_TaskNotFound_without_throwing()
    {
        await using var context = _fixture.CreateContext();

        var outcome = await CreateProcessor(context).ProcessAsync(
            NewMessage(Guid.NewGuid(), TaskStatus.Completed),
            CancellationToken.None);

        outcome.Should().Be(TaskProcessingOutcome.TaskNotFound);
    }

    [Fact]
    public async Task Processing_rejects_an_invalid_message_so_Service_Bus_can_dead_letter_it()
    {
        await using var context = _fixture.CreateContext();
        var processor = CreateProcessor(context);

        var act = async () => await processor.ProcessAsync(
            new ServiceBusTaskMessageDto(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private static ServiceBusTaskMessageDto NewMessage(Guid taskId, TaskStatus targetStatus) => new()
    {
        TaskId = taskId,
        TargetStatus = targetStatus,
        CorrelationId = "corr-test",
        RequestedAtUtc = Now
    };
}
