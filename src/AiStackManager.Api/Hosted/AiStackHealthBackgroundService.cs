using AiStackManager.Application.Health;

namespace AiStackManager.Api.Hosted;

public sealed class AiStackHealthBackgroundService : BackgroundService
{
    private readonly AiStackHealthMonitor _health;
    private readonly ILogger<AiStackHealthBackgroundService> _logger;

    public AiStackHealthBackgroundService(AiStackHealthMonitor health, ILogger<AiStackHealthBackgroundService> logger)
    {
        _health = health;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var snapshot = await _health.SnapshotAsync(stoppingToken);
                _logger.LogDebug("AI stack health snapshot: {@Snapshot}", snapshot);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health snapshot failed.");
            }
        }
    }
}
