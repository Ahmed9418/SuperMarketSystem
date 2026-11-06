using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Core.Data;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Tests;

/// <summary>
/// Test-only derivative of the production context.
/// SQLite has no native decimal support, so (and only here) every decimal column is mapped to
/// a double. The production SQL Server model keeps <c>decimal(18,4)</c> precision, which is why
/// this conversion lives in the test harness and never in <see cref="SupermarketDbContext"/>.
/// </summary>
public sealed class SqliteSupermarketDbContext : SupermarketDbContext
{
    public SqliteSupermarketDbContext(DbContextOptions<SupermarketDbContext> options, ICurrentUserService currentUser)
        : base(options, currentUser)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }
}

/// <summary>
/// Creates a throw-away database backed by an in-memory SQLite connection so the model,
/// the seed data and the services can be exercised on any CI agent.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Context = CreateContext();
        Context.Database.EnsureCreated();
    }

    /// <summary>Context used by the test that owns the harness.</summary>
    public SqliteSupermarketDbContext Context { get; }

    /// <summary>Creates an additional context sharing the same in-memory database.</summary>
    public SqliteSupermarketDbContext CreateContext()
    {
        // Built on the production context type so the constructor of the base class accepts it;
        // EF Core still derives the model from the runtime type, therefore the decimal
        // conversion below only affects this test harness.
        var options = new DbContextOptionsBuilder<SupermarketDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        return new SqliteSupermarketDbContext(options, new TestCurrentUser());
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserName => "unit-test";
    }
}

/// <summary>Fixed clock so the generated document numbers are deterministic.</summary>
public sealed class FixedClock : IDateTimeProvider
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; }

    public DateTime Now => UtcNow.ToLocalTime();

    public DateOnly Today => DateOnly.FromDateTime(Now);
}

/// <summary>Context factory used by the numbering service inside the tests.</summary>
public sealed class TestContextFactory : IDbContextFactory<SupermarketDbContext>
{
    private readonly SqliteTestDatabase _database;

    public TestContextFactory(SqliteTestDatabase database) => _database = database;

    public SupermarketDbContext CreateDbContext() => _database.CreateContext();

    public Task<SupermarketDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<SupermarketDbContext>(_database.CreateContext());
}

/// <summary>Helpers shared by the test classes.</summary>
public static class TestServices
{
    public static NullLogger<T> Logger<T>() => NullLogger<T>.Instance;
}
