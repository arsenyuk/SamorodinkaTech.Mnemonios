using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SamorodinkaTech.Mnemonios.Steward.Services;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Review;

/// <summary>
/// Страница просмотра мастер-записи персоны.
/// </summary>
public class PersonModel : PageModel
{
    private readonly IStewardService _stewardService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonModel"/>.
    /// </summary>
    public PersonModel(IStewardService stewardService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
    }

    /// <summary>Данные персоны.</summary>
    public PersonData? Person { get; private set; }

    /// <summary>
    /// Загрузка данных персоны.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid masterId, CancellationToken ct)
    {
        Person = await _stewardService.GetPersonDataAsync(masterId, ct);
        if (Person is null)
            return NotFound();

        return Page();
    }
}
