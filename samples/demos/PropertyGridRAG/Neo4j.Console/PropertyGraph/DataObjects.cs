﻿
namespace Neo4j.Console.PropertyGraph;

public record DocumentMetadata(string id, string source);
public record ChunkMetadata(string id, string name, int sequence, string documentId, string text);
public record TripletRow(string head, string head_type, string relation, string tail, string tail_type);
public class EntityMetadata
{
    public string name { get; set; }
    public string type { get; set; }
    public string id { get; set; }
    public string text { get; set; }
    public Dictionary<string, ChunkMetadata> mentionedInChunks { get; set; } = new Dictionary<string, ChunkMetadata>();
}

internal static class Defaults
{
    internal const string ENTITY_TYPES = "BLOG_POST,BOOK,MOVIE,PRESENTATION,EVENT,ORGANIZATION,PERSON,PLACE,PRODUCT,REVIEW,ACTION";
    internal const string RELATION_TYPES = "INTRODUCED,USED_FOR,WRITTEN_IN,PART_OF,LOCATED_IN,GIVEN,LIVES_IN,TRAVELED_TO";
    internal const int MAX_TRIPLETS_PER_CHUNK = 10;
}
