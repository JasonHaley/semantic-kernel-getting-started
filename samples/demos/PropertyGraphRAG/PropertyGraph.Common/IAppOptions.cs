using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace PropertyGraph.Common;

public interface IAppOptions
{
    Kernel Kernel { get; }
    OpenAIOptions OpenAI { get; }
    Neo4jOptions Neo4j { get; }
    PropertyGraphOptions PropertyGraph { get; }
    ILoggerFactory LoggerFactory { get; }
}

public record class DefaultOptions(
    Kernel Kernel,
    OpenAIOptions OpenAI,
    Neo4jOptions Neo4j,
    PropertyGraphOptions PropertyGraph,
    ILoggerFactory LoggerFactory) : IAppOptions;