using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages;

/// <summary>
/// Главная страница: список pending-записей из person_review_queue.
/// </summary>
public class IndexModel : PageModel
{
    private readonly IStewardService _stewardService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="IndexModel"/>.
    /// </summary>
    public IndexModel(IStewardService stewardService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
    }

    /// <summary>Список записей на обработку.</summary>
    public IReadOnlyList<ReviewQueueItem> PendingReviews { get; private set; } = [];

    /// <summary>
    /// Загрузка pending-записей.
    /// </summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        PendingReviews = await _stewardService.GetPendingReviewsAsync(ct);
    }
}
