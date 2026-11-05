using Azure.Core;
using Azure.Identity;
using DemoTaskManager.Infrastructure;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DemoTaskManager.ServiceBusWebJob;

/// <summary>
/// Azure App Service *continuous* WebJob hosting the Azure WebJobs SDK v3 runtime.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
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
            .ConfigureWebJobs(webJobs =>
            {
                webJobs.AddServiceBus(options =>
                {
                    options.MaxConcurrentCalls = 4;
                    options.AutoCompleteMessages = true;
                    options.MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5);
                });
            })
            .ConfigureServices((context, services) =>
            {
                services.AddPersistenceOnly(context.Configuration);
                services.AddApplicationInsightsTelemetryWorkerService();
                services.AddSingleton<TaskProcessingFunctions>();

                // Lets the ServiceBus extension resolve a TokenCredential when only
                // ServiceBusConnection__fullyQualifiedNamespace is configured.
                services.AddAzureClientsCore();
                services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
            });

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ServiceBusWebJob");

        try
        {
            logger.LogInformation("Service Bus WebJob starting");
            await host.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Service Bus WebJob terminated unexpectedly");
            return 1;
        }
    }
}
