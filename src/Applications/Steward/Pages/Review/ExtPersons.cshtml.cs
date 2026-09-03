using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mnemonios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Review;

/// <summary>
/// Страница просмотра staging-записей ext_persons для данной персоны.
/// </summary>
public class ExtPersonsModel : PageModel
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="ExtPersonsModel"/>.
    /// </summary>
    public ExtPersonsModel(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Список staging-записей.</summary>
    public IReadOnlyList<ExtPersonRow> ExtPersons { get; private set; } = [];

    /// <summary>Идентификатор персоны.</summary>
    public Guid PersonId { get; private set; }

    /// <summary>
    /// Загрузка staging-записей.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid personId, CancellationToken ct)
    {
        PersonId = personId;

        var extPersons = await _context.ExtPersons
            .Where(ep => ep.MasterId == personId)
            .OrderByDescending(ep => ep.CreatedAt)
            .ToListAsync(ct);

        var extPersonIds = extPersons.Select(ep => ep.Id).ToList();
        var extDefects = await _context.ExtPersonDefects
            .Where(ed => extPersonIds.Contains(ed.ExtPersonId))
            .ToListAsync(ct);
        var defectsByExtPerson = extDefects
            .GroupBy(ed => ed.ExtPersonId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var extLinks = await _context.PersonExternalIds
            .Where(e => extPersonIds.Contains(e.ExtPersonId!.Value))
            .ToListAsync(ct);
        var linksByExtPerson = extLinks
            .GroupBy(e => e.ExtPersonId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        ExtPersons = extPersons.Select(ep =>
        {
            defectsByExtPerson.TryGetValue(ep.Id, out var defects);
            linksByExtPerson.TryGetValue(ep.Id, out var links);
            return new ExtPersonRow
            {
                Id = ep.Id,
                SourceSystemId = ep.SourceSystemId,
                ExternalPersonId = ep.ExternalPersonId,
                ExternalPersonType = ep.ExternalPersonType,
                CreatedAt = ep.CreatedAt,
                ProcessedAt = ep.ProcessedAt,
                KeyInn = ep.KeyInn,
                KeySnils = ep.KeySnils,
                KeyDul = ep.KeyDul,
                KeyInnFio = ep.KeyInnFio,
                KeySnilsFio = ep.KeySnilsFio,
                KeyDulFio = ep.KeyDulFio,
                ExternalLinks = (links ?? []).Select(l => new ExternalLinkRow
                {
                    SourceSystemId = l.SourceSystemId,
                    ExternalPersonId = l.ExternalPersonId,
                    ExternalPersonType = l.ExternalPersonType,
                    OrganizationUnitKey = l.OrganizationUnitKey
                }).ToList(),
                Defects = (defects ?? []).Select(d => new ExtPersonDefectRow
                {
                    DefectType = d.DefectType,
                    DefectMessage = d.DefectMessage,
                    FieldName = d.FieldName
                }).ToList()
            };
        }).ToList();

        return Page();
    }
}

/// <summary>
/// Строка staging-записи для отображения.
/// </summary>
public record ExtPersonRow
{
    public Guid Id { get; init; }
    public string SourceSystemId { get; init; } = string.Empty;
    public string ExternalPersonId { get; init; } = string.Empty;
    public string? ExternalPersonType { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public string? KeyInn { get; init; }
    public string? KeySnils { get; init; }
    public string? KeyDul { get; init; }
    public string? KeyInnFio { get; init; }
    public string? KeySnilsFio { get; init; }
    public string? KeyDulFio { get; init; }
    public IReadOnlyList<ExternalLinkRow> ExternalLinks { get; init; } = [];
    public IReadOnlyList<ExtPersonDefectRow> Defects { get; init; } = [];
}

/// <summary>
/// Строка внешней ссылки.
/// </summary>
public record ExternalLinkRow
{
    public string SourceSystemId { get; init; } = string.Empty;
    public string ExternalPersonId { get; init; } = string.Empty;
    public string? ExternalPersonType { get; init; }
    public string? OrganizationUnitKey { get; init; }
}

/// <summary>
/// Строка дефекта staging-записи.
/// </summary>
public record ExtPersonDefectRow
{
    public string DefectType { get; init; } = string.Empty;
    public string DefectMessage { get; init; } = string.Empty;
    public string? FieldName { get; init; }
}
