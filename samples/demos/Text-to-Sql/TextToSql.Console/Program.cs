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
using SemanticKernel.Data.Nl2Sql.Library;
using System.Data;

internal class Program
{
    private const ConsoleColor ErrorColor = ConsoleColor.Magenta;
    private const ConsoleColor FocusColor = ConsoleColor.Yellow;
    private const ConsoleColor QueryColor = ConsoleColor.Green;
    private const ConsoleColor SystemColor = ConsoleColor.Cyan;

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
            //builder.SetMinimumLevel(LogLevel.Information);
            //builder.AddConfiguration(config);
            //builder.AddConsole();
        });

        // Configure Semantic Kernel
        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton(loggerFactory);
        builder.AddChatCompletionService(openAiSettings);
        //builder.AddChatCompletionService(openAiSettings, ApiLoggingLevel.ResponseAndRequest); // use this line to see the JSON between SK and OpenAI
        builder.AddTextEmbeddingGeneration(openAiSettings);
        //builder.AddTextEmbeddingGeneration(openAiSettings, ApiLoggingLevel.ResponseAndRequest);// use this line to see the JSON between SK and OpenAI

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

        while (true)
        {
            var userInput = System.Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(userInput) && userInput != "exit")
            {
                var queryGenerator = new SqlQueryGenerator(kernel, memory, 0.5);

                var result = await queryGenerator.SolveObjectiveAsync(userInput).ConfigureAwait(false);
                if (result == null)
                {
                    WriteLine(ErrorColor, "Could not find a schema that was semantically similiar to your request.");
                }
                else
                {
                    WriteLine(QueryColor, $"{Environment.NewLine}SQL generated:{Environment.NewLine}{result.Query}{Environment.NewLine}");
                    Write(SystemColor, "Executing...");

                    var sqlExecutor = new SqlCommandExecutor(schemaLoader.GetConnectionString(result.Schema));
                    var dataResult = await sqlExecutor.ExecuteAsync(result.Query);
                    if (dataResult.Count > 2)
                    {
                        ClearLine();
                        WriteLine();
                        WriteData(dataResult);
                        WriteLine();
                        var processedResult = await queryGenerator.DescribeResultsAsync(dataResult).ConfigureAwait(false);
                        WriteLine(SystemColor, $"{Environment.NewLine}{processedResult}");
                    }
                    else if (dataResult.Count == 2)
                    {
                        var processedResult = await queryGenerator.ProcessResultAsync(userInput, result.Query, dataResult).ConfigureAwait(false);
                        ClearLine();
                        WriteLine(SystemColor, $"{Environment.NewLine}{processedResult}");
                    }
                    else
                    {
                        ClearLine();
                    }
                }
            }
            else
            {
                return;
            }

            WriteLine(SystemColor, $"{Environment.NewLine}Do you have another question? Type exit to quit.{Environment.NewLine}");
        }
    }

#region Console Write Methods

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
    static void WriteData(List<List<string>> dataResult)
    {
        int maxPage = Console.WindowHeight - 10;
        var widths = GetWidths(dataResult[0]).ToArray();
        var isColumnTruncation = widths.Length < dataResult[0].Count;
        var rowFormatter = string.Join('│', widths.Select((width, index) => width == -1 ? $"{{{index}}}" : $"{{{index},-{width}}}"));

        WriteRow(dataResult[0]);

        WriteSeparator(widths);

        foreach (var row in dataResult.Skip(1))
        {
            WriteRow(row);
        }
        
        void WriteRow(IEnumerable<string> fields)
        {
            fields = TrimValues(fields).Concat(isColumnTruncation ? new[] { "..." } : Array.Empty<string>());

            WriteLine(SystemColor, rowFormatter, fields.ToArray());
        }
        IEnumerable<int> GetWidths(List<string> fields)
        {
            if (fields.Count == 1)
            {
                yield return -1;
                yield break;
            }

            int totalWidth = 0;

            for (int index = 0; index < fields.Count; ++index)
            {
                if (index == fields.Count - 1)
                {
                    // Last field gets remaining width
                    yield return -1;
                    yield break;
                }

                var width = 16;

                if (totalWidth + width > Console.WindowWidth - 11)
                {
                    yield break;
                }

                totalWidth += width;

                yield return width;
            }
        }
        IEnumerable<string> TrimValues(IEnumerable<string> fields)
        {
            int index = 0;
            int totalWidth = 0;

            foreach (var field in fields)
            {
                if (index >= widths.Length)
                {
                    yield break;
                }

                var width = widths[index];
                ++index;

                if (width == -1)
                {
                    var remainingWidth = Console.WindowWidth - totalWidth;

                    yield return TrimValue(field, remainingWidth);
                    yield break;
                }

                totalWidth += width + 1;

                yield return TrimValue(field, width);
            }
        }

        string TrimValue(string? value, int width)
        {
            value ??= string.Empty;

            if (value.Length <= width)
            {
                return value;
            }

            return string.Concat(value.AsSpan(0, width - 4), "...");
        }

        void WriteSeparator(int[] widths)
        {
            int totalWidth = 0;

            for (int index = 0; index < widths.Length; index++)
            {
                if (index > 0)
                {
                    Write(SystemColor, "┼");
                }

                var width = widths[index];

                Write(SystemColor, new string('─', width == -1 ? Console.WindowWidth - totalWidth : width));

                totalWidth += width + 1;
            }

            if (isColumnTruncation)
            {
                Write(SystemColor, "┼───");
            }

            WriteLine();
        }

    }
    static bool Confirm(string message)
    {
        Write(FocusColor, $"{message} (y/n) ");

        while (true)
        {
            var choice = Console.ReadKey(intercept: true);
            switch (char.ToUpperInvariant(choice.KeyChar))
            {
                case 'N':
                    Write(FocusColor, "N");
                    return false;
                case 'Y':
                    Write(FocusColor, "Y");
                    return true;
                default:
                    break;
            }
        }
    }
    static void ClearLine(bool previous = false)
    {
        if (previous)
        {
            --Console.CursorTop;
        }

        Console.CursorLeft = 0;
        Console.Write(new string(' ', Console.WindowWidth));
        Console.CursorLeft = 0;
    }
#endregion

}