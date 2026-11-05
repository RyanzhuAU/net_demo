using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Application.Common.Validation;
using DemoTaskManager.Application.Validation;
using FluentValidation;
using FluentValidation.Results;

namespace DemoTaskManager.Application.Tests.TestSupport;

public sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
}

/// <summary>
/// Wires the real FluentValidation validators without a DI container.
/// </summary>
public sealed class TestRequestValidator : IRequestValidator
{
    private readonly Dictionary<Type, object> _validators = new()
    {
        [typeof(Contracts.Tasks.CreateTaskItemRequest)] = new CreateTaskItemRequestValidator(),
        [typeof(Contracts.Tasks.UpdateTaskItemRequest)] = new UpdateTaskItemRequestValidator(),
        [typeof(Contracts.Tasks.ProcessTaskItemRequest)] = new ProcessTaskItemRequestValidator(),
        [typeof(Contracts.Messaging.ServiceBusTaskMessageDto)] = new ServiceBusTaskMessageDtoValidator()
    };

    public async Task ValidateAndThrowAsync<T>(T instance, CancellationToken cancellationToken)
        where T : class
    {
        var result = await ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    public Task<ValidationResult> ValidateAsync<T>(T instance, CancellationToken cancellationToken)
        where T : class
    {
        if (!_validators.TryGetValue(typeof(T), out var validator) || validator is not IValidator<T> typed)
        {
            return Task.FromResult(new ValidationResult());
        }

        return typed.ValidateAsync(instance, cancellationToken);
    }
}
