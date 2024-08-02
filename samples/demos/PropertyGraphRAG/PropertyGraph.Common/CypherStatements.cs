namespace PropertyGraph.Common;

public static class CypherStatements
{
    public const string CREATE_VECTOR_INDEX = @"CREATE VECTOR INDEX CHUNK_EMBEDDING IF NOT EXISTS
                            FOR (c:DOCUMENT_CHUNK) ON c.embedding
                            OPTIONS {indexConfig: {
                           `vector.dimensions`: 1536,
                            `vector.similarity_function`: 'cosine'
                            }}";


    public const string CREATE_FULLTEXT_INDEX = @"CREATE FULLTEXT INDEX ENTITY_TEXT IF NOT EXISTS 
                            FOR (n:ENTITY) ON EACH [n.text]";

    public const string POPULATE_EMBEDDINGS = @"
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

    public const string DELETE_ALL_NODES = @"MATCH (n) DETACH DELETE n";

    public const string NODE_COUNT = @"MATCH (n) RETURN count(n) as count";

    public const string RELATIONSHIP_COUNT = @"MATCH ()-[r]->() RETURN count(r) as count";

    public const string FULL_TEXT_SEARCH_FORMAT = @"CALL db.index.fulltext.queryNodes(""ENTITY_TEXT"", ""{0}"")
                            YIELD node AS e1
                            MATCH (e1)-[r]-(e2:ENTITY)
                            RETURN COALESCE(e1.text,'') + ' -> ' + COALESCE(type(r),'') + ' -> ' + COALESCE(e2.text,'')";

    public const string VECTOR_SIMILARITY_SEARCH = @"WITH genai.vector.encode(
                            $question,
                            'AzureOpenAI',
                            {
                                token: $token,
                                resource: $resource,
                                deployment: $deployment
                            }) AS question_embedding
                        CALL db.index.vector.queryNodes(
                            'CHUNK_EMBEDDING',
                            $top_k, 
                            question_embedding
                            ) YIELD node AS chunk, score 
                        RETURN chunk.text";

}
