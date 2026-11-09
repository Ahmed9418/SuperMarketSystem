using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Core.Data;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;
using Supermarket.Core.Services;

namespace Supermarket.Core.DependencyInjection;

/// <summary>
/// Composition root shared by both WPF applications. Each executable calls
/// <see cref="AddSupermarketCore(IServiceCollection, IConfiguration, string)"/> once from its
/// <c>App.xaml.cs</c> and receives the whole
/// object graph: DbContext factory, services, password hasher and the current-user abstraction.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    // (helper class at the bottom of this file)
    /// <summary>
    /// Registers the data layer and every shared service.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="configuration">Application configuration (appsettings.json).</param>
    /// <param name="applicationName">"Admin" or "POS" - stamped on the SQL session for tracing.</param>
    public static IServiceCollection AddSupermarketCore(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName)
    {
        return AddSupermarketCore(services, configuration.GetSupermarketConnectionString(applicationName), applicationName);
    }

    /// <summary>Overload used by tests and by applications that build the connection string themselves.</summary>
    public static IServiceCollection AddSupermarketCore(
        this IServiceCollection services,
        string connectionString,
        string applicationName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'SupermarketDb' is missing from appsettings.json.");
        }

        // Exposed for the error messages: when the SQL instance name is wrong, the operator must be able
        // to see WHICH server the application tried before changing anything.
        SqlServerDiagnostics.LastConnectionString = connectionString;

        // Tag the SQL sessions with the executable name so DBAs can tell the POS traffic
        // apart from the Admin traffic inside SQL Server Profiler / Extended Events.
        if (!connectionString.Contains("Application Name", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = $"{connectionString.TrimEnd(';')};Application Name=Supermarket.{applicationName};";
        }

        // NOTE: a *pooled* factory (AddPooledDbContextFactory) is deliberately NOT used: pooling requires a
        // context with a single (options-only) constructor, while SupermarketDbContext also takes the
        // ICurrentUserService used for audit stamping. The factory below creates a real instance per call,
        // which is what a long-running desktop session needs anyway (one context per operation).
        //
        // lifetime: ServiceLifetime.Transient is REQUIRED here. The default is Scoped, and every consumer of
        // this factory (services, view models, windows) is a singleton or transient in a desktop app with no
        // scope, so a scoped factory would make the whole graph unbuildable
        // ("Cannot consume scoped service 'IDbContextFactory<SupermarketDbContext>' from singleton …").
        services.AddDbContextFactory<SupermarketDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.UseCompatibilityLevel(120);
                sql.MigrationsAssembly(typeof(SupermarketDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sql.CommandTimeout(60);
            });

            options.EnableDetailedErrors();
        }, ServiceLifetime.Transient);

        // ------------------------------------------------------- Infrastructure
        // Logging is registered here as well: both shells already add it through the generic host, and
        // doing it in the shared library makes AddSupermarketCore self-sufficient (tests, tooling and the
        // new ValidateOnBuild start-up check all construct the same graph without a host).
        services.AddLogging();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // The WPF shells update this holder right after a successful sign-in so the audit
        // columns of every entity are stamped with the operator name.
        services.AddSingleton<MutableCurrentUserService>();
        services.AddSingleton<ICurrentUserService>(provider => provider.GetRequiredService<MutableCurrentUserService>());

        // ------------------------------------------------------------- Services
        services.AddSingleton<IStoreSettingsService, StoreSettingsService>(); // factory based + cached
        services.AddTransient<INumberSequenceService, NumberSequenceService>();
        services.AddTransient<IStockService, StockService>();
        services.AddTransient<IProductService, ProductService>();
        services.AddTransient<ICatalogService, CatalogService>();
        services.AddTransient<ISupplierService, SupplierService>();
        services.AddTransient<IInventoryService, InventoryService>();
        services.AddTransient<ICategoryService, CategoryService>();
        services.AddTransient<IUnitService, UnitService>();
        services.AddTransient<IDashboardService, DashboardService>();
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<ISalesService, SalesService>();
        services.AddTransient<IShiftService, ShiftService>();
        services.AddSingleton<IReceiptFormatter, ReceiptFormatter>();
        // Transient, not scoped: the POS resolves SalesViewModel from the root provider (no scope exists
        // in a desktop app), so a scoped service would trip ValidateScopes exactly like the DbContext did.
        services.AddTransient<IHeldCartService, HeldCartService>();
        services.AddTransient<IReportService, ReportService>();
        services.AddTransient<IUserAdminService, UserAdminService>();

        return services;
    }

    /// <summary>Reads and validates the shared connection string.</summary>
    public static string GetSupermarketConnectionString(this IConfiguration configuration, string? applicationName = null)
    {
        var connectionString = configuration.GetConnectionString("SupermarketDb");

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                "Connection string 'SupermarketDb' is missing from appsettings.json. " +
                "Copy the template from docs/appsettings.template.json and set your SQL Server instance.")
            : connectionString;
    }
}

/// <summary>
/// Singleton holder for the signed-in operator. The WPF shells set <c>UserName</c>
/// after login and clear it on sign-out; the DbContext reads it when stamping audit columns.
/// </summary>
/// <summary>
/// Diagnostics for the data layer: remembers the connection string the application actually built, and
/// turns a SQL Server exception into an Arabic message that names the server it tried to reach.
/// </summary>
public static class SqlServerDiagnostics
{
    /// <summary>Connection string built by <c>AddSupermarketCore</c> (includes the Application Name).</summary>
    public static string? LastConnectionString { get; internal set; }

    /// <summary>Server part of the connection string, e.g. <c>.\SQLEXPRESS</c>.</summary>
    public static string ServerName
    {
        get
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(LastConnectionString ?? string.Empty);
            return builder.DataSource;
        }
    }

    /// <summary>Database part of the connection string, e.g. <c>SupermarketDb</c>.</summary>
    public static string DatabaseName
    {
        get
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(LastConnectionString ?? string.Empty);
            return builder.InitialCatalog;
        }
    }

    /// <summary>
    /// Arabic explanation of a connection failure, with the server and database that were attempted and
    /// the two usual causes (the SQL Server service / the TCP protocol are not enabled).
    /// </summary>
    public static string Describe(Exception exception)
    {
        var message = exception.GetBaseException().Message;
        var server = string.IsNullOrWhiteSpace(ServerName) ? "(غير محدد)" : ServerName;

        return
            $"تعذر الاتصال بقاعدة البيانات.\n\n" +
            $"اسم السيرفر المُستخدم: {server}\n" +
            $"اسم قاعدة البيانات: {(string.IsNullOrWhiteSpace(DatabaseName) ? "(غير محدد)" : DatabaseName)}\n\n" +
            $"تأكد من:\n" +
            $"1) تشغيل خدمة SQL Server (Services → SQL Server (…)).\n" +
            $"2) تفعيل TCP/IP من SQL Server Configuration Manager.\n" +
            $"3) أن الاسم في appsettings.json مطابق للاسم الظاهر عند تسجيل الدخول في SSMS.\n\n" +
            $"رسالة الخطأ الأصلية:\n{message}";
    }
}

/// <summary>Mutable holder of the signed-in operator.</summary>
public sealed class MutableCurrentUserService : ICurrentUserService
{
    public string? UserName { get; set; }

    /// <summary>Id of the signed-in user, consumed by the POS and admin view models.</summary>
    public int? UserId { get; set; }

    /// <summary>Role of the signed-in user.</summary>
    public Common.UserRole? Role { get; set; }

    /// <summary>Resets the holder when the operator signs out.</summary>
    public void Clear()
    {
        UserName = null;
        UserId = null;
        Role = null;
    }
}
