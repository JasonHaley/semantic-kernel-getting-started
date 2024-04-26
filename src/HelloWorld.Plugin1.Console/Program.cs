using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Azure.AI.OpenAI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Plugin1.Console.Configuration;

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

        // Uncomment to add OpenTelemetry as a logging provider
        //using var meterProvider = Sdk.CreateMeterProviderBuilder()
        //    .AddMeter("Microsoft.SemanticKernel*")
        //    .AddConsoleExporter()
        //    .Build();

        #endregion

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);

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

        // --------------------------------------------------------------------------------------
        // Exercise from Virtual Boston Azure for creating a prompt
        // --------------------------------------------------------------------------------------

        Kernel kernel = builder.Build();

        // output today's date just for fun
        var today = DateTime.Now.ToString("MMMM dd");
        WriteLine($"Today is {today}");

        // Using a Prompt -----------------------------------------
        var prompts = kernel.CreatePluginFromPromptDirectory("Prompts");

        var result = await kernel.InvokeAsync(
            prompts["DailyFact"],
            new() {
                { "today", today },
            }
        );

        WriteLine($"\nRESPONSE: \n\n{result}");
    }

    static void WriteLine(string message)
    {
        Console.WriteLine("----------------------------------------------");

        Console.WriteLine(message);

        Console.WriteLine("----------------------------------------------");
    }
}