using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF-конфигурация для <see cref="PersonReviewHistory"/>.
/// </summary>
public class PersonReviewHistoryConfiguration : IEntityTypeConfiguration<PersonReviewHistory>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonReviewHistory> builder)
    {
        builder.ToTable("person_review_history");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.ReviewId).HasColumnName("review_id").IsRequired();
        builder.Property(e => e.PersonAId).HasColumnName("person_a_id").IsRequired();
        builder.Property(e => e.PersonBId).HasColumnName("person_b_id").IsRequired();
        builder.Property(e => e.SharedKeyType).HasColumnName("shared_key_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ConflictKeyType).HasColumnName("conflict_key_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Resolution).HasColumnName("resolution").HasMaxLength(20).IsRequired();
        builder.Property(e => e.ResolvedBy).HasColumnName("resolved_by").HasMaxLength(100).IsRequired();
        builder.Property(e => e.ResolvedAt).HasColumnName("resolved_at").IsRequired();
        builder.Property(e => e.ResolutionDetails).HasColumnName("resolution_details");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.ReviewId).HasDatabaseName("ix_person_review_history_review_id");
        builder.HasIndex(e => e.ResolvedAt).HasDatabaseName("ix_person_review_history_resolved_at");
    }
}
