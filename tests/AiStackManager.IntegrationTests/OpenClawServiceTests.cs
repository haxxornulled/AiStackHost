using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Configuration;
using AiStackManager.Infrastructure.OpenClaw;
using Xunit;

namespace AiStackManager.IntegrationTests;

public sealed class OpenClawServiceTests
{
    [Fact]
    public async Task ConfigureAsync_ConfiguresModelWithoutLocalInferenceAlias()
    {
        var runner = new RecordingCommandRunner();
        var service = new OpenClawService(runner);

        var result = await service.ConfigureAsync(new AiStackSettings
        {
            InferenceProvider = "ollama",
            Model = "qwen25-coder-14b-64k"
        }, CancellationToken.None);

        Assert.True(result.IsSucc);
        Assert.DoesNotContain(runner.Commands, command => command.Arguments.Any(arg => arg.Contains("Local Inference", StringComparison.Ordinal)));
        Assert.Contains(runner.Commands, command =>
            command.Arguments.SequenceEqual(["config", "unset", "agents.defaults.models[\"ollama/qwen25-coder-14b-64k\"].alias"]));
        Assert.Contains(runner.Commands, command =>
            command.Arguments.SequenceEqual(["models", "set", "ollama/qwen25-coder-14b-64k"]));
    }

    [Fact]
    public async Task ConfigureAsync_IgnoresMissingLegacyAliasPath()
    {
        var runner = new RecordingCommandRunner
        {
            Failure = command => command.Arguments.SequenceEqual(["config", "unset", "agents.defaults.models[\"ollama/qwen25-coder-14b-64k\"].alias"])
                ? "Config path not found: agents.defaults.models[\"ollama/qwen25-coder-14b-64k\"].alias"
                : null
        };
        var service = new OpenClawService(runner);

        var result = await service.ConfigureAsync(new AiStackSettings
        {
            InferenceProvider = "ollama",
            Model = "qwen25-coder-14b-64k"
        }, CancellationToken.None);

        Assert.True(result.IsSucc);
    }

    private sealed class RecordingCommandRunner : ICommandRunner
    {
        public List<CommandResult> Commands { get; } = [];
        public Func<CommandResult, string?> Failure { get; init; } = _ => null;

        public ValueTask<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var command = new CommandResult(fileName, arguments, workingDirectory, 0, "", "", TimeSpan.Zero);
            var failure = Failure(command);
            var result = failure is null
                ? command
                : command with { ExitCode = 1, StandardError = failure };

            Commands.Add(result);
            return ValueTask.FromResult(result);
        }
    }
}
