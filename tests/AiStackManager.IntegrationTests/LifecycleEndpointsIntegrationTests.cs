using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;
using AiStackManager.Domain.Stack;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Autofac;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class LifecycleEndpointsIntegrationTests
{
    [Fact]
    public async Task StartStopEndpoints_ReturnAccepted_WithMockedServices()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiStack:ManagementToken"] = "test-token",
                });
            });

                builder.ConfigureTestContainer<ContainerBuilder>(container =>
                {
                    // Allow opting into running real system commands in tests via env var:
                    // Set AISTACK_RUN_REAL_COMMANDS=true to use the real ProcessCommandRunner.
                    var runReal = string.Equals(Environment.GetEnvironmentVariable("AISTACK_RUN_REAL_COMMANDS"), "true", StringComparison.OrdinalIgnoreCase);

                    if (!runReal)
                    {
                        // Prevent actual shell command execution by overriding ICommandRunner
                        container.RegisterInstance<AiStackManager.Application.Abstractions.ICommandRunner>(new TestCommandRunner()).SingleInstance();
                    }

                    // IAiStackStateStore - returns a healthy default aggregate
                    container.RegisterInstance<IAiStackStateStore>(new TestStateStore(new AiStackSettings())).SingleInstance();

                    // IInferenceModelSelectionStore - returns a known selection
                    container.RegisterInstance<IInferenceModelSelectionStore>(new TestSelectionStore(new InferenceModelSelection("fake", "m1"))).SingleInstance();

                    // IInferenceProviderRegistry - a registry with a fake provider that succeeds
                    container.RegisterInstance<IInferenceProviderRegistry>(new TestProviderRegistry()).SingleInstance();

                    // IHermesService - always succeed
                    container.RegisterInstance<IHermesService>(new TestHermes()).SingleInstance();

                    // IOpenClawService - always succeed
                    container.RegisterInstance<IOpenClawService>(new TestOpenClaw()).SingleInstance();
                });
        });

        await using var _ = factory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-AiStack-Token", "test-token");

        var start = await client.PostAsync("/api/stack/start", null);
        Assert.True(start.IsSuccessStatusCode, await start.Content.ReadAsStringAsync());

        var stop = await client.PostAsync("/api/stack/stop", null);
        Assert.True(stop.IsSuccessStatusCode, await stop.Content.ReadAsStringAsync());
    }

    private sealed class TestCommandRunner : AiStackManager.Application.Abstractions.ICommandRunner
    {
        public ValueTask<AiStackManager.Application.Abstractions.CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var result = new AiStackManager.Application.Abstractions.CommandResult(
                fileName,
                arguments,
                workingDirectory,
                0,
                "",
                "",
                TimeSpan.Zero);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestStateStore : IAiStackStateStore
    {
        private readonly AiStackAggregate _agg;
        public TestStateStore(AiStackSettings settings) => _agg = AiStackAggregate.Create(settings).Value!;
        public ValueTask<AiStackAggregate> GetAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_agg);
        public ValueTask SaveAsync(AiStackAggregate aggregate, CancellationToken cancellationToken) { return ValueTask.CompletedTask; }
    }

    private sealed class TestSelectionStore : IInferenceModelSelectionStore
    {
        private readonly InferenceModelSelection _sel;
        public TestSelectionStore(InferenceModelSelection sel) => _sel = sel;
        public ValueTask<InferenceModelSelection> GetAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_sel);
        public ValueTask SaveAsync(InferenceModelSelection selection, CancellationToken cancellationToken) { return ValueTask.CompletedTask; }
    }

    private sealed class TestProviderRegistry : IInferenceProviderRegistry
    {
        public IReadOnlyList<InferenceProviderDescriptor> Providers => new[] { new InferenceProviderDescriptor("fake", "Fake", true, true) };
        public Fin<IInferenceProvider> Resolve(string providerId)
        {
            if (string.Equals(providerId, "fake", StringComparison.OrdinalIgnoreCase))
                return Fin<IInferenceProvider>.Succ(new TestProvider());
            return Fin<IInferenceProvider>.Fail(AiStackError.Validation($"Unknown provider '{providerId}'"));
        }
    }

    private sealed class TestProvider : IInferenceProvider
    {
        public InferenceProviderDescriptor Descriptor => new("fake", "Fake", true, true);
        public ValueTask<Fin> StartAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> StopAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<IReadOnlyList<InferenceModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<InferenceModelDescriptor>>(new[] { new InferenceModelDescriptor("fake", "m1", "m1", true, null, null) });
        public ValueTask<Fin> DownloadModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> EnsureModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> WarmModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<string> StatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult("ok");
    }

        private sealed class TestHermes : IHermesService
    {
        public ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> ToolCallSmokeTestAsync(string repositoryPath, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<AiStackManager.Domain.Review.ReviewResult> RunPrePushReviewAsync(AiStackManager.Domain.Review.CodeReviewRequest request, CancellationToken cancellationToken)
            => ValueTask.FromResult(new AiStackManager.Domain.Review.ReviewResult(
                AiStackManager.Domain.Review.ReviewVerdict.Approve,
                true,
                "ok",
                new List<AiStackManager.Domain.Review.ReviewFinding>(),
                new List<string>(),
                new List<string>()));
    }

    private sealed class TestOpenClaw : IOpenClawService
    {
        public ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> StartGatewayAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> StopGatewayAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<string> StatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult("ok");
    }
}
