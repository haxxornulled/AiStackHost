using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiStackManager.Application.Stack;

public sealed class AiStackOrchestrator
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly IAiStackStateStore _stateStore;
    private readonly IInferenceProviderRegistry _inferenceProviders;
    private readonly IInferenceModelSelectionStore _modelSelection;
    private readonly IOptionsMonitor<AiStackSettings> _settings;
    private readonly IHermesService _hermes;
    private readonly IOpenClawService _openClaw;
    private readonly ILogger<AiStackOrchestrator> _logger;

    public AiStackOrchestrator(
        IAiStackStateStore stateStore,
        IInferenceProviderRegistry inferenceProviders,
        IInferenceModelSelectionStore modelSelection,
        IOptionsMonitor<AiStackSettings> settings,
        IHermesService hermes,
        IOpenClawService openClaw,
        ILogger<AiStackOrchestrator> logger)
    {
        _stateStore = stateStore;
        _inferenceProviders = inferenceProviders;
        _modelSelection = modelSelection;
        _settings = settings;
        _hermes = hermes;
        _openClaw = openClaw;
        _logger = logger;
    }

    public async ValueTask<Fin> StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var stack = await _stateStore.GetAsync(cancellationToken);
            var settings = _settings.CurrentValue;
            var appliedSettings = stack.ApplySettings(settings);
            if (appliedSettings.IsFail) return appliedSettings;

            var begin = stack.BeginStart();
            if (begin.IsFail) return begin;

            var selection = await _modelSelection.GetAsync(cancellationToken);
            var effectiveSettings = settings with
            {
                InferenceProvider = selection.ProviderId,
                Model = selection.ModelId,
                OpenClawModelRef = ""
            };
            var provider = _inferenceProviders.Resolve(selection.ProviderId);
            if (provider.IsFail)
            {
                stack.Fail(selection.ProviderId, provider.Error.Message);
                await _stateStore.SaveAsync(stack, cancellationToken);
                return Fin.Fail(provider.Error);
            }

            _logger.LogInformation("Starting AI stack with model {ProviderId}/{ModelId}", selection.ProviderId, selection.ModelId);

            if (settings.OwnInferenceRuntime)
            {
                var runtimeStart = await provider.Value.StartAsync(cancellationToken);
                if (runtimeStart.IsFail) stack.Fail(selection.ProviderId, runtimeStart.Error.Message); else stack.Get(selection.ProviderId).MarkRunning("Inference provider started.");
            }

            var ensureModel = await provider.Value.EnsureModelAsync(selection.ModelId, cancellationToken);
            if (ensureModel.IsFail) stack.Fail(selection.ProviderId, ensureModel.Error.Message);

            var warm = await provider.Value.WarmModelAsync(selection.ModelId, cancellationToken);
            if (warm.IsFail) stack.Degrade(selection.ProviderId, warm.Error.Message);

            var hermesConfig = await _hermes.ConfigureAsync(effectiveSettings, cancellationToken);
            if (hermesConfig.IsFail) stack.Degrade("hermes", hermesConfig.Error.Message); else stack.Get("hermes").MarkRunning("Hermes configured.");

            var openClawConfig = await _openClaw.ConfigureAsync(effectiveSettings, cancellationToken);
            if (openClawConfig.IsFail) stack.Degrade("openclaw", openClawConfig.Error.Message);

            var gateway = await _openClaw.StartGatewayAsync(cancellationToken);
            if (gateway.IsFail) stack.Degrade("openclaw", gateway.Error.Message); else stack.Get("openclaw").MarkRunning("OpenClaw gateway started.");

            stack.CompleteStart();
            await _stateStore.SaveAsync(stack, cancellationToken);
            return Fin.Succ();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<Fin> StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var stack = await _stateStore.GetAsync(cancellationToken);
            var settings = _settings.CurrentValue;
            var appliedSettings = stack.ApplySettings(settings);
            if (appliedSettings.IsFail) return appliedSettings;

            var selection = await _modelSelection.GetAsync(cancellationToken);
            var begin = stack.BeginStop();
            if (begin.IsFail) return begin;

            await _openClaw.StopGatewayAsync(cancellationToken);
            if (settings.OwnInferenceRuntime)
            {
                var provider = _inferenceProviders.Resolve(selection.ProviderId);
                if (provider.IsSucc)
                    await provider.Value.StopAsync(cancellationToken);
            }

            stack.CompleteStop();
            await _stateStore.SaveAsync(stack, cancellationToken);
            return Fin.Succ();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<object> StatusAsync(CancellationToken cancellationToken)
    {
        var stack = await _stateStore.GetAsync(cancellationToken);
        stack.ApplySettings(_settings.CurrentValue);
        var selection = await _modelSelection.GetAsync(cancellationToken);
        return new
        {
            stack.Phase,
            Inference = selection,
            stack.Settings.ContextLength,
            stack.UpdatedAt,
            Components = stack.Components.ToDictionary(k => k.Key, v => new { v.Value.State, v.Value.UpdatedAt, v.Value.Diagnostics })
        };
    }
}
