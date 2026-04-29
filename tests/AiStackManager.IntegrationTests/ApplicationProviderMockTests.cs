using AiStackManager.Application.Abstractions;
using AiStackManager.Application.Models;
using AiStackManager.Domain.Configuration;
using AiStackManager.Domain.Common;
using AiStackManager.Infrastructure.Inference;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class ApplicationProviderMockTests
{
    [Fact]
    public async Task SelectAsync_Downloads_When_Missing_And_DownloadRequested()
    {
        var provider = Substitute.For<IInferenceProvider>();
        provider.Descriptor.Returns(new InferenceProviderDescriptor("fake", "Fake", SupportsModelDownload: true, SupportsLocalRuntime: false));
        provider.ListModelsAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IReadOnlyList<InferenceModelDescriptor>>(new[] { new InferenceModelDescriptor("fake", "m1", "m1", IsDownloaded: false, SizeBytes: null, Details: null) }));
        provider.DownloadModelAsync("m1", Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(Fin.Succ()));

        var registry = new InferenceProviderRegistry(new[] { provider });
        var store = new InMemoryInferenceModelSelectionStore(new TestOptionsMonitor<AiStackSettings>(new AiStackSettings { InferenceProvider = "fake", Model = "initial" }));
        var manager = new InferenceModelManager(registry, store, NullLogger<InferenceModelManager>.Instance);

        var result = await manager.SelectAsync(new InferenceModelSelection("fake", "m1"), downloadIfMissing: true, CancellationToken.None);

        Assert.True(result.IsSucc);
        await provider.Received(1).DownloadModelAsync("m1", Arg.Any<CancellationToken>());
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
