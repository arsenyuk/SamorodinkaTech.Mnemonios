using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemonios.Domain.Entities;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Infrastructure.Persistence;

namespace Mnemonios.Infrastructure.Services;

/// <summary>
/// Репозиторий для сохранения и получения данных персон.
/// </summary>
public class PersonRepository : IPersonRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<PersonRepository> _logger;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonRepository"/>.
    /// </summary>
    public PersonRepository(AppDbContext context, ILogger<PersonRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Guid>> FindPersonIdsByKeysAsync(
        IEnumerable<string> keyValues,
        CancellationToken cancellationToken = default)
    {
        var keyValuesList = keyValues.ToList();
        if (keyValuesList.Count == 0)
            return [];

        return await _context.PersonIdentificationKeys
            .Where(k => keyValuesList.Contains(k.KeyValue))
            .Select(k => k.MasterId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Persons.FindAsync([id], cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersonExternalId>> GetExternalIdsAsync(
        Guid masterId,
        IEnumerable<string>? sourceSystemIds,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PersonExternalIds
            .Where(e => e.MasterId == masterId);

        if (sourceSystemIds is not null)
        {
            var systemIdsList = sourceSystemIds.ToList();
            if (systemIdsList.Count > 0)
                query = query.Where(e => systemIdsList.Contains(e.SourceSystemId));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Person> CreateAsync(
        Person person,
        IEnumerable<PersonIdentificationKey> keys,
        PersonExternalId externalId,
        CancellationToken cancellationToken = default)
    {
        var existingTransaction = _context.Database.CurrentTransaction;
        if (existingTransaction is not null)
        {
            await CreatePersonCoreAsync(person, keys, externalId, cancellationToken);
            return person;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await CreatePersonCoreAsync(person, keys, externalId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return person;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction failed in CreateAsync: {Message}", GetDeepestMessage(ex));
            throw;
        }
    }

    private async Task CreatePersonCoreAsync(
        Person person,
        IEnumerable<PersonIdentificationKey> keys,
        PersonExternalId externalId,
        CancellationToken cancellationToken)
    {
        _context.Persons.Add(person);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var key in keys)
        {
            key.MasterId = person.MasterId;
            _context.PersonIdentificationKeys.Add(key);
        }

        externalId.MasterId = person.MasterId;
        _context.PersonExternalIds.Add(externalId);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddExternalIdAsync(
        PersonExternalId externalId,
        CancellationToken cancellationToken = default)
    {
        _context.PersonExternalIds.Add(externalId);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(bool Updated, Guid? ExistingId)> TryUpdateExternalIdAsync(
        PersonExternalId externalId,
        CancellationToken cancellationToken = default)
    {
        var existingTransaction = _context.Database.CurrentTransaction;
        if (existingTransaction is not null)
        {
            return await TryUpdateExternalIdCoreAsync(externalId, cancellationToken);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await TryUpdateExternalIdCoreAsync(externalId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction failed in TryUpdateExternalIdAsync: {Message}", GetDeepestMessage(ex));
            throw;
        }
    }

    private async Task<(bool Updated, Guid? ExistingId)> TryUpdateExternalIdCoreAsync(
        PersonExternalId externalId,
        CancellationToken cancellationToken)
    {
        var existing = await _context.PersonExternalIds
            .FirstOrDefaultAsync(
                e => e.SourceSystemId == externalId.SourceSystemId
                    && e.ExternalPersonId == externalId.ExternalPersonId,
                cancellationToken);

        if (existing is null)
            return (false, null);

        existing.ExternalPersonType = externalId.ExternalPersonType;
        existing.MasterId = externalId.MasterId;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return (true, existing.Id);
    }

    /// <inheritdoc/>
    public async Task SaveDefectsAsync(
        IEnumerable<PersonDefect> defects,
        CancellationToken cancellationToken = default)
    {
        var defectList = defects.ToList();
        if (defectList.Count == 0)
            return;

        _context.PersonDefects.AddRange(defectList);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersonIdentificationKey>> GetIdentificationKeysAsync(
        Guid masterId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PersonIdentificationKeys
            .Where(k => k.MasterId == masterId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersonDefect>> GetDefectsAsync(
        Guid masterId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PersonDefects
            .Where(d => d.MasterId == masterId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersonDeferredCessation>> GetDeferredCessationsAsync(
        Guid masterId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PersonDeferredCessations
            .Where(c => c.MasterId == masterId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PersonIdentificationKey>> GetIdentificationKeysByOrganizationUnitKeyAsync(
        string organizationUnitKey,
        CancellationToken cancellationToken = default)
    {
        return await _context.PersonIdentificationKeys
            .Where(k => k.OrganizationUnitKey == organizationUnitKey)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Guid?> FindMasterIdByExternalIdAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PersonExternalIds
            .Where(e => e.SourceSystemId == sourceSystemId && e.ExternalPersonId == externalPersonId)
            .Select(e => (Guid?)e.MasterId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeletePersonDataAsync(
        Person person,
        IEnumerable<PersonIdentificationKey> keys,
        IEnumerable<PersonExternalId> externalIds,
        IEnumerable<PersonDefect> defects,
        CancellationToken cancellationToken = default)
    {
        var existingTransaction = _context.Database.CurrentTransaction;
        if (existingTransaction is not null)
        {
            await DeletePersonDataCoreAsync(person, keys, externalIds, defects, cancellationToken);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await DeletePersonDataCoreAsync(person, keys, externalIds, defects, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction failed in DeletePersonDataAsync: {Message}", GetDeepestMessage(ex));
            throw;
        }
    }

    private async Task DeletePersonDataCoreAsync(
        Person person,
        IEnumerable<PersonIdentificationKey> keys,
        IEnumerable<PersonExternalId> externalIds,
        IEnumerable<PersonDefect> defects,
        CancellationToken cancellationToken)
    {
        var keysList = keys.ToList();
        if (keysList.Count > 0)
            _context.PersonIdentificationKeys.RemoveRange(keysList);

        var defectsList = defects.ToList();
        if (defectsList.Count > 0)
            _context.PersonDefects.RemoveRange(defectsList);

        var externalIdsList = externalIds.ToList();
        if (externalIdsList.Count > 0)
            _context.PersonExternalIds.RemoveRange(externalIdsList);

        _context.Persons.Remove(person);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PersonDeferredCessation?> GetPendingDeferredCessationAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PersonDeferredCessations
            .FirstOrDefaultAsync(
                c => c.SourceSystemId == sourceSystemId
                    && c.ExternalPersonId == externalPersonId
                    && c.Status == "pending",
                cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddDeferredCessationAsync(
        PersonDeferredCessation cessation,
        CancellationToken cancellationToken = default)
    {
        _context.PersonDeferredCessations.Add(cessation);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CancelDeferredCessationRecordAsync(
        PersonDeferredCessation cessation,
        CancellationToken cancellationToken = default)
    {
        cessation.Status = "cancelled";
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteExternalIdAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default)
    {
        var existingTransaction = _context.Database.CurrentTransaction;
        if (existingTransaction is not null)
        {
            await DeleteExternalIdCoreAsync(sourceSystemId, externalPersonId, cancellationToken);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await DeleteExternalIdCoreAsync(sourceSystemId, externalPersonId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Transaction failed in DeleteExternalIdAsync: {Message}", GetDeepestMessage(ex));
            throw;
        }
    }

    private async Task DeleteExternalIdCoreAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken)
    {
        var externalId = await _context.PersonExternalIds
            .FirstOrDefaultAsync(
                e => e.SourceSystemId == sourceSystemId && e.ExternalPersonId == externalPersonId,
                cancellationToken);

        if (externalId is not null)
        {
            _context.PersonExternalIds.Remove(externalId);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // =========================================================================
    // Staging (ext_*) методы
    // =========================================================================

    /// <inheritdoc/>
    public async Task<ExtPerson> CreateExtPersonAsync(
        ExtPerson extPerson,
        CancellationToken cancellationToken = default)
    {
        _context.ExtPersons.Add(extPerson);
        await _context.SaveChangesAsync(cancellationToken);
        return extPerson;
    }

    /// <inheritdoc/>
    public async Task SaveExtDefectsAsync(
        IEnumerable<ExtPersonDefect> defects,
        CancellationToken cancellationToken = default)
    {
        var defectList = defects.ToList();
        if (defectList.Count == 0)
            return;

        _context.ExtPersonDefects.AddRange(defectList);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ExtPersonCessation> CreateExtCessationAsync(
        ExtPersonCessation cessation,
        CancellationToken cancellationToken = default)
    {
        _context.ExtPersonCessations.Add(cessation);
        await _context.SaveChangesAsync(cancellationToken);
        return cessation;
    }

    /// <inheritdoc/>
    public async Task<ExtPersonDeferredCessation> CreateExtDeferredCessationAsync(
        ExtPersonDeferredCessation cessation,
        CancellationToken cancellationToken = default)
    {
        _context.ExtPersonDeferredCessations.Add(cessation);
        await _context.SaveChangesAsync(cancellationToken);
        return cessation;
    }

    /// <inheritdoc/>
    public async Task MarkExtPersonProcessedAsync(
        Guid extPersonId,
        Guid? masterId,
        CancellationToken cancellationToken = default)
    {
        var extPerson = await _context.ExtPersons.FindAsync([extPersonId], cancellationToken);
        if (extPerson is not null)
        {
            extPerson.MasterId = masterId;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static string GetDeepestMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
            current = current.InnerException;
        return current.Message;
    }
}
