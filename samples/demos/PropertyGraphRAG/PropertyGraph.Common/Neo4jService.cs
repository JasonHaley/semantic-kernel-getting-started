using Neo4j.Driver;
using Log = Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using PropertyGraph.Common.Models;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection.PortableExecutable;

namespace PropertyGraph.Common;

public class Neo4jService
{
    private const string AZURE_OPENAI = "AzureOpenAI";

    private readonly IAppOptions _options;
    private readonly Log.ILogger _logger;

    public Neo4jService(IAppOptions options)
    {
        _options = options;
        _logger = _options.LoggerFactory.CreateLogger(nameof(Neo4jService));
    }

    private IAsyncSession CreateAsyncSession()
    {
        IAuthToken token = AuthTokens.Basic(_options.Neo4j.User, _options.Neo4j.Password);
        IDriver driver = GraphDatabase.Driver(_options.Neo4j.URI, token);

        QueryConfig config = new QueryConfig();
        return driver.AsyncSession();
    }

    public async Task PopulateGraphFromDocumentAsync(string fileName)
    {
        string cypherText;

        // TODO: Move to saving individiual results from LLM (down a level)
        var cacheFile = $"{fileName}.cypher";
        if (!File.Exists(cacheFile))
        {
            _logger.LogInformation("No cached file found.");

            var extractor = new TripletsExtractor(_options);
            cypherText = await extractor.ExtractAsync(fileName);

            File.WriteAllText(cacheFile, cypherText);
        }
        else
        {
            _logger.LogInformation("Loading cached file: {cacheFile}.", cacheFile);
            cypherText = File.ReadAllText(cacheFile);
        }

        await PopulateGraphAsync(cypherText);

        await CreateVectorIndexAsync();

        await PopulateEmbeddingsAsync();

        await CreateEntityVectorIndexAsync();

        await PopulateEntityEmbeddingsAsync();

        await CreateFullTextIndexAsync();
    }

    public async Task CreateVectorIndexAsync()
    {

        _logger.LogInformation($"Creating Vector Index ...");

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(CypherStatements.CREATE_VECTOR_INDEX);
        }
    }

    public async Task CreateEntityVectorIndexAsync()
    {

        _logger.LogInformation($"Creating Entity Vector Index ...");

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(CypherStatements.CREATE_ENTITY_VECTOR_INDEX);
        }
    }

    public async Task CreateFullTextIndexAsync()
    {
        _logger.LogInformation($"Creating Fulltext Index ...");

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(CypherStatements.CREATE_FULLTEXT_INDEX);
        }
    }

    public async Task PopulateEmbeddingsAsync()
    {
        _logger.LogInformation($"Populating Embeddings ...");

        using (var session = CreateAsyncSession())
        {
            // TODO: Add ability to use OpenAI only
            await session.ExecuteWriteAsync(
                async tx =>
                {
                    if (_options.OpenAI.Source == AZURE_OPENAI)
                    {
                        await tx.RunAsync(CypherStatements.POPULATE_EMBEDDINGS_AZURE_OPENAI,
                            new
                            {
                                token = _options.OpenAI.ApiKey,
                                resource = _options.OpenAI.Resource,
                                deployment = _options.OpenAI.TextEmbeddingsDeploymentName
                            });
                    }
                    else
                    {
                        await tx.RunAsync(CypherStatements.POPULATE_EMBEDDINGS_OPENAI,
                            new
                            {
                                token = _options.OpenAI.ApiKey
                            });
                    }
                });
        }
    }
    public async Task PopulateEntityEmbeddingsAsync()
    {
        _logger.LogInformation($"Populating Entity Text Embeddings ...");

        using (var session = CreateAsyncSession())
        {
            // TODO: Add ability to use OpenAI only
            await session.ExecuteWriteAsync(
                async tx =>
                {
                    if (_options.OpenAI.Source == AZURE_OPENAI)
                    {
                        await tx.RunAsync(CypherStatements.POPULATE_ENTITY_TEXT_EMBEDDINGS_AZURE_OPENAI,
                            new
                            {
                                token = _options.OpenAI.ApiKey,
                                resource = _options.OpenAI.Resource,
                                deployment = _options.OpenAI.TextEmbeddingsDeploymentName
                            });
                    }
                    else
                    {
                        await tx.RunAsync(CypherStatements.POPULATE_ENTITY_TEXT_EMBEDDINGS_OPENAI,
                            new
                            {
                                token = _options.OpenAI.ApiKey
                            });
                    }
                });
        }
    }

    public async Task PopulateGraphAsync(string entityCypherText)
    {
        // TODO: Add stopwatch timings
        _logger.LogInformation($"Populating graph ...");

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(entityCypherText);
        }
    }

    public async Task RemoveAllNodesAsync()
    {
        _logger.LogInformation($"Removing all nodes ...");

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(CypherStatements.DELETE_ALL_NODES);
        }
    }

    public async Task RemoveNodesAsync()
    {
        _logger.LogInformation($"Removing all nodes ...");

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(CypherStatements.DELETE_ALL_NODES);
        }
    }

    public async Task<(int, int)> GetAllNodesAndRelationshipsCountsAsync()
    {
        int nodes = 0;
        int relationshipts = 0;

        using (var session = CreateAsyncSession())
        {
            var nodeCountResult = await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync(CypherStatements.NODE_COUNT);
                return await result.ToListAsync(r => r[0].As<string>());
            });

            var relationshipCountResult = await session.ExecuteReadAsync(async tx =>
            {
                var result = await tx.RunAsync(CypherStatements.RELATIONSHIP_COUNT);
                return await result.ToListAsync(r => r[0].As<string>());
            });


            var nodeResult = nodeCountResult.FirstOrDefault();
            if (nodeResult != null)
            {
                int.TryParse(nodeResult, out nodes);
            }

            var relationshipResult = relationshipCountResult.FirstOrDefault();
            if (relationshipResult != null)
            {
                int.TryParse(relationshipResult, out relationshipts);
            }
        }
        return (nodes, relationshipts);
    }

    public async Task<List<TripletWithChunk>> FullTextSearchEntityTextWithChunksAsync(string text)
    {
        _logger.LogInformation($"Full text search for {text} ...");

        await using var session = CreateAsyncSession();

        return await session.ExecuteReadAsync(
                async tx =>
                {
                    var triplets = new List<TripletWithChunk>();

                    var cypherText = string.Format(_options.PropertyGraph.IncludeRelatedChunks ? CypherStatements.FULL_TEXT_SEARCH_WITH_CHUNKS_FORMAT : CypherStatements.FULL_TEXT_SEARCH_FORMAT, text.Trim('"'));

                    _logger.LogTrace(cypherText);

                    var reader = await tx.RunAsync(cypherText);
                    while (await reader.FetchAsync())
                    {
                        if (reader.Current[0] != null && reader.Current[1] != null && reader.Current[2] != null)
                        {
                            var triplet = reader.Current[0].ToString() ?? "";
                            var chunk = reader.Current[1].ToString() ?? "";
                            var score = Convert.ToDouble(reader.Current[2]);

                            triplets.Add(new(triplet!, chunk!, Convert.ToDouble(reader.Current[2])));
                        }
                    }

                    _logger.LogInformation($"{triplets.Count} items returned");

                    StringBuilder sb = new StringBuilder();
                    foreach(var triplet in triplets)
                    {
                        sb.AppendLine($"{triplet.triplet} {triplet.score}");
                    }
                    _logger.LogTrace(sb.ToString());

                    return triplets;
                });
    }

    public async Task<List<TripletWithChunk>> VectorSearchEntityTextWithChunksAsync(string text)
    {
        _logger.LogInformation($"Vector search for {text} ...");

        await using var session = CreateAsyncSession();

        return await session.ExecuteReadAsync(
                async tx =>
                {
                    var triplets = new List<TripletWithChunk>();

                    IResultCursor reader;
                    if (_options.OpenAI.Source == AZURE_OPENAI)
                    {
                        reader = await tx.RunAsync(_options.PropertyGraph.IncludeRelatedChunks ? CypherStatements.VECTOR_TEXT_SEARCH_WITH_CHUNKS_AZURE_OPENAI : CypherStatements.VECTOR_TEXT_SEARCH_WITHOUT_CHUNKS_AZURE_OPENAI,
                                       new
                                       {
                                           question = text,
                                           token = _options.OpenAI.ApiKey,
                                           resource = _options.OpenAI.Resource,
                                           deployment = _options.OpenAI.TextEmbeddingsDeploymentName,
                                           top_k = _options.PropertyGraph.MaxChunks
                                       });
                    }
                    else
                    {
                        reader = await tx.RunAsync(_options.PropertyGraph.IncludeRelatedChunks ? CypherStatements.VECTOR_TEXT_SEARCH_WITH_CHUNKS_OPENAI : CypherStatements.VECTOR_TEXT_SEARCH_WITHOUT_CHUNKS_OPENAI,
                                      new
                                      {
                                          question = text,
                                          token = _options.OpenAI.ApiKey,
                                          top_k = _options.PropertyGraph.MaxChunks
                                      });
                    }

                    while (await reader.FetchAsync())
                    {
                        if (reader.Current[0] != null && reader.Current[1] != null && reader.Current[2] != null)
                        {
                            var triplet = reader.Current[0].ToString() ?? "";
                            var chunk = reader.Current[1].ToString() ?? "";
                            var score = Convert.ToDouble(reader.Current[2]);

                            triplets.Add(new(triplet!, chunk!, Convert.ToDouble(reader.Current[2])));
                        }
                    }

                    _logger.LogInformation($"{triplets.Count} items returned");

                    StringBuilder sb = new StringBuilder();
                    foreach (var triplet in triplets)
                    {
                        sb.AppendLine($"{triplet.triplet} {triplet.score}");
                    }
                    _logger.LogTrace(sb.ToString());

                    return triplets;
                });
    }

    public async Task<List<string>> VectorSimularitySearchAsync(string text)
    {
        _logger.LogInformation($"Full text searc for {text} ...");

        await using var session = CreateAsyncSession();

        return await session.ExecuteReadAsync(
                async tx =>
                {
                    var triplets = new List<string>();

                    IResultCursor reader;

                    if (_options.OpenAI.Source == AZURE_OPENAI)
                    {
                        reader = await tx.RunAsync(CypherStatements.VECTOR_SIMILARITY_SEARCH_AZURE_OPENAI,
                                        new
                                        {
                                            question = text,
                                            token = _options.OpenAI.ApiKey,
                                            resource = _options.OpenAI.Resource,
                                            deployment = _options.OpenAI.TextEmbeddingsDeploymentName,
                                            top_k = _options.PropertyGraph.MaxChunks
                                        });
                    }
                    else
                    {
                        reader = await tx.RunAsync(CypherStatements.VECTOR_SIMILARITY_SEARCH_OPENAI,
                                        new
                                        {
                                            question = text,
                                            token = _options.OpenAI.ApiKey,
                                            top_k = _options.PropertyGraph.MaxChunks
                                        });
                    }

                    while (await reader.FetchAsync())
                    {
                        // TODO: add the score to return type
                        if (reader.Current != null && reader.Current[0] != null)
                        {
                            triplets.Add(reader.Current[0].ToString() ?? "");
                        }
                    }

                    _logger.LogInformation($"{triplets.Count} items returned");

                    return triplets;
                });
    }

    public async Task RemoveNodesWithSource(string fileName)
    {
        _logger.LogInformation($"Removing nodes connected to {fileName} ...");

        using (var session = CreateAsyncSession())
        {
            var cursor = await session.RunAsync(string.Format(CypherStatements.GET_DOCUMENT_NODE_FORMAT, fileName));
            if (cursor != null)
            {
                var result = await cursor.FetchAsync();
                if (result && cursor.Current != null)
                {
                    var documentId = cursor.Current[0].ToString();

                    await session.RunAsync(string.Format(CypherStatements.DELETE_ENTITY_NODES_FORMAT, documentId));
                    await session.RunAsync(string.Format(CypherStatements.DELETE_DOCUMENT_CHUNK_NODES_FORMAT, documentId));
                    await session.RunAsync(string.Format(CypherStatements.DELETE_DOCUMENT_NODE_FORMAT, documentId));
                }
            }
        }
    }   
}
