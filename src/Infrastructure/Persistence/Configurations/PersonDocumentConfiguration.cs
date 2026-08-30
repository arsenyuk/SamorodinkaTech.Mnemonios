using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности PersonDocument.
/// </summary>
public class PersonDocumentConfiguration : IEntityTypeConfiguration<PersonDocument>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonDocument> builder)
    {
        builder.ToTable("person_documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.MasterId).HasColumnName("person_id").IsRequired();
        builder.Property(d => d.DocumentType).HasColumnName("document_type").HasMaxLength(50).IsRequired();
        builder.Property(d => d.DocumentHash).HasColumnName("document_hash").HasMaxLength(255).IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(d => d.MasterId).HasDatabaseName("ix_person_documents_person_id");

        builder.HasIndex(d => new { d.MasterId, d.DocumentHash })
            .IsUnique()
            .HasDatabaseName("ux_person_documents_person_id_hash");
    }
}
