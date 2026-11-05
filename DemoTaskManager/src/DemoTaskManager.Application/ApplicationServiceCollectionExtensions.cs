using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Application.Common.Validation;
using DemoTaskManager.Application.Options;
using DemoTaskManager.Application.Services;
using DemoTaskManager.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DemoTaskManager.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddValidatorsFromAssemblyContaining<CreateTaskItemRequestValidator>(ServiceLifetime.Scoped);
        services.AddScoped<IRequestValidator, RequestValidator>();

        services.AddOptions<ReminderOptions>()
            .Bind(configuration.GetSection(ReminderOptions.SectionName));

        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<ITaskMessageProcessor, TaskMessageProcessor>();

        return services;
    }
}
