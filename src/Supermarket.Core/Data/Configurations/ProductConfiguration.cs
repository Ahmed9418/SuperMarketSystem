using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Common;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of the product aggregate: <see cref="Product"/>, <see cref="Category"/>, <see cref="Unit"/>.</summary>
public sealed class ProductConfiguration : EntityConfigurationBase<Product>
{
    protected override string TableName => "Products";

    protected override void ConfigureEntity(EntityTypeBuilder<Product> builder)
    {
        builder.Property(x => x.Name)
            .IsRequired()
            .IsUnicode(true) // Arabic product names must be stored as nvarchar
            .HasMaxLength(DomainConstants.ProductNameMaxLength);

        builder.Property(x => x.Description).IsUnicode(true).HasMaxLength(500);

        builder.Property(x => x.Barcode)
            .IsRequired()
            .IsUnicode(false)
            .HasMaxLength(DomainConstants.BarcodeMaxLength);

        builder.Property(x => x.AlternativeBarcode)
            .IsUnicode(false)
            .HasMaxLength(DomainConstants.BarcodeMaxLength);

        builder.Property(x => x.ShelfLocation).IsUnicode(true).HasMaxLength(120);
        builder.Property(x => x.ImagePath).HasMaxLength(500);

        builder.Property(x => x.CostPrice).HasPrecision(18, 4);
        builder.Property(x => x.SellingPrice).HasPrecision(18, 4);
        builder.Property(x => x.DiscountPrice).HasPrecision(18, 4);
        builder.Property(x => x.VatRate).HasPrecision(9, 4);

        builder.Property(x => x.StockQuantity).HasPrecision(18, 3);
        builder.Property(x => x.MinimumStockLevel).HasPrecision(18, 3);
        builder.Property(x => x.ReorderQuantity).HasPrecision(18, 3);

        builder.Property(x => x.IsWeighted).HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        // Computed properties are never persisted.
        builder.Ignore(x => x.IsLowStock);
        builder.Ignore(x => x.EffectivePrice);
        builder.Ignore(x => x.UnitMargin);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Supplier)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Unit)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // Barcodes are unique among the non-deleted rows only.
        builder.HasIndex(x => x.Barcode)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Products_Barcode_Unique");

        builder.HasIndex(x => x.Name).HasDatabaseName("IX_Products_Name");
        builder.HasIndex(x => x.CategoryId).HasDatabaseName("IX_Products_CategoryId");
        builder.HasIndex(x => new { x.IsActive, x.StockQuantity })
            .HasDatabaseName("IX_Products_Active_Stock");
    }
}

/// <summary>Mapping of <see cref="Category"/>.</summary>
public sealed class CategoryConfiguration : EntityConfigurationBase<Category>
{
    protected override string TableName => "Categories";

    protected override void ConfigureEntity(EntityTypeBuilder<Category> builder)
    {
        builder.Property(x => x.Name)
            .IsRequired()
            .IsUnicode(true)
            .HasMaxLength(120);

        builder.Property(x => x.Description).IsUnicode(true).HasMaxLength(500);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasOne(x => x.ParentCategory)
            .WithMany(x => x.SubCategories)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Name).HasDatabaseName("IX_Categories_Name");
    }
}

/// <summary>Mapping of <see cref="Unit"/>.</summary>
public sealed class UnitConfiguration : EntityConfigurationBase<Unit>
{
    protected override string TableName => "Units";

    protected override void ConfigureEntity(EntityTypeBuilder<Unit> builder)
    {
        builder.Property(x => x.Name)
            .IsRequired()
            .IsUnicode(true)
            .HasMaxLength(50);

        builder.Property(x => x.Symbol).IsUnicode(true).HasMaxLength(20);
        builder.Property(x => x.AllowFractions).HasDefaultValue(false);

        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
