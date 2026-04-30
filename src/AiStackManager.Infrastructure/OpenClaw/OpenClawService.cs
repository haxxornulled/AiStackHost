using System.Text.Json;
using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;

namespace AiStackManager.Infrastructure.OpenClaw;

public sealed class OpenClawService : IOpenClawService
{
    private readonly ICommandRunner _commands;
    public OpenClawService(ICommandRunner commands) => _commands = commands;

    public async ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken)
    {
        var modelRef = settings.EffectiveOpenClawModelRef;
        var modelsConfig = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [modelRef] = new Dictionary<string, object?>()
        });

        var operations = new[]
        {
            new OpenClawOperation(["config", "set", "gateway.mode", "local"]),
            new OpenClawOperation(["config", "set", "gateway.port", settings.OpenClawPort.ToString(), "--strict-json"]),
            new OpenClawOperation(["config", "set", "gateway.bind", "loopback"]),
            new OpenClawOperation(["config", "set", "agents.defaults.workspace", settings.OpenClawWorkspace]),
            new OpenClawOperation(["config", "set", "models.providers.ollama.apiKey", settings.OpenClawApiKey]),
            new OpenClawOperation(["config", "set", "models.providers.ollama.baseUrl", settings.OpenClawBaseUrl]),
            new OpenClawOperation(["config", "set", "models.providers.ollama.api", "ollama"]),
            new OpenClawOperation(["config", "set", "agents.defaults.models", modelsConfig, "--strict-json", "--merge"]),
            new OpenClawOperation(["config", "unset", $"agents.defaults.models[\"{EscapeConfigPathSegment(modelRef)}\"].alias"], IgnoreMissingPath: true),
            new OpenClawOperation(["models", "set", modelRef])
        };

        foreach (var operation in operations)
        {
            var result = await _commands.RunAsync("openclaw", operation.Arguments, null, TimeSpan.FromSeconds(30), cancellationToken);
            if (!result.Succeeded && !operation.CanIgnore(result))
                return Fin.Fail(AiStackError.ExternalTool(result.StandardError));
        }

        return Fin.Succ();
    }

    public async ValueTask<Fin> StartGatewayAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("systemctl", ["--user", "restart", "openclaw-gateway.service"], null, TimeSpan.FromSeconds(30), cancellationToken);
        return result.Succeeded ? Fin.Succ() : Fin.Fail(AiStackError.ExternalTool(result.StandardError));
    }

    public async ValueTask<Fin> StopGatewayAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("systemctl", ["--user", "stop", "openclaw-gateway.service"], null, TimeSpan.FromSeconds(30), cancellationToken);
        return result.Succeeded ? Fin.Succ() : Fin.Fail(AiStackError.ExternalTool(result.StandardError));
    }

    public async ValueTask<string> StatusAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("openclaw", ["models", "status"], null, TimeSpan.FromSeconds(20), cancellationToken);
        return result.Succeeded ? result.StandardOutput : result.StandardError;
    }

    private static string EscapeConfigPathSegment(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed record OpenClawOperation(IReadOnlyList<string> Arguments, bool IgnoreMissingPath = false)
    {
        public bool CanIgnore(CommandResult result)
            => IgnoreMissingPath &&
               result.StandardError.Contains("Config path not found", StringComparison.OrdinalIgnoreCase);
    }
}
