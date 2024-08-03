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
        if (_options.PropertyGraph.IncludeFulltextSearch)
        {
            var keywords = await _keyworkExtractor.ExtractAsync(userMessage);

            var tripletList = new List<TripletWithChunk>();
            foreach (var keyword in keywords)
            {
                tripletList.AddRange(await _graphService.FullTextSearchWithChunksAsync(keyword));
            }

            if (tripletList.Count > 0)
            {
                foreach (var triplet in tripletList)
                {
                    if (!uniqueNodes.Contains(triplet.triplet))
                    {
                        uniqueNodes.Add(triplet.triplet);
                        if (uniqueNodes.Count >= _options.PropertyGraph.MaxTriplets)
                        {
                            break;
                        }

                        if (_options.PropertyGraph.IncludeRelatedChuncks && chunks.Count < _options.PropertyGraph.MaxRelatedChunks)
                        {
                            chunks.Add(triplet.chunk);
                        }
                    }
                }
            }

            if (uniqueNodes.Count > 0)
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
        if (_options.PropertyGraph.IncludeVectorSearch)
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
