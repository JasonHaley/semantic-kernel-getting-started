namespace TextToSql.Console.Configuration;
public class SqlSchemaOptions
{
    public const string SqlSchemaConfig = "SqlSchemaConfig ";

    public string ConnectionName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Tables { get; set; }
}
