using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of the stock ledger.</summary>
public sealed class StockMovementConfiguration : EntityConfigurationBase<StockMovement>
{
    protected override string TableName => "StockMovements";

    protected override void ConfigureEntity(EntityTypeBuilder<StockMovement> builder)
    {
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.ReferenceNumber).IsUnicode(false).HasMaxLength(60);
        builder.Property(x => x.Reason).IsUnicode(true).HasMaxLength(400);
        builder.Property(x => x.PerformedBy).IsUnicode(true).HasMaxLength(120);

        // Stored as int so reports can be indexed and filtered with a single scan.
        builder.Property(x => x.MovementType).HasConversion<int>();

        builder.HasOne(x => x.Product)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ProductId, x.CreatedAtUtc })
            .HasDatabaseName("IX_StockMovements_Product_Date");

        builder.HasIndex(x => x.MovementType).HasDatabaseName("IX_StockMovements_Type");
    }
}

/// <summary>Mapping of the price change audit trail.</summary>
public sealed class PriceHistoryConfiguration : EntityConfigurationBase<PriceHistory>
{
    protected override string TableName => "PriceHistories";

    protected override void ConfigureEntity(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.Property(x => x.OldCostPrice).HasPrecision(18, 4);
        builder.Property(x => x.NewCostPrice).HasPrecision(18, 4);
        builder.Property(x => x.OldSellingPrice).HasPrecision(18, 4);
        builder.Property(x => x.NewSellingPrice).HasPrecision(18, 4);
        builder.Property(x => x.Reason).IsUnicode(true).HasMaxLength(400);
        builder.Property(x => x.ChangedBy).IsUnicode(true).HasMaxLength(120);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.PriceHistories)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProductId, x.CreatedAtUtc })
            .HasDatabaseName("IX_PriceHistories_Product_Date");
    }
}

/// <summary>Mapping of stock count sessions.</summary>
public sealed class StockCountConfiguration : EntityConfigurationBase<StockCount>
{
    protected override string TableName => "StockCounts";

    protected override void ConfigureEntity(EntityTypeBuilder<StockCount> builder)
    {
        builder.Property(x => x.ReferenceNumber).IsRequired().IsUnicode(false).HasMaxLength(40);
        builder.Property(x => x.CountedBy).IsUnicode(true).HasMaxLength(120);
        builder.Property(x => x.Notes).IsUnicode(true).HasMaxLength(1000);
        builder.HasIndex(x => x.ReferenceNumber).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasMany(x => x.Items)
            .WithOne(x => x.StockCount)
            .HasForeignKey(x => x.StockCountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Mapping of stock count lines.</summary>
public sealed class StockCountItemConfiguration : EntityConfigurationBase<StockCountItem>
{
    protected override string TableName => "StockCountItems";

    protected override void ConfigureEntity(EntityTypeBuilder<StockCountItem> builder)
    {
        builder.Property(x => x.SystemQuantity).HasPrecision(18, 3);
        builder.Property(x => x.CountedQuantity).HasPrecision(18, 3);
        builder.Ignore(x => x.Difference);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StockCountId, x.ProductId }).IsUnique();
    }
}
