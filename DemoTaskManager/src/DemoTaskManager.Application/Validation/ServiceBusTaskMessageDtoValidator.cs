using DemoTaskManager.Contracts.Messaging;
using FluentValidation;

namespace DemoTaskManager.Application.Validation;

public sealed class ServiceBusTaskMessageDtoValidator : AbstractValidator<ServiceBusTaskMessageDto>
{
    public ServiceBusTaskMessageDtoValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");

        RuleFor(x => x.TargetStatus)
            .IsInEnum().WithMessage("Target status must be Todo, InProgress or Completed.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty().WithMessage("CorrelationId is required.")
            .MaximumLength(128);

        RuleFor(x => x.RequestedAtUtc)
            .NotEqual(default(DateTime)).WithMessage("RequestedAtUtc is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(500);
    }
}
