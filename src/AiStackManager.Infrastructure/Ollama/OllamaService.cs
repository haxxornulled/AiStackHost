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
        var result = await _commands.RunAsync("systemctl", ["--user", "start", "ollama"], null, TimeSpan.FromSeconds(30), cancellationToken);
        return result.Succeeded ? Fin.Succ() : Fin.Fail(AiStackError.ExternalTool(result.StandardError));
    }

    public async ValueTask<Fin> StopAsync(CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync("systemctl", ["--user", "stop", "ollama"], null, TimeSpan.FromSeconds(30), cancellationToken);
        return result.Succeeded ? Fin.Succ() : Fin.Fail(AiStackError.ExternalTool(result.StandardError));
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
        if (models.Any(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase))) return Fin.Succ();

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
}
