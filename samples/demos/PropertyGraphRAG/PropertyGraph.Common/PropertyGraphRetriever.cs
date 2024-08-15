using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PropertyGraph.Common.Models;

namespace PropertyGraph.Common;

public class PropertyGraphRetriever
{
    private readonly IAppOptions _options;
    private readonly ILogger _logger;
    private readonly KeywordExtractor _keyworkExtractor;
    private readonly Neo4jService _graphService;
    
    public PropertyGraphRetriever(IAppOptions options)
    {
        _options = options;
        _logger = _options.LoggerFactory.CreateLogger(nameof(PropertyGraphRetriever));
        _keyworkExtractor = new KeywordExtractor(options);
        _graphService = new Neo4jService(_options);
    }

    public async Task<IAsyncEnumerable<StreamingKernelContent>> RetrieveAsync(string userMessage)
    {
        string context = "";

        HashSet<string> uniqueNodes = new HashSet<string>();
        HashSet<string> chunks = new HashSet<string>();
        if (_options.PropertyGraph.IncludeEntityTextSearch)
        {
            var tripletList = new List<TripletWithChunk>();
            if (_options.PropertyGraph.UseKeywords)
            {
                var keywords = await _keyworkExtractor.ExtractAsync(userMessage);
                foreach (var keyword in keywords)
                {
                    _logger.LogInformation("Performing {searchType} search", _options.PropertyGraph.TypeEntityTextOfSearch);
                    _logger.LogInformation("Using keyword: {keyword}", keyword);

                    if (_options.PropertyGraph.TypeEntityTextOfSearch == "FULL_TEXT")
                    {
                        var textSearchResult = await _graphService.FullTextSearchEntityTextWithChunksAsync(keyword);

                        _logger.LogInformation("{resultCount} items found", textSearchResult.Count);

                        tripletList.AddRange(textSearchResult);
                    }
                    else if (_options.PropertyGraph.TypeEntityTextOfSearch == "VECTOR")
                    {
                        var vectorSearchResult = await _graphService.VectorSearchEntityTextWithChunksAsync(keyword);

                        _logger.LogInformation("{resultCount} items found", vectorSearchResult.Count);

                        tripletList.AddRange(vectorSearchResult);
                    }
                }
            }
            else
            {
                _logger.LogInformation("Performing {searchType} search", _options.PropertyGraph.TypeEntityTextOfSearch);
                _logger.LogInformation("Using text: {userMessage}", userMessage);

                if (_options.PropertyGraph.TypeEntityTextOfSearch == "FULL_TEXT")
                {
                    var textSearchResult = await _graphService.FullTextSearchEntityTextWithChunksAsync(userMessage);

                    _logger.LogInformation("{resultCount} items found", textSearchResult.Count);

                    tripletList.AddRange(textSearchResult);
                }
                else if (_options.PropertyGraph.TypeEntityTextOfSearch == "VECTOR")
                {
                    var vectorSearchResult = await _graphService.VectorSearchEntityTextWithChunksAsync(userMessage);

                    _logger.LogInformation("{resultCount} items found", vectorSearchResult.Count);

                    tripletList.AddRange(vectorSearchResult);
                }
            }

            if (tripletList.Count > 0)
            {
                var ordered = tripletList.OrderByDescending(p => p.score).ToList();
                foreach (var triplet in ordered)
                {
                    if (!uniqueNodes.Contains(triplet.triplet))
                    {
                        uniqueNodes.Add(triplet.triplet);
                        if (uniqueNodes.Count >= _options.PropertyGraph.MaxTriplets)
                        {
                            break;
                        }

                        if (_options.PropertyGraph.IncludeRelatedChunks && chunks.Count < _options.PropertyGraph.MaxRelatedChunks)
                        {
                            _logger.LogInformation("Addding chunk.");

                            chunks.Add(triplet.chunk);
                        }
                    }
                }
            }

            if (_options.PropertyGraph.IncludeTriplets && uniqueNodes.Count > 0)
            {
                context = $@"Information about relationships between important entities:
                    {string.Join(Environment.NewLine, uniqueNodes.ToArray())}";

                _logger.LogInformation("Context after nodes: {context}", context);
            }

            if (chunks.Count > 0)
            {
                context += $@"
                    Related text content that may hold important information:
                    {string.Join(Environment.NewLine, chunks.ToArray())}";

                _logger.LogInformation("Context after chunks: {context}", context);
            }
        }

        List<string> chunkTexts = new List<string>();
        if (_options.PropertyGraph.IncludeChunkVectorSearch)
        {
            chunkTexts = await _graphService.VectorSimularitySearchAsync(userMessage);

            var uniqueChunks = chunkTexts.Except(chunks);

            context += $@"
                More text content that may hold important information:
                {string.Join(Environment.NewLine, uniqueChunks.ToArray())}";

            _logger.LogInformation("Context after text chunks: {context}", context);
        }

        var prompts = _options.Kernel.CreatePluginFromPromptDirectory("Prompts");

        var prompt = prompts["RequestWithContext"];
        return prompt.InvokeStreamingAsync(_options.Kernel,                
                new() {
                    { "context", context },
                    { "questionText", userMessage }
                });
    }
}
