using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Plugin.Console.Plugins;
using HelloWorld.Plugin2.Console.Configuration;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using HandlebarsDotNet;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;
using System.ComponentModel;
using System.Text.Json;

internal class Program
{
    private static string[] TEST_MESSAGES = new string[]
    {
        "What does Jason blog about?",
        "How many blogs has he written?",
        "How many blogs has Jason written?",
        "What blogs has Jason written?",
        "What has he written about Java?",
        "How about Python?",
        "What presentations has he given?",
        "Are all his blogs about AI in some way?",
        "What do you know about Code Camp?",
        "What has Jason mentioned about Boston Azure?",
        "Which blogs are about Semantic Kernel?",
        "What blogs are about LangChain?",
    };
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
            builder.SetMinimumLevel(LogLevel.Warning);

            builder.AddConfiguration(config);
            builder.AddConsole();
        });

        // Configure Semantic Kernel
        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton(loggerFactory);
        builder.AddChatCompletionService(config.GetConnectionString("OpenAI"));
        //builder.AddChatCompletionService(config.GetConnectionString("OpenAI"), ApiLoggingLevel.RequestOnly); // use this line to see the JSON between SK and OpenAI);

        builder.Plugins.AddFromType<AssistantTools>();

        OpenAIPromptExecutionSettings settings = new()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.0f
        };

        //var tools = new AssistantTools();

        Kernel kernel = builder.Build();
        //kernel.Plugins.AddFromObject(tools);

        IChatCompletionService chat = kernel.GetRequiredService<IChatCompletionService>();

        var sysMessage = @"Below is a history of the conversation so far, and a new question asked by the user that needs to be answered by searching a retriever system.
Generate a search query based on the conversation and the new question.
If you cannot generate a search query, return the original user question.
DO NOT return anything besides the query.";

        ChatHistory chatHistory = new ChatHistory(sysMessage);
        chatHistory.AddUserMessage("What does Jason blog about?");
        chatHistory.AddAssistantMessage("Jason blogs about xyz.");
        chatHistory.AddUserMessage("What is the name of his blog?");
        chatHistory.AddAssistantMessage("Jason's blog is named blog.");
        chatHistory.AddUserMessage("Does he blog often?");
        chatHistory.AddAssistantMessage("Yes Jason blogs 2 or 3 times a month.");
        //while (true)
        //{
        //Console.WriteLine("Enter User Message:");
        //Console.WriteLine("");

        //string? userMessage = Console.ReadLine();
        //if (string.IsNullOrEmpty(userMessage) || userMessage.ToLower() == "exit")
        //{
        //    return;
        //}

        //if (userMessage.ToLower() == "clear")
        //{
        //    chatHistory.Clear();
        //    continue;
        //}

        //chatHistory.AddUserMessage(userMessage);
        foreach (var userMessage in TEST_MESSAGES)
        {
            Console.WriteLine(userMessage);
            chatHistory.AddUserMessage(userMessage);
            await foreach (StreamingKernelContent update in chat.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel))
            {
                Console.Write(update);

                var json = JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
            }

            Console.WriteLine("");
            Console.WriteLine("");
        }
        //await foreach (StreamingKernelContent update in chat.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel))
        //{
        //    Console.Write(update);
        //}

        Console.WriteLine("");
        Console.WriteLine("");
        //}

    }

    public class AssistantTools
    {
        [KernelFunction, Description("Provides answers to questions about blogs.")]
        public async Task<string> SearchAsync([Description("User query")] string query)
        {
            return await Task.FromResult(query);
        }
    }
}