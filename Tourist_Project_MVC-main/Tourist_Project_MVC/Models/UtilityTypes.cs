using Microsoft.AspNetCore.Mvc.Rendering;

namespace Tourist_Project_MVC.Models
{
    // Single source of truth for the utility categories shown in the
    // Utility Create/Edit dropdown and the public type filter bar.
    public static class UtilityTypes
    {
        public static readonly string[] All =
        {
            "Police Station",
            "Fire Station",
            "Hospital",
            "Pharmacy"
        };

        public static List<SelectListItem> ToSelectList(string? selected = null)
        {
            return All
                .Select(c => new SelectListItem
                {
                    Value = c,
                    Text = c,
                    Selected = string.Equals(c, selected, System.StringComparison.OrdinalIgnoreCase)
                })
                .ToList();
        }

        public static bool IsValid(string? type)
        {
            return !string.IsNullOrWhiteSpace(type) &&
                   All.Contains(type, System.StringComparer.OrdinalIgnoreCase);
        }
    }
}
