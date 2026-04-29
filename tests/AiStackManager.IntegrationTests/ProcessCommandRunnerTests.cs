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
        Assert.Contains("hello", result.StandardOutput);
    }
}
