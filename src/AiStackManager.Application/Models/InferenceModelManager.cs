using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Common;
using Microsoft.Extensions.Logging;

namespace AiStackManager.Application.Models;

public sealed class InferenceModelManager
{
    private readonly IInferenceProviderRegistry _providers;
    private readonly IInferenceModelSelectionStore _selectionStore;
    private readonly ILogger<InferenceModelManager> _logger;

    public InferenceModelManager(
        IInferenceProviderRegistry providers,
        IInferenceModelSelectionStore selectionStore,
        ILogger<InferenceModelManager> logger)
    {
        _providers = providers;
        _selectionStore = selectionStore;
        _logger = logger;
    }

    public IReadOnlyList<InferenceProviderDescriptor> Providers => _providers.Providers;

    public async ValueTask<Fin<IReadOnlyList<InferenceModelDescriptor>>> ListModelsAsync(string? providerId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            var provider = _providers.Resolve(providerId);
            if (provider.IsFail) return Fin<IReadOnlyList<InferenceModelDescriptor>>.Fail(provider.Error);
            return Fin<IReadOnlyList<InferenceModelDescriptor>>.Succ(await provider.Value.ListModelsAsync(cancellationToken));
        }

        var models = new List<InferenceModelDescriptor>();
        foreach (var descriptor in _providers.Providers)
        {
            var provider = _providers.Resolve(descriptor.Id);
            if (provider.IsSucc)
                models.AddRange(await provider.Value.ListModelsAsync(cancellationToken));
        }

        return Fin<IReadOnlyList<InferenceModelDescriptor>>.Succ(models);
    }

    public ValueTask<InferenceModelSelection> CurrentAsync(CancellationToken cancellationToken)
        => _selectionStore.GetAsync(cancellationToken);

    public async ValueTask<Fin<InferenceModelSelection>> SelectAsync(InferenceModelSelection selection, bool downloadIfMissing, CancellationToken cancellationToken)
    {
        var provider = _providers.Resolve(selection.ProviderId);
        if (provider.IsFail) return Fin<InferenceModelSelection>.Fail(provider.Error);

        var models = await provider.Value.ListModelsAsync(cancellationToken);
        var local = models.FirstOrDefault(m => string.Equals(m.Id, selection.ModelId, StringComparison.OrdinalIgnoreCase));

        if (local is null || !local.IsDownloaded)
        {
            if (!downloadIfMissing)
                return Fin<InferenceModelSelection>.Fail(AiStackError.Validation($"Model '{selection.ProviderId}/{selection.ModelId}' is not downloaded."));

            var download = await provider.Value.DownloadModelAsync(selection.ModelId, cancellationToken);
            if (download.IsFail) return Fin<InferenceModelSelection>.Fail(download.Error);
        }

        _logger.LogInformation("Selected inference model {ProviderId}/{ModelId}", selection.ProviderId, selection.ModelId);
        await _selectionStore.SaveAsync(selection, cancellationToken);
        return Fin<InferenceModelSelection>.Succ(selection);
    }

    public async ValueTask<Fin> DownloadAsync(InferenceModelSelection selection, CancellationToken cancellationToken)
    {
        var provider = _providers.Resolve(selection.ProviderId);
        return provider.IsFail
            ? Fin.Fail(provider.Error)
            : await provider.Value.DownloadModelAsync(selection.ModelId, cancellationToken);
    }
}
