using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Common;

namespace AiStackManager.Infrastructure.Inference;

public sealed class InferenceProviderRegistry : IInferenceProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IInferenceProvider> _providers;

    public InferenceProviderRegistry(IEnumerable<IInferenceProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<InferenceProviderDescriptor> Providers => _providers.Values.Select(p => p.Descriptor).OrderBy(p => p.Id).ToArray();

    public Fin<IInferenceProvider> Resolve(string providerId)
        => _providers.TryGetValue(providerId, out var provider)
            ? Fin<IInferenceProvider>.Succ(provider)
            : Fin<IInferenceProvider>.Fail(AiStackError.Validation($"Inference provider '{providerId}' is not registered."));
}
