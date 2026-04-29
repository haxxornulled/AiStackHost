using AiStackManager.Domain.Review;
using Xunit;

namespace AiStackManager.UnitTests;

public sealed class ReviewSessionTests
{
    [Fact]
    public void Validate_Rejects_Edit_Mode()
    {
        var session = new ReviewSession(new CodeReviewRequest("/tmp/repo", AllowEdits: true));
        Assert.True(session.Validate().IsFail);
    }

    [Fact]
    public void Complete_Requests_Changes_When_Critical_Findings_Exist()
    {
        var session = new ReviewSession(new CodeReviewRequest("/tmp/repo"));
        session.AddFinding(new ReviewFinding(ReviewSeverity.Critical, "a.cs", 1, "bug", "breaks", "fix it"));
        var result = session.Complete("done");
        Assert.Equal(ReviewVerdict.RequestChanges, result.Verdict);
    }
}
