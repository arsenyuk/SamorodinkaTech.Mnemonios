using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности PersonExternalId.
/// </summary>
public class PersonExternalIdConfiguration : IEntityTypeConfiguration<PersonExternalId>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonExternalId> builder)
    {
        builder.ToTable("person_external_ids");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.MasterId).HasColumnName("person_id").IsRequired();
        builder.Property(e => e.ExtPersonId).HasColumnName("ext_person_id");
        builder.Property(e => e.SourceSystemId).HasColumnName("source_system_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExternalPersonId).HasColumnName("external_person_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.ExternalPersonType).HasColumnName("external_person_type").HasMaxLength(255);
        builder.Property(e => e.OrganizationUnitKey).HasColumnName("organization_unit_key").HasMaxLength(100);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.SourceSystemId, e.ExternalPersonId })
            .IsUnique()
            .HasDatabaseName("ux_person_external_ids_system_extid");

        builder.HasIndex(e => e.MasterId).HasDatabaseName("ix_person_external_ids_person_id");
        builder.HasIndex(e => e.SourceSystemId).HasDatabaseName("ix_person_external_ids_source_system_id");

        builder.HasOne<Person>()
            .WithMany(p => p.ExternalIds)
            .HasForeignKey(e => e.MasterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
