using DemoTaskManager.Application.Validation;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;
using FluentAssertions;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Application.Tests;

public class ValidationTests
{
    private readonly CreateTaskItemRequestValidator _createValidator = new();
    private readonly UpdateTaskItemRequestValidator _updateValidator = new();
    private readonly ServiceBusTaskMessageDtoValidator _messageValidator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_requires_a_title(string title)
    {
        var result = await _createValidator.ValidateAsync(new CreateTaskItemRequest { Title = title });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaskItemRequest.Title));
    }

    [Fact]
    public async Task Create_rejects_a_title_longer_than_the_column_length()
    {
        var request = new CreateTaskItemRequest { Title = new string('x', TaskItem.TitleMaxLength + 1) };

        var result = await _createValidator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("200 characters"));
    }

    [Fact]
    public async Task Create_accepts_a_well_formed_request()
    {
        var request = new CreateTaskItemRequest
        {
            Title = "Valid",
            Description = "Detail",
            Status = TaskStatus.InProgress,
            DueDateUtc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        (await _createValidator.ValidateAsync(request)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Create_rejects_an_out_of_range_status()
    {
        var request = new CreateTaskItemRequest { Title = "Valid", Status = (TaskStatus)42 };

        (await _createValidator.ValidateAsync(request)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Create_rejects_an_implausible_due_date()
    {
        var request = new CreateTaskItemRequest
        {
            Title = "Valid",
            DueDateUtc = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        (await _createValidator.ValidateAsync(request)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Update_requires_a_title()
    {
        var result = await _updateValidator.ValidateAsync(new UpdateTaskItemRequest { Title = "" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Update_rejects_an_over_long_description()
    {
        var request = new UpdateTaskItemRequest
        {
            Title = "Valid",
            Description = new string('x', TaskItem.DescriptionMaxLength + 1)
        };

        (await _updateValidator.ValidateAsync(request)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Service_bus_message_requires_task_id_and_correlation_id()
    {
        var result = await _messageValidator.ValidateAsync(new ServiceBusTaskMessageDto());

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.PropertyName).Should().Contain([
            nameof(ServiceBusTaskMessageDto.TaskId),
            nameof(ServiceBusTaskMessageDto.CorrelationId),
            nameof(ServiceBusTaskMessageDto.RequestedAtUtc)
        ]);
    }

    [Fact]
    public async Task Service_bus_message_accepts_a_complete_payload()
    {
        var message = new ServiceBusTaskMessageDto
        {
            TaskId = Guid.NewGuid(),
            TargetStatus = TaskStatus.Completed,
            CorrelationId = "corr-1",
            RequestedAtUtc = DateTime.UtcNow
        };

        (await _messageValidator.ValidateAsync(message)).IsValid.Should().BeTrue();
    }
}
