using DemoTaskManager.Contracts.Tasks;
using DemoTaskManager.Domain.Entities;
using FluentValidation;

namespace DemoTaskManager.Application.Validation;

public sealed class UpdateTaskItemRequestValidator : AbstractValidator<UpdateTaskItemRequest>
{
    public UpdateTaskItemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(TaskItem.TitleMaxLength)
            .WithMessage($"Title must be {TaskItem.TitleMaxLength} characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(TaskItem.DescriptionMaxLength)
            .WithMessage($"Description must be {TaskItem.DescriptionMaxLength} characters or fewer.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Status must be Todo, InProgress or Completed.");

        RuleFor(x => x.DueDateUtc)
            .Must(BeAReasonableDate)
            .When(x => x.DueDateUtc.HasValue)
            .WithMessage("Due date must be between 2000-01-01 and 2100-01-01 (UTC).");
    }

    private static bool BeAReasonableDate(DateTime? value) =>
        value is { } date && date > new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                          && date < new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
