using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Supermarket.Core.Common;

namespace Supermarket.Core.Data.Configurations;

/// <summary>
/// Reusable base for every entity configuration. Centralizes:
/// table naming, primary key, audit defaults, soft-delete query filter and
/// Unicode (Arabic safe) string columns.
/// </summary>
/// <typeparam name="TEntity">Entity type being configured.</typeparam>
public abstract class EntityConfigurationBase<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
    /// <summary>Physical table name. Configured by the derived class.</summary>
    protected abstract string TableName { get; }

    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.Property(x => x.CreatedBy).IsUnicode(true).HasMaxLength(120);
        builder.Property(x => x.UpdatedBy).IsUnicode(true).HasMaxLength(120);

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        // Global filter: soft-deleted rows disappear from every query automatically.
        builder.HasQueryFilter(x => !x.IsDeleted);

        ConfigureEntity(builder);
    }

    /// <summary>Entity specific mapping.</summary>
    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);
}
