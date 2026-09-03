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
    private readonly IUrlMaskService _urlMaskService;

    /// <summary>
    /// Создаёт новый экземпляр <see cref="PersonModel"/>.
    /// </summary>
    public PersonModel(IStewardService stewardService, IUrlMaskService urlMaskService)
    {
        _stewardService = stewardService ?? throw new ArgumentNullException(nameof(stewardService));
        _urlMaskService = urlMaskService ?? throw new ArgumentNullException(nameof(urlMaskService));
    }

    /// <summary>Данные персоны.</summary>
    public PersonData? Person { get; private set; }

    /// <summary>URL-ссылки для внешних идентификаторов.</summary>
    public Dictionary<(string, string, string), string> ExternalUrls { get; private set; } = new();

    /// <summary>
    /// Загрузка данных персоны.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(Guid masterId, CancellationToken ct)
    {
        Person = await _stewardService.GetPersonDataAsync(masterId, ct);
        if (Person is null)
            return NotFound();

        // Загрузить маски и сформировать URL для каждого внешнего идентификатора
        foreach (var ext in Person.ExternalIds)
        {
            var mask = await _urlMaskService.GetMaskAsync(
                ext.OrganizationUnitKey ?? "",
                ext.SourceSystemId,
                ext.ExternalPersonType ?? "", ct);

            if (mask is not null)
            {
                var url = _urlMaskService.BuildUrl(mask.UrlPattern, ext.ExternalPersonId);
                ExternalUrls[(ext.SourceSystemId, ext.ExternalPersonId, ext.ExternalPersonType ?? "")] = url;
            }
        }

        return Page();
    }
}
