using AiStackManager.Application.Stack;

namespace AiStackManager.Api.Endpoints;

public static class AiStackMinimalEndpoints
{
    public static IEndpointRouteBuilder MapAiStackMinimalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stack").WithTags("Stack");

        group.MapGet("/status", async (AiStackOrchestrator orchestrator, CancellationToken ct)
            => Results.Ok(await orchestrator.StatusAsync(ct)));

        group.MapPost("/start", async (AiStackOrchestrator orchestrator, CancellationToken ct) =>
        {
            var result = await orchestrator.StartAsync(ct);
            return result.IsSucc ? Results.Accepted() : Results.Conflict(result.Error);
        });

        group.MapPost("/stop", async (AiStackOrchestrator orchestrator, CancellationToken ct) =>
        {
            var result = await orchestrator.StopAsync(ct);
            return result.IsSucc ? Results.Accepted() : Results.Conflict(result.Error);
        });

        group.MapPost("/restart", async (AiStackOrchestrator orchestrator, CancellationToken ct) =>
        {
            await orchestrator.StopAsync(ct);
            var result = await orchestrator.StartAsync(ct);
            return result.IsSucc ? Results.Accepted() : Results.Conflict(result.Error);
        });

        return app;
    }
}
