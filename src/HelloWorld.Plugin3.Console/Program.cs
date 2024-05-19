using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Plugin.Console.Plugins;
using HelloWorld.Plugin2.Console.Configuration;
using Microsoft.SemanticKernel.Connectors.OpenAI;

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

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);

            builder.AddConfiguration(config);
            builder.AddConsole();
        });

        // Configure Semantic Kernel
        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton(loggerFactory);
        //builder.AddChatCompletionService(openAiSettings);
        builder.AddChatCompletionService(openAiSettings, ApiLoggingLevel.ResponseAndRequest); // use this line to see the JSON between SK and OpenAI
                
        // --------------------------------------------------------------------------------------
        // Exercise from Virtual Boston Azure for creating a prompt
        // --------------------------------------------------------------------------------------

        builder.Plugins.AddFromType<DailyFactPlugin>();
               
        Kernel kernel = builder.Build();

        // TODO: CHALLENGE 1: does the AI respond accurately to this prompt? How to fix?
        var prompt = $"Tell me an interesting fact from world about an event " +
                    $"that took place on today's date. " +
                    $"Be sure to mention the date in history for context.";

        OpenAIPromptExecutionSettings settings = new() 
        { 
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, 
            Temperature = 0.7f,
            MaxTokens = 250
        };
                
        var funcresult = await kernel.InvokePromptAsync(prompt, new KernelArguments(settings));
        
        WriteLine($"\nRESPONSE: \n\n{funcresult}");
    }

    static void WriteLine(string message)
    {
        Console.WriteLine("----------------------------------------------");

        Console.WriteLine(message);

        Console.WriteLine("----------------------------------------------");
    }
}