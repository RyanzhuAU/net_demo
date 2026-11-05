using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Application.Common.Exceptions;
using DemoTaskManager.Application.Services;
using DemoTaskManager.Application.Tests.TestSupport;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;
using DemoTaskManager.Infrastructure.Persistence;
using DemoTaskManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Application.Tests;

public class TaskServiceTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    private SqliteDbFixture _fixture = null!;
    private ITaskMessagePublisher _publisher = null!;

    public async Task InitializeAsync()
    {
        _fixture = await SqliteDbFixture.CreateAsync();
        _publisher = Substitute.For<ITaskMessagePublisher>();
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    private (TaskService Service, AppDbContext Context) CreateService()
    {
        var context = _fixture.CreateContext();
        var service = new TaskService(
            new TaskRepository(context),
            _publisher,
            new TestRequestValidator(),
            new FixedDateTimeProvider(Now),
            NullLogger<TaskService>.Instance);

        return (service, context);
    }

    private async Task SeedAsync(params TaskItem[] items)
    {
        await using var context = _fixture.CreateContext();
        context.Tasks.AddRange(items);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_persists_the_task_and_returns_a_dto()
    {
        var (service, context) = CreateService();
        await using var _ = context;

        var dto = await service.CreateAsync(
            new CreateTaskItemRequest { Title = "Write docs", Status = TaskStatus.Todo },
            CancellationToken.None);

        dto.Title.Should().Be("Write docs");

        await using var verifyContext = _fixture.CreateContext();
        (await verifyContext.Tasks.FindAsync(dto.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_throws_a_validation_exception_for_an_empty_title()
    {
        var (service, context) = CreateService();
        await using var _ = context;

        var act = async () => await service.CreateAsync(
            new CreateTaskItemRequest { Title = " " },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_throws_NotFound_for_an_unknown_id()
    {
        var (service, context) = CreateService();
        await using var _ = context;

        var act = async () => await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateTaskItemRequest { Title = "Anything" },
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_persists_changes()
    {
        var task = TaskItem.Create("Before", null, TaskStatus.Todo, null, Now.AddDays(-1));
        await SeedAsync(task);

        var (service, context) = CreateService();
        await using var _ = context;

        var dto = await service.UpdateAsync(
            task.Id,
            new UpdateTaskItemRequest { Title = "After", Status = TaskStatus.Completed },
            CancellationToken.None);

        dto.Title.Should().Be("After");
        dto.Status.Should().Be(TaskStatus.Completed);
        dto.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_row()
    {
        var task = TaskItem.Create("Delete me", null, TaskStatus.Todo, null, Now);
        await SeedAsync(task);

        var (service, context) = CreateService();
        await using var _ = context;

        await service.DeleteAsync(task.Id, CancellationToken.None);

        await using var verifyContext = _fixture.CreateContext();
        (await verifyContext.Tasks.FindAsync(task.Id)).Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_status()
    {
        await SeedAsync(
            TaskItem.Create("A", null, TaskStatus.Todo, null, Now.AddDays(-3)),
            TaskItem.Create("B", null, TaskStatus.InProgress, null, Now.AddDays(-2)),
            TaskItem.Create("C", null, TaskStatus.InProgress, null, Now.AddDays(-1)));

        var (service, context) = CreateService();
        await using var _ = context;

        var result = await service.GetPagedAsync(
            new TaskQueryParameters { Status = TaskStatus.InProgress },
            CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(t => t.Status == TaskStatus.InProgress);
    }

    [Fact]
    public async Task GetPagedAsync_returns_newest_first_with_stable_paging()
    {
        var items = Enumerable.Range(1, 25)
            .Select(i => TaskItem.Create($"Task {i:D2}", null, TaskStatus.Todo, null, Now.AddMinutes(-i)))
            .ToArray();
        await SeedAsync(items);

        var (service, context) = CreateService();
        await using var _ = context;

        var firstPage = await service.GetPagedAsync(
            new TaskQueryParameters { PageNumber = 1, PageSize = 10 },
            CancellationToken.None);
        var secondPage = await service.GetPagedAsync(
            new TaskQueryParameters { PageNumber = 2, PageSize = 10 },
            CancellationToken.None);
        var thirdPage = await service.GetPagedAsync(
            new TaskQueryParameters { PageNumber = 3, PageSize = 10 },
            CancellationToken.None);

        firstPage.TotalCount.Should().Be(25);
        firstPage.TotalPages.Should().Be(3);
        firstPage.HasNextPage.Should().BeTrue();
        firstPage.HasPreviousPage.Should().BeFalse();
        firstPage.Items.Should().HaveCount(10);
        firstPage.Items[0].Title.Should().Be("Task 01");

        thirdPage.Items.Should().HaveCount(5);
        thirdPage.HasNextPage.Should().BeFalse();

        firstPage.Items.Select(i => i.Id)
            .Should().NotIntersectWith(secondPage.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task GetPagedAsync_clamps_an_oversized_page_size()
    {
        await SeedAsync(TaskItem.Create("Only", null, TaskStatus.Todo, null, Now));

        var (service, context) = CreateService();
        await using var _ = context;

        var result = await service.GetPagedAsync(
            new TaskQueryParameters { PageSize = 10_000 },
            CancellationToken.None);

        result.PageSize.Should().Be(TaskQueryParameters.MaxPageSize);
    }

    [Fact]
    public async Task GetPagedAsync_filters_by_search_term()
    {
        await SeedAsync(
            TaskItem.Create("Deploy to Azure", null, TaskStatus.Todo, null, Now.AddDays(-2)),
            TaskItem.Create("Write tests", null, TaskStatus.Todo, null, Now.AddDays(-1)));

        var (service, context) = CreateService();
        await using var _ = context;

        var result = await service.GetPagedAsync(
            new TaskQueryParameters { Search = "azure" },
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Title.Should().Be("Deploy to Azure");
    }

    [Fact]
    public async Task RequestProcessingAsync_publishes_a_message_for_an_existing_task()
    {
        var task = TaskItem.Create("Process me", null, TaskStatus.Todo, null, Now);
        await SeedAsync(task);

        var (service, context) = CreateService();
        await using var _ = context;

        var message = await service.RequestProcessingAsync(
            task.Id,
            new ProcessTaskItemRequest { TargetStatus = TaskStatus.InProgress, Reason = "manual" },
            "corr-9",
            CancellationToken.None);

        message.TaskId.Should().Be(task.Id);
        message.CorrelationId.Should().Be("corr-9");
        await _publisher.Received(1).PublishAsync(
            Arg.Is<ServiceBusTaskMessageDto>(m => m.TaskId == task.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestProcessingAsync_throws_NotFound_and_publishes_nothing()
    {
        var (service, context) = CreateService();
        await using var _ = context;

        var act = async () => await service.RequestProcessingAsync(
            Guid.NewGuid(),
            new ProcessTaskItemRequest(),
            "corr-9",
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _publisher.DidNotReceive().PublishAsync(
            Arg.Any<ServiceBusTaskMessageDto>(),
            Arg.Any<CancellationToken>());
    }
}
