using Microsoft.EntityFrameworkCore;
using Mnemonios.Domain.Entities;
using Mnemonios.Infrastructure.Persistence;

namespace SamorodinkaTech.Mnemonios.Steward.Services;

/// <summary>
/// Сервис для управления URL-масками.
/// </summary>
public interface IUrlMaskService
{
    /// <summary>Получить все уникальные триады из person_external_ids.</summary>
    Task<IReadOnlyList<TriadInfo>> GetTriadsAsync(CancellationToken ct);

    /// <summary>Получить маску для триады.</summary>
    Task<UrlMask?> GetMaskAsync(string organizationUnitKey, string sourceSystemId, string externalPersonType, CancellationToken ct);

    /// <summary>Создать или обновить маску.</summary>
    Task SaveMaskAsync(UrlMask mask, CancellationToken ct);

    /// <summary>Удалить маску.</summary>
    Task<bool> DeleteMaskAsync(Guid id, CancellationToken ct);

    /// <summary>Сформировать URL по маске и externalPersonId.</summary>
    string BuildUrl(string urlPattern, string externalPersonId);
}

/// <summary>
/// Информация о триаде (ЮЛ, Система, Тип).
/// </summary>
public record TriadInfo
{
    public string OrganizationUnitKey { get; init; } = string.Empty;
    public string SourceSystemId { get; init; } = string.Empty;
    public string ExternalPersonType { get; init; } = string.Empty;
    public bool HasMask { get; init; }
    public string? UrlPattern { get; init; }
}

/// <summary>
/// Реализация сервиса URL-масок.
/// </summary>
public class UrlMaskService : IUrlMaskService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="UrlMaskService"/>.
    /// </summary>
    public UrlMaskService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TriadInfo>> GetTriadsAsync(CancellationToken ct)
    {
        var triads = await _context.PersonExternalIds
            .Select(e => new
            {
                e.OrganizationUnitKey,
                e.SourceSystemId,
                e.ExternalPersonType
            })
            .Distinct()
            .OrderBy(t => t.OrganizationUnitKey)
            .ThenBy(t => t.SourceSystemId)
            .ThenBy(t => t.ExternalPersonType)
            .ToListAsync(ct);

        var masks = await _context.UrlMasks.ToListAsync(ct);
        var maskDict = masks.ToDictionary(
            m => (m.OrganizationUnitKey, m.SourceSystemId, m.ExternalPersonType),
            m => m);

        return triads.Select(t =>
        {
            var key = (t.OrganizationUnitKey ?? "", t.SourceSystemId, t.ExternalPersonType ?? "");
            var hasMask = maskDict.TryGetValue(key, out var mask);
            return new TriadInfo
            {
                OrganizationUnitKey = t.OrganizationUnitKey ?? "",
                SourceSystemId = t.SourceSystemId,
                ExternalPersonType = t.ExternalPersonType ?? "",
                HasMask = hasMask,
                UrlPattern = mask?.UrlPattern
            };
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<UrlMask?> GetMaskAsync(string organizationUnitKey, string sourceSystemId, string externalPersonType, CancellationToken ct)
    {
        return await _context.UrlMasks
            .FirstOrDefaultAsync(m =>
                m.OrganizationUnitKey == organizationUnitKey &&
                m.SourceSystemId == sourceSystemId &&
                m.ExternalPersonType == externalPersonType, ct);
    }

    /// <inheritdoc/>
    public async Task SaveMaskAsync(UrlMask mask, CancellationToken ct)
    {
        var existing = await _context.UrlMasks
            .FirstOrDefaultAsync(m =>
                m.OrganizationUnitKey == mask.OrganizationUnitKey &&
                m.SourceSystemId == mask.SourceSystemId &&
                m.ExternalPersonType == mask.ExternalPersonType, ct);

        if (existing is not null)
        {
            existing.UrlPattern = mask.UrlPattern;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            mask.Id = Guid.NewGuid();
            mask.CreatedAt = DateTime.UtcNow;
            mask.UpdatedAt = DateTime.UtcNow;
            _context.UrlMasks.Add(mask);
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteMaskAsync(Guid id, CancellationToken ct)
    {
        var mask = await _context.UrlMasks.FindAsync([id], ct);
        if (mask is null)
            return false;

        _context.UrlMasks.Remove(mask);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc/>
    public string BuildUrl(string urlPattern, string externalPersonId)
    {
        return urlPattern
            .Replace("{external_person_id}", externalPersonId)
            .Replace("{source_system_id}", "")
            .Replace("{organization_unit_key}", "");
    }
}
