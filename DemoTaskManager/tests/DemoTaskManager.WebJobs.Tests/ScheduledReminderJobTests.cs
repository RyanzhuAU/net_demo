using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Application.Options;
using DemoTaskManager.Application.Services;
using DemoTaskManager.Domain.Entities;
using DemoTaskManager.Infrastructure.Persistence;
using DemoTaskManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.WebJobs.Tests;

public class ScheduledReminderJobTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 10, 6, 0, 0, DateTimeKind.Utc);

    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private ReminderService CreateService(AppDbContext context, int graceMinutes = 0) =>
        new(new TaskRepository(context),
            new StubClock(Now),
            Microsoft.Extensions.Options.Options.Create(new ReminderOptions { BatchSize = 50, GraceMinutes = graceMinutes }),
            NullLogger<ReminderService>.Instance);

    private async Task SeedAsync(params TaskItem[] items)
    {
        await using var context = CreateContext();
        context.Tasks.AddRange(items);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Selects_overdue_tasks_that_have_not_been_reminded()
    {
        await SeedAsync(
            TaskItem.Create("Overdue 1", null, TaskStatus.Todo, Now.AddHours(-3), Now.AddDays(-5)),
            TaskItem.Create("Overdue 2", null, TaskStatus.InProgress, Now.AddMinutes(-1), Now.AddDays(-5)),
            TaskItem.Create("Not due", null, TaskStatus.Todo, Now.AddHours(3), Now.AddDays(-5)));

        await using var context = CreateContext();
        var result = await CreateService(context).RunOnceAsync(CancellationToken.None);

        result.Candidates.Should().Be(2);
        result.RemindersSent.Should().Be(2);
    }

    [Fact]
    public async Task Grace_period_defers_freshly_overdue_tasks()
    {
        await SeedAsync(TaskItem.Create("Just overdue", null, TaskStatus.Todo, Now.AddMinutes(-5), Now.AddDays(-1)));

        await using var context = CreateContext();
        var result = await CreateService(context, graceMinutes: 30).RunOnceAsync(CancellationToken.None);

        result.Candidates.Should().Be(0);
    }

    [Fact]
    public async Task Updates_reminder_state_and_timestamp()
    {
        var task = TaskItem.Create("Overdue", null, TaskStatus.Todo, Now.AddHours(-1), Now.AddDays(-2));
        await SeedAsync(task);

        await using (var context = CreateContext())
        {
            await CreateService(context).RunOnceAsync(CancellationToken.None);
        }

        await using var verify = CreateContext();
        var reloaded = await verify.Tasks.SingleAsync(t => t.Id == task.Id);
        reloaded.IsReminderSent.Should().BeTrue();
        reloaded.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Second_run_finds_nothing_left_to_do()
    {
        await SeedAsync(TaskItem.Create("Overdue", null, TaskStatus.Todo, Now.AddHours(-1), Now.AddDays(-2)));

        await using var context = CreateContext();
        var service = CreateService(context);

        await service.RunOnceAsync(CancellationToken.None);
        var second = await service.RunOnceAsync(CancellationToken.None);

        second.Candidates.Should().Be(0);
        second.RemindersSent.Should().Be(0);
    }

    [Fact]
    public async Task Honours_cancellation()
    {
        await SeedAsync(TaskItem.Create("Overdue", null, TaskStatus.Todo, Now.AddHours(-1), Now.AddDays(-2)));

        await using var context = CreateContext();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await CreateService(context).RunOnceAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class StubClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
