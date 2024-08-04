using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
                    if (_options.PropertyGraph.TypeEntityTextOfSearch == "FULL_TEXT")
                    {
                        tripletList.AddRange(await _graphService.FullTextSearchEntityTextWithChunksAsync(keyword));
                    }
                    else if (_options.PropertyGraph.TypeEntityTextOfSearch == "VECTOR")
                    {
                        tripletList.AddRange(await _graphService.VectorSearchEntityTextWithChunksAsync(keyword));
                    }
                }
            }
            else
            {
                if (_options.PropertyGraph.TypeEntityTextOfSearch == "FULL_TEXT")
                {
                    tripletList.AddRange(await _graphService.FullTextSearchEntityTextWithChunksAsync(userMessage));
                }
                else if (_options.PropertyGraph.TypeEntityTextOfSearch == "VECTOR")
                {
                    tripletList.AddRange(await _graphService.VectorSearchEntityTextWithChunksAsync(userMessage));
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
                            chunks.Add(triplet.chunk);
                        }
                    }
                }
            }

            if (_options.PropertyGraph.IncludeTriplets && uniqueNodes.Count > 0)
            {
                context = $@"Information about relationships between important entities:
                    {string.Join(Environment.NewLine, uniqueNodes.ToArray())}";
            }

            if (chunks.Count > 0)
            {
                context += $@"
                    Related text content that may hold important information:
                    {string.Join(Environment.NewLine, chunks.ToArray())}";
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
