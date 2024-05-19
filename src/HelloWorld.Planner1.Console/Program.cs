using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Plugin.Console.Plugins;
using HelloWorld.Plugin2.Console.Configuration;
using Microsoft.SemanticKernel.Planning.Handlebars;

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
        builder.AddChatCompletionService(openAiSettings);
        //builder.AddChatCompletionService(openAiSettings, ApiLoggingLevel.ResponseAndRequest); // use this line to see the JSON between SK and OpenAI
                
        // --------------------------------------------------------------------------------------
        // Exercise from Virtual Boston Azure for creating a prompt
        // --------------------------------------------------------------------------------------

        builder.Plugins.AddFromType<DailyFactPlugin>();
               
        Kernel kernel = builder.Build();

        var planner = new HandlebarsPlanner(new HandlebarsPlannerOptions() { AllowLoops = true });
                        
        // TODO: CHALLENGE 1: does the AI respond accurately to this prompt? How to fix?
        var goal = $"Tell me an interesting fact from world about an event " +
                    $"that took place on today's date. " +
                    $"Be sure to mention the date in history for context.";

        HandlebarsPlan? plan = null;
        var fileName = "SavedPlan.hbs";
        if (File.Exists(fileName))
        {
            // Load the saved plan
            var savedPlan = File.ReadAllText(fileName);
            
            // Populate intance
            plan = new HandlebarsPlan(savedPlan);
        }
        else
        {
            // Call LLM to create the plan 
            plan = await planner.CreatePlanAsync(kernel, goal);

            // Save for next run
            File.WriteAllText(fileName, plan.ToString());

            WriteLine($"\nPLAN: \n\n{plan}");
        }

        if (plan != null)
        {
            var result = await plan.InvokeAsync(kernel);

            WriteLine($"\nRESPONSE: \n\n{result}");
        }
    }

    static void WriteLine(string message)
    {
        Console.WriteLine("----------------------------------------------");

        Console.WriteLine(message);

        Console.WriteLine("----------------------------------------------");
    }
}