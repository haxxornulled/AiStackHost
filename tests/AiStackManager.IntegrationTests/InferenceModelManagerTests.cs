using AiStackManager.Application.Abstractions;
using AiStackManager.Application.Models;
using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;
using AiStackManager.Infrastructure.Inference;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class InferenceModelManagerTests
{
    [Fact]
    public async Task SelectAsync_Rejects_Missing_Model_When_Download_Not_Requested()
    {
        var manager = CreateManager(new FakeInferenceProvider([]));

        var result = await manager.SelectAsync(new InferenceModelSelection("fake", "missing"), downloadIfMissing: false, CancellationToken.None);

        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task SelectAsync_Downloads_Missing_Model_When_Requested()
    {
        var provider = new FakeInferenceProvider([]);
        var manager = CreateManager(provider);

        var result = await manager.SelectAsync(new InferenceModelSelection("fake", "new-model"), downloadIfMissing: true, CancellationToken.None);

        Assert.True(result.IsSucc);
        Assert.Contains("new-model", provider.Downloads);
        Assert.Equal(new InferenceModelSelection("fake", "new-model"), await manager.CurrentAsync(CancellationToken.None));
    }

    private static InferenceModelManager CreateManager(IInferenceProvider provider)
    {
        var registry = new InferenceProviderRegistry([provider]);
        var store = new InMemoryInferenceModelSelectionStore(new TestOptionsMonitor<AiStackSettings>(new AiStackSettings { InferenceProvider = "fake", Model = "initial" }));
        return new InferenceModelManager(registry, store, NullLogger<InferenceModelManager>.Instance);
    }

    private sealed class FakeInferenceProvider : IInferenceProvider<InferenceProviderOptions>
    {
        private readonly List<InferenceModelDescriptor> _models;

        public FakeInferenceProvider(IEnumerable<InferenceModelDescriptor> models)
        {
            _models = models.ToList();
        }

        public List<string> Downloads { get; } = [];
        public InferenceProviderOptions Options { get; } = new FakeInferenceProviderOptions();
        public InferenceProviderDescriptor Descriptor { get; } = new("fake", "Fake", SupportsModelDownload: true, SupportsLocalRuntime: false);

        public ValueTask<Fin> StartAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> StopAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<IReadOnlyList<InferenceModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<InferenceModelDescriptor>>(_models);

        public ValueTask<Fin> DownloadModelAsync(string modelId, CancellationToken cancellationToken)
        {
            Downloads.Add(modelId);
            _models.Add(new InferenceModelDescriptor("fake", modelId, modelId, IsDownloaded: true, SizeBytes: null, Details: null));
            return ValueTask.FromResult(Fin.Succ());
        }

        public ValueTask<Fin> EnsureModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> WarmModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<string> StatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult("ok");
    }

    private sealed record FakeInferenceProviderOptions : InferenceProviderOptions;

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
