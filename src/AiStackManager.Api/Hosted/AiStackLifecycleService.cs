using AiStackManager.Application.Stack;
using AiStackManager.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace AiStackManager.Api.Hosted;

public sealed class AiStackLifecycleService : IHostedLifecycleService
{
    private readonly AiStackOrchestrator _orchestrator;
    private readonly IOptionsMonitor<AiStackSettings> _settings;
    private readonly ILogger<AiStackLifecycleService> _logger;

    public AiStackLifecycleService(AiStackOrchestrator orchestrator, IOptionsMonitor<AiStackSettings> settings, ILogger<AiStackLifecycleService> logger)
    {
        _orchestrator = orchestrator;
        _settings = settings;
        _logger = logger;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartingAsync: validate configuration and prepare bridge seams.");
        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.CurrentValue.AutoStartOnHostStartup)
        {
            _logger.LogInformation("StartAsync: managed AI stack auto-start disabled.");
            return;
        }

        _logger.LogInformation("StartAsync: starting managed AI stack because auto-start is enabled.");
        var result = await _orchestrator.StartAsync(cancellationToken);
        if (result.IsFail) _logger.LogWarning("AI stack start completed with failure: {Error}", result.Error.Message);
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartedAsync: API surface is ready.");
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StoppingAsync: stop accepting orchestration work.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StopAsync: stopping managed AI stack.");
        await _orchestrator.StopAsync(cancellationToken);
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StoppedAsync: final state recorded.");
        return Task.CompletedTask;
    }
}
