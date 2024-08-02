using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
namespace PropertyGraph.Common;

public class KeyworkExtractor
{
    private readonly IAppOptions _options;
    private readonly ILogger _logger;

    public KeyworkExtractor(IAppOptions options)
    {
        _options = options;
        _logger = _options.LoggerFactory.CreateLogger(nameof(KeyworkExtractor));
    }

    public async Task<IList<string>> ExtractAsync(string text)
    {
        var prompts = _options.Kernel.CreatePluginFromPromptDirectory("Prompts");

        var result = await _options.Kernel.InvokeAsync(
                prompts["ExtractKeywords"],
                new() {
                    { "maxKeywords", Defaults.MAX_KEYWORDS},
                    { "questionText", text },
                });

        _logger.LogTrace(result.ToString());
        
        return result.ToString().Split("~");
    }
}
