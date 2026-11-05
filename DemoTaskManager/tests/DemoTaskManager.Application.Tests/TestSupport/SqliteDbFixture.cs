using DemoTaskManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DemoTaskManager.Application.Tests.TestSupport;

/// <summary>
/// Relational SQLite in-memory database. The EF Core InMemory provider is deliberately
/// avoided because these tests assert real query translation and paging behaviour.
/// </summary>
public sealed class SqliteDbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteDbFixture(SqliteConnection connection) => _connection = connection;

    public static async Task<SqliteDbFixture> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var fixture = new SqliteDbFixture(connection);
        await using var context = fixture.CreateContext();
        await context.Database.EnsureCreatedAsync();

        return fixture;
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
