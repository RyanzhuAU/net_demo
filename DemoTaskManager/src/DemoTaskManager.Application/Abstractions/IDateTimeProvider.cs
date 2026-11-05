namespace DemoTaskManager.Application.Abstractions;

/// <summary>
/// Abstracts the clock so scheduled and message-driven behaviour is deterministically testable.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
