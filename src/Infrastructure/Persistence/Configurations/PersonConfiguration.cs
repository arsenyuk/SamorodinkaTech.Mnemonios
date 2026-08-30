using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence.Configurations;

/// <summary>
/// Конфигурация EF Core для сущности Person.
/// </summary>
public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons");

        builder.HasKey(p => p.MasterId);
        builder.Property(p => p.MasterId).HasColumnName("id");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(p => p.IdentificationKeys)
            .WithOne()
            .HasForeignKey(k => k.MasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.ExternalIds)
            .WithOne()
            .HasForeignKey(e => e.MasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Defects)
            .WithOne()
            .HasForeignKey(d => d.MasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.DeferredCessations)
            .WithOne()
            .HasForeignKey(c => c.MasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Documents)
            .WithOne()
            .HasForeignKey(d => d.MasterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
