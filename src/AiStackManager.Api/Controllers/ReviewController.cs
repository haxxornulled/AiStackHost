using AiStackManager.Application.Abstractions;
using AiStackManager.Application.Review;
using AiStackManager.Domain.Review;
using Microsoft.AspNetCore.Mvc;

namespace AiStackManager.Api.Controllers;

[ApiController]
[Route("api/review")]
public sealed class ReviewController : ControllerBase
{
    private readonly ReviewWorkflow _workflow;
    private readonly IHermesService _hermes;

    public ReviewController(ReviewWorkflow workflow, IHermesService hermes)
    {
        _workflow = workflow;
        _hermes = hermes;
    }

    [HttpPost("pre-push")]
    public async ValueTask<ActionResult<ReviewResult>> PrePush([FromBody] CodeReviewRequest request, CancellationToken cancellationToken)
        => Ok(await _workflow.RunPrePushReviewAsync(request, cancellationToken));

    [HttpPost("/api/hermes/smoke-test")]
    public async ValueTask<IActionResult> SmokeTest([FromBody] SmokeTestRequest request, CancellationToken cancellationToken)
    {
        var result = await _hermes.ToolCallSmokeTestAsync(request.RepositoryPath, cancellationToken);
        return result.IsSucc ? Ok(new { ok = true }) : StatusCode(502, result.Error);
    }
}

public sealed record SmokeTestRequest(string RepositoryPath);
