using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mnemonios.Domain.Entities;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Reference;

/// <summary>
/// Форма создания/редактирования URL-маски.
/// </summary>
public class UrlMaskEditModel : PageModel
{
    private readonly IUrlMaskService _urlMaskService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="UrlMaskEditModel"/>.
    /// </summary>
    public UrlMaskEditModel(IUrlMaskService urlMaskService)
    {
        _urlMaskService = urlMaskService ?? throw new ArgumentNullException(nameof(urlMaskService));
    }

    /// <summary>Ключ ЮЛ.</summary>
    [BindProperty(SupportsGet = true)]
    public string OrganizationUnitKey { get; set; } = string.Empty;

    /// <summary>Система.</summary>
    [BindProperty(SupportsGet = true)]
    public string SourceSystemId { get; set; } = string.Empty;

    /// <summary>Тип объекта.</summary>
    [BindProperty(SupportsGet = true)]
    public string ExternalPersonType { get; set; } = string.Empty;

    /// <summary>URL-маска.</summary>
    [BindProperty]
    public string UrlPattern { get; set; } = string.Empty;

    /// <summary>Существующая маска (для редактирования).</summary>
    public UrlMask? ExistingMask { get; private set; }

    /// <summary>
    /// Загрузка данных.
    /// </summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        ExistingMask = await _urlMaskService.GetMaskAsync(
            OrganizationUnitKey, SourceSystemId, ExternalPersonType, ct);

        if (ExistingMask is not null)
            UrlPattern = ExistingMask.UrlPattern;
    }

    /// <summary>
    /// Сохранение маски.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UrlPattern))
        {
            ModelState.AddModelError(nameof(UrlPattern), "URL-маска обязательна");
            return Page();
        }

        var mask = new UrlMask
        {
            OrganizationUnitKey = OrganizationUnitKey,
            SourceSystemId = SourceSystemId,
            ExternalPersonType = ExternalPersonType,
            UrlPattern = UrlPattern.Trim()
        };

        await _urlMaskService.SaveMaskAsync(mask, ct);
        return RedirectToPage("/Reference/UrlMasks");
    }
}
