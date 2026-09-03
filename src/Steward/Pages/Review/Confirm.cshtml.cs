using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Review;

/// <summary>
/// POST-страница: подтверждение merge.
/// </summary>
public class ConfirmModel : PageModel
{
    private readonly IStewardService _stewardService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="ConfirmModel"/>.
    /// </summary>
    public ConfirmModel(IStewardService stewardService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
    }

    /// <summary>
    /// POST: merge personB → personA, обновить статус.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(Guid reviewId, CancellationToken ct)
    {
        var found = await _stewardService.ConfirmReviewAsync(reviewId, ct);
        if (!found)
            return NotFound();

        return RedirectToPage("/Index");
    }
}
