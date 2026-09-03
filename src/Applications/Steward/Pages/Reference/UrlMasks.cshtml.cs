using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Reference;

/// <summary>
/// Страница управления URL-масками для триад.
/// </summary>
public class UrlMasksModel : PageModel
{
    private readonly IUrlMaskService _urlMaskService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="UrlMasksModel"/>.
    /// </summary>
    public UrlMasksModel(IUrlMaskService urlMaskService)
    {
        _urlMaskService = urlMaskService ?? throw new ArgumentNullException(nameof(urlMaskService));
    }

    /// <summary>Список триад.</summary>
    public IReadOnlyList<TriadInfo> Triads { get; private set; } = [];

    /// <summary>
    /// Загрузка триад.
    /// </summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Triads = await _urlMaskService.GetTriadsAsync(ct);
    }

    /// <summary>
    /// Удаление маски.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(Guid maskId, CancellationToken ct)
    {
        await _urlMaskService.DeleteMaskAsync(maskId, ct);
        return RedirectToPage();
    }
}
