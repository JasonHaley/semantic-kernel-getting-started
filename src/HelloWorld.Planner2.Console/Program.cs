using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Plugin.Console.Plugins;
using HelloWorld.Plugin2.Console.Configuration;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.ChatCompletion;

internal class Program
{
    static void Main(string[] args)
    {
        MainAsync(args).Wait();
    }

    static async Task MainAsync(string[] args)
    {
#pragma warning disable SKEXP0060 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
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

        var options = new FunctionCallingStepwisePlannerOptions
        {
            MaxIterations = 15,
            MaxTokens = 4000,
        };
        var planner = new FunctionCallingStepwisePlanner(options);

        // TODO: CHALLENGE 1: does the AI respond accurately to this prompt? How to fix?
        var goal = $"Tell me an interesting fact from world about an event " +
                    $"that took place on today's date. " +
                    $"Be sure to mention the date in history for context.";
        
        //FunctionCallingStepwisePlannerResult result = await planner.ExecuteAsync(kernel, goal);

        ChatHistory steps = new ChatHistory();

        var originalRequest = @"Original request: Tell me an interesting fact from world about an event that took place on today&#39;s date. Be sure to mention the date in history for context.

You are in the process of helping the user fulfill this request using the following plan:
Step 1: Retrieve the current day using the function DailyFactPlugin-GetCurrentDay.
Step 2: Use the current day obtained in step 1 as input to the function DailyFactPlugin-GetDailyFact to get an interesting historic fact for the current date.
Step 3: Send the fact obtained in step 2 as the final answer using the function UserInteraction-SendFinalAnswer.

The user will ask you for help with each step.";
        steps.AddSystemMessage(originalRequest);

//        steps.AddUserMessage(goal);
//        steps.AddAssistantMessage(@"Step 1: Retrieve the current day using the DailyFactPlugin-GetCurrentDay function.
//Step 2: Use the retrieved current day as input to the DailyFactPlugin-GetDailyFact function to get an interesting historic fact for the current date.
//Step 3: Send the retrieved fact as the final answer using the UserInteraction-SendFinalAnswer function.");

        FunctionCallingStepwisePlannerResult result = await planner.ExecuteAsync(kernel, goal, steps);
                
        WriteLine($"\nRESPONSE: \n\n{result.FinalAnswer}");

#pragma warning restore SKEXP0060 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    static void WriteLine(string message)
    {
        Console.WriteLine("----------------------------------------------");

        Console.WriteLine(message);

        Console.WriteLine("----------------------------------------------");
    }
}