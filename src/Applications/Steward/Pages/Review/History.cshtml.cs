using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Review;

/// <summary>
/// История разрешённых конфликтов.
/// </summary>
public class HistoryModel : PageModel
{
    private readonly IStewardService _stewardService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="HistoryModel"/>.
    /// </summary>
    public HistoryModel(IStewardService stewardService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
    }

    /// <summary>Список записей истории.</summary>
    public IReadOnlyList<ReviewHistoryItem> Items { get; private set; } = [];

    /// <summary>
    /// Загрузка списка.
    /// </summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Items = await _stewardService.GetReviewHistoryAsync(ct);
    }
}
