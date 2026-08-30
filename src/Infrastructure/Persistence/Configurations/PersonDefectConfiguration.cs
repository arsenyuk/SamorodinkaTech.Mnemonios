using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности PersonDefect.
/// </summary>
public class PersonDefectConfiguration : IEntityTypeConfiguration<PersonDefect>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonDefect> builder)
    {
        builder.ToTable("person_defects");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.MasterId).HasColumnName("person_id").IsRequired();
        builder.Property(d => d.DefectType).HasColumnName("defect_type").HasMaxLength(50).IsRequired();
        builder.Property(d => d.DefectMessage).HasColumnName("defect_message").HasMaxLength(500).IsRequired();
        builder.Property(d => d.FieldName).HasColumnName("field_name").HasMaxLength(100);
        builder.Property(d => d.OriginalValue).HasColumnName("original_value").HasMaxLength(500);
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(d => d.MasterId).HasDatabaseName("ix_person_defects_person_id");
        builder.HasIndex(d => d.DefectType).HasDatabaseName("ix_person_defects_defect_type");

        builder.HasOne<Person>()
            .WithMany(p => p.Defects)
            .HasForeignKey(d => d.MasterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
