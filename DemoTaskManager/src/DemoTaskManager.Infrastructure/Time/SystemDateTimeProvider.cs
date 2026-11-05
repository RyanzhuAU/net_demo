using DemoTaskManager.Application.Abstractions;

namespace DemoTaskManager.Infrastructure.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
