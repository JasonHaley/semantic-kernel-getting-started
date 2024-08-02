using Neo4j.Driver;
using Log = Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;

namespace PropertyGraph.Common;

public class Neo4jService
{
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
        var extractor = new TripletsExtractor(_options);
        var cypherText = await extractor.ExtractAsync(fileName);

        await PopulateGraphAsync(cypherText);

        await CreateVectorIndexAsync();

        await PopulateEmbeddingsAsync();

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
                    await tx.RunAsync(CypherStatements.POPULATE_EMBEDDINGS,
                        new
                        {
                            token = _options.OpenAI.ApiKey,
                            resource = _options.OpenAI.Resource,
                            deployment = _options.OpenAI.TextEmbeddingsDeploymentName
                        });
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

    public async Task<List<string>> FullTextSearchAsync(string text)
    {
        _logger.LogInformation($"Full text searc for {text} ...");

        await using var session = CreateAsyncSession();

        return await session.ExecuteReadAsync(
                async tx =>
                {
                    var triplets = new List<string>();

                    var reader = await tx.RunAsync(string.Format(CypherStatements.FULL_TEXT_SEARCH_FORMAT, text),
                                    new
                                    {
                                        token = _options.OpenAI.ApiKey,
                                        resource = _options.OpenAI.Resource,
                                        deployment = _options.OpenAI.TextEmbeddingsDeploymentName
                                    });

                    while (await reader.FetchAsync())
                    {
                        triplets.Add(reader.Current[0].ToString());
                    }
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

                    var reader = await tx.RunAsync(CypherStatements.VECTOR_SIMILARITY_SEARCH,
                                    new
                                    {
                                        question= text,
                                        token = _options.OpenAI.ApiKey,
                                        resource = _options.OpenAI.Resource,
                                        deployment = _options.OpenAI.TextEmbeddingsDeploymentName,
                                        top_k = 5
                                    });

                    while (await reader.FetchAsync())
                    {
                        triplets.Add(reader.Current[0].ToString());
                    }

                    return triplets;
                });
    }
}
