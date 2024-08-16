﻿namespace PropertyGraph.Common;

public class OpenAIOptions
{
    public const string OpenAI = "OpenAI";

    public string Source { get; set; } = string.Empty;
    public string ChatModelId { get; set; } = string.Empty;
    public string TextEmbeddingsModelId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatDeploymentName { get; set; } = string.Empty;
    public string TextEmbeddingsDeploymentName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty; // Used by Neo4j
}
