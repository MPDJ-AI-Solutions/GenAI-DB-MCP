using System.Text.RegularExpressions;
using MySqlConnector;

namespace DatabaseManager.Tools;

public sealed class DatabaseHelper
{
    private static DatabaseHelper? _instance;
    public static DatabaseHelper GetInstance()
    {
        if (_instance == null)
            throw new InvalidOperationException("DatabaseHelper is not initialized. Call DatabaseHelper.Initialize(connectionString) before using it.");
        return _instance;
    }

    private readonly MySqlDataSource db;

    private DatabaseHelper(string connectionString)
    {
        db = new MySqlDataSource(connectionString);
    }

    public static void Initialize(string connectionString)
    {
        _instance = new DatabaseHelper(connectionString);
    }

    public MySqlConnection OpenConnection() => db.OpenConnection();
    public ValueTask<MySqlConnection> OpenConnectionAsync() => db.OpenConnectionAsync();

    public static void ValidateSelectQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            throw new ArgumentException("SQL query cannot be null or empty.", nameof(sqlQuery));
        }

        string sanitized = RemoveComments(sqlQuery).Trim();

        if (!Regex.IsMatch(sanitized, @"^\s*SELECT\b", RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException(
                "Only SELECT queries are allowed. The query must begin with the SELECT keyword.");
        }

        string[] forbiddenPatterns =
        [
            @"\bINSERT\b",
            @"\bUPDATE\b",
            @"\bDELETE\b",
            @"\bDROP\b",
            @"\bTRUNCATE\b",
            @"\bALTER\b",
            @"\bCREATE\b",
            @"\bREPLACE\b",   // MySQL REPLACE INTO
            @"\bUPSERT\b",
            @"\bEXEC\b",
            @"\bEXECUTE\b",
            @"\bCALL\b",     
            @"\bGRANT\b",
            @"\bREVOKE\b",
            @"\bLOCK\b",
            @"\bSET\b",    
        ];

        foreach (string pattern in forbiddenPatterns)
        {
            if (Regex.IsMatch(sanitized, pattern, RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Query contains a forbidden keyword that could mutate data or schema. " +
                    $"Pattern matched: '{pattern.Replace(@"\b", "")}'.");
            }
                
        }

        if (Regex.IsMatch(sanitized, @";\s*\S"))
        {
            throw new InvalidOperationException(
                "Multiple SQL statements are not allowed. Remove the semicolon separator.");
        }
            
    }

    private static string RemoveComments(string sql)
    {
        // Remove block comments first (non-greedy)
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        // Remove single-line comments
        sql = Regex.Replace(sql, @"--[^\r\n]*", " ");

        return sql;
    }
}