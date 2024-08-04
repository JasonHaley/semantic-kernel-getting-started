namespace PropertyGraph.Common;

public class PropertyGraphOptions
{
    public const string PropertyGraph = "PropertyGraph";

    public bool UseTokenSplitter { get; set; } = true;
    public int? ChunkSize { get; set; }
    public int? Overlap { get; set; }

    public string? EntityTypes { get; set; }
    public string? RelationshipTypes { get; set; }
    public string? EntityExtractonTemplatePreamble { get; set; }
    public string? DocumentChunkTypeLabel { get; set; }
    public int? MaxTripletsPerChunk { get; set; }
    public int? MaxKeywords { get; set; }

    public bool IncludeEntityTextSearch { get; set; } = true;
    public bool UseKeywords { get; set; } = true;
    public string TypeEntityTextOfSearch { get; set; } = "FULL_TEXT"; // "VECTOR"
    public bool IncludeTriplets { get; set; } = true;
    public int MaxTriplets { get; set; } = 30;
    public bool IncludeRelatedChunks { get; set; } = true;
    public int MaxRelatedChunks { get; set; } = 3;

    public bool IncludeChunkVectorSearch { get; set; } = true;
    public int MaxChunks { get; set; } = 5;
}
