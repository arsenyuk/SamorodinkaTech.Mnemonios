using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Review;

/// <summary>
/// Список мастер-записей, имеющих дефекты.
/// </summary>
public class DefectsModel : PageModel
{
    private readonly IStewardService _stewardService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="DefectsModel"/>.
    /// </summary>
    public DefectsModel(IStewardService stewardService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
    }

    /// <summary>Список записей с дефектами.</summary>
    public IReadOnlyList<PersonDefectsListItem> Items { get; private set; } = [];

    /// <summary>
    /// Загрузка списка.
    /// </summary>
    public async Task OnGetAsync(CancellationToken ct)
    {
        Items = await _stewardService.GetPersonsWithDefectsAsync(ct);
    }
}
