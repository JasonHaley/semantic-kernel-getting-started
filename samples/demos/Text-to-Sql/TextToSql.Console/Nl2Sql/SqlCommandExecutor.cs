
using Microsoft.Data.SqlClient;
using System.Data;

namespace TextToSql.Console.Nl2Sql;
public class SqlCommandExecutor
{
    private readonly string _connectionString;

    public SqlCommandExecutor(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<List<string>>> ExecuteAsync(string sql)
    {
        if (sql.IndexOf("SELECT") == -1)
        {
            return new List<List<string>>();
        }

        var rows = new List<List<string>>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                command.CommandText = sql;
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities

                using var reader = await command.ExecuteReaderAsync();

                bool headersAdded = false;
                while (reader.Read())
                {
                    var cols = new List<string>();
                    var headerCols = new List<string>();
                    if (!headersAdded)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            headerCols.Add(reader.GetName(i).ToString());
                        }
                        headersAdded = true;
                        rows.Add(headerCols);
                    }

                    for (int i = 0; i <= reader.FieldCount - 1; i++)
                    {
                        try
                        {
                            cols.Add(reader.GetValue(i).ToString());
                        }
                        catch
                        {
                            cols.Add("DataTypeConversionError");
                        }
                    }
                    rows.Add(cols);
                }
            }
        }
        catch
        {
            throw;
        }
        return rows;
    }
}
