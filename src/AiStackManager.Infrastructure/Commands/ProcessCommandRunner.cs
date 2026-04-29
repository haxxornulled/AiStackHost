using System.Diagnostics;
using System.Text;
using AiStackManager.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AiStackManager.Infrastructure.Commands;

public sealed class ProcessCommandRunner : ICommandRunner
{
    private readonly ILogger<ProcessCommandRunner> _logger;

    public ProcessCommandRunner(ILogger<ProcessCommandRunner> logger) => _logger = logger;

    public async ValueTask<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory;

        if (!Directory.Exists(resolvedWorkingDirectory))
        {
            return new CommandResult(fileName, arguments, resolvedWorkingDirectory, 127, "", $"Working directory does not exist: {resolvedWorkingDirectory}", Stopwatch.GetElapsedTime(started));
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = resolvedWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        _logger.LogInformation("Running command {Command}", $"{fileName} {string.Join(' ', arguments)}");

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new CommandResult(fileName, arguments, resolvedWorkingDirectory, 127, "", ex.Message, Stopwatch.GetElapsedTime(started));
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new CommandResult(fileName, arguments, workingDirectory, 124, stdout.ToString(), $"Timed out after {timeout}.\n{stderr}", Stopwatch.GetElapsedTime(started));
        }

        return new CommandResult(fileName, arguments, workingDirectory, process.ExitCode, stdout.ToString(), stderr.ToString(), Stopwatch.GetElapsedTime(started));
    }
}
