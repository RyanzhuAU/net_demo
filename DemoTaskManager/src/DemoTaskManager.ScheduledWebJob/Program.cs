using DemoTaskManager.Application.Services;
using DemoTaskManager.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DemoTaskManager.ScheduledWebJob;

/// <summary>
/// Azure App Service *triggered* WebJob. It performs exactly one reminder batch and exits;
/// the schedule lives in settings.job, never in an in-process timer loop.
/// </summary>
public static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;

    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        using var host = BuildHost(args);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ScheduledWebJob");

        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();

            var result = await reminderService.RunOnceAsync(cancellation.Token).ConfigureAwait(false);

            logger.LogInformation(
                "Reminder run completed. Candidates={Candidates} Sent={Sent} SkippedAlreadyProcessed={Skipped}",
                result.Candidates,
                result.RemindersSent,
                result.SkippedAlreadyProcessed);

            return ExitSuccess;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            logger.LogWarning("Reminder run cancelled by host shutdown");
            return ExitFailure;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reminder run failed");
            return ExitFailure;
        }
        finally
        {
            // Give Application Insights a chance to flush before the process exits.
            await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static IHost BuildHost(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
                    options.UseUtcTimestamp = true;
                });
            })
            .ConfigureServices((context, services) =>
            {
                services.AddPersistenceOnly(context.Configuration);
                services.AddApplicationInsightsTelemetryWorkerService();
            })
            .Build();
}
