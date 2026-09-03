using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Entities;
using Mnemonios.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemonios.Infrastructure.Persistence;

namespace Mnemonios.Infrastructure.Services;

/// <summary>
/// Сервис прекращения обработки персональных данных во всех системах.
/// Поддерживает мгновенное и отложенное прекращение.
/// </summary>
public class PersonCessationService : IPersonCessationService
{
    private const string AuditCategory = "Audit.Cessation";

    private readonly IPersonRepository _repository;
    private readonly ILogger<PersonCessationService> _logger;
    private readonly AppDbContext _context;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonCessationService"/>.
    /// </summary>
    public PersonCessationService(
        IPersonRepository repository,
        ILogger<PersonCessationService> logger,
        AppDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<CessationResponse?> CeaseProcessingAsync(
        CessationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Identifiers.Count > 0 && !string.IsNullOrWhiteSpace(request.OrganizationUnitKey))
            throw new ArgumentException("Нельзя указывать одновременно идентификаторы и ключ организации. Используйте либо то, либо другое.");

        if (request.Identifiers.Count == 0 && string.IsNullOrWhiteSpace(request.OrganizationUnitKey))
            throw new ArgumentException("Требуется указать либо идентификаторы, либо ключ организации.");

        // Фаза 1: пометить внешние ключи для прекращения обработки
        var linksToMark = new List<(string SourceSystemId, string ExternalPersonId)>();

        if (request.Identifiers.Count > 0)
        {
            foreach (var identifier in request.Identifiers)
            {
                linksToMark.Add((identifier.SourceSystemId, identifier.ExternalPersonId));
            }
        }
        else
        {
            // Режим: вся организация — найти все ключи по organization_unit_key
            var orgKeys = await _repository.GetIdentificationKeysByOrganizationUnitKeyAsync(
                request.OrganizationUnitKey!, cancellationToken);

            var personIds = orgKeys.Select(k => k.MasterId).Distinct().ToList();

            foreach (var pid in personIds)
            {
                var personExternalIds = await _repository.GetExternalIdsAsync(pid, null, cancellationToken);
                foreach (var ext in personExternalIds)
                {
                    linksToMark.Add((ext.SourceSystemId, ext.ExternalPersonId));
                }
            }
        }

        // Найти лиц по внешним ссылкам
        var linksByPerson = new Dictionary<Guid, List<(string SourceSystemId, string ExternalPersonId)>>();
        Guid? resultMasterId = null;

        foreach (var (sourceSystemId, externalPersonId) in linksToMark)
        {
            var masterId = await _repository.FindMasterIdByExternalIdAsync(
                sourceSystemId, externalPersonId, cancellationToken);

            if (masterId is null)
                continue;

            resultMasterId = masterId.Value;

            if (!linksByPerson.TryGetValue(masterId.Value, out var personLinks))
            {
                personLinks = [];
                linksByPerson[masterId.Value] = personLinks;
            }
            personLinks.Add((sourceSystemId, externalPersonId));
        }

        if (linksByPerson.Count == 0)
            return null;

        // Создать staging-записи с processing_status = 'cessation'
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var (masterId, personLinks) in linksByPerson)
            {
                foreach (var (sourceSystemId, externalPersonId) in personLinks)
                {
                    // Найти ext_person по person_id + source_system_id + external_person_id
                    var extPerson = await _context.ExtPersons
                        .FirstOrDefaultAsync(e =>
                            e.MasterId == masterId &&
                            e.SourceSystemId == sourceSystemId &&
                            e.ExternalPersonId == externalPersonId, cancellationToken);

                    if (extPerson is null)
                        continue;

                    var extCessation = new ExtPersonCessation
                    {
                        Id = Guid.NewGuid(),
                        PersonId = extPerson.Id,
                        SourceSystemId = sourceSystemId,
                        ExternalPersonId = externalPersonId,
                        OrganizationUnitKey = request.OrganizationUnitKey ?? string.Empty,
                        ProcessingStatus = "cessation",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _repository.CreateExtCessationAsync(extCessation, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new CessationResponse
        {
            MasterId = resultMasterId,
            DeletedKeys = 0,
            DeletedExternalIds = 0,
            DeletedDefects = 0
        };
    }

    /// <inheritdoc/>
    public async Task<DeferredCessationResponse?> DeferProcessingAsync(
        DeferredCessationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Identifiers.Count > 0 && !string.IsNullOrWhiteSpace(request.OrganizationUnitKey))
            throw new ArgumentException("Нельзя указывать одновременно идентификаторы и ключ организации. Используйте либо то, либо другое.");

        if (request.Identifiers.Count == 0 && string.IsNullOrWhiteSpace(request.OrganizationUnitKey))
            throw new ArgumentException("Требуется указать либо идентификаторы, либо ключ организации.");

        if (request.ScheduledDeletionDate <= DateTime.UtcNow)
            throw new ArgumentException("Дата удаления должна быть в будущем.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Guid? resultMasterId = null;

            // Собрать идентификаторы для обработки
            var identifiersToProcess = new List<(string SourceSystemId, string ExternalPersonId)>();

            if (request.Identifiers.Count > 0)
            {
                foreach (var identifier in request.Identifiers)
                {
                    identifiersToProcess.Add((identifier.SourceSystemId, identifier.ExternalPersonId));
                }
            }
            else
            {
                // Режим: вся организация
                var orgKeys = await _repository.GetIdentificationKeysByOrganizationUnitKeyAsync(
                    request.OrganizationUnitKey!, cancellationToken);
                var personIds = orgKeys.Select(k => k.MasterId).Distinct().ToList();

                foreach (var pid in personIds)
                {
                    var personExternalIds = await _repository.GetExternalIdsAsync(pid, null, cancellationToken);
                    foreach (var ext in personExternalIds)
                    {
                        identifiersToProcess.Add((ext.SourceSystemId, ext.ExternalPersonId));
                    }
                }
            }

            foreach (var (sourceSystemId, externalPersonId) in identifiersToProcess)
            {
                var masterId = await _repository.FindMasterIdByExternalIdAsync(
                    sourceSystemId, externalPersonId, cancellationToken);

                if (masterId is null)
                    continue;

                resultMasterId = masterId.Value;

                var existing = await _repository.GetPendingDeferredCessationAsync(
                    sourceSystemId, externalPersonId, cancellationToken);

                if (existing is not null)
                    continue; // Уже запланировано — пропускаем

                var cessation = new PersonDeferredCessation
                {
                    Id = Guid.NewGuid(),
                    MasterId = masterId.Value,
                    SourceSystemId = sourceSystemId,
                    ExternalPersonId = externalPersonId,
                    OrganizationUnitKey = request.OrganizationUnitKey ?? string.Empty,
                    ScheduledDeletionDate = request.ScheduledDeletionDate,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _repository.AddDeferredCessationAsync(cessation, cancellationToken);

                // --- Staging: create ext_person_deferred_cessations record ---
                var extPerson = await _context.ExtPersons
                    .FirstOrDefaultAsync(e =>
                        e.MasterId == masterId.Value &&
                        e.SourceSystemId == sourceSystemId &&
                        e.ExternalPersonId == externalPersonId, cancellationToken);

                if (extPerson is not null)
                {
                    var extDeferred = new ExtPersonDeferredCessation
                    {
                        Id = Guid.NewGuid(),
                        PersonId = extPerson.Id,
                        SourceSystemId = sourceSystemId,
                        ExternalPersonId = externalPersonId,
                        ScheduledDeletionDate = request.ScheduledDeletionDate,
                        OrganizationUnitKey = request.OrganizationUnitKey ?? string.Empty,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _repository.CreateExtDeferredCessationAsync(extDeferred, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);

            return new DeferredCessationResponse
            {
                MasterId = resultMasterId,
                ScheduledDeletionDate = request.ScheduledDeletionDate
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task CancelDeferredCessationAsync(
        string sourceSystemId,
        string externalPersonId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingDeferredCessationAsync(
            sourceSystemId, externalPersonId, cancellationToken);

        if (pending is null)
            return;

        await _repository.CancelDeferredCessationRecordAsync(pending, cancellationToken);

        _logger.LogWarning(
            "[DeferredCessation] Cancelled. MasterId ={PersonId}, SourceSystemId={SourceSystemId}, ExternalPersonId={ExternalPersonId}",
            pending.MasterId, sourceSystemId, externalPersonId);
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDeferredCessationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueDeferred = await _context.PersonDeferredCessations
            .Where(d => d.Status == "pending" && d.ScheduledDeletionDate <= now)
            .ToListAsync(cancellationToken);

        if (dueDeferred.Count == 0)
            return 0;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var processedCount = 0;

            foreach (var deferred in dueDeferred)
            {
                // Найти ext_person по source_system_id + external_person_id
                var extPerson = await _context.ExtPersons
                    .FirstOrDefaultAsync(e =>
                        e.SourceSystemId == deferred.SourceSystemId &&
                        e.ExternalPersonId == deferred.ExternalPersonId, cancellationToken);

                if (extPerson is not null)
                {
                    // Создать пометку прекращения обработки
                    var extCessation = new ExtPersonCessation
                    {
                        Id = Guid.NewGuid(),
                        PersonId = extPerson.Id,
                        SourceSystemId = deferred.SourceSystemId,
                        ExternalPersonId = deferred.ExternalPersonId,
                        OrganizationUnitKey = deferred.OrganizationUnitKey,
                        ProcessingStatus = "cessation",
                        CreatedAt = now
                    };
                    await _repository.CreateExtCessationAsync(extCessation, cancellationToken);
                }

                // Пометить отложенный отзыв как выполненный
                deferred.Status = "completed";
                processedCount++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "[DeferredCessation] Обработано {Count} отложенных отзывов с наступившей датой",
                processedCount);

            return processedCount;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        // Найти все ext_person_cessations с processing_status = 'cessation'
        var marked = await _context.ExtPersonCessations
            .Where(c => c.ProcessingStatus == "cessation")
            .ToListAsync(cancellationToken);

        if (marked.Count == 0)
            return 0;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Сгруппировать по ext_person (PersonId → ext_persons.id)
            var extPersonIds = marked.Select(c => c.PersonId).Distinct().ToList();

            foreach (var extPersonId in extPersonIds)
            {
                var cessationRecords = marked.Where(c => c.PersonId == extPersonId).ToList();

                // Найти ext_person чтобы получить masterId (golden person)
                var extPerson = await _context.ExtPersons.FindAsync([extPersonId], cancellationToken);
                if (extPerson is null || extPerson.MasterId is null)
                {
                    // ext_person не найден или не привязан к лицу — просто удаляем cessation
                    _context.ExtPersonCessations.RemoveRange(cessationRecords);
                    continue;
                }

                var masterId = extPerson.MasterId.Value;

                // Удалить помеченные ext_person_cessations
                _context.ExtPersonCessations.RemoveRange(cessationRecords);

                // Удалить соответствующие person_external_ids
                foreach (var c in cessationRecords)
                {
                    var extId = await _context.PersonExternalIds
                        .FirstOrDefaultAsync(e =>
                            e.MasterId == masterId &&
                            e.SourceSystemId == c.SourceSystemId &&
                            e.ExternalPersonId == c.ExternalPersonId, cancellationToken);

                    if (extId is not null)
                        _context.PersonExternalIds.Remove(extId);
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Проверить оставшиеся ссылки
                var remaining = await _context.PersonExternalIds
                    .CountAsync(e => e.MasterId == masterId, cancellationToken);

                if (remaining == 0)
                {
                    // Нет ссылок → удалить золотые записи
                    await DeleteGoldenRecordsAsync(masterId, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return marked.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task DeleteGoldenRecordsAsync(Guid masterId, CancellationToken cancellationToken)
    {
        var keys = await _context.PersonIdentificationKeys
            .Where(k => k.MasterId == masterId)
            .ToListAsync(cancellationToken);
        _context.PersonIdentificationKeys.RemoveRange(keys);

        var defects = await _context.PersonDefects
            .Where(d => d.MasterId == masterId)
            .ToListAsync(cancellationToken);
        _context.PersonDefects.RemoveRange(defects);

        var deferred = await _context.PersonDeferredCessations
            .Where(c => c.MasterId == masterId)
            .ToListAsync(cancellationToken);
        _context.PersonDeferredCessations.RemoveRange(deferred);

        var documents = await _context.PersonDocuments
            .Where(d => d.MasterId == masterId)
            .ToListAsync(cancellationToken);
        _context.PersonDocuments.RemoveRange(documents);

        var extPersons = await _context.ExtPersons
            .Where(e => e.MasterId == masterId)
            .ToListAsync(cancellationToken);
        var extPersonIds = extPersons.Select(e => e.Id).ToList();

        var extDefects = await _context.ExtPersonDefects
            .Where(d => extPersonIds.Contains(d.ExtPersonId))
            .ToListAsync(cancellationToken);
        _context.ExtPersonDefects.RemoveRange(extDefects);
        _context.ExtPersons.RemoveRange(extPersons);

        var person = await _context.Persons.FindAsync([masterId], cancellationToken);
        if (person is not null)
            _context.Persons.Remove(person);
    }
}
