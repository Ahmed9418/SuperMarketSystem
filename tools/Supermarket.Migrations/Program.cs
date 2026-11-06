using Microsoft.EntityFrameworkCore;
using Supermarket.Core.Data;

namespace Supermarket.Migrations;

/// <summary>
/// Small console host so the migrations can also be applied without the EF CLI:
/// <c>dotnet run --project tools/Supermarket.Migrations -- "Server=...;Database=SupermarketDb;..."</c>
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0)
        {
            Console.WriteLine("Supermarket design-time host");
            Console.WriteLine("DbContext     : " + typeof(SupermarketDbContext).FullName);
            Console.WriteLine("Generate      : dotnet ef migrations add InitialCreate --project src/Supermarket.Core --startup-project tools/Supermarket.Migrations");
            Console.WriteLine("Apply scripts : dotnet run --project tools/Supermarket.Migrations -- \"<connection-string>\"");
            return 0;
        }

        var connectionString = args[0];
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<SupermarketDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly("Supermarket.Core"))
            .Options;

        await using var db = new SupermarketDbContext(options);
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count == 0)
        {
            Console.WriteLine("Database is up to date.");
            return 0;
        }

        Console.WriteLine($"Applying {pending.Count} migration(s): {string.Join(", ", pending)}");
        await db.Database.MigrateAsync();
        Console.WriteLine("Done.");
        return 0;
    }
}
