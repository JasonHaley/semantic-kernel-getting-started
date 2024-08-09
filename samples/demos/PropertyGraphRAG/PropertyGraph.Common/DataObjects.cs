namespace PropertyGraph.Common;
public record DocumentMetadata(string id, string source);
public record ChunkMetadata(string id, string name, int sequence, string documentId, string text);
public record TripletRow(string head, string head_type, string relation, string tail, string tail_type);
public class EntityMetadata
{
    public string name { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public string id { get; set; } = string.Empty;
    public string text { get; set; } = string.Empty;
    public Dictionary<string, ChunkMetadata> mentionedInChunks { get; set; } = new Dictionary<string, ChunkMetadata>();
}

public static class Defaults
{
    public const int CHUNK_SIZE = 500;
    public const int OVERLAP = 100;
    public const string ENTITY_TYPES = "BLOG_POST,BOOK,MOVIE,PRESENTATION,EVENT,ORGANIZATION,PERSON,PLACE,PRODUCT,REVIEW,ACTION";
    public const string RELATION_TYPES = "INTRODUCED,USED_FOR,WRITTEN_IN,PART_OF,LOCATED_IN,GIVEN,LIVES_IN,TRAVELED_TO";
    public const string DOCUMENT_CHUNK_TYPE = "DOCUMENT_CHUNK";
    public const int MAX_TRIPLETS_PER_CHUNK = 10;
    public const int MAX_KEYWORDS = 10;
        
}
