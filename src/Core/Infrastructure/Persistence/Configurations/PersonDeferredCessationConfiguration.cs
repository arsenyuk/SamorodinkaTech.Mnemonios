using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности PersonDeferredCessation.
/// </summary>
public class PersonDeferredCessationConfiguration : IEntityTypeConfiguration<PersonDeferredCessation>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonDeferredCessation> builder)
    {
        builder.ToTable("person_deferred_cessations");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.MasterId).HasColumnName("person_id").IsRequired();
        builder.Property(c => c.SourceSystemId).HasColumnName("source_system_id").HasMaxLength(100).IsRequired();
        builder.Property(c => c.ExternalPersonId).HasColumnName("external_person_id").HasMaxLength(255).IsRequired();
        builder.Property(c => c.ScheduledDeletionDate).HasColumnName("scheduled_deletion_date").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasMaxLength(20).IsRequired().HasDefaultValue("pending");
        builder.Property(c => c.OrganizationUnitKey).HasColumnName("organization_unit_key").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<Person>()
            .WithMany(p => p.DeferredCessations)
            .HasForeignKey(c => c.MasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.SourceSystemId, c.ExternalPersonId })
            .HasFilter("status = 'pending'")
            .IsUnique()
            .HasDatabaseName("ux_person_deferred_cessations_system_extid");

        builder.HasIndex(c => c.ScheduledDeletionDate)
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ix_person_deferred_cessations_scheduled_date");

        builder.HasIndex(c => c.MasterId)
            .HasDatabaseName("ix_person_deferred_cessations_person_id");
    }
}
