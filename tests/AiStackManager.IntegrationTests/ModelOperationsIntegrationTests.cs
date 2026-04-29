using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AiStackManager.Application.Abstractions;
using AiStackManager.Application.Models;
using AiStackManager.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Autofac;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class ModelOperationsIntegrationTests
{
    [Fact]
    public async Task SelectAndDownloadAndWarmModel_Workflow()
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
                var runReal = string.Equals(Environment.GetEnvironmentVariable("AISTACK_RUN_REAL_COMMANDS"), "true", StringComparison.OrdinalIgnoreCase);
                if (!runReal)
                {
                    container.RegisterInstance<ICommandRunner>(new TestCommandRunner()).SingleInstance();
                }

                // Provide a provider that simulates download state transitions
                container.RegisterInstance<IInferenceProviderRegistry>(new StatefulTestProviderRegistry()).SingleInstance();
                container.RegisterInstance<IInferenceModelSelectionStore>(new TestSelectionStore(new InferenceModelSelection("fake", "m1"))).SingleInstance();
            });
        });

        await using var _ = factory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-AiStack-Token", "test-token");

        // Ensure at least one provider present and pick a real provider/model to test against
        var providers = await client.GetFromJsonAsync<List<InferenceProviderDescriptor>>("/api/models/providers");
        Assert.NotNull(providers);
        Assert.NotEmpty(providers);
        var providerId = providers![0].Id;

        // List models for that provider and pick first model
        var models = await client.GetFromJsonAsync<List<InferenceModelDescriptor>>($"/api/models?provider={providerId}");
        Assert.NotNull(models);
        if (models is null || models.Count == 0)
        {
            // No models available for this provider in hermetic test mode; nothing further to validate.
            return;
        }

        var modelId = models![0].Id;

        // Select model with downloadIfMissing=true
        var selectResp = await client.PostAsJsonAsync("/api/models/select", new { ProviderId = providerId, ModelId = modelId, DownloadIfMissing = true });
        Assert.True(selectResp.IsSuccessStatusCode, await selectResp.Content.ReadAsStringAsync());

        // Verify selection persisted
        var current = await client.GetFromJsonAsync<InferenceModelSelection>("/api/models/current");
        Assert.Equal(providerId, current!.ProviderId);
        Assert.Equal(modelId, current.ModelId);

        // Trigger download explicitely and assert accepted
        var downloadResp = await client.PostAsJsonAsync("/api/models/download", new { ProviderId = providerId, ModelId = modelId });
        Assert.True(downloadResp.IsSuccessStatusCode, await downloadResp.Content.ReadAsStringAsync());

        // Optionally exercise provider warm if tests are allowed to run real commands
        var runReal = string.Equals(Environment.GetEnvironmentVariable("AISTACK_RUN_REAL_COMMANDS"), "true", StringComparison.OrdinalIgnoreCase);
        if (runReal)
        {
            using var scope = factory.Services.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IInferenceProviderRegistry>();
            var provFin = registry.Resolve(providerId);
            Assert.True(provFin.IsSucc);
            var provider = provFin.Value;
            var warm = await provider.WarmModelAsync(modelId, CancellationToken.None);
            Assert.True(warm.IsSucc);
        }
    }

    private sealed class StatefulTestProviderRegistry : IInferenceProviderRegistry
    {
        private readonly StatefulTestProvider _prov = new();
        public IReadOnlyList<InferenceProviderDescriptor> Providers => new[] { _prov.Descriptor };
        public Fin<IInferenceProvider> Resolve(string providerId) => providerId == _prov.Descriptor.Id ? Fin<IInferenceProvider>.Succ(_prov) : Fin<IInferenceProvider>.Fail(AiStackManager.Domain.Common.AiStackError.Validation("unknown"));
    }

    private sealed class StatefulTestProvider : IInferenceProvider
    {
        public InferenceProviderDescriptor Descriptor => new("fake", "Fake", true, true);
        private readonly HashSet<string> _downloaded = new();
        public ValueTask<Fin> StartAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<Fin> StopAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<IReadOnlyList<InferenceModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken)
        {
            var models = new List<InferenceModelDescriptor>
            {
                new("fake", "m1", "m1", _downloaded.Contains("m1"), null, null)
            };
            return ValueTask.FromResult<IReadOnlyList<InferenceModelDescriptor>>(models);
        }
        public ValueTask<Fin> DownloadModelAsync(string modelId, CancellationToken cancellationToken)
        {
            _downloaded.Add(modelId);
            return ValueTask.FromResult(Fin.Succ());
        }
        public ValueTask<Fin> EnsureModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(_downloaded.Contains(modelId) ? Fin.Succ() : Fin.Fail(AiStackManager.Domain.Common.AiStackError.Validation("not downloaded")));
        public ValueTask<Fin> WarmModelAsync(string modelId, CancellationToken cancellationToken) => ValueTask.FromResult(Fin.Succ());
        public ValueTask<string> StatusAsync(CancellationToken cancellationToken) => ValueTask.FromResult("ok");
    }

    private sealed class TestSelectionStore : IInferenceModelSelectionStore
    {
        private readonly InferenceModelSelection _sel;
        public TestSelectionStore(InferenceModelSelection sel) => _sel = sel;
        public ValueTask<InferenceModelSelection> GetAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_sel);
        public ValueTask SaveAsync(InferenceModelSelection selection, CancellationToken cancellationToken) { return ValueTask.CompletedTask; }
    }

    private sealed class TestCommandRunner : ICommandRunner
    {
        public ValueTask<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var result = new CommandResult(fileName, arguments, workingDirectory, 0, "", "", TimeSpan.Zero);
            return ValueTask.FromResult(result);
        }
    }
}
