using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Supermarket.Core.Data;

namespace Supermarket.Migrations;

/// <summary>
/// Design-time factory consumed by the EF Core CLI. The connection string below is only used to
/// generate / script the migration; both applications read their own from appsettings.json.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SupermarketDbContext>
{
    public SupermarketDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SupermarketDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=SupermarketDb;Trusted_Connection=False;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;",
                sql => sql.MigrationsAssembly("Supermarket.Core"));

        return new SupermarketDbContext(optionsBuilder.Options);
    }
}
