using Azure.AI.OpenAI;
using HelloWorld.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using System.Text.Json;

internal class Program
{
	static void Main(string[] args)
	{
		MainAsync(args).Wait();
	}

	private const string TravelPlannerName = "TravelPlanner";
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
        Include the source URL in the response.
        Only provide a single race per response.
        """;
	
	private const string HotelFinderName = "HotelFinder";
	private const string HotelFinderInstructions =
		"""
        You search the web for hotels closest to a location provided by the RaceFinder.
        Only provide a single hotel per response with its address.
        Include the source URL in the response.
        """;

	private const string InnerSelectionInstructions =
	   $$$"""
        Select which participant will take the next turn based on the conversation history.
        
        Only choose from these participants:
        - {{{TravelPlannerName}}}
        - {{{RaceFinderName}}}
        - {{{HotelFinderName}}}
        
        Choose the next participant according to the action of the most recent participant:
        - After user input, it is {{{RaceFinderName}}}'a turn.
        - After {{{HotelFinderName}}} replies with a hotel, it is {{{TravelPlannerName}}}'s turn.
        
        Respond in JSON format.  The JSON schema can include only:
        {
            "name": "string (the name of the assistant selected for the next turn)",
            "reason": "string (the reason for the participant was selected)"
        }

        History:
        {{${{{KernelFunctionSelectionStrategy.DefaultHistoryVariableName}}}}}
        """;

	private const string OuterTerminationInstructions =
		$$$"""
        Determine if both a half marathon race and hotel have been found.
        
        Respond in JSON format.  The JSON schema can include only:
        {
            "isAnswered": "bool (true if the user request has been fully answered)",
            "reason": "string (the reason for your determination)"
        }
        
        History:
        {{${{{KernelFunctionTerminationStrategy.DefaultHistoryVariableName}}}}}
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
			builder.SetMinimumLevel(LogLevel.Trace);

			builder.AddConfiguration(config);
			builder.AddConsole();
		});
		
		OpenAIPromptExecutionSettings jsonSettings = new() { ResponseFormat = ChatCompletionsResponseFormat.JsonObject };
		OpenAIPromptExecutionSettings invokeSettings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };

		// Define the agents: one of each type
		ChatCompletionAgent agentPlanner =
			new()
			{
				Instructions = PlannerInstructions,
				Name = TravelPlannerName,
				Kernel = CreateKernelWithChatCompletion(openAiSettings, pluginSettings, loggerFactory)
			};

		ChatCompletionAgent agentRaceFinder =
			new()
			{
				Instructions = RaceFinderInstructions,
				Name = RaceFinderName,
				Kernel = CreateKernelWithChatCompletion(openAiSettings, pluginSettings, loggerFactory),
				ExecutionSettings = invokeSettings
			};

		ChatCompletionAgent agentHotelFinder =
			new()
			{
				Instructions = HotelFinderInstructions,
				Name = HotelFinderName,
				Kernel = CreateKernelWithChatCompletion(openAiSettings, pluginSettings, loggerFactory),
				ExecutionSettings = invokeSettings
			};

		KernelFunction innerSelectionFunction = KernelFunctionFactory.CreateFromPrompt(InnerSelectionInstructions);
		KernelFunction outerTerminationFunction = KernelFunctionFactory.CreateFromPrompt(OuterTerminationInstructions);

		AggregatorAgent tripPlannerAgent =
			new(CreateChat)
			{
				Name = "TripPlanner",
				Mode = AggregatorMode.Nested,
			};
				
		AgentGroupChat chat =
			new(tripPlannerAgent)
			{
				ExecutionSettings =
					new()
					{
						TerminationStrategy =
							new KernelFunctionTerminationStrategy(outerTerminationFunction, CreateKernelWithChatCompletion(openAiSettings, pluginSettings, loggerFactory))
							{
								ResultParser =
									(result) =>
									{
										OuterTerminationResult? jsonResult = JsonResultTranslator.Translate<OuterTerminationResult>(result.GetValue<string>());

										return jsonResult?.isAnswered ?? false;
									},
								MaximumIterations = 5,
							},
					}
			};
				
		// Invoke chat and display messages.
		Console.WriteLine("What location and month would you like to find a race for?\n");
		var input = Console.ReadLine();

		chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));
		Console.WriteLine($"# {AuthorRole.User}: '{input}'");

		await foreach (var content in chat.InvokeAsync())
		{
			Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
		}

		Console.WriteLine($"# IS COMPLETE: {chat.IsComplete}");

		Console.WriteLine("\n\n>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
		Console.WriteLine(">>>> AGGREGATED CHAT");
		Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");

		await foreach (var content in chat.GetChatMessagesAsync(tripPlannerAgent).Reverse())
		{
			Console.WriteLine($">>>> {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
		}

		AgentGroupChat CreateChat() =>
				new(agentPlanner, agentRaceFinder, agentHotelFinder)
				{
					ExecutionSettings =
						new()
						{
							SelectionStrategy =
								new KernelFunctionSelectionStrategy(innerSelectionFunction, CreateKernelWithChatCompletion(openAiSettings, pluginSettings, loggerFactory))
								{
									ResultParser =
										(result) =>
										{
											AgentSelectionResult? jsonResult = JsonResultTranslator.Translate<AgentSelectionResult>(result.GetValue<string>());

											string? agentName = string.IsNullOrWhiteSpace(jsonResult?.name) ? null : jsonResult?.name;
											agentName ??= TravelPlannerName;

											Console.WriteLine($"\t>>>> INNER TURN: {agentName}");

											return agentName;
										}
								},
							TerminationStrategy =
								new AgentTerminationStrategy()
								{
									Agents = [agentPlanner],
									MaximumIterations = 7,
									AutomaticReset = true,
								},
						}
				};
	}
	
	static Kernel CreateKernelWithChatCompletion(OpenAIOptions openAiSettings, PluginOptions pluginSettings, ILoggerFactory loggerFactory)
	{
		var builder = Kernel.CreateBuilder();

		builder.Services.AddSingleton(loggerFactory);

		builder.AddChatCompletionService(openAiSettings, ApiLoggingLevel.None);
		builder.AddBingConnector2(pluginSettings, loggerFactory, ApiLoggingLevel.None);
		builder.Plugins.AddFromType<WebSearchEnginePlugin>();

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

sealed record OuterTerminationResult(bool isAnswered, string reason);

sealed record AgentSelectionResult(string name, string reason);

sealed class AgentTerminationStrategy : TerminationStrategy
{
	/// <inheritdoc/>
	protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}
}