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
        var operations = new[]
        {
            new[] { "config", "set", "gateway.mode", "local" },
            new[] { "config", "set", "gateway.port", settings.OpenClawPort.ToString(), "--strict-json" },
            new[] { "config", "set", "gateway.bind", "loopback" },
            new[] { "config", "set", "agents.defaults.workspace", settings.OpenClawWorkspace },
            new[] { "config", "set", "models.providers.ollama.apiKey", settings.OpenClawApiKey },
            new[] { "config", "set", "models.providers.ollama.baseUrl", settings.OpenClawBaseUrl },
            new[] { "config", "set", "models.providers.ollama.api", "ollama" },
            new[] { "config", "set", "agents.defaults.models", $"{{\"{settings.EffectiveOpenClawModelRef}\":{{\"alias\":\"Local Inference\"}}}}", "--strict-json", "--merge" },
            new[] { "models", "set", settings.EffectiveOpenClawModelRef }
        };

        foreach (var args in operations)
        {
            var result = await _commands.RunAsync("openclaw", args, null, TimeSpan.FromSeconds(30), cancellationToken);
            if (!result.Succeeded) return Fin.Fail(AiStackError.ExternalTool(result.StandardError));
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
}
