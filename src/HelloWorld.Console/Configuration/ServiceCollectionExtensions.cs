using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace HelloWorld.Console.Configuration;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddChatCompletionService(this IServiceCollection serviceCollection, OpenAIOptions openAIOptions)
    {
        switch (openAIOptions.Source)
        {
            case "AzureOpenAI":
                serviceCollection = serviceCollection.AddAzureOpenAIChatCompletion(openAIOptions.ChatDeploymentName, endpoint: openAIOptions.Endpoint,
                    apiKey: openAIOptions.ApiKey, serviceId: openAIOptions.ChatModelId);
                break;

            case "OpenAI":
                serviceCollection = serviceCollection.AddOpenAIChatCompletion(modelId: openAIOptions.ChatModelId, apiKey: openAIOptions.ApiKey);
                break;

            default:
                throw new ArgumentException($"Invalid source: {openAIOptions.Source}");
        }

        return serviceCollection;
    }
}
