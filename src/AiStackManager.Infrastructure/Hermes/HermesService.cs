using System.Text;
using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Common;
using AiStackManager.Domain.Configuration;
using AiStackManager.Domain.Review;

namespace AiStackManager.Infrastructure.Hermes;

public sealed class HermesService : IHermesService
{
    private readonly ICommandRunner _commands;

    public HermesService(ICommandRunner commands) => _commands = commands;

    public async ValueTask<Fin> ConfigureAsync(AiStackSettings settings, CancellationToken cancellationToken)
    {
        var operations = new[]
        {
            new[] { "config", "set", "model.provider", settings.HermesProvider },
            new[] { "config", "set", "model.default", settings.Model },
            new[] { "config", "set", "model.base_url", settings.HermesBaseUrl },
            new[] { "config", "set", "model.context_length", settings.ContextLength.ToString() }
        };

        foreach (var args in operations)
        {
            var result = await _commands.RunAsync("hermes", args, null, TimeSpan.FromSeconds(20), cancellationToken);
            if (!result.Succeeded) return Fin.Fail(AiStackError.ExternalTool(result.StandardError));
        }

        return Fin.Succ();
    }

    public async ValueTask<Fin> ToolCallSmokeTestAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var prompt = "Use the terminal tool to run: pwd && git status --short. Do not print JSON. Execute the command.";
        var result = await _commands.RunAsync("hermes", ["chat", "--quiet", "--toolsets", "terminal", "--yolo", "--max-turns", "8", "-q", prompt], repositoryPath, TimeSpan.FromMinutes(2), cancellationToken);

        var combined = result.StandardOutput + "\n" + result.StandardError;
        if (!result.Succeeded) return Fin.Fail(AiStackError.ExternalTool(combined));

        if (combined.Contains("0 tool calls", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("\"name\": \"terminal\"", StringComparison.OrdinalIgnoreCase))
            return Fin.Fail(AiStackError.ExternalTool("Hermes emitted raw tool JSON or reported zero tool calls."));

        return Fin.Succ();
    }

    public async ValueTask<ReviewResult> RunPrePushReviewAsync(CodeReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await _commands.RunAsync(
            "hermes",
            ["chat", "--quiet", "--toolsets", "terminal,skills", "--skills", "dotnet-code-review", "--yolo", "--max-turns", "40", "-q", BuildReviewPrompt(request)],
            request.RepositoryPath,
            TimeSpan.FromMinutes(20),
            cancellationToken);

        var raw = new[] { result.StandardOutput, result.StandardError }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return result.Succeeded
            ? new ReviewResult(ReviewVerdict.Comment, true, "Hermes review completed. Raw output included for parser implementation.", [], [result.CommandLine], raw)
            : new ReviewResult(ReviewVerdict.Failed, true, "Hermes review command failed.", [], [result.CommandLine], raw);
    }

    private static string BuildReviewPrompt(CodeReviewRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Do a strict Codex-style pre-push review of this repo.");
        sb.AppendLine("Do not edit files. Do not commit. Do not push.");
        sb.AppendLine($"Compare against base branch: {request.BaseBranch}.");
        sb.AppendLine("Run git status, branch, fetch, diff stats, full diff, staged diff, and unstaged diff.");
        sb.AppendLine("Inspect changed files directly for context.");
        sb.AppendLine($"Find the solution file. Run restore: {request.RunRestore}. Run Release build: {request.RunBuild}. Run tests: {request.RunTests}.");
        sb.AppendLine("Return Critical, Warnings, Suggestions, Looks Good, Follow-up Fix Plan, and Verdict.");
        sb.AppendLine("Use exact file paths and line numbers.");
        return sb.ToString();
    }
}
