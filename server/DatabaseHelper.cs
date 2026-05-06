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
}