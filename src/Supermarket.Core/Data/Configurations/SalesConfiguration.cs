using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Common;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of <see cref="Invoice"/>.</summary>
public sealed class InvoiceConfiguration : EntityConfigurationBase<Invoice>
{
    protected override string TableName => "Invoices";

    protected override void ConfigureEntity(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(x => x.InvoiceNumber).IsRequired().IsUnicode(false).HasMaxLength(40);
        builder.Property(x => x.ReferenceNumber).IsUnicode(false).HasMaxLength(40);
        builder.Property(x => x.Notes).IsUnicode(true).HasMaxLength(1000);

        builder.Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountPercentage).HasPrecision(9, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
        builder.Property(x => x.ChangeAmount).HasPrecision(18, 2);
        builder.Property(x => x.RemainingAmount).HasPrecision(18, 2);

        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.PaymentMethod).HasConversion<int>();
        builder.Property(x => x.IsWalkIn).HasDefaultValue(true);

        builder.Ignore(x => x.NetTotal);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Payments)
            .WithOne(x => x.Invoice)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.InvoiceNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.InvoiceDateUtc).HasDatabaseName("IX_Invoices_Date");
        builder.HasIndex(x => new { x.ShiftId, x.Status }).HasDatabaseName("IX_Invoices_Shift_Status");
    }
}

/// <summary>Mapping of <see cref="InvoiceItem"/>.</summary>
public sealed class InvoiceItemConfiguration : EntityConfigurationBase<InvoiceItem>
{
    protected override string TableName => "InvoiceItems";

    protected override void ConfigureEntity(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.Property(x => x.ProductName).IsRequired().IsUnicode(true).HasMaxLength(DomainConstants.ProductNameMaxLength);
        builder.Property(x => x.Barcode).IsUnicode(false).HasMaxLength(DomainConstants.BarcodeMaxLength);

        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.VatRate).HasPrecision(9, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.Property(x => x.LineTotalWithTax).HasPrecision(18, 2);

        builder.Ignore(x => x.LineProfit);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.InvoiceItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.InvoiceId).HasDatabaseName("IX_InvoiceItems_InvoiceId");
        builder.HasIndex(x => x.ProductId).HasDatabaseName("IX_InvoiceItems_ProductId");
    }
}

/// <summary>Mapping of <see cref="Payment"/>.</summary>
public sealed class PaymentConfiguration : EntityConfigurationBase<Payment>
{
    protected override string TableName => "Payments";

    protected override void ConfigureEntity(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Method).HasConversion<int>();
        builder.Property(x => x.TransactionReference).IsUnicode(false).HasMaxLength(60);

        builder.HasIndex(x => new { x.InvoiceId, x.Method });
    }
}
