using AiStackManager.Domain.Common;

namespace AiStackManager.Domain.Review;

public sealed record CodeReviewRequest(
    string RepositoryPath,
    string? BaseBranch = "origin/main",
    bool RunRestore = true,
    bool RunBuild = true,
    bool RunTests = true,
    bool AllowEdits = false);

public enum ReviewSeverity { Critical, Warning, Suggestion, LooksGood }
public enum ReviewVerdict { Approve, Comment, RequestChanges, Failed }

public sealed record ReviewFinding(
    ReviewSeverity Severity,
    string FilePath,
    int? Line,
    string Issue,
    string WhyItMatters,
    string Fix);

public sealed record ReviewResult(
    ReviewVerdict Verdict,
    bool ToolCallsWorked,
    string Summary,
    IReadOnlyList<ReviewFinding> Findings,
    IReadOnlyList<string> CommandsRun,
    IReadOnlyList<string> RawOutput);

public sealed class ReviewSession
{
    private readonly List<string> _commands = [];
    private readonly List<string> _rawOutput = [];
    private readonly List<ReviewFinding> _findings = [];

    public ReviewSession(CodeReviewRequest request) => Request = request;

    public CodeReviewRequest Request { get; }
    public bool ToolCallSmokeTestPassed { get; private set; }
    public IReadOnlyList<string> CommandsRun => _commands;
    public IReadOnlyList<string> RawOutput => _rawOutput;
    public IReadOnlyList<ReviewFinding> Findings => _findings;

    public Fin Validate()
    {
        if (string.IsNullOrWhiteSpace(Request.RepositoryPath))
            return Fin.Fail(AiStackError.Validation("RepositoryPath is required."));
        if (Request.AllowEdits)
            return Fin.Fail(AiStackError.Validation("This workflow is review-only. File edits are intentionally disabled."));
        return Fin.Succ();
    }

    public void RecordCommand(string command) => _commands.Add(command);
    public void RecordOutput(string output) { if (!string.IsNullOrWhiteSpace(output)) _rawOutput.Add(output); }
    public void MarkToolCallSmokeTestPassed() => ToolCallSmokeTestPassed = true;
    public void AddFinding(ReviewFinding finding) => _findings.Add(finding);

    public ReviewResult Complete(string summary)
    {
        var verdict = Findings.Any(f => f.Severity == ReviewSeverity.Critical)
            ? ReviewVerdict.RequestChanges
            : Findings.Any(f => f.Severity == ReviewSeverity.Warning)
                ? ReviewVerdict.Comment
                : ReviewVerdict.Approve;

        return new ReviewResult(verdict, ToolCallSmokeTestPassed, summary, Findings, CommandsRun, RawOutput);
    }

    public ReviewResult Fail(string summary)
        => new(ReviewVerdict.Failed, ToolCallSmokeTestPassed, summary, Findings, CommandsRun, RawOutput);
}
