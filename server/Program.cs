using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using MySqlConnector;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

DatabaseManager.Tools.DatabaseHelper.Initialize("Server=localhost;User=mcp;Password=1234;");

var app = builder.Build();

await app.RunAsync();