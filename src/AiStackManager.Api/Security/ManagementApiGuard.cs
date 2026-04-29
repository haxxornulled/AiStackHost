using System.Net;
using AiStackManager.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace AiStackManager.Api.Security;

public sealed class ManagementApiGuard
{
    private const string HeaderName = "X-AiStack-Token";
    private readonly RequestDelegate _next;

    public ManagementApiGuard(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<AiStackSettings> settings, ILogger<ManagementApiGuard> logger)
    {
        var currentSettings = settings.CurrentValue;

        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        if (HasValidToken(context, currentSettings) || IsAllowedLoopback(context, currentSettings))
        {
            await _next(context);
            return;
        }

        logger.LogWarning("Rejected management API request from {RemoteIp} to {Path}.", context.Connection.RemoteIpAddress, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "management_api_unauthorized" });
    }

    private static bool HasValidToken(HttpContext context, AiStackSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ManagementToken))
            return false;

        if (context.Request.Headers.TryGetValue(HeaderName, out var header) &&
            string.Equals(header.ToString(), settings.ManagementToken, StringComparison.Ordinal))
            return true;

        var authorization = context.Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(authorization["Bearer ".Length..], settings.ManagementToken, StringComparison.Ordinal);
    }

    private static bool IsAllowedLoopback(HttpContext context, AiStackSettings settings)
        => settings.AllowManagementWithoutTokenFromLoopback &&
           context.Connection.RemoteIpAddress is { } remoteIp &&
           IPAddress.IsLoopback(remoteIp);
}
