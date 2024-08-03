using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using PropertyGraph.Common;
using Microsoft.SemanticKernel.ChatCompletion;
using PropertyGraphRAG;

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

        var neo4jSettings = new Neo4jOptions();
        config.GetSection(Neo4jOptions.Neo4j).Bind(neo4jSettings);

        var propertyGraphSettings = new PropertyGraphOptions();
        config.GetSection(PropertyGraphOptions.PropertyGraph).Bind(propertyGraphSettings);

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

        Kernel kernel = builder.Build();

        var appOptions = new DefaultOptions(kernel, openAiSettings, neo4jSettings, propertyGraphSettings, loggerFactory);
        
        ChatHistory chatHistory = new ChatHistory();
        PropertyGraphRetriever graphRAGRetriever = new PropertyGraphRetriever(appOptions);
        while (true)
        {
            Console.WriteLine("Enter User Message:");
            Console.WriteLine("");

            string? userMessage = Console.ReadLine();
            if (string.IsNullOrEmpty(userMessage) || userMessage.ToLower() == "exit")
            {
                return;
            }

            if (userMessage.ToLower() == "clear")
            {
                chatHistory.Clear();
            }

            await foreach (StreamingKernelContent update in Extensions.AddStreamingMessageAsync(chatHistory, await graphRAGRetriever.RetrieveAsync(userMessage)))
            {
                Console.Write(update);
            }

            Console.WriteLine("");
            Console.WriteLine("");
        }
    }
}
