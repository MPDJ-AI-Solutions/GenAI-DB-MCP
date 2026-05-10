using System.Text.Json;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

var clientTransport = new StdioClientTransport(new()
{
    Name = "Demo Server",
    Command = "dotnet",
    Arguments = ["run", "--project", @"../server/Server.csproj"],
});

await using var mcpClient = await McpClient.CreateAsync(clientTransport);

var tools = await mcpClient.ListToolsAsync();

foreach (var tool in tools)
{
    Console.WriteLine($"Connected to server with tools: {tool.Name}");
}

using var anthropicClient = new AnthropicClient(new APIAuthentication(builder.Configuration["ANTHROPIC_API_KEY"]))
    .Messages
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var options = new ChatOptions
{
    MaxOutputTokens = 1000,
    ModelId = "claude-haiku-4-5-20251001",
    Tools=[.. tools]
};

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("MCP Client Started!");

PromptForInput();

while(Console.ReadLine() is string query && !"exit".Equals(query, StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(query))
    {
        PromptForInput();
        continue;
    }

    try
    {
        var message = await anthropicClient.GetResponseAsync(query, options);
        
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write(message);

    }
    catch (JsonException  ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[JSON Error] Raw content that failed: '{ex.Message}'");
        Console.ResetColor();
    }
    catch (Anthropic.SDK.RateLimitsExceeded ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[RATE LIMIT] Too many tokens — wait a moment and retry. Detail: {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine();

    PromptForInput();
}


static void PromptForInput()
{
    Console.ResetColor();
    Console.WriteLine("Enter a command (or 'exit' to quit):");
    Console.Write("> ");
}