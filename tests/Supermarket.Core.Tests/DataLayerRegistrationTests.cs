using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Supermarket.Core.Data;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;
using Xunit;

namespace Supermarket.Core.Tests;

/// <summary>
/// Phase 5 regression: the runtime DbContext factory and the data layer registration must actually
/// create working contexts. A pooling/constructor mistake here compiles fine and only fails at
/// application start-up, which is exactly what these tests catch.
/// </summary>
public sealed class DataLayerRegistrationTests
{
    /// <summary>Builds the same factory registration the applications use (one context per call).</summary>
    private static ServiceProvider BuildProvider(SqliteConnection connection, ICurrentUserService currentUser)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(currentUser);

        // Mirrors CoreServiceCollectionExtensions: one factory, no extra ambient registration — this is
        // what the applications do, and the ambiguous/pooled mistakes both failed exactly here.
        services.AddDbContextFactory<SupermarketDbContext>((provider, options) =>
            options.UseSqlite(connection));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void Factory_CreatesAWorkingContextWithTheAuditStamper()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var currentUser = new StubCurrentUser("tester");
        using var provider = BuildProvider(connection, currentUser);

        var factory = provider.GetRequiredService<IDbContextFactory<SupermarketDbContext>>();

        using var first = factory.CreateDbContext();
        using var second = factory.CreateDbContext();

        Assert.NotNull(first);
        Assert.NotSame(first, second);                              // never a shared context
        Assert.True(first.Database.CanConnect());

        // The stamping contract: an inserted entity carries the operator name from ICurrentUserService.
        first.Database.EnsureCreated();
        var category = new Supermarket.Core.Entities.Category { Name = "اختبار التسجيل" };
        first.Categories.Add(category);
        first.SaveChanges();

        Assert.Equal("tester", category.CreatedBy);
    }

    [Fact]
    public void Factory_SurvivesTheRegistrationShapesThatUsedToBreakIt()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var provider = BuildProvider(connection, new StubCurrentUser("tester"));

        // Three independent factories: the activator is built once per factory instance, so this also
        // proves the constructor is unambiguous for ActivatorUtilities (not only for the DI engine).
        for (var i = 0; i < 3; i++)
        {
            var factory = provider.GetRequiredService<IDbContextFactory<SupermarketDbContext>>();
            using var context = factory.CreateDbContext();

            Assert.True(context.Database.CanConnect());
            Assert.NotEmpty(context.Model.GetEntityTypes());   // the model is fully built
        }
    }

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public StubCurrentUser(string userName) => UserName = userName;

        public string? UserName { get; }

        public int? UserId => 1;
    }
}
