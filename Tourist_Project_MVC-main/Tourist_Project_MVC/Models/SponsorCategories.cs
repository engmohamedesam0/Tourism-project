using Microsoft.AspNetCore.Mvc.Rendering;

namespace Tourist_Project_MVC.Models
{
    // Single source of truth for the sponsor categories shown in the
    // category dropdown (Admin Sponsor Create/Edit and the sponsor's own
    // "complete profile" form). Keeps the values consistent across every
    // form, filter bar and branch auto-categorisation.
    public static class SponsorCategories
    {
        public static readonly string[] All =
        {
            "Cafe",
            "Restaurant",
            "Hotel",
            "Airline",
            "Tourism Agency",
            "Souvenir Shop",
            "Museum",
            "Transport",
            "Bank",
            "Other"
        };

        // Builds <option> items for a <select>, preserving the currently
        // selected value (used with asp-items on the Type fields).
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

        // Server-side guard used by the Create/Edit POST handlers so the
        // dropdown value is validated even when the form is posted directly.
        public static bool IsValid(string? type)
        {
            return !string.IsNullOrWhiteSpace(type) &&
                   All.Contains(type, System.StringComparer.OrdinalIgnoreCase);
        }
    }
}
