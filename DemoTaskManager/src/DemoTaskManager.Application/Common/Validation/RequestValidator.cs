using FluentValidation;
using FluentValidation.Results;

namespace DemoTaskManager.Application.Common.Validation;

/// <summary>
/// Runs FluentValidation validators resolved from DI and throws a
/// <see cref="ValidationException"/> that the API translates into a 400 ProblemDetails.
/// </summary>
public interface IRequestValidator
{
    Task ValidateAndThrowAsync<T>(T instance, CancellationToken cancellationToken) where T : class;

    Task<ValidationResult> ValidateAsync<T>(T instance, CancellationToken cancellationToken) where T : class;
}

public sealed class RequestValidator(IServiceProvider serviceProvider) : IRequestValidator
{
    public async Task ValidateAndThrowAsync<T>(T instance, CancellationToken cancellationToken)
        where T : class
    {
        var result = await ValidateAsync(instance, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    public async Task<ValidationResult> ValidateAsync<T>(T instance, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (serviceProvider.GetService(typeof(IValidator<T>)) is not IValidator<T> validator)
        {
            return new ValidationResult();
        }

        return await validator.ValidateAsync(instance, cancellationToken).ConfigureAwait(false);
    }
}
