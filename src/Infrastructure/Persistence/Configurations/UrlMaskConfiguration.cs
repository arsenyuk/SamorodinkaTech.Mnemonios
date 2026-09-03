using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности UrlMask.
/// </summary>
public class UrlMaskConfiguration : IEntityTypeConfiguration<UrlMask>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UrlMask> builder)
    {
        builder.ToTable("url_masks");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.OrganizationUnitKey).HasColumnName("organization_unit_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.SourceSystemId).HasColumnName("source_system_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExternalPersonType).HasColumnName("external_person_type").HasMaxLength(255).IsRequired();
        builder.Property(e => e.UrlPattern).HasColumnName("url_pattern").HasMaxLength(500).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.OrganizationUnitKey, e.SourceSystemId, e.ExternalPersonType })
            .IsUnique()
            .HasDatabaseName("ux_url_masks_triad");
    }
}
