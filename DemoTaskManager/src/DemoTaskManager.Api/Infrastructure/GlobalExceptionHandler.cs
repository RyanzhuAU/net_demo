using DemoTaskManager.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ValidationException = FluentValidation.ValidationException;

namespace DemoTaskManager.Api.Infrastructure;

/// <summary>
/// Translates application exceptions into RFC 9457 ProblemDetails. Exception details are
/// only surfaced in Development; production clients get a generic message plus the
/// correlation id needed to find the real error in Application Insights.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var correlationId = httpContext.GetCorrelationId();
        var (status, title, detail) = Describe(exception, environment.IsDevelopment());

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for correlation {CorrelationId}", correlationId);
        }
        else
        {
            logger.LogWarning("Request rejected ({Status}) for correlation {CorrelationId}: {Title}",
                status, correlationId, title);
        }

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["correlationId"] = correlationId;

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        }).ConfigureAwait(false);
    }

    private static (int Status, string Title, string? Detail) Describe(Exception exception, bool isDevelopment) =>
        exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "One or more validation errors occurred.", null),
            NotFoundException notFound => (StatusCodes.Status404NotFound, "Resource not found.", notFound.Message),
            ArgumentException argument => (StatusCodes.Status400BadRequest, "Invalid request.", argument.Message),
            _ => (StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                isDevelopment ? exception.ToString() : null)
        };
}
