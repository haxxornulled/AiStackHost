using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace AiStackManager.Infrastructure.Inference;

public sealed class InMemoryInferenceModelSelectionStore : IInferenceModelSelectionStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceModelSelection _selection;

    public InMemoryInferenceModelSelectionStore(IOptionsMonitor<AiStackSettings> settings)
    {
        _selection = FromSettings(settings.CurrentValue);
        settings.OnChange(changed =>
        {
            _gate.Wait();
            try
            {
                _selection = FromSettings(changed);
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    public async ValueTask<InferenceModelSelection> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return _selection; }
        finally { _gate.Release(); }
    }

    public async ValueTask SaveAsync(InferenceModelSelection selection, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { _selection = selection; }
        finally { _gate.Release(); }
    }

    private static InferenceModelSelection FromSettings(AiStackSettings settings)
        => new(settings.InferenceProvider, settings.Model);
}
