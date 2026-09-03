using Microsoft.EntityFrameworkCore;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Interfaces;
using Mnemonios.Domain.Validation;
using Mnemonios.Infrastructure.Persistence;

namespace Mnemonios.Api.Endpoints;

/// <summary>
/// ЕДИН — эндпоинты API персон MPI.
/// </summary>
public static class PersonEndpoints
{
    private const string BaseTag = "Persons";

    /// <summary>
    /// Регистрирует эндпоинты, связанные с персонами.
    /// </summary>
    public static IEndpointRouteBuilder MapPersonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/persons")
            .WithTags(BaseTag);

        group.MapPost("/resolve", HandleResolveAsync)
            .WithName("ResolvePerson")
            .WithSummary("Идентификация физического лица — поиск существующего или создание нового PersonID.")
            .Produces<ResolveResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{masterId:guid}", HandleGetPersonAsync)
            .WithName("GetPerson")
            .WithSummary("Получение данных физического лица по PersonID (включая связи и дефекты).")
            .Produces<PersonDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{masterId:guid}/identifiers", HandleAddIdentifierAsync)
            .WithName("AddIdentifier")
            .WithSummary("Добавление связи с внешней информационной системой.")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/validate/inn", HandleValidateInnAsync)
            .WithName("ValidateInn")
            .WithSummary("Проверка формата и контрольной суммы ИНН.")
            .Produces<ValidationResultDto>(StatusCodes.Status200OK);

        group.MapPost("/validate/snils", HandleValidateSnilsAsync)
            .WithName("ValidateSnils")
            .WithSummary("Проверка формата и контрольной суммы СНИЛС.")
            .Produces<ValidationResultDto>(StatusCodes.Status200OK);

        group.MapPost("/cessation", HandleCessationAsync)
            .WithName("CeaseProcessing")
            .WithSummary("Прекращение обработки персональных данных.")
            .Produces<CessationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/cessation/deferred", HandleDeferredCessationAsync)
            .WithName("DeferProcessing")
            .WithSummary("Отложенное прекращение обработки персональных данных.")
            .Produces<DeferredCessationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/cessation/reconcile", HandleReconcileAsync)
            .WithName("ReconcileCessations")
            .WithSummary("Реконсилизация: удаление помеченных staging и золотых записей.")
            .Produces<int>(StatusCodes.Status200OK);

        group.MapGet("/dul-classifier", HandleGetDulClassifierAsync)
            .WithName("GetDulClassifier")
            .WithSummary("Получение классификатора видов документов, удостоверяющих личность (ДУЛ).")
            .Produces<DulClassifierResponse>(StatusCodes.Status200OK);

        group.MapGet("/review", HandleGetReviewQueueAsync)
            .WithName("GetReviewQueue")
            .WithSummary("Очередь на ручную обработку стюардом (Ambiguous).")
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/review/{reviewId:guid}/confirm", HandleConfirmReviewAsync)
            .WithName("ConfirmReview")
            .WithSummary("Подтверждение: merge personB → personA.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/review/{reviewId:guid}/reject", HandleRejectReviewAsync)
            .WithName("RejectReview")
            .WithSummary("Отклонение: оставить записи раздельно.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> HandleResolveAsync(
        ResolveRequest request,
        IPersonResolveService resolveService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.Resolve");

        try
        {
            var result = await resolveService.ResolveAsync(request, ct);
            logger.LogInformation("Person resolved: status={Status}, masterId={MasterId}",
                result.Status, result.MasterId);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Validation failed: {Message}", ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Conflict: {Message}", ex.Message);
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> HandleGetPersonAsync(
        Guid masterId,
        IPersonRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.GetPerson");

        try
        {
            var person = await repository.GetByIdAsync(masterId, ct);
            if (person is null)
            {
                logger.LogWarning("Person not found: {MasterId}", masterId);
                return Results.NotFound(new { error = $"Физическое лицо {masterId} не найдено." });
            }

            var externalIds = await repository.GetExternalIdsAsync(masterId, null, ct);
            var defects = await repository.GetDefectsAsync(masterId, ct);
            var identificationKeys = await repository.GetIdentificationKeysAsync(masterId, ct);
            var deferredCessations = await repository.GetDeferredCessationsAsync(masterId, ct);

            var dto = new PersonDto
            {
                MasterId = person.MasterId,
                CreatedAt = person.CreatedAt,
                Identifiers = externalIds.Select(e => new ExternalIdentifierDto
                {
                    SourceSystemId = e.SourceSystemId,
                    ExternalPersonId = e.ExternalPersonId,
                    ExternalPersonType = e.ExternalPersonType
                }).ToList(),
                Defects = defects.Select(d => new DefectInfo
                {
                    DefectType = d.DefectType,
                    DefectMessage = d.DefectMessage,
                    FieldName = d.FieldName
                }).ToList(),
                IdentificationKeys = identificationKeys.Select(k => new IdentificationKeyDto
                {
                    Id = k.Id,
                    KeyType = k.KeyType,
                    NormalizationVersion = k.NormalizationVersion,
                    CreatedAt = k.CreatedAt
                }).ToList(),
                DeferredCessations = deferredCessations.Select(c => new DeferredCessationDto
                {
                    Id = c.Id,
                    SourceSystemId = c.SourceSystemId,
                    ExternalPersonId = c.ExternalPersonId,
                    ScheduledDeletionDate = c.ScheduledDeletionDate,
                    Status = c.Status,
                    OrganizationUnitKey = c.OrganizationUnitKey,
                    CreatedAt = c.CreatedAt
                }).ToList()
            };

            return Results.Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting person {MasterId}", masterId);
            throw;
        }
    }

    private static async Task<IResult> HandleAddIdentifierAsync(
        Guid masterId,
        AddExternalIdRequest request,
        IPersonRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.AddIdentifier");

        try
        {
            var person = await repository.GetByIdAsync(masterId, ct);
            if (person is null)
            {
                logger.LogWarning("Person not found: {MasterId}", masterId);
                return Results.NotFound(new { error = $"Физическое лицо {masterId} не найдено." });
            }

            if (string.IsNullOrWhiteSpace(request.SourceSystemId))
                return Results.BadRequest(new { error = "Обязательное поле «Идентификатор внешней системы»." });

            if (string.IsNullOrWhiteSpace(request.ExternalPersonId))
                return Results.BadRequest(new { error = "Обязательное поле «Идентификатор лица во внешней системе»." });

            var now = DateTime.UtcNow;
            var externalId = new Domain.Entities.PersonExternalId
            {
                Id = Guid.NewGuid(),
                MasterId = masterId,
                SourceSystemId = request.SourceSystemId,
                ExternalPersonId = request.ExternalPersonId,
                ExternalPersonType = request.ExternalPersonType,
                CreatedAt = now,
                UpdatedAt = now
            };

            var (updated, existingId) = await repository.TryUpdateExternalIdAsync(externalId, ct);
            if (!updated)
            {
                await repository.AddExternalIdAsync(externalId, ct);
                existingId = externalId.Id;
            }

            logger.LogInformation("External identifier added/updated for person {MasterId}: {SystemId}/{ExtId}",
                masterId, request.SourceSystemId, request.ExternalPersonId);

            return Results.Created($"/persons/{masterId}/identifiers", new { Id = existingId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding identifier for person {MasterId}", masterId);
            throw;
        }
    }

    private static IResult HandleValidateInnAsync(InnValidationRequest request)
    {
        var isValid = InnValidator.Validate(request.Inn);

        return Results.Ok(new ValidationResultDto
        {
            IsValid = isValid,
            Error = isValid ? null : "Некорректный формат или контрольная сумма ИНН."
        });
    }

    private static IResult HandleValidateSnilsAsync(SnilsValidationRequest request)
    {
        var isValid = SnilsValidator.Validate(request.Snils);

        return Results.Ok(new ValidationResultDto
        {
            IsValid = isValid,
            Error = isValid ? null : "Некорректный формат или контрольная сумма СНИЛС."
        });
    }

    private static async Task<IResult> HandleCessationAsync(
        CessationRequest request,
        IPersonCessationService cessationService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.Cessation");

        try
        {
            var result = await cessationService.CeaseProcessingAsync(request, ct);
            if (result is null || result.MasterId is null)
            {
                return Results.NotFound(new { error = "Физическое лицо не найдено по указанным идентификаторам." });
            }

            logger.LogInformation(
                "Processing ceased for person {MasterId}: keys={Keys}, externalIds={ExternalIds}, defects={Defects}",
                result.MasterId, result.DeletedKeys, result.DeletedExternalIds, result.DeletedDefects);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Validation failed: {Message}", ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> HandleDeferredCessationAsync(
        DeferredCessationRequest request,
        IPersonCessationService cessationService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.DeferredCessation");

        try
        {
            var result = await cessationService.DeferProcessingAsync(request, ct);
            if (result is null)
            {
                return Results.NotFound(new { error = "Физическое лицо не найдено по указанным идентификаторам." });
            }

            logger.LogInformation(
                "Deferred cessation scheduled for person {MasterId}: deletionDate={ScheduledDeletionDate}",
                result.MasterId, result.ScheduledDeletionDate);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Validation failed: {Message}", ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Conflict: {Message}", ex.Message);
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> HandleReconcileAsync(
        IPersonCessationService cessationService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.ReconcileCessations");

        try
        {
            var processed = await cessationService.ReconcileAsync(ct);
            logger.LogInformation("Reconciliation completed: {Processed} records processed", processed);
            return Results.Ok(processed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during reconciliation");
            throw;
        }
    }

    private static IResult HandleGetDulClassifierAsync()
    {
        var classifier = DulClassifier.GetClassifier();
        return Results.Ok(classifier);
    }

    private static async Task<IResult> HandleGetReviewQueueAsync(
        AppDbContext context,
        CancellationToken ct)
    {
        var pending = await context.PersonReviewQueues
            .Where(r => r.Status == "pending")
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReviewQueueDto
            {
                Id = r.Id,
                PersonAId = r.PersonAId,
                PersonBId = r.PersonBId,
                SharedKeyType = r.SharedKeyType,
                ConflictKeyType = r.ConflictKeyType,
                Status = r.Status
            })
            .ToListAsync(ct);

        return Results.Ok(pending);
    }

    private static async Task<IResult> HandleConfirmReviewAsync(
        Guid reviewId,
        AppDbContext context,
        IPersonMergeService mergeService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.Review");
        var review = await context.PersonReviewQueues.FindAsync([reviewId], ct);

        if (review is null)
            return Results.NotFound();

        // Merge personB → personA
        await mergeService.MergePersonsAsync(review.PersonAId, review.PersonBId, "steward_confirm", ct);

        review.Status = "confirmed";
        review.ReviewedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Review {Id} confirmed: merged {PersonB} into {PersonA}",
            reviewId, review.PersonBId, review.PersonAId);

        return Results.Ok(new { merged = review.PersonBId, surviving = review.PersonAId });
    }

    private static async Task<IResult> HandleRejectReviewAsync(
        Guid reviewId,
        AppDbContext context,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("PersonEndpoints.Review");
        var review = await context.PersonReviewQueues.FindAsync([reviewId], ct);

        if (review is null)
            return Results.NotFound();

        review.Status = "rejected";
        review.ReviewedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Review {Id} rejected: persons {PersonA} and {PersonB} remain separate",
            reviewId, review.PersonAId, review.PersonBId);

        return Results.Ok(new { personA = review.PersonAId, personB = review.PersonBId });
    }
}
