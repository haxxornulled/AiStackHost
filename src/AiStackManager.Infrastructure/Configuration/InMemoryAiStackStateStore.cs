using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Configuration;
using AiStackManager.Domain.Stack;
using Microsoft.Extensions.Options;

namespace AiStackManager.Infrastructure.Configuration;

public sealed class InMemoryAiStackStateStore : IAiStackStateStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AiStackAggregate _aggregate;

    public InMemoryAiStackStateStore(IOptionsMonitor<AiStackSettings> settings)
    {
        var created = AiStackAggregate.Create(settings.CurrentValue);
        _aggregate = created.IsSucc ? created.Value : throw new InvalidOperationException(created.Error.Message);

        settings.OnChange(changed =>
        {
            _gate.Wait();
            try
            {
                _aggregate.ApplySettings(changed);
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    public async ValueTask<AiStackAggregate> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return _aggregate; }
        finally { _gate.Release(); }
    }

    public async ValueTask SaveAsync(AiStackAggregate aggregate, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { _aggregate = aggregate; }
        finally { _gate.Release(); }
    }
}
