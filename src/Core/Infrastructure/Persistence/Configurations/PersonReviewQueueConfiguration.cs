using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF-конфигурация для <see cref="PersonReviewQueue"/>.
/// </summary>
public class PersonReviewQueueConfiguration : IEntityTypeConfiguration<PersonReviewQueue>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PersonReviewQueue> builder)
    {
        builder.ToTable("person_review_queue");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.PersonAId)
            .HasColumnName("person_a_id")
            .IsRequired();

        builder.Property(e => e.PersonBId)
            .HasColumnName("person_b_id")
            .IsRequired();

        builder.Property(e => e.SharedKeyType)
            .HasColumnName("shared_key_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ConflictKeyType)
            .HasColumnName("conflict_key_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(e => e.PersonAId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(e => e.PersonBId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Status)
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ix_person_review_queue_status");
    }
}
