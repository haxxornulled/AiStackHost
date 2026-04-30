using AiStackManager.Application.Abstractions;
using AiStackManager.Infrastructure.Ollama;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class OllamaServiceTests
{
    [Fact]
    public async Task StartAsync_CreatesUserService_WhenSystemdUnitIsMissing()
    {
        var runner = new RecordingCommandRunner(command =>
            command.FileName == "systemctl" && command.Arguments.SequenceEqual(["--user", "start", "ollama.service"])
                ? "Unit ollama.service not found."
                : null);
        var service = new OllamaService(runner, NullLogger<OllamaService>.Instance);

        var result = await service.StartAsync(CancellationToken.None);

        Assert.True(result.IsSucc);
        Assert.Contains(runner.Commands, command => command.FileName == "bash" &&
                                                    command.Arguments.Count == 2 &&
                                                    command.Arguments[0] == "-lc" &&
                                                    command.Arguments[1].Contains("systemctl --user enable --now ollama.service", StringComparison.Ordinal));
        Assert.Contains(runner.Commands, command => command.FileName == "ollama" && command.Arguments.SequenceEqual(["ps"]));
    }

    [Fact]
    public async Task StopAsync_TreatsMissingUserServiceAsNoOp()
    {
        var runner = new RecordingCommandRunner(command =>
            command.FileName == "systemctl" && command.Arguments.SequenceEqual(["--user", "stop", "ollama.service"])
                ? "Unit ollama.service could not be found."
                : null);
        var service = new OllamaService(runner, NullLogger<OllamaService>.Instance);

        var result = await service.StopAsync(CancellationToken.None);

        Assert.True(result.IsSucc);
    }

    [Fact]
    public async Task EnsureModelAsync_TreatsUntaggedModelAsLatest()
    {
        var runner = new RecordingCommandRunner(_ => null)
        {
            Output = command => command.FileName == "ollama" && command.Arguments.SequenceEqual(["list"])
                ? """
                  NAME                           ID              SIZE      MODIFIED
                  qwen25-coder-14b-64k:latest    0f6fd99e6bff    9.0 GB    10 hours ago
                  """
                : ""
        };
        var service = new OllamaService(runner, NullLogger<OllamaService>.Instance);

        var result = await service.EnsureModelAsync("qwen25-coder-14b-64k", CancellationToken.None);

        Assert.True(result.IsSucc);
        Assert.DoesNotContain(runner.Commands, command => command.FileName == "ollama" && command.Arguments.Contains("pull"));
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        private readonly Func<CommandResult, string?> _failure;

        public RecordingCommandRunner(Func<CommandResult, string?> failure) => _failure = failure;
        public List<CommandResult> Commands { get; } = [];
        public Func<CommandResult, string> Output { get; init; } = _ => "";

        public ValueTask<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var command = new CommandResult(fileName, arguments, workingDirectory, 0, "", "", TimeSpan.Zero);
            var failure = _failure(command);
            var result = failure is null
                ? command with { StandardOutput = Output(command) }
                : command with { ExitCode = 1, StandardError = failure };

            Commands.Add(result);
            return ValueTask.FromResult(result);
        }
    }
}
