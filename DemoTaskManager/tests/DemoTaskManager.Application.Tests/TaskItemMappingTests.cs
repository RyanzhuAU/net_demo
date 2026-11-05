using DemoTaskManager.Application.Mapping;
using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;
using FluentAssertions;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Application.Tests;

public class TaskItemMappingTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToDto_copies_every_field_and_computes_overdue()
    {
        var entity = TaskItem.Create("Ship it", "Description", TaskStatus.InProgress, Now.AddDays(-1), Now.AddDays(-5));

        var dto = entity.ToDto(Now);

        dto.Id.Should().Be(entity.Id);
        dto.Title.Should().Be("Ship it");
        dto.Description.Should().Be("Description");
        dto.Status.Should().Be(TaskStatus.InProgress);
        dto.DueDateUtc.Should().Be(Now.AddDays(-1));
        dto.CreatedAtUtc.Should().Be(Now.AddDays(-5));
        dto.IsReminderSent.Should().BeFalse();
        dto.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public void ToDto_reports_completed_tasks_as_not_overdue()
    {
        var entity = TaskItem.Create("Done", null, TaskStatus.Completed, Now.AddDays(-10), Now.AddDays(-20));

        entity.ToDto(Now).IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void ToEntity_trims_input_and_normalises_blank_descriptions()
    {
        var request = new CreateTaskItemRequest
        {
            Title = "  Padded title  ",
            Description = "   ",
            Status = TaskStatus.Todo,
            DueDateUtc = null
        };

        var entity = request.ToEntity(Now);

        entity.Title.Should().Be("Padded title");
        entity.Description.Should().BeNull();
        entity.CreatedAtUtc.Should().Be(Now);
        entity.UpdatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplyTo_updates_the_entity_and_stamps_UpdatedAtUtc()
    {
        var entity = TaskItem.Create("Old", null, TaskStatus.Todo, null, Now.AddDays(-1));
        var request = new UpdateTaskItemRequest
        {
            Title = "New",
            Description = "Detail",
            Status = TaskStatus.Completed,
            DueDateUtc = Now.AddDays(3)
        };

        request.ApplyTo(entity, Now);

        entity.Title.Should().Be("New");
        entity.Description.Should().Be("Detail");
        entity.Status.Should().Be(TaskStatus.Completed);
        entity.DueDateUtc.Should().Be(Now.AddDays(3));
        entity.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void ToMessage_carries_the_task_id_and_correlation_id()
    {
        var taskId = Guid.NewGuid();
        var request = new ProcessTaskItemRequest { TargetStatus = TaskStatus.Completed, Reason = "manual" };

        var message = request.ToMessage(taskId, "corr-123", Now);

        message.TaskId.Should().Be(taskId);
        message.TargetStatus.Should().Be(TaskStatus.Completed);
        message.Reason.Should().Be("manual");
        message.CorrelationId.Should().Be("corr-123");
        message.RequestedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Local_dates_are_converted_to_Utc()
    {
        var local = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Local);

        var entity = TaskItem.Create("Local", null, TaskStatus.Todo, local, Now);

        entity.DueDateUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        entity.DueDateUtc.Should().Be(local.ToUniversalTime());
    }
}
