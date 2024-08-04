namespace PropertyGraph.Common;

public class Neo4jOptions
{
    public const string Neo4j = "Neo4j";

    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string URI { get; set; } = string.Empty;
}
