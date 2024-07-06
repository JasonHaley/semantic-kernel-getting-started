using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SemanticKernel.Data.Nl2Sql.Harness;
using TextToSql.Console.Configuration;
using SemanticKernel.Data.Nl2Sql.Library.Schema;
using Microsoft.SemanticKernel.Memory;

namespace TextToSql.Console.Nl2Sql;
internal class SqlScemaLoader
{
    private readonly IConfiguration _configuration;
    private TextToSqlOptions _textToSqlOptions = new TextToSqlOptions();
    private readonly Dictionary<string, SqlSchemaOptions> _sqlSchemaOptionsMap = new Dictionary<string, SqlSchemaOptions>();
    public SqlScemaLoader(IConfiguration confuration)
    {
        _configuration = confuration;
        Initialize();
    }

    public IList<string> SchemaNames { get { return _textToSqlOptions.SchemaNames.Split(','); } }
    public double MinSchemaRelevance { get { return _textToSqlOptions.MinSchemaRelevance; } }

    private void Initialize()
    {
        _configuration.GetSection(TextToSqlOptions.TextToSqlConfig).Bind(_textToSqlOptions);
        if (!string.IsNullOrEmpty(_textToSqlOptions.SchemaNames))
        {
            foreach (var name in SchemaNames)
            {
                var schema = new SqlSchemaOptions();
                _configuration.GetSection(name).Bind(schema);
                _sqlSchemaOptionsMap.Add(name, schema);
            }
        }
    }

    private bool HasSchemas()
    {
        return _sqlSchemaOptionsMap.Count > 0;
    }

    private bool SchemeFileExists(string schemaName)
    {
        // If the database schema doesn't exist, the create one
        var schemaFile = $"{schemaName}.json";
        return File.Exists(schemaFile);
    }

    private async Task<bool> CreateSchemaFileAsync(string schemaName, SqlSchemaOptions options)
    {
        // If there is no ConnectionString, messages and exit
        var connectionString = GetConnectionString(schemaName);
        if (string.IsNullOrEmpty(connectionString))
        {
            System.Console.WriteLine($"Please add a connection string for {schemaName} in the appsettings.json file before running");
            return false;
        }

        // Connect to database and create schema
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var provider = new SqlSchemaProvider(connection);

            SchemaDefinition schemaDef;
            string[] tableNames;
            if (options.Tables != null)
            {
               tableNames = options.Tables.Split(',');
               schemaDef = await provider.GetSchemaAsync(schemaName, options.Description, tableNames).ConfigureAwait(false);
            }
            else
            {
                schemaDef = await provider.GetSchemaAsync(schemaName, options.Description).ConfigureAwait(false);
            }

            await connection.CloseAsync().ConfigureAwait(false);

            using var streamCompact = new StreamWriter(
                    $"{schemaName}.json",
                    new FileStreamOptions
                    {
                        Access = FileAccess.Write,
                        Mode = FileMode.Create,
                    });

            await streamCompact.WriteAsync(schemaDef.ToJson()).ConfigureAwait(false);
        }
        return true;
    }

    public string GetConnectionString(string schemaName)
    {
        return _configuration.GetConnectionString(schemaName) ?? "";
    }
    public async Task<bool> TryLoadAsync(ISemanticTextMemory memory)
    {
        if (!HasSchemas())
        {
            System.Console.WriteLine("No schemas configured in appsettings.json");
            return false;
        }

        foreach (var schema in _sqlSchemaOptionsMap.Keys)
        {
            if (!SchemeFileExists(schema))
            {
                await CreateSchemaFileAsync(schema, _sqlSchemaOptionsMap[schema]);
            }
        }

        await SchemaProvider.InitializeAsync(memory, _sqlSchemaOptionsMap.Keys.Select(s => $"{s}.json")).ConfigureAwait(false);

        return true;
    }
}
