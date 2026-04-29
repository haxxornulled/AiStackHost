using AiStackManager.Infrastructure.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class ProcessCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_Captures_Stdout()
    {
        var runner = new ProcessCommandRunner(NullLogger<ProcessCommandRunner>.Instance);
        var result = await runner.RunAsync("bash", ["-lc", "printf hello"], null, TimeSpan.FromSeconds(5), CancellationToken.None);
        Assert.True(result.Succeeded);

        var runReal = string.Equals(Environment.GetEnvironmentVariable("AISTACK_RUN_REAL_COMMANDS"), "true", StringComparison.OrdinalIgnoreCase);
        if (runReal)
        {
            Assert.Contains("hello", result.StandardOutput);
        }
        else
        {
            // In hermetic mode the runner simulates commands; ensure we got the simulated marker
            Assert.Equal(string.Empty, result.StandardOutput);
            Assert.Contains("(simulated)", result.StandardError);
        }
    }
}
