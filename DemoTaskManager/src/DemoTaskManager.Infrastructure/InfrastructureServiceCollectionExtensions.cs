using Azure.Identity;
using Azure.Messaging.ServiceBus;
using DemoTaskManager.Application;
using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Infrastructure.Messaging;
using DemoTaskManager.Infrastructure.Persistence;
using DemoTaskManager.Infrastructure.Persistence.Repositories;
using DemoTaskManager.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DemoTaskManager.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public const string DefaultConnectionName = "DefaultConnection";

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddApplicationServices(configuration);

        var connectionString = configuration.GetConnectionString(DefaultConnectionName)
                               ?? throw new InvalidOperationException(
                                   $"Connection string '{DefaultConnectionName}' is not configured. " +
                                   "Set ConnectionStrings__DefaultConnection as an environment variable or app setting.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            }));

        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddServiceBus(configuration);

        return services;
    }

    /// <summary>
    /// Registers only the persistence stack. Used by the Service Bus WebJob, which gets its
    /// queue plumbing from the WebJobs SDK rather than from a publisher.
    /// </summary>
    public static IServiceCollection AddPersistenceOnly(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddApplicationServices(configuration);

        var connectionString = configuration.GetConnectionString(DefaultConnectionName)
                               ?? throw new InvalidOperationException(
                                   $"Connection string '{DefaultConnectionName}' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ITaskMessagePublisher, NullTaskMessagePublisher>();

        return services;
    }

    private static IServiceCollection AddServiceBus(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ServiceBusOptions.SectionName);
        services.AddOptions<ServiceBusOptions>().Bind(section);

        var options = section.Get<ServiceBusOptions>() ?? new ServiceBusOptions();

        // App Service supplies ServiceBusConnection__fullyQualifiedNamespace for managed identity.
        options.FullyQualifiedNamespace ??= configuration["ServiceBusConnection:fullyQualifiedNamespace"];

        var canConnect = !string.IsNullOrWhiteSpace(options.ConnectionString)
                         || !string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace);

        if (!options.Enabled || !canConnect)
        {
            services.AddSingleton<ITaskMessagePublisher, NullTaskMessagePublisher>();
            return services;
        }

        services.AddSingleton(_ =>
        {
            var clientOptions = new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpWebSockets
            };

            return string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new ServiceBusClient(options.FullyQualifiedNamespace, new DefaultAzureCredential(), clientOptions)
                : new ServiceBusClient(options.ConnectionString, clientOptions);
        });

        services.AddSingleton<ITaskMessagePublisher, ServiceBusTaskMessagePublisher>();

        return services;
    }
}
