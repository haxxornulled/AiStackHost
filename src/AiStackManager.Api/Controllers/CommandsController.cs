using AiStackManager.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AiStackManager.Api.Controllers;

[ApiController]
[Route("api/commands")]
public sealed class CommandsController : ControllerBase
{
    private readonly ICommandRunner _commands;

    public CommandsController(ICommandRunner commands) => _commands = commands;

    [HttpPost("run-safe-status")]
    public async ValueTask<ActionResult<CommandResult>> RunSafeStatus([FromBody] SafeCommandRequest request, CancellationToken cancellationToken)
    {
        var allowed = request.Command switch
        {
            "git" => IsOneOf(request.Arguments, ["status", "--short"], ["branch", "--show-current"], ["diff", "--stat"]),
            "dotnet" => IsOneOf(request.Arguments, ["--info"]),
            "ollama" => IsOneOf(request.Arguments, ["ps"], ["list"]),
            "openclaw" => IsOneOf(request.Arguments, ["models", "status"]),
            "hermes" => IsOneOf(request.Arguments, ["--version"]),
            _ => false
        };

        if (!allowed)
            return BadRequest("Command and arguments are not on the safe status allow-list.");

        var result = await _commands.RunAsync(request.Command, request.Arguments, request.WorkingDirectory, TimeSpan.FromSeconds(60), cancellationToken);
        return Ok(result);
    }

    private static bool IsOneOf(string[] actual, params string[][] allowed)
        => allowed.Any(candidate => actual.SequenceEqual(candidate, StringComparer.Ordinal));
}

public sealed record SafeCommandRequest(string Command, string[] Arguments, string? WorkingDirectory);
