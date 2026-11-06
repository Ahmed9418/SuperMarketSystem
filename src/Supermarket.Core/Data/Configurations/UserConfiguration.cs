using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of <see cref="User"/>.</summary>
public sealed class UserConfiguration : EntityConfigurationBase<User>
{
    protected override string TableName => "Users";

    protected override void ConfigureEntity(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.UserName).IsRequired().IsUnicode(false).HasMaxLength(80);
        builder.Property(x => x.FullName).IsRequired().IsUnicode(true).HasMaxLength(200);
        builder.Property(x => x.PasswordHash).IsRequired().IsUnicode(false).HasMaxLength(500);
        builder.Property(x => x.PinHash).IsUnicode(false).HasMaxLength(200);
        builder.Property(x => x.PhoneNumber).IsUnicode(false).HasMaxLength(30);
        builder.Property(x => x.Role).HasConversion<int>();
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.UserName).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

/// <summary>Mapping of <see cref="UserPermission"/>.</summary>
public sealed class UserPermissionConfiguration : EntityConfigurationBase<UserPermission>
{
    protected override string TableName => "UserPermissions";

    protected override void ConfigureEntity(EntityTypeBuilder<UserPermission> builder)
    {
        builder.Property(x => x.PermissionKey).IsRequired().IsUnicode(false).HasMaxLength(80);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.PermissionKey }).IsUnique();
    }
}

/// <summary>Mapping of the single-row <see cref="StoreSetting"/> table.</summary>
public sealed class StoreSettingConfiguration : EntityConfigurationBase<StoreSetting>
{
    protected override string TableName => "StoreSettings";

    protected override void ConfigureEntity(EntityTypeBuilder<StoreSetting> builder)
    {
        builder.Property(x => x.StoreName).IsRequired().IsUnicode(true).HasMaxLength(200);
        builder.Property(x => x.Address).IsUnicode(true).HasMaxLength(300);
        builder.Property(x => x.PhoneNumber).IsUnicode(false).HasMaxLength(30);
        builder.Property(x => x.TaxNumber).IsUnicode(false).HasMaxLength(50);
        builder.Property(x => x.ReceiptFooterMessage).IsUnicode(true).HasMaxLength(400);
        builder.Property(x => x.ReceiptPrinterName).IsUnicode(true).HasMaxLength(200);
        builder.Property(x => x.ReceiptLineWidth).HasDefaultValue(42);
        builder.Property(x => x.DefaultVatRate).HasPrecision(9, 4);
        builder.Property(x => x.DecimalPlaces).HasDefaultValue(2);
        builder.Property(x => x.PricesIncludeVat).HasDefaultValue(true);
        builder.Property(x => x.AutoGenerateBarcode).HasDefaultValue(true);
        builder.Property(x => x.InvoicePaperSize).IsRequired().IsUnicode(false).HasMaxLength(20).HasDefaultValue("80mm");
        builder.Property(x => x.LogoPath).IsUnicode(true).HasMaxLength(500);
    }
}
