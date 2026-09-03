using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности ExtPerson.
/// </summary>
public class ExtPersonConfiguration : IEntityTypeConfiguration<ExtPerson>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExtPerson> builder)
    {
        builder.ToTable("ext_persons");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.MasterId).HasColumnName("person_id");
        builder.Property(e => e.SourceSystemId).HasColumnName("source_system_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ExternalPersonId).HasColumnName("external_person_id").HasMaxLength(255).IsRequired();
        builder.Property(e => e.ExternalPersonType).HasColumnName("external_person_type").HasMaxLength(255);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(e => e.MasterId).HasDatabaseName("ix_ext_persons_person_id");
        builder.HasIndex(e => e.SourceSystemId).HasDatabaseName("ix_ext_persons_source_system_id");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_ext_persons_created_at");

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(e => e.MasterId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
