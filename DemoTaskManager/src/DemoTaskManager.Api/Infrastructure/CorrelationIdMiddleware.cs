namespace DemoTaskManager.Api.Infrastructure;

/// <summary>
/// Ensures every request carries a correlation id (reusing an inbound one when present) and
/// echoes it back so browser, API, Service Bus and WebJob traces can be stitched together.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var inbound)
                            && !string.IsNullOrWhiteSpace(inbound)
            ? inbound.ToString()[..Math.Min(inbound.ToString().Length, 128)]
            : context.TraceIdentifier;

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static string GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) && value is string id
            ? id
            : context.TraceIdentifier;
    }
}
