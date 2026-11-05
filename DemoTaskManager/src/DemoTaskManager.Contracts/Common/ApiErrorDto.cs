namespace DemoTaskManager.Contracts.Common;

/// <summary>
/// Shape of the RFC 9457 ProblemDetails payload returned by the API, expressed as a
/// DTO so the Angular client can type its error handling.
/// </summary>
public sealed record ApiErrorDto
{
    public string? Type { get; init; }

    public required string Title { get; init; }

    public required int Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    public string? CorrelationId { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
