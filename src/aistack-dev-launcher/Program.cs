using System.Diagnostics;
using System.Net.Http;

Console.WriteLine("AiStack Dev Launcher — simple dev helper");

if (args.Length == 0)
{
    Console.WriteLine("Usage: dev-launcher [api|start|stop] [--options]");
    return;
}

var cmd = args[0].ToLowerInvariant();
string? token = null;
for (var i = 1; i < args.Length; i++)
{
    if (args[i] == "--token" && i + 1 < args.Length)
    {
        token = args[i + 1];
        i++;
    }
    if (args[i] == "--urls" && i + 1 < args.Length)
    {
        // handled below
        i++;
    }
}

if (cmd == "api")
{
    var urls = "http://localhost:5000";
    for (var i = 0; i < args.Length; i++)
        if (args[i] == "--urls" && i + 1 < args.Length) urls = args[i + 1];

    var psi = new ProcessStartInfo("dotnet", $"run --project ../AiStackManager.Api/AiStackManager.Api.csproj -- --urls {urls}")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    using var p = Process.Start(psi)!;
    Console.WriteLine("API host started. Press Ctrl+C to stop.");
    await p.WaitForExitAsync();
    return;
}

if (cmd == "start" || cmd == "stop")
{
    token ??= Environment.GetEnvironmentVariable("AISTACK_MANAGEMENT_TOKEN");
    if (string.IsNullOrWhiteSpace(token))
    {
        Console.WriteLine("No management token supplied. Aborting to avoid accidental system changes.");
        return;
    }

    var url = cmd == "start" ? "http://localhost/api/stack/start" : "http://localhost/api/stack/stop";
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("X-AiStack-Token", token);
    var resp = await client.PostAsync(url, null);
    var cap = string.IsNullOrEmpty(cmd) ? cmd : char.ToUpperInvariant(cmd[0]) + cmd.Substring(1);
    Console.WriteLine($"{cap} request: {(int)resp.StatusCode} {resp.ReasonPhrase}");
    return;
}

Console.WriteLine($"Unknown command '{cmd}'");