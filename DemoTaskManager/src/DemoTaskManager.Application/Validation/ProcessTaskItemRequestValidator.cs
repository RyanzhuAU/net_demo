using DemoTaskManager.Contracts.Tasks;
using FluentValidation;

namespace DemoTaskManager.Application.Validation;

public sealed class ProcessTaskItemRequestValidator : AbstractValidator<ProcessTaskItemRequest>
{
    public ProcessTaskItemRequestValidator()
    {
        RuleFor(x => x.TargetStatus)
            .IsInEnum().WithMessage("Target status must be Todo, InProgress or Completed.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must be 500 characters or fewer.");
    }
}
