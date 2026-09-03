using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности ExtPersonDeferredCessation.
/// </summary>
public class ExtPersonDeferredCessationConfiguration : IEntityTypeConfiguration<ExtPersonDeferredCessation>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExtPersonDeferredCessation> builder)
    {
        builder.ToTable("ext_person_deferred_cessations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.PersonId).HasColumnName("person_id");
        builder.Property(e => e.SourceSystemId).HasColumnName("source_system_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExternalPersonId).HasColumnName("external_person_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.ScheduledDeletionDate).HasColumnName("scheduled_deletion_date").IsRequired();
        builder.Property(e => e.OrganizationUnitKey).HasColumnName("organization_unit_key").HasMaxLength(100).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(e => e.PersonId).HasDatabaseName("ix_ext_person_deferred_cessations_person_id");
        builder.HasIndex(e => e.SourceSystemId).HasDatabaseName("ix_ext_person_deferred_cessations_source_system_id");

        builder.HasOne<ExtPerson>()
            .WithMany()
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
