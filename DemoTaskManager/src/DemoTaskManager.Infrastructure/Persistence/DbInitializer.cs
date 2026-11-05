using DemoTaskManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskStatus = DemoTaskManager.Domain.Enums.TaskStatus;

namespace DemoTaskManager.Infrastructure.Persistence;

/// <summary>
/// Development-only helper: applies pending migrations and inserts demo rows.
/// Production deployments run migrations from the pipeline instead.
/// </summary>
public static class DbInitializer
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));

        logger.LogInformation("Applying EF Core migrations (development mode)");
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        if (await dbContext.Tasks.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        logger.LogInformation("Seeding demo tasks");
        dbContext.Tasks.AddRange(CreateSeedData(DateTime.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyList<TaskItem> CreateSeedData(DateTime utcNow) =>
    [
        TaskItem.Create(
            "Prepare Azure demo environment",
            "Provision the resource group, App Service plan and SQL database.",
            TaskStatus.InProgress,
            utcNow.AddDays(2),
            utcNow.AddDays(-3),
            Guid.Parse("11111111-1111-1111-1111-111111111111")),
        TaskItem.Create(
            "Write architecture documentation",
            "Describe the Angular, API and WebJob request flows.",
            TaskStatus.Todo,
            utcNow.AddDays(5),
            utcNow.AddDays(-2),
            Guid.Parse("22222222-2222-2222-2222-222222222222")),
        TaskItem.Create(
            "Review Service Bus retry policy",
            null,
            TaskStatus.Todo,
            utcNow.AddDays(-1),
            utcNow.AddDays(-10),
            Guid.Parse("33333333-3333-3333-3333-333333333333")),
        TaskItem.Create(
            "Publish first release notes",
            "Summarise the demo scenario for stakeholders.",
            TaskStatus.Completed,
            utcNow.AddDays(-4),
            utcNow.AddDays(-12),
            Guid.Parse("44444444-4444-4444-4444-444444444444"))
    ];
}
