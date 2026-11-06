using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of <see cref="Supplier"/>.</summary>
public sealed class SupplierConfiguration : EntityConfigurationBase<Supplier>
{
    protected override string TableName => "Suppliers";

    protected override void ConfigureEntity(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(x => x.Name).IsRequired().IsUnicode(true).HasMaxLength(200);
        builder.Property(x => x.ContactPerson).IsUnicode(true).HasMaxLength(150);
        builder.Property(x => x.PhoneNumber).IsUnicode(false).HasMaxLength(30);
        builder.Property(x => x.Email).IsUnicode(false).HasMaxLength(150);
        builder.Property(x => x.Address).IsUnicode(true).HasMaxLength(400);
        builder.Property(x => x.TaxNumber).IsUnicode(false).HasMaxLength(50);
        builder.Property(x => x.Notes).IsUnicode(true).HasMaxLength(1000);
        builder.Property(x => x.Balance).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.Name).HasDatabaseName("IX_Suppliers_Name");
        builder.HasIndex(x => x.PhoneNumber).HasDatabaseName("IX_Suppliers_Phone");
    }
}

/// <summary>Mapping of <see cref="Customer"/>.</summary>
public sealed class CustomerConfiguration : EntityConfigurationBase<Customer>
{
    protected override string TableName => "Customers";

    protected override void ConfigureEntity(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(x => x.Name).IsRequired().IsUnicode(true).HasMaxLength(200);
        builder.Property(x => x.PhoneNumber).IsUnicode(false).HasMaxLength(30);
        builder.Property(x => x.Address).IsUnicode(true).HasMaxLength(200);
        builder.Property(x => x.Balance).HasPrecision(18, 2).HasDefaultValue(0m);
        builder.Property(x => x.LoyaltyPoints).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL AND [IsDeleted] = 0");
    }
}
