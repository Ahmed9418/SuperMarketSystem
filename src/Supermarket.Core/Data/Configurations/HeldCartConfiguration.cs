using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of <see cref="HeldCart"/>.</summary>
public sealed class HeldCartConfiguration : EntityConfigurationBase<HeldCart>
{
    protected override string TableName => "HeldCarts";

    protected override void ConfigureEntity(EntityTypeBuilder<HeldCart> builder)
    {
        builder.Property(x => x.Label).IsRequired().IsUnicode(true).HasMaxLength(120);
        builder.Property(x => x.TerminalId).IsRequired().IsUnicode(false).HasMaxLength(60).HasDefaultValue("POS-01");
                // JSON payload: 4000 nvarchar characters (8000 bytes) holds a realistic basket and still
        // fits inside a single SQL Server row.
        builder.Property(x => x.CartJson).IsRequired().IsUnicode(true).HasMaxLength(4000);
        builder.Property(x => x.LineCount).IsRequired();
        builder.Property(x => x.TotalQuantity).HasPrecision(18, 3);
        builder.Property(x => x.EstimatedTotal).HasPrecision(18, 2);

        // The resume list is always "my parked baskets, newest first".
        builder.HasIndex(x => new { x.UserId, x.IsDeleted });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
