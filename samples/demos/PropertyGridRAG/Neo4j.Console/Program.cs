﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Neo4j.Console;
using System.CommandLine;
using static Neo4j.Console.CommandOptions;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Console.PropertyGraph;

internal class Program
{
    static void Main(string[] args)
    {
        MainAsync(args).Wait();
    }

    static async Task<int> MainAsync(string[] args)
    {
        var config = Configuration.ConfigureAppSettings();

        // Get Settings (all this is just so I don't have hard coded config settings here)
        var openAiSettings = new OpenAIOptions();
        config.GetSection(OpenAIOptions.OpenAI).Bind(openAiSettings);

        var neo4jSettings = new Neo4jOptions();
        config.GetSection(Neo4jOptions.Neo4j).Bind(neo4jSettings);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);

            builder.AddConfiguration(config);
            builder.AddConsole();
        });

        // Configure Semantic Kernel
        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddChatCompletionService(openAiSettings);

        Kernel kernel = builder.Build();

        var rootCommand = CommandOptions.RootCommand;

        rootCommand.SetHandler(
            async (context) =>
            {
                var options = CommandOptions.GetParsedAppOptions(context, kernel, openAiSettings, neo4jSettings, loggerFactory);
                if (options.RemoveAll)
                {
                    await RemoveAllNodes(options);
                }
                else
                {
                    Matcher matcher = new();
                    matcher.AddInclude(options.Files);

                    var results = matcher.Execute(
                        new DirectoryInfoWrapper(
                            new DirectoryInfo(Directory.GetCurrentDirectory())));

                    var files = results.HasMatches
                        ? results.Files.Select(f => f.Path).ToArray()
                        : Array.Empty<string>();

                    context.Console.WriteLine($"Processing {files.Length} files...");

                    var tasks = Enumerable.Range(0, files.Length)
                       .Select(i =>
                       {
                           var fileName = files[i];
                           return ProcessSingleFileAsync(options, fileName);
                       });

                    await Task.WhenAll(tasks);
                }

                Console.WriteLine("Done.");
            });
        return await rootCommand.InvokeAsync(args);
    }

    static async Task ProcessSingleFileAsync(AppOptions options, string fileName)
    {
        if (options.Verbose)
        {
            options.Console.WriteLine($"Processing '{fileName}'");
        }

        if (options.Remove)
        {
            // TODO: Implement ability to just remove entities and relationships for a specified document
            //await GetAllNodesAndRelationshipsForDocumentCountsAsync(options, fileName);

            return;
        }

        var service = new Neo4jService(options);
        await service.PopulateGraphFromDocumentAsync(fileName);

        if (options.Verbose)
        {
            // TODO: Add stopwatch timings
            options.Console.WriteLine($"'{fileName}' processing complete");
        }
    }

    static async Task RemoveAllNodes(AppOptions options)
    {
        var service = new Neo4jService(options);
        var counts = await service.GetAllNodesAndRelationshipsCountsAsync();

        Console.WriteLine($"There are {counts.Item1} nodes and {counts.Item2} relationships in the database.");
        Console.WriteLine("");
        Console.WriteLine("Are you sure you want to remove all nodes? Y/N");

        var response = Console.ReadLine();
        if (response != null && response.ToLower() == "y")
        {
            await service.RemoveAllNodesAsync();
        }
    }

}