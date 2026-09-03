using Microsoft.EntityFrameworkCore;
using Mnemonios.Domain.Entities;

namespace Mnemonios.Infrastructure.Persistence;

/// <summary>
/// Контекст базы данных приложения для ЕДИН MPI.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>Единая запись физического лица в MPI.</summary>
    public DbSet<Person> Persons => Set<Person>();

    /// <summary>Запись документа ДУЛ (тип + хеш, без ПДн).</summary>
    public DbSet<PersonDocument> PersonDocuments => Set<PersonDocument>();

    /// <summary>HMAC-ключ идентификации для детерминированного сопоставления персон.</summary>
    public DbSet<PersonIdentificationKey> PersonIdentificationKeys => Set<PersonIdentificationKey>();

    /// <summary>Ссылка между PersonID и внешним системным идентификатором.</summary>
    public DbSet<PersonExternalId> PersonExternalIds => Set<PersonExternalId>();

    /// <summary>Дефект данных персоны, обнаруженный при идентификации.</summary>
    public DbSet<PersonDefect> PersonDefects => Set<PersonDefect>();

    /// <summary>Запись отложенной прекращения обработки персональных данных.</summary>
    public DbSet<PersonDeferredCessation> PersonDeferredCessations => Set<PersonDeferredCessation>();

    /// <summary>Сырые данные запроса идентификации (staging).</summary>
    public DbSet<ExtPerson> ExtPersons => Set<ExtPerson>();

    /// <summary>Сырые данные дефектов из входящего запроса идентификации (staging).</summary>
    public DbSet<ExtPersonDefect> ExtPersonDefects => Set<ExtPersonDefect>();

    /// <summary>Сырые данные запроса прекращения (staging).</summary>
    public DbSet<ExtPersonCessation> ExtPersonCessations => Set<ExtPersonCessation>();

    /// <summary>Сырые данные запроса отложенного прекращения (staging).</summary>
    public DbSet<ExtPersonDeferredCessation> ExtPersonDeferredCessations => Set<ExtPersonDeferredCessation>();

    /// <summary>Очередь на ручную обработку стюардом (Ambiguous).</summary>
    public DbSet<PersonReviewQueue> PersonReviewQueues => Set<PersonReviewQueue>();

    /// <summary>
    /// Создаёт новый экземпляр <see cref="AppDbContext"/>.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
