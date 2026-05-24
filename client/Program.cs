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

var chatHistory = new List<ChatMessage>{ new(ChatRole.System, "You are a helpful assistant that can use MCP tools with MySQL databases.") };
while(Console.ReadLine() is string query && !"exit".Equals(query, StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(query))
    {
        PromptForInput();
        continue;
    }

    try
    {
        chatHistory.Add(new ChatMessage(ChatRole.User, query));

        var limitedHistory = BuildWindowedHistory(chatHistory, maxCharacters: 4000);
        
        var messageTask = anthropicClient.GetResponseAsync(limitedHistory, options);
        while(messageTask.IsCompleted is false)
        {
            PrintThinkingAnimation();
        }
        var message = await messageTask;

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write(message);

        chatHistory.Add(new ChatMessage(ChatRole.Assistant, message.Text));
        await File.WriteAllTextAsync(
            "chat_history_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".json", 
            JsonSerializer.Serialize(chatHistory, new JsonSerializerOptions { WriteIndented = true })
        );
    }
    catch (JsonException ex)
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

static void PrintThinkingAnimation()
{
    var animation = new[] { "", ".", "..", "..." };
    for (int i = 0; i < 4; i++)
    {
        Console.Write($"\rThinking{animation[i]}");
        Thread.Sleep(250);
    }
    Console.Write("\r                \r");
}

static List<ChatMessage> BuildWindowedHistory(
    List<ChatMessage> history,
    int maxCharacters)
{
    var result = new List<ChatMessage>();

    var systemMessages = history
        .Where(m => m.Role == ChatRole.System)
        .ToList();

    result.AddRange(systemMessages);

    int currentCharacters = systemMessages.Sum(m => m.Text?.Length ?? 0);

    var temp = new List<ChatMessage>();
    for (int i = history.Count - 1; i >= 0; i--)
    {
        var msg = history[i];

        if (msg.Role == ChatRole.System)
            continue;

        var textLength = msg.Text?.Length ?? 0;

        if (currentCharacters + textLength > maxCharacters)
            break;

        temp.Add(msg);
        currentCharacters += textLength;
    }

    // Restore chronological order
    temp.Reverse();
    result.AddRange(temp);

    return result;
}