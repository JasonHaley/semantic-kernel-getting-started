﻿using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Console.Configuration;

internal class Program
{
    static void Main(string[] args)
    {
        MainAsync(args).Wait();
    }

    static async Task MainAsync(string[] args)
    {
        var config = Configuration.ConfigureAppSettings();

        // Get Settings (all this is just so I don't have hard coded config settings here)
        var openAiSettings = new OpenAIOptions();
        config.GetSection(OpenAIOptions.OpenAI).Bind(openAiSettings);

        #region OpenTelemetry Logging Provider

        //// Uncomment to add OpenTelemetry as a logging provider
        //using var meterProvider = Sdk.CreateMeterProviderBuilder()
        //    .AddMeter("Microsoft.SemanticKernel*")
        //    .AddConsoleExporter()
        //    .Build();

        #endregion

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);

            #region OpenTelemetry Logging Provider

            // Uncomment to add OpenTelemetry as a logging provider
            //builder.AddOpenTelemetry(options =>
            //{
            //    options.AddConsoleExporter();
            //    options.IncludeFormattedMessage = true;
            //});

            #endregion

            builder.AddConfiguration(config);
            builder.AddConsole();
        });

        // Configure Semantic Kernel
        var builder = Kernel.CreateBuilder();
                
        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddChatCompletionService(openAiSettings);

        Kernel kernel = builder.Build();

        // --------------------------------------------------------------------------------------
        // Exercise from Virtual Boston Azure for creating a prompt
        // --------------------------------------------------------------------------------------

        // output today's date just for fun
        WriteLine($"\n----------------- DEBUG INFO -----------------");
        var today = DateTime.Now.ToString("MMMM dd");
        WriteLine($"Today is {today}");
        WriteLine("----------------------------------------------");

        IChatCompletionService chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        // TODO: CHALLENGE 1: does the AI respond accurately to this prompt? How to fix?
        var prompt = $"Tell me an interesting fact from world about an event " +
                    $"that took place on {today}. " +
                    $"Be sure to mention the date in history for context.";
                                
        OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
        {
            Temperature = 0.7f,
            MaxTokens = 250
        };

        var result = await chatCompletionService.GetChatMessageContentsAsync(prompt, openAIPromptExecutionSettings, kernel);

        WriteLine($"\nPROMPT: \n\n{prompt}");

        // Write out the result
        foreach (var content in result)
        {
            WriteLine($"\nRESPONSE:\n{content}");
        }
    }

    static void WriteLine(string message)
    {
        var currentColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        
        Console.WriteLine(message);
        
        Console.ForegroundColor = currentColor;
    }
}