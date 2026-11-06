using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of <see cref="PurchaseOrder"/>.</summary>
public sealed class PurchaseOrderConfiguration : EntityConfigurationBase<PurchaseOrder>
{
    protected override string TableName => "PurchaseOrders";

    protected override void ConfigureEntity(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.Property(x => x.OrderNumber).IsRequired().IsUnicode(false).HasMaxLength(40);
        builder.Property(x => x.Notes).IsUnicode(true).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
        builder.Ignore(x => x.RemainingAmount);

        builder.HasOne(x => x.Supplier)
            .WithMany(x => x.PurchaseOrders)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.PurchaseOrder)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.OrderNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.OrderDateUtc);
    }
}

/// <summary>Mapping of <see cref="PurchaseOrderItem"/>.</summary>
public sealed class PurchaseOrderItemConfiguration : EntityConfigurationBase<PurchaseOrderItem>
{
    protected override string TableName => "PurchaseOrderItems";

    protected override void ConfigureEntity(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.Property(x => x.OrderedQuantity).HasPrecision(18, 3);
        builder.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Ignore(x => x.PendingQuantity);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.PurchaseOrderItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PurchaseOrderId);
    }
}
