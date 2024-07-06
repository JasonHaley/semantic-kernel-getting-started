namespace TextToSql.Console.Configuration;
public class TextToSqlOptions
{
    public const string TextToSqlConfig = "TextToSql";

    public string SchemaNames { get; set; } = string.Empty;
    public double MinSchemaRelevance { get; set; } = 0.7;
}
