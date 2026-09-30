using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using MySqlConnector;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

DatabaseManager.Tools.DatabaseHelper.Initialize("Server=<insert_server>;User=mcp;Password=<inser_password>;");

var app = builder.Build();

await app.RunAsync();
