using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Entities;

namespace Supermarket.Core.Data.Configurations;

/// <summary>Mapping of <see cref="Shift"/>.</summary>
public sealed class ShiftConfiguration : EntityConfigurationBase<Shift>
{
    protected override string TableName => "Shifts";

    protected override void ConfigureEntity(EntityTypeBuilder<Shift> builder)
    {
        builder.Property(x => x.ShiftNumber).IsRequired().IsUnicode(false).HasMaxLength(40);
        builder.Property(x => x.TerminalId).IsRequired().IsUnicode(false).HasMaxLength(60).HasDefaultValue("POS-01");
        builder.Property(x => x.Notes).IsUnicode(true).HasMaxLength(1000);
        builder.Property(x => x.ClosedBy).IsUnicode(true).HasMaxLength(120);

        builder.Property(x => x.Status).HasConversion<int>();

        builder.Property(x => x.OpeningCash).HasPrecision(18, 2);
        builder.Property(x => x.ClosingCash).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedCash).HasPrecision(18, 2);
        builder.Property(x => x.CashDifference).HasPrecision(18, 2);
        builder.Property(x => x.TotalCashSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalCardSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalWalletSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalCreditSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2);
        builder.Property(x => x.TotalTax).HasPrecision(18, 2);
        builder.Property(x => x.TotalRefunds).HasPrecision(18, 2);

        builder.Ignore(x => x.Duration);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Shifts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ShiftNumber).IsUnique().HasFilter("[IsDeleted] = 0");

        // A cashier can only have one open shift per terminal at any time.
        builder.HasIndex(x => new { x.UserId, x.TerminalId, x.Status })
            .HasDatabaseName("IX_Shifts_User_Terminal_Status");
    }
}

/// <summary>Mapping of <see cref="ShiftReport"/>.</summary>
public sealed class ShiftReportConfiguration : EntityConfigurationBase<ShiftReport>
{
    protected override string TableName => "ShiftReports";

    protected override void ConfigureEntity(EntityTypeBuilder<ShiftReport> builder)
    {
        builder.Property(x => x.ReportType).HasConversion<int>();
        builder.Property(x => x.GeneratedBy).IsUnicode(true).HasMaxLength(120);
        builder.Property(x => x.PrintedContent).IsUnicode(true);

        builder.Property(x => x.TotalSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2);
        builder.Property(x => x.TotalTax).HasPrecision(18, 2);
        builder.Property(x => x.TotalCashSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalCardSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalWalletSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalCreditSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalRefunds).HasPrecision(18, 2);
        builder.Property(x => x.OpeningCash).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedCash).HasPrecision(18, 2);
        builder.Property(x => x.ClosingCash).HasPrecision(18, 2);
        builder.Property(x => x.CashDifference).HasPrecision(18, 2);

        builder.HasOne(x => x.Shift)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ShiftId, x.ReportType });
    }
}
