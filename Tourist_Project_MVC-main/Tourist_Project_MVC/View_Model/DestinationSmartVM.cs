namespace Tourist_Project_MVC.View_Model;

public sealed record DestinationSmartRow(
    ArcGISDestinationRecord Feature,
    Tourist_Project_MVC.Models.Destination? DatabaseRecord);

public sealed class DestinationSmartIndexVM
{
    public IReadOnlyList<ArcGISFieldDefinition> Fields { get; init; } = Array.Empty<ArcGISFieldDefinition>();
    public IReadOnlyList<DestinationSmartRow> Records { get; init; } = Array.Empty<DestinationSmartRow>();
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Category { get; init; }
    public string? Field { get; init; }
    public string? Filter { get; init; }
    public string Sort { get; init; } = "ObjectId";
    public string Direction { get; init; } = "asc";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalRecords { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
    public bool HasError { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> StatusValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CategoryValues { get; init; } = Array.Empty<string>();
}

public sealed class DestinationSmartDetailsVM
{
    public IReadOnlyList<ArcGISFieldDefinition> Fields { get; init; } = Array.Empty<ArcGISFieldDefinition>();
    public ArcGISDestinationRecord? Feature { get; init; }
    public Tourist_Project_MVC.Models.Destination? DatabaseRecord { get; init; }
    public string? Error { get; init; }
}

public sealed class DestinationSmartEditVM
{
    public int DatabaseId { get; init; }
    public int? ObjectId { get; init; }
    public IReadOnlyList<ArcGISFieldDefinition> Fields { get; init; } = Array.Empty<ArcGISFieldDefinition>();
    public Dictionary<string, string?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Error { get; init; }
}
