
// Found most of this implementation via: https://github.com/microsoft/semantic-kernel/issues/5107
public class RequestAndResponseLoggingHttpClientHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string requestContent;
        if (request.Content is not null)
        {
			requestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            System.Console.WriteLine("***********************************************");
            System.Console.WriteLine("Request:");
            System.Console.WriteLine(requestContent);
			System.Console.WriteLine("***********************************************");
		}

        HttpResponseMessage result = null;
        string content = null;
        try
        {
            result = await base.SendAsync(request, cancellationToken);

            if (result.Content is not null)
            {
                content = await result.Content.ReadAsStringAsync(cancellationToken);
                System.Console.WriteLine("***********************************************");
                System.Console.WriteLine("Response:");
                System.Console.WriteLine(content);
				System.Console.WriteLine("***********************************************");
			}
        }
        catch (Exception ex) 
        {
			System.Console.WriteLine($"Exception: {ex.Message}");
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
            System.Console.WriteLine("***********************************************");
            System.Console.WriteLine("Request:");
            System.Console.WriteLine(content);
			System.Console.WriteLine("***********************************************");
		}

        return await base.SendAsync(request, cancellationToken);
    }
}
