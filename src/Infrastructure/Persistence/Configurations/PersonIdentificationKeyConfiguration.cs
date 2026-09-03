using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности PersonIdentificationKey.
/// </summary>
public class PersonIdentificationKeyConfiguration : IEntityTypeConfiguration<PersonIdentificationKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonIdentificationKey> builder)
    {
        builder.ToTable("person_identification_keys");

        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasColumnName("id");

        builder.Property(k => k.MasterId).HasColumnName("person_id").IsRequired();
        builder.Property(k => k.KeyType).HasColumnName("key_type").HasMaxLength(50).IsRequired();
        builder.Property(k => k.KeyValue).HasColumnName("key_value").HasMaxLength(255).IsRequired();
        builder.Property(k => k.NormalizationVersion).HasColumnName("normalization_version").HasDefaultValue(1).IsRequired();
        builder.Property(k => k.OrganizationUnitKey).HasColumnName("organization_unit_key").HasMaxLength(100);
        builder.Property(k => k.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(k => new { k.KeyType, k.KeyValue, k.MasterId })
            .IsUnique()
            .HasDatabaseName("ux_person_identification_keys_type_value_person");

        builder.HasIndex(k => k.MasterId).HasDatabaseName("ix_person_identification_keys_person_id");

        builder.HasOne<Person>()
            .WithMany(p => p.IdentificationKeys)
            .HasForeignKey(k => k.MasterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
