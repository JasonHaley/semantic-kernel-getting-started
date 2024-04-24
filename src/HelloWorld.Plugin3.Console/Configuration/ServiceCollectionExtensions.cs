using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Text.Json;


namespace HelloWorld.Plugin2.Console.Configuration;

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

// Found most of this implementation via: https://github.com/microsoft/semantic-kernel/issues/5107
public class RequestAndResponseLoggingHttpClientHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var content = await request.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(content),
                new JsonSerializerOptions { WriteIndented = true });
            System.Console.WriteLine("Request:");
            System.Console.WriteLine(json);
        }
                
        var result = await base.SendAsync(request, cancellationToken);
        
        if (result.Content is not null)
        {
            var content = await result.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(content),
                new JsonSerializerOptions { WriteIndented = true });
            System.Console.WriteLine("Response:");
            System.Console.WriteLine(json);
        }

        return result;
    }
}
public class RequestLoggingHttpClientHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var content = await request.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(content),
                new JsonSerializerOptions { WriteIndented = true });
            System.Console.WriteLine("Request:");
            System.Console.WriteLine(json);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
