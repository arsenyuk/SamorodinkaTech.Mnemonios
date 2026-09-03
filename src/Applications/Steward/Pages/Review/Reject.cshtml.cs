using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Review;

/// <summary>
/// POST-страница: отклонение — оставить записи раздельно.
/// </summary>
public class RejectModel : PageModel
{
    private readonly IStewardService _stewardService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="RejectModel"/>.
    /// </summary>
    public RejectModel(IStewardService stewardService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
    }

    /// <summary>
    /// POST: обновить статус на "rejected".
    /// </summary>
    public async Task<IActionResult> OnPostAsync(Guid reviewId, CancellationToken ct)
    {
        var found = await _stewardService.RejectReviewAsync(reviewId, ct);
        if (!found)
            return NotFound();

        return RedirectToPage("/Index");
    }
}
