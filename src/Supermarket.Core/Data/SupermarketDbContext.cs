using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Supermarket.Core.Common;
using Supermarket.Core.Data.Seed;
using Supermarket.Core.Entities;
using Supermarket.Core.Interfaces;

namespace Supermarket.Core.Data;

/// <summary>
/// Single EF Core context shared by the Admin and the POS application.
/// Both executables point at the same SQL Server database, therefore all model
/// configuration lives in this assembly (Fluent API, one file per aggregate).
/// </summary>
public class SupermarketDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUser;

    /// <summary>
    /// Constructor used by the runtime factory (and by the design-time tools). The audit stamper is
    /// optional: the migration host and the tests create the context without one. There is deliberately
    /// only ONE public constructor because <c>AddDbContextFactory</c> refuses to build an activator for a
    /// type with more than one.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public SupermarketDbContext(DbContextOptions<SupermarketDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // ---------------------------------------------------------------- Catalog
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();

    // -------------------------------------------------------------- Inventory
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<StockCountItem> StockCountItems => Set<StockCountItem>();

    // ----------------------------------------------------------------- Sales
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftReport> ShiftReports => Set<ShiftReport>();
    public DbSet<HeldCart> HeldCarts => Set<HeldCart>();

    // ------------------------------------------------------------ Purchasing
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    // ------------------------------------------------------ Security & store
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Every IEntityTypeConfiguration<> in this assembly is applied automatically.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupermarketDbContext).Assembly);

        // Deterministic seed data (units, categories, suppliers, demo products, users,
        // store settings and one open cashier shift) is part of the model itself, so it
        // ships inside the generated migration and survives `dotnet ef database update`.
        SeedData.Apply(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Safety net: any decimal that was not explicitly configured still gets a
        // deterministic precision so EF Core never silently truncates money values.
        configurationBuilder.Properties<decimal>().HavePrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);
        configurationBuilder.Properties<string>().HaveMaxLength(400);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps <see cref="BaseEntity.CreatedAtUtc"/>, <see cref="BaseEntity.UpdatedAtUtc"/> and the
    /// operator columns on every tracked change. Soft-deleted rows are converted into updates
    /// so that the deleted flag is persisted instead of a physical delete.
    /// </summary>
    private void ApplyAuditInformation()
    {
        var userName = _currentUser?.UserName ?? "system";
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = utcNow;
                    entry.Entity.CreatedBy ??= userName;
                    entry.Entity.IsDeleted = false;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = utcNow;
                    entry.Entity.UpdatedBy = userName;
                    entry.Property(x => x.CreatedAtUtc).IsModified = false;
                    entry.Property(x => x.CreatedBy).IsModified = false;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAtUtc = utcNow;
                    entry.Entity.UpdatedBy = userName;
                    break;
            }
        }
    }
}
