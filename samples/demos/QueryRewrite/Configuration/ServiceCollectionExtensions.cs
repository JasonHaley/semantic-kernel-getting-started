using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System.Data.Common;
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
public enum ApiLoggingLevel
{
    None = 0,
    RequestOnly = 1,
    ResponseAndRequest = 2,
}

internal static class IKernelBuilderExtensions
{
    internal static IKernelBuilder AddChatCompletionService(this IKernelBuilder kernelBuilder, string? connectionString, ApiLoggingLevel apiLoggingLevel = ApiLoggingLevel.None)
    {
        var connectionStringBuilder = new DbConnectionStringBuilder();
        connectionStringBuilder.ConnectionString = connectionString;

        var source = connectionStringBuilder.TryGetValue("Source", out var sourceValue) ? (string)sourceValue : throw new InvalidOperationException($"Connection string is missing 'Source'");
                
        switch (source)
        {
            case "AzureOpenAI":
                {
                    var chatDeploymentName = connectionStringBuilder.TryGetValue("ChatDeploymentName", out var deploymentValue) ? (string)deploymentValue : throw new InvalidOperationException($"Connection string is missing 'ChatDeploymentName'");
                    var endpoint = connectionStringBuilder.TryGetValue("Endpoint", out var endpointValue) ? (string)endpointValue : throw new InvalidOperationException($"Connection string is missing 'Endpoint'");
                    var key = connectionStringBuilder.TryGetValue("Key", out var keyValue) ? (string)keyValue : throw new InvalidOperationException($"Connection string is missing 'Key'");

                    if (apiLoggingLevel == ApiLoggingLevel.None)
                    {
                        kernelBuilder = kernelBuilder.AddAzureOpenAIChatCompletion(chatDeploymentName, endpoint: endpoint, apiKey: key);
                    }
                    else
                    {
                        var client = CreateHttpClient(apiLoggingLevel);
                        kernelBuilder.AddAzureOpenAIChatCompletion(chatDeploymentName, endpoint, key, null, null, client);
                    }
                    break;
                }
            case "OpenAI":
                {
                    var chatModelId = connectionStringBuilder.TryGetValue("ChatModelId", out var chatModelIdValue) ? (string)chatModelIdValue : throw new InvalidOperationException($"Connection string is missing 'ChatModelId'");
                    var apiKey = connectionStringBuilder.TryGetValue("ApiKey", out var apiKeyValue) ? (string)apiKeyValue : throw new InvalidOperationException($"Connection string is missing 'ApiKey'");

                    if (apiLoggingLevel == ApiLoggingLevel.None)
                    {
                        kernelBuilder = kernelBuilder.AddOpenAIChatCompletion(modelId: chatModelId, apiKey: apiKey);
                        break;
                    }
                    else
                    {
                        var client = CreateHttpClient(apiLoggingLevel);
                        kernelBuilder.AddOpenAIChatCompletion(chatModelId, apiKey, null, null, client);
                    }
                    break;
                }
            default:
                throw new ArgumentException($"Invalid source: {source}");
        }
        return kernelBuilder;
    }

    internal static IKernelBuilder AddChatCompletionService(this IKernelBuilder kernelBuilder, OpenAIOptions openAIOptions, ApiLoggingLevel apiLoggingLevel = ApiLoggingLevel.None)
    {
        switch (openAIOptions.Source)
        {
            case "AzureOpenAI":
                {
                    if (apiLoggingLevel == ApiLoggingLevel.None)
                    {
                        kernelBuilder = kernelBuilder.AddAzureOpenAIChatCompletion(openAIOptions.ChatDeploymentName, endpoint: openAIOptions.Endpoint,
                            apiKey: openAIOptions.ApiKey, serviceId: openAIOptions.ChatModelId);
                    }
                    else
                    {
                        var client = CreateHttpClient(apiLoggingLevel);
                        kernelBuilder.AddAzureOpenAIChatCompletion(openAIOptions.ChatDeploymentName, openAIOptions.Endpoint, openAIOptions.ApiKey, null, null, client);
                    }
                    break;
                }
            case "OpenAI":
                {
                    if (apiLoggingLevel == ApiLoggingLevel.None)
                    {
                        kernelBuilder = kernelBuilder.AddOpenAIChatCompletion(modelId: openAIOptions.ChatModelId, apiKey: openAIOptions.ApiKey);
                        break;
                    }
                    else
                    {
                        var client = CreateHttpClient(apiLoggingLevel);
                        kernelBuilder.AddOpenAIChatCompletion(openAIOptions.ChatModelId, openAIOptions.ApiKey, null, null, client);
                    }
                    break;
                }
            default:
                throw new ArgumentException($"Invalid source: {openAIOptions.Source}");
        }

        return kernelBuilder;
    }

    private static HttpClient CreateHttpClient(ApiLoggingLevel apiLoggingLevel)
    {
        HttpClientHandler httpClientHandler;
        if (apiLoggingLevel == ApiLoggingLevel.RequestOnly)
        {
            httpClientHandler = new RequestLoggingHttpClientHandler();
        }
        else
        {
            httpClientHandler = new RequestAndResponseLoggingHttpClientHandler();
        }
        var client = new HttpClient(httpClientHandler);
        return client;
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
            System.Console.WriteLine("***********************************************");
            System.Console.WriteLine("Request:");
            System.Console.WriteLine(json);
        }

        var result = await base.SendAsync(request, cancellationToken);

        if (result.Content is not null)
        {
            var content = await result.Content.ReadAsStringAsync(cancellationToken);
            //var json = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonDocument>(content),
            //    new JsonSerializerOptions { WriteIndented = true });
            System.Console.WriteLine("***********************************************");
            System.Console.WriteLine("Response:");
            System.Console.WriteLine(content);
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
            System.Console.WriteLine("***********************************************");
            System.Console.WriteLine("Request:");
            System.Console.WriteLine(json);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
