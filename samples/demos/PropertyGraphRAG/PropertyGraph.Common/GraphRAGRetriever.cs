using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using static System.Net.Mime.MediaTypeNames;

namespace PropertyGraph.Common;

public class GraphRAGRetriever
{
    private readonly IAppOptions _options;
    private readonly ILogger _logger;

    public GraphRAGRetriever(IAppOptions options)
    {
        _options = options;
        _logger = _options.LoggerFactory.CreateLogger(nameof(GraphRAGRetriever));
    }

    public async Task<string> RetrieveAsync(string userMessage)
    {
        var keywordExtractor = new KeyworkExtractor(_options);
        var keywords = await keywordExtractor.ExtractAsync(userMessage);

        var graphService = new Neo4jService(_options);

        var tripletList = new List<string>();
        foreach (var keyword in keywords)
        {
            tripletList.AddRange(await graphService.FullTextSearchAsync(keyword));
        }

        HashSet<string> uniqueNodes = new HashSet<string>();
        if (tripletList.Count > 0)
        {
            foreach (var triplet in tripletList)
            {
                if (!uniqueNodes.Contains(triplet))
                {
                    uniqueNodes.Add(triplet);
                }
            }
        }

        var chunkTexts = await graphService.VectorSimularitySearchAsync(userMessage);

        var prompts = _options.Kernel.CreatePluginFromPromptDirectory("Prompts");

        string context = $@"Structured data:
                {string.Join(Environment.NewLine, uniqueNodes.ToArray())}
                Unstructured data:
                {string.Join(Environment.NewLine, chunkTexts.ToArray())}";

        var result = await _options.Kernel.InvokeAsync(
                prompts["RequestWithContext"],
                new() {
                    { "context", context },
                    { "questionText", userMessage }
                });

        return result.ToString();
    }
}
