using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DemoTaskManager.Application.Services;

public sealed record ReminderRunResult(int Candidates, int RemindersSent, int SkippedAlreadyProcessed);

public interface IReminderService
{
    Task<ReminderRunResult> RunOnceAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One batch of overdue-reminder work. Concurrency strategy: each candidate is claimed
/// with a conditional UPDATE (<see cref="ITaskRepository.TryClaimReminderAsync"/>) before
/// the reminder is dispatched, so two overlapping executions can never both win a task.
/// </summary>
public sealed class ReminderService(
    ITaskRepository repository,
    IDateTimeProvider clock,
    IOptions<ReminderOptions> options,
    ILogger<ReminderService> logger) : IReminderService
{
    private readonly ReminderOptions _options = options.Value;

    public async Task<ReminderRunResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var threshold = now.AddMinutes(-_options.GraceMinutes);

        var candidates = await repository
            .GetOverdueUnremindedAsync(threshold, _options.BatchSize, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Reminder sweep found {CandidateCount} overdue task(s) at {UtcNow:o}",
            candidates.Count,
            now);

        var sent = 0;
        var skipped = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var claimed = await repository
                .TryClaimReminderAsync(candidate.Id, now, cancellationToken)
                .ConfigureAwait(false);

            if (!claimed)
            {
                skipped++;
                logger.LogDebug("Reminder for task {TaskId} was already claimed elsewhere", candidate.Id);
                continue;
            }

            sent++;
            logger.LogInformation(
                "Reminder processed for task {TaskId} due at {DueDateUtc:o}",
                candidate.Id,
                candidate.DueDateUtc);
        }

        return new ReminderRunResult(candidates.Count, sent, skipped);
    }
}
