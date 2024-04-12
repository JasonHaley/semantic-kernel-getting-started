namespace HelloWorld.Plugin1.Console.Configuration;
internal class OpenAIOptions
{
    public const string OpenAI = "OpenAI";

    public string Source { get; set; } = string.Empty;
    public string ChatModelId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatDeploymentName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
}
