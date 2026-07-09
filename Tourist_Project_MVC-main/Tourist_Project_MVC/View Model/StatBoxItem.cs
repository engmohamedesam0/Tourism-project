namespace Tourist_Project_MVC.View_Model
{
    // A single stat-box used by the reusable _StatBoxRow partial.
    // Color is an accent key (blue/green/gold/purple/teal/red/amber) that maps
    // to a CSS modifier class (.stat-<color>) defining the icon square tone.
    public class StatBoxItem
    {
        public string IconClass { get; set; } = string.Empty; // e.g. "bi-people-fill"
        public string Color { get; set; } = "gold";           // accent key
        public string Value { get; set; } = string.Empty;     // big number / short text
        public string Label { get; set; } = string.Empty;     // muted caption
    }
}
