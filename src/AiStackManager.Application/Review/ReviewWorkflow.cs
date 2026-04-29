using AiStackManager.Application.Abstractions;
using AiStackManager.Domain.Review;
using Microsoft.Extensions.Logging;

namespace AiStackManager.Application.Review;

public sealed class ReviewWorkflow
{
    private readonly IHermesService _hermes;
    private readonly ICommandRunner _commands;
    private readonly ILogger<ReviewWorkflow> _logger;

    public ReviewWorkflow(IHermesService hermes, ICommandRunner commands, ILogger<ReviewWorkflow> logger)
    {
        _hermes = hermes;
        _commands = commands;
        _logger = logger;
    }

    public async ValueTask<ReviewResult> RunPrePushReviewAsync(CodeReviewRequest request, CancellationToken cancellationToken)
    {
        var session = new ReviewSession(request);
        var valid = session.Validate();
        if (valid.IsFail) return session.Fail(valid.Error.Message);
        if (!Directory.Exists(request.RepositoryPath))
            return session.Fail("RepositoryPath does not exist.");

        _logger.LogInformation("Starting pre-push review for {RepositoryPath}", request.RepositoryPath);

        var smoke = await _hermes.ToolCallSmokeTestAsync(request.RepositoryPath, cancellationToken);
        if (smoke.IsSucc)
        {
            session.MarkToolCallSmokeTestPassed();
            return await _hermes.RunPrePushReviewAsync(request, cancellationToken);
        }

        session.RecordOutput("Hermes tool-call smoke test failed: " + smoke.Error.Message);
        session.AddFinding(new ReviewFinding(
            ReviewSeverity.Warning,
            "Hermes",
            null,
            "Hermes did not execute a terminal tool-call smoke test.",
            "The model/runtime may emit raw JSON instead of real tool calls, causing reviews to silently do nothing.",
            "Use a tool-capable inference provider, switch model, or use fallback command-based evidence gathering."));

        await RunFallbackEvidenceGatheringAsync(session, cancellationToken);
        return session.Complete("Hermes tool calling failed, so the workflow gathered git/dotnet evidence but did not trust Hermes findings.");
    }

    private async Task RunFallbackEvidenceGatheringAsync(ReviewSession session, CancellationToken cancellationToken)
    {
        var repo = session.Request.RepositoryPath;
        var baseBranch = string.IsNullOrWhiteSpace(session.Request.BaseBranch) ? "origin/main" : session.Request.BaseBranch;
        var commands = new List<(string File, string[] Args)>
        {
            ("git", ["status", "--short"]),
            ("git", ["branch", "--show-current"]),
            ("git", ["fetch", "--all", "--prune"]),
            ("git", ["diff", "--stat"]),
            ("git", ["diff"]),
            ("git", ["diff", "--staged"]),
            ("git", ["diff", "--", "."])
        };

        if (!string.IsNullOrWhiteSpace(baseBranch))
            commands.Add(("git", ["diff", "--stat", baseBranch + "...HEAD"]));

        var solution = Directory.GetFiles(repo, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (solution is not null)
        {
            if (session.Request.RunRestore)
                commands.Add(("dotnet", ["restore", solution]));
            if (session.Request.RunBuild)
                commands.Add(("dotnet", ["build", solution, "-c", "Release", "--no-restore"]));
            if (session.Request.RunTests)
                commands.Add(("dotnet", ["test", solution, "-c", "Release", "--no-build"]));
        }
        else
        {
            session.AddFinding(new ReviewFinding(
                ReviewSeverity.Warning,
                repo,
                null,
                "No solution file was found at the repository root.",
                "The review workflow cannot run the documented restore/build/test sequence without an explicit solution entrypoint.",
                "Add a solution file or extend the workflow to discover project files recursively."));
            commands.Add(("dotnet", ["--info"]));
        }

        foreach (var command in commands)
        {
            var result = await _commands.RunAsync(command.File, command.Args, repo, TimeSpan.FromMinutes(2), cancellationToken);
            session.RecordCommand(result.CommandLine);
            session.RecordOutput(result.StandardOutput);
            if (!result.Succeeded)
            {
                session.RecordOutput(result.StandardError);
                session.AddFinding(new ReviewFinding(
                    ReviewSeverity.Critical,
                    command.File,
                    null,
                    $"Required evidence command failed: {result.CommandLine}",
                    "A failed pre-push evidence command means the review cannot prove the repository is safe to push.",
                    string.IsNullOrWhiteSpace(result.StandardError) ? "Rerun the command locally and fix the failure." : result.StandardError.Trim()));
            }
        }
    }
}
