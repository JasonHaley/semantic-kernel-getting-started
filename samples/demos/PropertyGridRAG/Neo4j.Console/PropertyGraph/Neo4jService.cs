using log = Microsoft.Extensions.Logging;
using Neo4j.Driver;
using static Neo4j.Console.CommandOptions;
using Microsoft.Extensions.Logging;

namespace Neo4j.Console.PropertyGraph;

internal class Neo4jService
{
    private readonly AppOptions _options;
    private readonly log.ILogger _logger;
    public Neo4jService(AppOptions options)
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
        if (_options.Verbose)
        {
            _logger.LogInformation($"Creating Vector Index ...");
        }

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(CypherStatements.CREATE_VECTOR_INDEX);
        }
    }

    public async Task CreateFullTextIndexAsync()
    {
        if (_options.Verbose)
        {
            _logger.LogInformation($"Creating Fulltext Index ...");
        }

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(CypherStatements.CREATE_FULLTEXT_INDEX);
        }
    }

    public async Task PopulateEmbeddingsAsync()
    {
        if (_options.Verbose)
        {
            _logger.LogInformation($"Populating Embeddings ...");
        }

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
        if (_options.Verbose)
        {
            // TODO: Add stopwatch timings
            _logger.LogInformation($"Populating graph ...");
        }

        using (var session = CreateAsyncSession())
        {
            await session.RunAsync(entityCypherText);
        }
    }

    public async Task RemoveAllNodesAsync()
    {
        if (_options.Verbose)
        {
            _logger.LogInformation($"Removing all nodes ...");
        }

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


}
