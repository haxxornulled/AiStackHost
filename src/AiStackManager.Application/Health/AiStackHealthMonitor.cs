using AiStackManager.Application.Abstractions;

namespace AiStackManager.Application.Health;

public sealed class AiStackHealthMonitor
{
    private readonly IInferenceProviderRegistry _inferenceProviders;
    private readonly IInferenceModelSelectionStore _modelSelection;
    private readonly IOpenClawService _openClaw;

    public AiStackHealthMonitor(
        IInferenceProviderRegistry inferenceProviders,
        IInferenceModelSelectionStore modelSelection,
        IOpenClawService openClaw)
    {
        _inferenceProviders = inferenceProviders;
        _modelSelection = modelSelection;
        _openClaw = openClaw;
    }

    public async ValueTask<object> SnapshotAsync(CancellationToken cancellationToken)
    {
        var selection = await _modelSelection.GetAsync(cancellationToken);
        var provider = _inferenceProviders.Resolve(selection.ProviderId);

        return new
        {
            Inference = provider.IsSucc ? await provider.Value.StatusAsync(cancellationToken) : provider.Error.Message,
            OpenClaw = await _openClaw.StatusAsync(cancellationToken),
            At = DateTimeOffset.UtcNow
        };
    }
}
