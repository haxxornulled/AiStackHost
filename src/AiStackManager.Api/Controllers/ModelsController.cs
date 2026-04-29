using AiStackManager.Application.Abstractions;
using AiStackManager.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiStackManager.Api.Controllers;

[ApiController]
[Route("api/models")]
public sealed class ModelsController : ControllerBase
{
    private readonly InferenceModelManager _models;

    public ModelsController(InferenceModelManager models) => _models = models;

    [HttpGet("providers")]
    public ActionResult<IReadOnlyList<InferenceProviderDescriptor>> Providers()
        => Ok(_models.Providers);

    [HttpGet]
    public async ValueTask<IActionResult> List([FromQuery] string? provider, CancellationToken cancellationToken)
    {
        var result = await _models.ListModelsAsync(provider, cancellationToken);
        return result.IsSucc ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("current")]
    public async ValueTask<ActionResult<InferenceModelSelection>> Current(CancellationToken cancellationToken)
        => Ok(await _models.CurrentAsync(cancellationToken));

    [HttpPost("select")]
    public async ValueTask<IActionResult> Select([FromBody] SelectModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _models.SelectAsync(new InferenceModelSelection(request.ProviderId, request.ModelId), request.DownloadIfMissing, cancellationToken);
        return result.IsSucc ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("download")]
    public async ValueTask<IActionResult> Download([FromBody] DownloadModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _models.DownloadAsync(new InferenceModelSelection(request.ProviderId, request.ModelId), cancellationToken);
        return result.IsSucc ? Accepted() : BadRequest(result.Error);
    }
}

public sealed record SelectModelRequest(string ProviderId, string ModelId, bool DownloadIfMissing = false);
public sealed record DownloadModelRequest(string ProviderId, string ModelId);
