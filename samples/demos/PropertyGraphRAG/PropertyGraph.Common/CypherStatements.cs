namespace PropertyGraph.Common;

public static class CypherStatements
{
    public const string CREATE_VECTOR_INDEX = @"CREATE VECTOR INDEX CHUNK_EMBEDDING IF NOT EXISTS
                            FOR (c:DOCUMENT_CHUNK) ON c.embedding
                            OPTIONS {indexConfig: {
                           `vector.dimensions`: 1536,
                            `vector.similarity_function`: 'cosine'
                            }}";

    public const string CREATE_ENTITY_VECTOR_INDEX = @"CREATE VECTOR INDEX TEXT_EMBEDDING IF NOT EXISTS
                            FOR (e:ENTITY) ON e.embedding
                            OPTIONS {indexConfig: {
                           `vector.dimensions`: 1536,
                            `vector.similarity_function`: 'cosine'
                            }}";

    public const string CREATE_FULLTEXT_INDEX = @"CREATE FULLTEXT INDEX ENTITY_TEXT IF NOT EXISTS 
                            FOR (n:ENTITY) ON EACH [n.text]";

    public const string POPULATE_EMBEDDINGS_AZURE_OPENAI = @"
                            MATCH (n:DOCUMENT_CHUNK) WHERE n.text IS NOT NULL
                            WITH n, genai.vector.encode(
                                n.text,
                                'AzureOpenAI',
                                {
                                    token: $token,
                                    resource: $resource,
                                    deployment: $deployment
                                }) AS vector
                            CALL db.create.setNodeVectorProperty(n, 'embedding', vector)
                            ";

    public const string POPULATE_EMBEDDINGS_OPENAI = @"
                            MATCH (n:DOCUMENT_CHUNK) WHERE n.text IS NOT NULL
                            WITH n, genai.vector.encode(
                                n.text,
                                'OpenAI',
                                {
                                    token: $token
                                }) AS vector
                            CALL db.create.setNodeVectorProperty(n, 'embedding', vector)
                            ";

    public const string POPULATE_ENTITY_TEXT_EMBEDDINGS_AZURE_OPENAI = @"
                            MATCH (n:ENTITY) WHERE n.text IS NOT NULL
                            WITH n, genai.vector.encode(
                                n.text,
                                'AzureOpenAI',
                                {
                                    token: $token,
                                    resource: $resource,
                                    deployment: $deployment
                                }) AS vector
                            CALL db.create.setNodeVectorProperty(n, 'embedding', vector)
                            ";

    public const string POPULATE_ENTITY_TEXT_EMBEDDINGS_OPENAI = @"
                            MATCH (n:ENTITY) WHERE n.text IS NOT NULL
                            WITH n, genai.vector.encode(
                                n.text,
                                'OpenAI',
                                {
                                    token: $token
                                }) AS vector
                            CALL db.create.setNodeVectorProperty(n, 'embedding', vector)
                            ";

    public const string DELETE_ALL_NODES = @"MATCH (n) DETACH DELETE n";

    public const string GET_DOCUMENT_NODE_FORMAT = @"MATCH (d:DOCUMENT {{source: '{0}'}}) RETURN (d.id)";
    public const string DELETE_ENTITY_NODES_FORMAT = @"MATCH (e:ENTITY {{documentId:'{0}'}}) DETACH DELETE (e)";
    public const string DELETE_DOCUMENT_CHUNK_NODES_FORMAT = @"MATCH (dc:DOCUMENT_CHUNK {{documentId:'{0}'}}) DETACH DELETE (dc)";
    public const string DELETE_DOCUMENT_NODE_FORMAT = @"MATCH (d:DOCUMENT {{id:'{0}'}}) DELETE (d)";

    public const string NODE_COUNT = @"MATCH (n) RETURN count(n) as count";

    public const string RELATIONSHIP_COUNT = @"MATCH ()-[r]->() RETURN count(r) as count";

    public const string FULL_TEXT_SEARCH_FORMAT = @"CALL db.index.fulltext.queryNodes(""ENTITY_TEXT"", ""{0}"")
                            YIELD node AS e1, score
                            MATCH (e1)-[r]-(e2:ENTITY)
                            RETURN '(' + COALESCE(e1.text,'') + ')-[:' + COALESCE(type(r),'') + ']->(' + COALESCE(e2.text,'') + ')' as triplet, '' as t, score";

    public const string FULL_TEXT_SEARCH_WITH_CHUNKS_FORMAT = @"CALL db.index.fulltext.queryNodes(""ENTITY_TEXT"", ""{0}"")
                            YIELD node AS e1, score
                            MATCH (e1)-[r]-(e2:ENTITY)-[r2:MENTIONED_IN]->(dc)
                            RETURN '(' + COALESCE(e1.text,'') + ')-[:' + COALESCE(type(r),'') + ']->(' + COALESCE(e2.text,'') + ')' as triplet, dc.text as t, score";

    public const string VECTOR_TEXT_SEARCH_WITHOUT_CHUNKS_AZURE_OPENAI = @"WITH genai.vector.encode(
                            $question,
                            'AzureOpenAI',
                            {
                                token: $token,
                                resource: $resource,
                                deployment: $deployment
                            }) AS question_embedding
                        CALL db.index.vector.queryNodes(
                            'TEXT_EMBEDDING',
                            $top_k, 
                            question_embedding
                            ) 
                        YIELD node AS e1, score
                        MATCH (e1)-[r]-(e2:ENTITY)
                        RETURN '(' + COALESCE(e1.text,'') + ')-[:' + COALESCE(type(r),'') + ']->(' + COALESCE(e2.text,'') + ')' as triplet, '' as t, score";

    public const string VECTOR_TEXT_SEARCH_WITHOUT_CHUNKS_OPENAI = @"WITH genai.vector.encode(
                            $question,
                            'OpenAI',
                            {
                                token: $token
                            }) AS question_embedding
                        CALL db.index.vector.queryNodes(
                            'TEXT_EMBEDDING',
                            $top_k, 
                            question_embedding
                            ) 
                        YIELD node AS e1, score
                        MATCH (e1)-[r]-(e2:ENTITY)
                        RETURN '(' + COALESCE(e1.text,'') + ')-[:' + COALESCE(type(r),'') + ']->(' + COALESCE(e2.text,'') + ')' as triplet, '' as t, score";

    public const string VECTOR_TEXT_SEARCH_WITH_CHUNKS_AZURE_OPENAI = @"WITH genai.vector.encode(
                            $question,
                            'AzureOpenAI',
                            {
                                token: $token,
                                resource: $resource,
                                deployment: $deployment
                            }) AS question_embedding
                        CALL db.index.vector.queryNodes(
                            'TEXT_EMBEDDING',
                            $top_k, 
                            question_embedding
                            ) 
                        YIELD node AS e1, score
                        MATCH (e1)-[r]-(e2:ENTITY)-[r2:MENTIONED_IN]->(dc)
                        RETURN '(' + COALESCE(e1.text,'') + ')-[:' + COALESCE(type(r),'') + ']->(' + COALESCE(e2.text,'') + ')' as triplet, dc.text as t, score";

    public const string VECTOR_TEXT_SEARCH_WITH_CHUNKS_OPENAI = @"WITH genai.vector.encode(
                            $question,
                            'OpenAI',
                            {
                                token: $token
                            }) AS question_embedding
                        CALL db.index.vector.queryNodes(
                            'TEXT_EMBEDDING',
                            $top_k, 
                            question_embedding
                            ) 
                        YIELD node AS e1, score
                        MATCH (e1)-[r]-(e2:ENTITY)-[r2:MENTIONED_IN]->(dc)
                        RETURN '(' + COALESCE(e1.text,'') + ')-[:' + COALESCE(type(r),'') + ']->(' + COALESCE(e2.text,'') + ')' as triplet, dc.text as t, score";

    public const string VECTOR_SIMILARITY_SEARCH_AZURE_OPENAI = @"WITH genai.vector.encode(
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
                        RETURN chunk.text, score";

    public const string VECTOR_SIMILARITY_SEARCH_OPENAI = @"WITH genai.vector.encode(
                            $question,
                            'OpenAI',
                            {
                                token: $token
                            }) AS question_embedding
                        CALL db.index.vector.queryNodes(
                            'CHUNK_EMBEDDING',
                            $top_k, 
                            question_embedding
                            ) YIELD node AS chunk, score 
                        RETURN chunk.text, score";

}
