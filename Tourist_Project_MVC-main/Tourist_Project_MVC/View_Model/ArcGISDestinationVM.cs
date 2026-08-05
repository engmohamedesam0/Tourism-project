namespace Tourist_Project_MVC.View_Model;

public sealed record ArcGISFieldDefinition(
    string Name,
    string Type,
    string Alias,
    bool Nullable,
    bool Editable);

public sealed record ArcGISDestinationRecord(
    int? ObjectId,
    int? DatabaseId,
    Dictionary<string, object?> Attributes,
    double? Latitude,
    double? Longitude);

public sealed record ArcGISDestinationSnapshot(
    IReadOnlyList<ArcGISFieldDefinition> Fields,
    IReadOnlyList<ArcGISDestinationRecord> Records,
    string? Error = null)
{
    public bool Success => string.IsNullOrWhiteSpace(Error);
}
