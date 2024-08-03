namespace PropertyGraph.Common;

public class PropertyGraphOptions
{
    public const string PropertyGraph = "PropertyGraph";

    public int? ChunkSize { get; set; }
    public int? Overlap { get; set; }

    public string? EntityTypes { get; set; }
    public string? RelationshipTypes { get; set; }
    public int? MaxTripletsPerChunk { get; set; }
    public int? MaxKeywords { get; set; }
    
    public bool IncludeFulltextSearch { get; set; } = true;
    public bool IncludeTriplets { get; set; } = true;
    public int MaxTriplets { get; set; } = 30;
    public bool IncludeRelatedChuncks { get; set; } = true;
    public int MaxRelatedChunks { get; set; } = 3;

    public bool IncludeVectorSearch { get; set; } = true;
    public int MaxChunks { get; set; } = 5;
}
