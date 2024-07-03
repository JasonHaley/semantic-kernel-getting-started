using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TextToSql.Console.Configuration;
using TextToSql.Console.Nl2Sql;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Services;
using System.Security.AccessControl;
using SemanticKernel.Data.Nl2Sql.Library;

internal class Program
{
    private const ConsoleColor ErrorColor = ConsoleColor.Magenta;
    private const ConsoleColor FocusColor = ConsoleColor.Yellow;
    private const ConsoleColor QueryColor = ConsoleColor.Green;
    private const ConsoleColor SystemColor = ConsoleColor.Cyan;

    private static readonly Dictionary<Type, int> s_typeWidths =
        new()
        {
            { typeof(bool), 5 },
            { typeof(int), 9 },
            { typeof(DateTime), 12 },
            { typeof(TimeSpan), 12 },
            { typeof(Guid), 8 },
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
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddConfiguration(config);
            builder.AddConsole();
        });

        // Configure Semantic Kernel
        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton(loggerFactory);
        //builder.AddChatCompletionService(openAiSettings);
        builder.AddChatCompletionService(openAiSettings, ApiLoggingLevel.ResponseAndRequest); // use this line to see the JSON between SK and OpenAI
        //builder.AddTextEmbeddingGeneration(openAiSettings);
        builder.AddTextEmbeddingGeneration(openAiSettings, ApiLoggingLevel.ResponseAndRequest);// use this line to see the JSON between SK and OpenAI

        var kernel = builder.Build();

        var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        var memoryBuilder = new MemoryBuilder();
        memoryBuilder.WithTextEmbeddingGeneration(embeddingService);
        memoryBuilder.WithMemoryStore(new VolatileMemoryStore());

        var memory = memoryBuilder.Build();

        var schemaLoader = new SqlScemaLoader(config);
        if (!await schemaLoader.TryLoadAsync(memory).ConfigureAwait(false))
        {
            WriteLine(ErrorColor, "Unable to load schema files");
        }

        WriteIntroduction(kernel, schemaLoader.SchemaNames);

        var userInput = System.Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(userInput) && userInput != "exit")
        {
            var queryGenerator = new SqlQueryGenerator(kernel, memory, 0.7);

            var result = await queryGenerator.SolveObjectiveAsync(userInput).ConfigureAwait(false);

        }
    }

    static void WriteIntroduction(Kernel kernel, IList<string> schemaNames)
    {
        WriteLine(SystemColor, $"I can translate your question into a SQL query for the following data schemas:{Environment.NewLine}");
        WriteLine(SystemColor, $"Model: {kernel.GetRequiredService<IChatCompletionService>().GetModelId()}{Environment.NewLine}");

        foreach (var schemaName in schemaNames)
        {
            WriteLine(SystemColor, $"- {schemaName}");
        }

        WriteLine(SystemColor, $"{Environment.NewLine}Type exit to quit.{Environment.NewLine}");
    }

    static void WriteLine(ConsoleColor? color = null, string? message = null, params string[] args)
    {
        Write(color ?? Console.ForegroundColor, message ?? string.Empty, args);

        Console.WriteLine();
    }
    static void Write(ConsoleColor color, string message, params string[] args)
    {
        var currentColor = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = color;
            if (args.Length == 0)
            {
                Console.Write(message);
            }
            else
            {
                Console.Write(message, args);
            }
        }
        finally
        {
            Console.ForegroundColor = currentColor;
        }
    }
}