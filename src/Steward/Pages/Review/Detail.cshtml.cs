using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Review;

/// <summary>
/// Страница детального просмотра конфликта между двумя персонами.
/// </summary>
public class DetailModel : PageModel
{
    private readonly IStewardService _stewardService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="DetailModel"/>.
    /// </summary>
    public DetailModel(IStewardService stewardService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
    }

    /// <summary>Детали конфликта.</summary>
    public ConflictDetailDto? Detail { get; private set; }

    /// <summary>
    /// Загрузка данных конфликта.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid reviewId, CancellationToken ct)
    {
        Detail = await _stewardService.GetConflictDetailAsync(reviewId, ct);
        if (Detail is null)
            return NotFound();

        return Page();
    }
}
