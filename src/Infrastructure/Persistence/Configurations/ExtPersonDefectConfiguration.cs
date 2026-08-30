using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности ExtPersonDefect.
/// </summary>
public class ExtPersonDefectConfiguration : IEntityTypeConfiguration<ExtPersonDefect>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ExtPersonDefect> builder)
    {
        builder.ToTable("ext_person_defects");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.ExtPersonId).HasColumnName("ext_person_id").IsRequired();
        builder.Property(e => e.DefectType).HasColumnName("defect_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.DefectMessage).HasColumnName("defect_message").HasMaxLength(500).IsRequired();
        builder.Property(e => e.FieldName).HasColumnName("field_name").HasMaxLength(100);
        builder.Property(e => e.OriginalValue).HasColumnName("original_value").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.ExtPersonId).HasDatabaseName("ix_ext_person_defects_ext_person_id");

        builder.HasOne<ExtPerson>()
            .WithMany()
            .HasForeignKey(e => e.ExtPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
