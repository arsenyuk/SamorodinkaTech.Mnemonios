using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;

namespace Mnemonios.Infrastructure.Services;

/// <summary>
/// Сервис автоматического слияния двух мастер-записей при обнаружении конфликта.
/// </summary>
public class PersonMergeService : IPersonMergeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PersonMergeService> _logger;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonMergeService"/>.
    /// </summary>
    public PersonMergeService(
        AppDbContext context,
        ILogger<PersonMergeService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task MergePersonsAsync(
        Guid survivingMasterId,
        Guid mergedMasterId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (survivingMasterId == mergedMasterId)
            throw new ArgumentException("Нельзя слить запись с самой собой");

        // Транзакция не создаётся — метод вызывается изнутри транзакции вызывающего кода.

        // 1. Перенести ключи идентификации (пропуск дубликатов)
        var existingKeys = await _context.PersonIdentificationKeys
            .Where(k => k.MasterId == survivingMasterId)
            .Select(k => k.KeyValue)
            .ToListAsync(cancellationToken);

        var mergedKeys = await _context.PersonIdentificationKeys
            .Where(k => k.MasterId == mergedMasterId)
            .ToListAsync(cancellationToken);

        var newKeys = mergedKeys.Where(k => !existingKeys.Contains(k.KeyValue)).ToList();
        foreach (var key in newKeys)
        {
            key.MasterId = survivingMasterId;
        }

        var duplicateKeys = mergedKeys.Where(k => existingKeys.Contains(k.KeyValue)).ToList();
        _context.PersonIdentificationKeys.RemoveRange(duplicateKeys);

        // 2. Перенести внешние ссылки
        var mergedExternalIds = await _context.PersonExternalIds
            .Where(e => e.MasterId == mergedMasterId)
            .ToListAsync(cancellationToken);

        foreach (var extId in mergedExternalIds)
        {
            extId.MasterId = survivingMasterId;
        }

        // 3. Перенести документы (пропуск дубликатов)
        var existingDocHashes = await _context.PersonDocuments
            .Where(d => d.MasterId == survivingMasterId)
            .Select(d => d.DocumentHash)
            .ToListAsync(cancellationToken);

        var mergedDocs = await _context.PersonDocuments
            .Where(d => d.MasterId == mergedMasterId)
            .ToListAsync(cancellationToken);

        var newDocs = mergedDocs.Where(d => !existingDocHashes.Contains(d.DocumentHash)).ToList();
        foreach (var doc in newDocs)
        {
            doc.MasterId = survivingMasterId;
        }

        var duplicateDocs = mergedDocs.Where(d => existingDocHashes.Contains(d.DocumentHash)).ToList();
        _context.PersonDocuments.RemoveRange(duplicateDocs);

        // 4. Удалить дефекты merged
        var mergedDefects = await _context.PersonDefects
            .Where(d => d.MasterId == mergedMasterId)
            .ToListAsync(cancellationToken);
        _context.PersonDefects.RemoveRange(mergedDefects);

        // 5. Удалить отложенные прекращения merged
        var mergedDeferred = await _context.PersonDeferredCessations
            .Where(c => c.MasterId == mergedMasterId)
            .ToListAsync(cancellationToken);
        _context.PersonDeferredCessations.RemoveRange(mergedDeferred);

        // 6. Удалить merged-запись
        var mergedPerson = await _context.Persons.FindAsync([mergedMasterId], cancellationToken);
        if (mergedPerson is not null)
            _context.Persons.Remove(mergedPerson);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "[Merge] surviving={SurvivingMasterId}, merged={MergedMasterId}, reason={Reason}, movedKeys={MovedKeys}, movedExtIds={MovedExtIds}, movedDocs={MovedDocs}",
            survivingMasterId, mergedMasterId, reason,
            newKeys.Count, mergedExternalIds.Count, newDocs.Count);
    }
}
