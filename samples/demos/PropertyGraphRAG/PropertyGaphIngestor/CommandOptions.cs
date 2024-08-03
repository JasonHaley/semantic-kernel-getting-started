
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PropertyGraph.Common;
using System.CommandLine;
using System.CommandLine.Invocation;

internal static class CommandOptions
{
    internal static readonly Argument<string> Files = new(name: "-f", description: "Files or directory to be processed");

    internal static readonly Option<bool> Help = new(name: "-h", description: "List commands");

    internal static readonly Option<bool> Verbose = new(name: "-v", description: "Writes out verbose logging to console.");

    internal static readonly Option<bool> Remove = new(name: "-r", description: "Remove all nodes and relations for a specified file.");

    internal static readonly Option<bool> RemoveAll = new(name: "-ra", description: "Removes all nodes and relations in the database.");

    internal static readonly RootCommand RootCommand = new(description: """
        Prepare documents by extracting knowledge graph triplets (<entity> -> <relation> -> <entity>) using OpenAI and
        inserting the entities and relations into Neo4j.
        """)
        {
            Files,
            Help,
            Verbose,
            Remove,
            RemoveAll,
        };
    internal static AppOptions GetParsedAppOptions(InvocationContext context, Kernel kernel, OpenAIOptions openAIOptions, Neo4jOptions neo4JOptions, PropertyGraphOptions propertyGraph, ILoggerFactory loggerFactory) => new(
            Files: context.ParseResult.GetValueForArgument(Files),
            Help: context.ParseResult.GetValueForOption(Help),
            Remove: context.ParseResult.GetValueForOption(Remove),
            RemoveAll: context.ParseResult.GetValueForOption(RemoveAll),
            Verbose: context.ParseResult.GetValueForOption(Verbose),
            Console: context.Console,
            Kernel: kernel,
            OpenAI: openAIOptions,
            Neo4j: neo4JOptions,
            PropertyGraph : propertyGraph,
            LoggerFactory: loggerFactory );

    internal record class AppOptions(
        string Files,
        bool Help,
        bool Verbose,
        bool Remove,
        bool RemoveAll,
        IConsole Console,
        Kernel Kernel,
        OpenAIOptions OpenAI,
        Neo4jOptions Neo4j,
        PropertyGraphOptions PropertyGraph,
        ILoggerFactory LoggerFactory) : AppConsole(Console), IAppOptions;

    internal record class AppConsole(IConsole Console);
}
