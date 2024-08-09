// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.SemanticKernel.Plugins.Web.Bing;

/// <summary>
/// Bing API connector2.
/// </summary>
public sealed class BingConnector2: IWebSearchEngineConnector
{
	private readonly ILogger _logger;
	private readonly HttpClient _httpClient;
	private readonly string? _apiKey;
	private readonly Uri? _uri = null;
	private const string DefaultUri = "https://api.bing.microsoft.com/v7.0/search?q";

	/// <summary>
	/// Initializes a new instance of the <see cref="BingConnector"/> class.
	/// </summary>
	/// <param name="apiKey">The API key to authenticate the connector.</param>
	/// <param name="httpClient">The HTTP client to use for making requests.</param>
	/// <param name="uri">The URI of the Bing Search instance. Defaults to "https://api.bing.microsoft.com/v7.0/search?q".</param>
	/// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
	public BingConnector2(string apiKey, HttpClient httpClient, Uri? uri = null, ILoggerFactory? loggerFactory = null)
	{

		this._apiKey = apiKey;
		this._logger = loggerFactory?.CreateLogger(typeof(BingConnector)) ?? NullLogger.Instance;
		this._httpClient = httpClient;
		this._uri = uri ?? new Uri(DefaultUri);
	}

	/// <inheritdoc/>
	public async Task<IEnumerable<T>> SearchAsync<T>(string query, int count = 1, int offset = 0, CancellationToken cancellationToken = default)
	{
		if (count is <= 0 or >= 50)
		{
			throw new ArgumentOutOfRangeException(nameof(count), count, $"{nameof(count)} value must be greater than 0 and less than 50.");
		}

		Uri uri = new($"{this._uri}={Uri.EscapeDataString(query.Trim())}&count={count}&offset={offset}");

		this._logger.LogDebug("Sending request: {Uri}", uri);

		using HttpResponseMessage response = await this.SendGetRequestAsync(uri, cancellationToken).ConfigureAwait(false);

		this._logger.LogDebug("Response received: {StatusCode}", response.StatusCode);

		string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		// Sensitive data, logging as trace, disabled by default
		this._logger.LogTrace("Response content received: {Data}", json);

		WebSearchResponse? data = JsonSerializer.Deserialize<WebSearchResponse>(json);

		List<T>? returnValues = null;
		if (data?.WebPages?.Value is not null)
		{
			if (typeof(T) == typeof(string))
			{
				WebPage[]? results = data?.WebPages?.Value;
				var item = results?.GetRandomItem();
				returnValues = new string[] { @$"Source URL: {item?.Url}
{item?.Snippet}" }.ToList() as List<T>;
			}
			else if (typeof(T) == typeof(WebPage))
			{
				List<WebPage>? webPages = [.. data.WebPages.Value];
				returnValues = webPages.Take(count).ToList() as List<T>;
			}
			else
			{
				throw new NotSupportedException($"Type {typeof(T)} is not supported.");
			}
		}

		return
			returnValues is null ? [] :
			returnValues.Count <= count ? returnValues :
			returnValues.Take(count);
	}

	/// <summary>
	/// Sends a GET request to the specified URI.
	/// </summary>
	/// <param name="uri">The URI to send the request to.</param>
	/// <param name="cancellationToken">A cancellation token to cancel the request.</param>
	/// <returns>A <see cref="HttpResponseMessage"/> representing the response from the request.</returns>
	private async Task<HttpResponseMessage> SendGetRequestAsync(Uri uri, CancellationToken cancellationToken = default)
	{
		using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

		if (!string.IsNullOrEmpty(this._apiKey))
		{
			httpRequestMessage.Headers.Add("Ocp-Apim-Subscription-Key", this._apiKey);
		}
		
		return await this._httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);
	}
}
public static class EnumerableExtensions
{
	private static readonly Random _random = new Random();

	public static T GetRandomItem<T>(this IEnumerable<T> source)
	{
		if (source == null)
		{
			throw new ArgumentNullException(nameof(source));
		}

		var list = source.ToList();
		if (!list.Any())
		{
			throw new InvalidOperationException("Sequence contains no elements");
		}

		int randomIndex = _random.Next(list.Count);
		return list[randomIndex];
	}
}