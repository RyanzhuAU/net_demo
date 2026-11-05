using DemoTaskManager.Application.Abstractions;
using DemoTaskManager.Contracts.Messaging;
using DemoTaskManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DemoTaskManager.Api.Tests;

/// <summary>
/// Boots the real API pipeline but swaps SQL Server for a relational SQLite in-memory
/// database and captures published Service Bus messages instead of sending them.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public RecordingTaskMessagePublisher Publisher { get; } = new();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Staging);

        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Server=placeholder;Database=placeholder;Trusted_Connection=True");
        builder.UseSetting("ServiceBus:Enabled", "false");
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<ITaskMessagePublisher>();
            services.AddSingleton<ITaskMessagePublisher>(Publisher);
        });
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
}

public sealed class RecordingTaskMessagePublisher : ITaskMessagePublisher
{
    private readonly List<ServiceBusTaskMessageDto> _messages = [];

    public IReadOnlyList<ServiceBusTaskMessageDto> Messages => _messages;

    public Task PublishAsync(ServiceBusTaskMessageDto message, CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}
