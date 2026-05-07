using ModelContextProtocol.Server;
using MySqlConnector;
using System.ComponentModel;
using System.Text;

namespace DatabaseManager.Tools;

[McpServerToolType]
public static class DatabaseManager
{
    [McpServerTool, Description("Lists all databases on the MySQL server.")]
    public static async Task<IEnumerable<string>> ShowDatabases()
    {
        DatabaseHelper dbHelper = DatabaseHelper.GetInstance();
        await using var conn = await dbHelper.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SHOW DATABASES;";

        var databases = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            databases.Add(reader.GetString(0));

        return databases;
    }

    [McpServerTool, Description("Get full schema info including table relations.")]
    public static async Task<string> GetDatabaseSchema(
        [Description("The name of the database to get the schema for.")] string databaseName)
    {
        DatabaseHelper dbHelper = DatabaseHelper.GetInstance();
        await using var conn = await dbHelper.OpenConnectionAsync();

        var result = new StringBuilder();

        // Get Tables
        var tables = new List<string>();

        await using (var tablesCmd = conn.CreateCommand())
        {
            tablesCmd.CommandText = @"
                SELECT TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = @db;";

            tablesCmd.Parameters.AddWithValue("@db", databaseName);


            await using var reader = await tablesCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));

            result.AppendLine("=== TABLES ===");
        }

        foreach (var table in tables)
        {
            result.AppendLine($"\nTable: {table}");

            // Columns
            await using var columnCmd = conn.CreateCommand();
            columnCmd.CommandText = @"
                SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @table;";
            columnCmd.Parameters.AddWithValue("@db", databaseName);
            columnCmd.Parameters.AddWithValue("@table", table);

            await using var columnReader = await columnCmd.ExecuteReaderAsync();
            result.AppendLine("  Columns:");
            while (await columnReader.ReadAsync())
            {
                result.AppendLine(
                    $"    - {columnReader.GetString(0)} ({columnReader.GetString(1)}) Nullable: {columnReader.GetString(2)}");
            }
        }
        

        // Primary Keys
        await using (var pkCmd = conn.CreateCommand())
        {
            pkCmd.CommandText = @"
                SELECT TABLE_NAME, COLUMN_NAME
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                WHERE TABLE_SCHEMA = @db
                AND CONSTRAINT_NAME = 'PRIMARY';";

            pkCmd.Parameters.AddWithValue("@db", databaseName);

            await using var pkReader = await pkCmd.ExecuteReaderAsync();

            result.AppendLine("\n=== PRIMARY KEYS ===");
            while (await pkReader.ReadAsync())
            {
                result.AppendLine(
                    $"Table: {pkReader.GetString(0)} -> PK: {pkReader.GetString(1)}");
            }
        }

        // Foreign Keys
        await using (var fkCmd = conn.CreateCommand())
        {
            fkCmd.CommandText = @"
                SELECT 
                    TABLE_NAME,
                    COLUMN_NAME,
                    REFERENCED_TABLE_NAME,
                    REFERENCED_COLUMN_NAME
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                WHERE TABLE_SCHEMA = @db
                AND REFERENCED_TABLE_NAME IS NOT NULL;";

            fkCmd.Parameters.AddWithValue("@db", databaseName);

            await using var fkReader = await fkCmd.ExecuteReaderAsync();

            result.AppendLine("\n=== RELATIONS (FOREIGN KEYS) ===");

            while (await fkReader.ReadAsync())
            {
                var table = fkReader.GetString(0);
                var column = fkReader.GetString(1);
                var refTable = fkReader.GetString(2);
                var refColumn = fkReader.GetString(3);

                result.AppendLine(
                    $"{table}.{column} -> {refTable}.{refColumn}");
            }
        }

        return result.ToString();
    }

    [McpServerTool, Description("Execute a SQL SELECT query on a specific database.")]
    public static async Task<string> ExecuteSelectQuery(
        [Description("The name of the database to execute the query on.")] string databaseName,
        [Description("The SQL SELECT query to execute.")] string sqlQuery)
    {
        DatabaseHelper.ValidateSelectQuery(sqlQuery);

        DatabaseHelper dbHelper = DatabaseHelper.GetInstance();
        await using var conn = await dbHelper.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = $"USE {databaseName}; {sqlQuery}";

        await using var reader = await cmd.ExecuteReaderAsync();

        return await FormatResultsAsync(reader);
    }

    private static async Task<string> FormatResultsAsync(MySqlDataReader reader)
    {
        var sb = new StringBuilder();

        do
        {
            if (reader.FieldCount == 0)
                continue;

            // ── Header ──────────────────────────────────────────────────────
            var columns = Enumerable
                .Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i))
                .ToList();

            int[] widths = columns.Select(c => c.Length).ToArray();

            var rows = new List<string[]>();

            while (await reader.ReadAsync())
            {
                var row = new string[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)!.ToString()!;
                    widths[i] = Math.Max(widths[i], row[i].Length);
                }
                rows.Add(row);
            }

            // ── Render ───────────────────────────────────────────────────────
            string separator = "+-" + string.Join("-+-", widths.Select(w => new string('-', w))) + "-+";

            sb.AppendLine(separator);
            sb.AppendLine("| " + string.Join(" | ", columns.Select((c, i) => c.PadRight(widths[i]))) + " |");
            sb.AppendLine(separator);

            if (rows.Count == 0)
            {
                sb.AppendLine("| (no rows) |");
            }
            else
            {
                foreach (var row in rows)
                    sb.AppendLine("| " + string.Join(" | ", row.Select((v, i) => v.PadRight(widths[i]))) + " |");
            }

            sb.AppendLine(separator);
            sb.AppendLine($"  {rows.Count} row(s)");

        } while (await reader.NextResultAsync()); // handles multiple result sets

        return sb.Length > 0 ? sb.ToString() : "(query returned no results)";
    }
}