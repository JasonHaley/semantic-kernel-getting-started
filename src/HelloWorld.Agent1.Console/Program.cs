using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using HelloWorld.Configuration;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Agents.Chat;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.Agents;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;

internal class Program
{
	static void Main(string[] args)
	{
		MainAsync(args).Wait();
	}

	protected static bool ForceOpenAI => true;

	private const string InternalLeaderName = "InternalLeader";
	private const string InternalLeaderInstructions =
		"""
        Your job is to clearly and directly communicate the current assistant response to the user.

        If information has been requested, only repeat the request.

        If information is provided, only repeat the information.

        Do not come up with your own shopping suggestions.
        """;

	private const string InternalGiftIdeaAgentName = "InternalGiftIdeas";
	private const string InternalGiftIdeaAgentInstructions =
		"""        
        You are a personal shopper that provides gift ideas.

        Only provide ideas when the following is known about the gift recipient:
        - Relationship to giver
        - Reason for gift

        Request any missing information before providing ideas.

        Only describe the gift by name.

        Always immediately incorporate review feedback and provide an updated response.
        """;

	private const string InternalGiftReviewerName = "InternalGiftReviewer";
	private const string InternalGiftReviewerInstructions =
		"""
        Review the most recent shopping response.

        Either provide critical feedback to improve the response without introducing new ideas or state that the response is adequate.
        """;

	private const string InnerSelectionInstructions =
		$$$"""
        Select which participant will take the next turn based on the conversation history.
        
        Only choose from these participants:
        - {{{InternalGiftIdeaAgentName}}}
        - {{{InternalGiftReviewerName}}}
        - {{{InternalLeaderName}}}
        
        Choose the next participant according to the action of the most recent participant:
        - After user input, it is {{{InternalGiftIdeaAgentName}}}'a turn.
        - After {{{InternalGiftIdeaAgentName}}} replies with ideas, it is {{{InternalGiftReviewerName}}}'s turn.
        - After {{{InternalGiftIdeaAgentName}}} requests additional information, it is {{{InternalLeaderName}}}'s turn.
        - After {{{InternalGiftReviewerName}}} provides feedback or instruction, it is {{{InternalGiftIdeaAgentName}}}'s turn.
        - After {{{InternalGiftReviewerName}}} states the {{{InternalGiftIdeaAgentName}}}'s response is adequate, it is {{{InternalLeaderName}}}'s turn.
                
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
        Determine if user request has been fully answered.
        
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
			builder.SetMinimumLevel(LogLevel.Information);

			builder.AddConfiguration(config);
			builder.AddConsole();
		});

		// Configure Semantic Kernel
		var builder = Kernel.CreateBuilder();

		builder.Services.AddSingleton(loggerFactory);
		//builder.AddChatCompletionService(openAiSettings);
		//builder.AddChatCompletionService(openAiSettings, ApiLoggingLevel.ResponseAndRequest); // use this line to see the JSON between SK and OpenAI

		OpenAIPromptExecutionSettings jsonSettings = new() { ResponseFormat = ChatCompletionsResponseFormat.JsonObject };
		//OpenAIPromptExecutionSettings autoInvokeSettings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };

		ChatCompletionAgent internalLeaderAgent = CreateAgent(InternalLeaderName, InternalLeaderInstructions);
		ChatCompletionAgent internalGiftIdeaAgent = CreateAgent(InternalGiftIdeaAgentName, InternalGiftIdeaAgentInstructions);
		ChatCompletionAgent internalGiftReviewerAgent = CreateAgent(InternalGiftReviewerName, InternalGiftReviewerInstructions);
		
		KernelFunction innerSelectionFunction = KernelFunctionFactory.CreateFromPrompt(InnerSelectionInstructions, jsonSettings);
		KernelFunction outerTerminationFunction = KernelFunctionFactory.CreateFromPrompt(OuterTerminationInstructions, jsonSettings);


		AggregatorAgent personalShopperAgent =
			new(CreateChat)
			{
				Name = "PersonalShopper",
				Mode = AggregatorMode.Nested,
			};

		AgentGroupChat chat =
			new(personalShopperAgent)
			{
				ExecutionSettings =
					new()
					{
						TerminationStrategy =
							new KernelFunctionTerminationStrategy(outerTerminationFunction, CreateKernelWithChatCompletion(openAiSettings))
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
		Console.WriteLine("\n######################################");
		Console.WriteLine("# DYNAMIC CHAT");
		Console.WriteLine("######################################");

		await InvokeChatAsync("Can you provide three original birthday gift ideas.  I don't want a gift that someone else will also pick.");

		await InvokeChatAsync("The gift is for my adult brother.");

		if (!chat.IsComplete)
		{
			await InvokeChatAsync("He likes photography.");
		}

		Console.WriteLine("\n\n>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
		Console.WriteLine(">>>> AGGREGATED CHAT");
		Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");

		await foreach (var content in chat.GetChatMessagesAsync(personalShopperAgent).Reverse())
		{
			Console.WriteLine($">>>> {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
		}

		async Task InvokeChatAsync(string input)
		{
			chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

			Console.WriteLine($"# {AuthorRole.User}: '{input}'");

			await foreach (var content in chat.InvokeAsync(personalShopperAgent))
			{
				Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");
			}

			Console.WriteLine($"\n# IS COMPLETE: {chat.IsComplete}");
		}
		
		ChatCompletionAgent CreateAgent(string agentName, string agentInstructions) =>
		   new()
		   {
			   Instructions = agentInstructions,
			   Name = agentName,
			   Kernel = CreateKernelWithChatCompletion(openAiSettings),
		   };

		AgentGroupChat CreateChat() =>
			   new(internalLeaderAgent, internalGiftReviewerAgent, internalGiftIdeaAgent)
			   {
				   ExecutionSettings =
					   new()
					   {
						   SelectionStrategy =
							   new KernelFunctionSelectionStrategy(innerSelectionFunction, CreateKernelWithChatCompletion(openAiSettings))
							   {
								   ResultParser =
									   (result) =>
									   {
										   AgentSelectionResult? jsonResult = JsonResultTranslator.Translate<AgentSelectionResult>(result.GetValue<string>());

										   string? agentName = string.IsNullOrWhiteSpace(jsonResult?.name) ? null : jsonResult?.name;
										   agentName ??= InternalGiftIdeaAgentName;

										   Console.WriteLine($"\t>>>> INNER TURN: {agentName}");

										   return agentName;
									   }
							   },
						   TerminationStrategy =
							   new AgentTerminationStrategy()
							   {
								   Agents = [internalLeaderAgent],
								   MaximumIterations = 7,
								   AutomaticReset = true,
							   },
					   }
			   };
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
	record OuterTerminationResult(bool isAnswered, string reason);
	record AgentSelectionResult(string name, string reason);
	static Kernel CreateKernelWithChatCompletion(OpenAIOptions openAIOptions)
	{
		var builder = Kernel.CreateBuilder();

		//if (this.UseOpenAIConfig)
		//{
		var client = new HttpClient(new RequestAndResponseLoggingHttpClientHandler());
		builder.AddOpenAIChatCompletion(openAIOptions.ChatModelId, openAIOptions.ApiKey, null, null, client);
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
class AgentTerminationStrategy : TerminationStrategy
{
	/// <inheritdoc/>
	protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}
}