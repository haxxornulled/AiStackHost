using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Common;
using Microsoft.Extensions.Logging;

namespace AiStackManager.Infrastructure.Ollama;

public sealed record OllamaProviderOptions : InferenceProviderOptions
{
    public string ProviderId { get; init; } = "ollama";
    public string DisplayName { get; init; } = "Ollama";
}

public sealed class OllamaService : IInferenceProvider<OllamaProviderOptions>
{
    private readonly ICommandRunner _commands;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(ICommandRunner commands, ILogger<OllamaService> logger)
    {
        _commands = commands;
        _logger = logger;
        Options = new OllamaProviderOptions();
    }

    public OllamaProviderOptions Options { get; }
    public InferenceProviderDescriptor Descriptor => new(Options.ProviderId, Options.DisplayName, SupportsModelDownload: true, SupportsLocalRuntime: true);

    public async ValueTask<Fin> StartAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("systemctl", ["--user", "start", "ollama.service"], null, TimeSpan.FromSeconds(30), cancellationToken);
        if (!result.Succeeded && !IsMissingUserService(result))
            return Fin.Fail(AiStackError.ExternalTool(result.StandardError));

        if (IsMissingUserService(result))
        {
            _logger.LogInformation("Ollama user service is missing; creating a managed user service.");
            var install = await _commands.RunAsync("bash", ["-lc", InstallUserServiceScript], null, TimeSpan.FromSeconds(30), cancellationToken);
            if (!install.Succeeded) return Fin.Fail(AiStackError.ExternalTool(install.StandardError));
        }

        return await WaitUntilReadyAsync(cancellationToken);
    }

    public async ValueTask<Fin> StopAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("systemctl", ["--user", "stop", "ollama.service"], null, TimeSpan.FromSeconds(30), cancellationToken);
        return result.Succeeded || IsMissingUserService(result) ? Fin.Succ() : Fin.Fail(AiStackError.ExternalTool(result.StandardError));
    }

    public async ValueTask<IReadOnlyList<InferenceModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var list = await _commands.RunAsync("ollama", ["list"], null, TimeSpan.FromSeconds(30), cancellationToken);
        if (!list.Succeeded) return [];

        return list.StandardOutput
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .Select(ParseModelLine)
            .Where(model => model is not null)
            .Select(model => model!)
            .ToArray();
    }

    public async ValueTask<Fin> DownloadModelAsync(string modelId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pulling Ollama model {Model}", modelId);
        var pull = await _commands.RunAsync("ollama", ["pull", modelId], null, TimeSpan.FromMinutes(30), cancellationToken);
        return pull.Succeeded ? Fin.Succ() : Fin.Fail(AiStackError.ExternalTool(pull.StandardError));
    }

    public async ValueTask<Fin> EnsureModelAsync(string modelId, CancellationToken cancellationToken)
    {
        var models = await ListModelsAsync(cancellationToken);
        if (models.Any(m => ModelIdsMatch(m.Id, modelId))) return Fin.Succ();

        return await DownloadModelAsync(modelId, cancellationToken);
    }

    public async ValueTask<Fin> WarmModelAsync(string modelId, CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("ollama", ["run", modelId, "Reply with exactly: pong"], null, TimeSpan.FromMinutes(5), cancellationToken);
        return result.Succeeded ? Fin.Succ() : Fin.Fail(AiStackError.ExternalTool(result.StandardError));
    }

    public async ValueTask<string> StatusAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("ollama", ["ps"], null, TimeSpan.FromSeconds(15), cancellationToken);
        return result.Succeeded ? result.StandardOutput : result.StandardError;
    }

    private InferenceModelDescriptor? ParseModelLine(string line)
    {
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length == 0) return null;

        var size = fields.Length >= 3 ? string.Join(' ', fields.Skip(2).Take(2)) : null;
        return new InferenceModelDescriptor(
            Descriptor.Id,
            fields[0],
            fields[0],
            IsDownloaded: true,
            SizeBytes: null,
            Details: size);
    }

    private async ValueTask<Fin> WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        CommandResult? last = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            last = await _commands.RunAsync("ollama", ["ps"], null, TimeSpan.FromSeconds(5), cancellationToken);
            if (last.Succeeded) return Fin.Succ();

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        return Fin.Fail(AiStackError.ExternalTool(last?.StandardError ?? "Ollama did not become ready."));
    }

    private static bool IsMissingUserService(CommandResult result)
        => result.StandardError.Contains("Unit ollama.service not found", StringComparison.OrdinalIgnoreCase) ||
           result.StandardError.Contains("Unit ollama.service could not be found", StringComparison.OrdinalIgnoreCase);

    private static bool ModelIdsMatch(string actual, string expected)
        => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(actual, $"{expected}:latest", StringComparison.OrdinalIgnoreCase) ||
           string.Equals($"{actual}:latest", expected, StringComparison.OrdinalIgnoreCase);

    private const string InstallUserServiceScript = """
        set -euo pipefail
        ollama_path="$(command -v ollama)"
        service_dir="$HOME/.config/systemd/user"
        service_file="$service_dir/ollama.service"
        mkdir -p "$service_dir"
        cat > "$service_file" <<EOF
        [Unit]
        Description=Ollama local model runtime
        After=network-online.target

        [Service]
        ExecStart=$ollama_path serve
        Restart=on-failure
        RestartSec=3
        Environment=OLLAMA_HOST=127.0.0.1:11434

        [Install]
        WantedBy=default.target
        EOF
        systemctl --user daemon-reload
        systemctl --user enable --now ollama.service
        """;
}
