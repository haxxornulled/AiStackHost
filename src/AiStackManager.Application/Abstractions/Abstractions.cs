using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;
using AiStackManager.Domain.Review;
using AiStackManager.Domain.Stack;

namespace AiStackManager.Application.Abstractions;

public sealed record CommandResult(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration)
{
    public bool Succeeded => ExitCode == 0;
    public string CommandLine => $"{FileName} {string.Join(' ', Arguments)}";
}

public interface ICommandRunner
{
    ValueTask<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IAiStackStateStore
{
    ValueTask<AiStackAggregate> GetAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(AiStackAggregate aggregate, CancellationToken cancellationToken);
}

public sealed record InferenceProviderDescriptor(
    string Id,
    string DisplayName,
    bool SupportsModelDownload,
    bool SupportsLocalRuntime);

public sealed record InferenceModelDescriptor(
    string ProviderId,
    string Id,
    string DisplayName,
    bool IsDownloaded,
    long? SizeBytes,
    string? Details);

public sealed record InferenceModelSelection(
    string ProviderId,
    string ModelId);

public abstract record InferenceProviderOptions;

public interface IInferenceProvider
{
    InferenceProviderDescriptor Descriptor { get; }
    ValueTask<Fin> StartAsync(CancellationToken cancellationToken);
    ValueTask<Fin> StopAsync(CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<InferenceModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken);
    ValueTask<Fin> DownloadModelAsync(string modelId, CancellationToken cancellationToken);
    ValueTask<Fin> EnsureModelAsync(string modelId, CancellationToken cancellationToken);
    ValueTask<Fin> WarmModelAsync(string modelId, CancellationToken cancellationToken);
    ValueTask<string> StatusAsync(CancellationToken cancellationToken);
}

public interface IInferenceProvider<out TOptions> : IInferenceProvider
    where TOptions : InferenceProviderOptions
{
    TOptions Options { get; }
}

public interface IInferenceProviderRegistry
{
    IReadOnlyList<InferenceProviderDescriptor> Providers { get; }
    Fin<IInferenceProvider> Resolve(string providerId);
}

public interface IInferenceModelSelectionStore
{
    ValueTask<InferenceModelSelection> GetAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(InferenceModelSelection selection, CancellationToken cancellationToken);
}

public interface IHermesService
{
    ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken);
    ValueTask<Fin> ToolCallSmokeTestAsync(string repositoryPath, CancellationToken cancellationToken);
    ValueTask<ReviewResult> RunPrePushReviewAsync(CodeReviewRequest request, CancellationToken cancellationToken);
}

public interface IOpenClawService
{
    ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken);
    ValueTask<Fin> StartGatewayAsync(CancellationToken cancellationToken);
    ValueTask<Fin> StopGatewayAsync(CancellationToken cancellationToken);
    ValueTask<string> StatusAsync(CancellationToken cancellationToken);
}
