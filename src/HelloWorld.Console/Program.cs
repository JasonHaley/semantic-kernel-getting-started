using HelloWorld.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Azure.AI.OpenAI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

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

        // Uncomment to add OpenTelemetry as a logging provider
        //using var meterProvider = Sdk.CreateMeterProviderBuilder()
        //    .AddMeter("Microsoft.SemanticKernel*")
        //    .AddConsoleExporter()
        //    .Build();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);

            // Uncomment to add OpenTelemetry as a logging provider
            //builder.AddOpenTelemetry(options =>
            //{
            //    options.AddConsoleExporter();
            //    options.IncludeFormattedMessage = true;
            //});

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

        // TODO: CHALLENGE 1: does the AI respond accurately to this prompt? How to fix?
        var prompt = $"Tell me an interesting fact from world about an event " +
                    $"that took place on {today}. " +
                    $"Be sure to mention the date in history for context.";

        WriteLine($"\nPROMPT: \n\n{prompt}");
        WriteLine("----------------------------------------------");
                                
        IChatCompletionService chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory chatMessages = [];
        chatMessages.AddUserMessage(prompt);

        OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
        {
            Temperature = 0.7f,
            MaxTokens = 250
        };

        var result = await chatCompletionService.GetChatMessageContentsAsync(chatMessages, openAIPromptExecutionSettings, kernel);

        // Write out the result
        foreach (var content in result)
        {
            WriteLine($"\nRESPONSE:\n{content}");
        }

        Console.WriteLine("");
        Console.WriteLine("");
        Console.WriteLine("");
    }

    static void WriteLine(string message)
    {
        var currentColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        
        Console.WriteLine(message);
        
        Console.ForegroundColor = currentColor;
    }
}