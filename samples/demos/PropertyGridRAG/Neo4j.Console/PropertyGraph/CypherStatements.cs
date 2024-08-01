
namespace Neo4j.Console.PropertyGraph;

internal static class CypherStatements
{
    internal const string CREATE_VECTOR_INDEX = @"CREATE VECTOR INDEX CHUNK_EMBEDDING IF NOT EXISTS
                            FOR (c:DOCUMENT_CHUNK) ON c.embedding
                            OPTIONS {indexConfig: {
                           `vector.dimensions`: 1536,
                            `vector.similarity_function`: 'cosine'
                            }}";


    internal const string CREATE_FULLTEXT_INDEX = @"CREATE FULLTEXT INDEX ENTITY_TEXT IF NOT EXISTS 
                                FOR (n:ENTITY) ON EACH [n.text]";

    internal const string POPULATE_EMBEDDINGS = $@"
                            MATCH (n:DOCUMENT_CHUNK) WHERE n.text IS NOT NULL
                            WITH n, genai.vector.encode(
                                n.text,
                                'AzureOpenAI',
                                {{
                                    token: $token,
                                    resource: $resource,
                                    deployment: $deployment
                                }}) AS vector
                            CALL db.create.setNodeVectorProperty(n, 'embedding', vector)
                            ";

    internal const string DELETE_ALL_NODES = @"MATCH (n) DETACH DELETE n";

    internal const string NODE_COUNT = @"MATCH (n) RETURN count(n) as count";

    internal const string RELATIONSHIP_COUNT = @"MATCH ()-[r]->() RETURN count(r) as count";
}
