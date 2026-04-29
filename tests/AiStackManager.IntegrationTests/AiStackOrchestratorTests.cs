using AiStackManager.Application.Abstractions;
using AiStackManager.Application.Stack;
using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;
using AiStackManager.Domain.Review;
using AiStackManager.Domain.Stack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class AiStackOrchestratorTests
{
    [Fact]
    public async Task StartAsync_DoesNotStartProviderRuntime_WhenRuntimeIsExternallyManaged()
    {
        var settings = new AiStackSettings { OwnInferenceRuntime = false };
        var provider = new RecordingProvider();
        var orchestrator = CreateOrchestrator(settings, provider);

        var result = await orchestrator.StartAsync(CancellationToken.None);

        Assert.True(result.IsSucc);
        Assert.Equal(0, provider.StartCalls);
        Assert.Equal(1, provider.EnsureCalls);
        Assert.Equal(1, provider.WarmCalls);
    }

    [Fact]
    public async Task StartAsync_StartsProviderRuntime_WhenRuntimeIsOwned()
    {
        var settings = new AiStackSettings { OwnInferenceRuntime = true };
        var provider = new RecordingProvider();
        var orchestrator = CreateOrchestrator(settings, provider);

        var result = await orchestrator.StartAsync(CancellationToken.None);

        Assert.True(result.IsSucc);
        Assert.Equal(1, provider.StartCalls);
    }

    private static AiStackOrchestrator CreateOrchestrator(AiStackSettings settings, RecordingProvider provider)
    {
        return new AiStackOrchestrator(
            new TestStateStore(settings),
            new TestProviderRegistry(provider),
            new TestSelectionStore(new InferenceModelSelection("fake", settings.Model)),
            new TestOptionsMonitor<AiStackSettings>(settings),
            new TestHermes(),
            new TestOpenClaw(),
            NullLogger<AiStackOrchestrator>.Instance);
    }

    private sealed class TestStateStore : IAiStackStateStore
    {
        private AiStackAggregate _aggregate;

        public TestStateStore(AiStackSettings settings) => _aggregate = AiStackAggregate.Create(settings).Value;
        public ValueTask<AiStackAggregate> GetAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_aggregate);
        public ValueTask SaveAsync(AiStackAggregate aggregate, CancellationToken cancellationToken)
        {
            _aggregate = aggregate;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestSelectionStore : IInferenceModelSelectionStore
    {
        private readonly InferenceModelSelection _selection;

        public TestSelectionStore(InferenceModelSelection selection) => _selection = selection;
        public ValueTask<InferenceModelSelection> GetAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_selection);
        public ValueTask SaveAsync(InferenceModelSelection selection, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class TestProviderRegistry : IInferenceProviderRegistry
    {
        private readonly IInferenceProvider _provider;

        public TestProviderRegistry(IInferenceProvider provider) => _provider = provider;
        public IReadOnlyList<InferenceProviderDescriptor> Providers => [_provider.Descriptor];

        public Fin<IInferenceProvider> Resolve(string providerId)
            => string.Equals(providerId, _provider.Descriptor.Id, StringComparison.OrdinalIgnoreCase)
                ? Fin<IInferenceProvider>.Succ(_provider)
                : Fin<IInferenceProvider>.Fail(AiStackError.Validation($"Unknown provider '{providerId}'"));
    }

    private sealed class RecordingProvider : IInferenceProvider
    {
        public int StartCalls { get; private set; }
        public int EnsureCalls { get; private set; }
        public int WarmCalls { get; private set; }
        public InferenceProviderDescriptor Descriptor { get; } = new("fake", "Fake", SupportsModelDownload: true, SupportsLocalRuntime: true);

        public ValueTask<Fin> StartAsync(CancellationToken cancellationToken)
        {
            StartCalls++;
            return ValueTask.FromResult(Fin.Succ());
        }

        public ValueTask<Fin> StopAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<IReadOnlyList<InferenceModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<InferenceModelDescriptor>>([]);
        public ValueTask<Fin> DownloadModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());

        public ValueTask<Fin> EnsureModelAsync(string modelId, CancellationToken cancellationToken)
        {
            EnsureCalls++;
            return ValueTask.FromResult(Fin.Succ());
        }

        public ValueTask<Fin> WarmModelAsync(string modelId, CancellationToken cancellationToken)
        {
            WarmCalls++;
            return ValueTask.FromResult(Fin.Succ());
        }

        public ValueTask<string> StatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult("ok");
    }

    private sealed class TestHermes : IHermesService
    {
        public ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> ToolCallSmokeTestAsync(string repositoryPath, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<ReviewResult> RunPrePushReviewAsync(CodeReviewRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ReviewResult(ReviewVerdict.Approve, true, "ok", [], [], []));
    }

    private sealed class TestOpenClaw : IOpenClawService
    {
        public ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> StartGatewayAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> StopGatewayAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<string> StatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult("ok");
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
