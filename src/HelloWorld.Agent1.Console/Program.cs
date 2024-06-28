using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Configuration;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

internal class Program
{
	static void Main(string[] args)
	{
		MainAsync(args).Wait();
	}

	private const string TravelPalnnerName = "TravelPlanner";
	private const string PlannerInstructions =
		"""
        You are a travel planner uses RaceFinder and HotelFinder to plan trip details.
        The goal is to find a half marathon race to run and a hotel close to the starting or finish line.
        If a half marathon race and hotel have been found, then it is complete.
        If not, provide more information on how to locate what is still missing.
        """;

	private const string RaceFinderName = "RaceFinder";
	private const string RaceFinderInstructions =
		"""
        You search the web for half marathon races in a location and month the user specifies.
        The goal is to find a half marathon race, its date, the location of its starting point and endpoint.
        Only provide a single race per response.
        """;
	
	private const string HotelFinderName = "HotelFinder";
	private const string HotelFinderInstructions =
		"""
        You search the web for hotels closest to a location provided by the RaceFinder.
        Only provide a single hotel per response with its address.
        """;

	static async Task MainAsync(string[] args)
	{
		var config = Configuration.ConfigureAppSettings();

		// Get Settings (all this is just so I don't have hard coded config settings here)
		var openAiSettings = new OpenAIOptions();
		config.GetSection(OpenAIOptions.OpenAI).Bind(openAiSettings);

		var pluginSettings = new PluginOptions();
		config.GetSection(PluginOptions.PluginConfig).Bind(pluginSettings);

		using var loggerFactory = LoggerFactory.Create(builder =>
		{
			builder.SetMinimumLevel(LogLevel.Information);

			builder.AddConfiguration(config);
			builder.AddConsole();
		});

		// Configure Semantic Kernel
		var builder = Kernel.CreateBuilder();

		builder.Services.AddSingleton(loggerFactory);
		//builder.AddChatCompletionService(openAiSettings);
		//builder.AddChatCompletionService(openAiSettings, ApiLoggingLevel.ResponseAndRequest); // use this line to see the JSON between SK and OpenAI

		// Define the agents: one of each type
		ChatCompletionAgent agentPlanner =
			new()
			{
				Instructions = PlannerInstructions,
				Name = TravelPalnnerName,
				Kernel = CreateKernelWithChatCompletion(openAiSettings, pluginSettings),
			};

		OpenAIAssistantAgent agentRaceFinder =
			await OpenAIAssistantAgent.CreateAsync(
				kernel: new(),
				config: new(openAiSettings.ApiKey),
				definition: new()
				{
					Instructions = RaceFinderInstructions,
					Name = RaceFinderName,
					ModelId = openAiSettings.ChatModelId,
				});

		OpenAIAssistantAgent agentHotelFinder = 
			await OpenAIAssistantAgent.CreateAsync(
				kernel: new(),
				config: new(openAiSettings.ApiKey),
				definition: new()
				{
					Instructions = HotelFinderInstructions,
					Name = HotelFinderName,
					ModelId = openAiSettings.ChatModelId,
				});

		// Create a chat for agent interaction.
		var chat =
			new AgentGroupChat(agentPlanner, agentRaceFinder, agentHotelFinder)
			{
				ExecutionSettings =
					new()
					{
						// Here a TerminationStrategy subclass is used that will terminate when
						// an assistant message contains the term "approve".
						TerminationStrategy =
							new ApprovalTerminationStrategy()
							{
								// Only the art-director may approve.
								Agents = [agentPlanner],
								// Limit total number of turns
								MaximumIterations = 5,
							}
					}
			};

		// Invoke chat and display messages.
		Console.WriteLine("What location and month would you like to find a race for?\n");
		string input = Console.ReadLine();
		chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));
		Console.WriteLine($"# {AuthorRole.User}: '{input}'");

		await foreach (var content in chat.InvokeAsync())
		{
			Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
		}

		Console.WriteLine($"# IS COMPLETE: {chat.IsComplete}");

		///////////----------------------------------------------
		
		//builder.AddBingConnector(pluginSettings);
		//builder.AddBingConnector(pluginSettings, ApiLoggingLevel.ResponseAndRequest); // use this line to see the JSON between SK and OpenAI

		//builder.Plugins.AddFromType<WebSearchEnginePlugin>();

		//Kernel kernel = builder.Build();

		//var prompt = "Who are the organizers for the Boston Azure meetup?";

		//WriteLine($"\nQUESTION: \n\n{prompt}");

		//OpenAIPromptExecutionSettings settings = new() 
		//{ 
		//    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, 
		//    Temperature = 0.7f,
		//    MaxTokens = 250
		//};

		//var funcresult = await kernel.InvokePromptAsync(prompt, new KernelArguments(settings));

		//WriteLine($"\nANSWER: \n\n{funcresult}");
	}
	
	static Kernel CreateKernelWithChatCompletion(OpenAIOptions openAIOptions, PluginOptions pluginSettings)
	{
		var builder = Kernel.CreateBuilder();

		//if (this.UseOpenAIConfig)
		//{

		var client = new HttpClient();// new RequestAndResponseLoggingHttpClientHandler());
		builder.AddOpenAIChatCompletion(openAIOptions.ChatModelId, openAIOptions.ApiKey, null, null, client);
		builder.AddBingConnector(pluginSettings);
		//}
		//else
		////{
		//	builder.AddAzureOpenAIChatCompletion(
		//		openAIOptions.ChatDeploymentName,
		//		openAIOptions.Endpoint,
		//		openAIOptions.ApiKey);
		//}

		return builder.Build();
	}
	static void WriteLine(string message)
	{
		Console.WriteLine("----------------------------------------------");

		Console.WriteLine(message);

		Console.WriteLine("----------------------------------------------");
	}
}

public static class JsonResultTranslator
{
	private const string LiteralDelimiter = "```";
	private const string JsonPrefix = "json";

	/// <summary>
	/// Utility method for extracting a JSON result from an agent response.
	/// </summary>
	/// <param name="result">A text result</param>
	/// <typeparam name="TResult">The target type of the <see cref="FunctionResult"/>.</typeparam>
	/// <returns>The JSON translated to the requested type.</returns>
	public static TResult? Translate<TResult>(string? result)
	{
		if (string.IsNullOrWhiteSpace(result))
		{
			return default;
		}

		string rawJson = ExtractJson(result);

		return JsonSerializer.Deserialize<TResult>(rawJson);
	}

	private static string ExtractJson(string result)
	{
		// Search for initial literal delimiter: ```
		int startIndex = result.IndexOf(LiteralDelimiter, System.StringComparison.Ordinal);
		if (startIndex < 0)
		{
			// No initial delimiter, return entire expression.
			return result;
		}

		startIndex += LiteralDelimiter.Length;

		// Accommodate "json" prefix, if present.
		if (JsonPrefix.Equals(result.Substring(startIndex, JsonPrefix.Length), System.StringComparison.OrdinalIgnoreCase))
		{
			startIndex += JsonPrefix.Length;
		}

		// Locate final literal delimiter
		int endIndex = result.IndexOf(LiteralDelimiter, startIndex, System.StringComparison.OrdinalIgnoreCase);
		if (endIndex < 0)
		{
			endIndex = result.Length;
		}

		// Extract JSON
		return result.Substring(startIndex, endIndex - startIndex);
	}
}
class ApprovalTerminationStrategy : TerminationStrategy
{
	// Terminate when the final message contains the term "approve"
	protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
		=> Task.FromResult(history[history.Count - 1].Content?.Contains("complete", StringComparison.OrdinalIgnoreCase) ?? false);
}