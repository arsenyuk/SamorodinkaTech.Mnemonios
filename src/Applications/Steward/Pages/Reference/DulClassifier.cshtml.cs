using Microsoft.AspNetCore.Mvc.RazorPages;
using Mnemonios.Domain.DTOs;
using Mnemonios.Domain.Validation;

namespace SamorodinkaTech.Mnemonios.Steward.Pages.Reference;

/// <summary>
/// Страница просмотра классификатора видов ДУЛ.
/// </summary>
public class DulClassifierModel : PageModel
{
    /// <summary>Категории классификатора.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<DulClassifierEntry>> ByCategory { get; private set; }
        = new Dictionary<string, IReadOnlyList<DulClassifierEntry>>();

    /// <summary>Загрузка классификатора.</summary>
    public void OnGet()
    {
        var classifier = DulClassifier.GetClassifier();
        ByCategory = classifier.ByCategory;
    }
}
