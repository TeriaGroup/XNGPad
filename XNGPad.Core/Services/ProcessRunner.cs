using System.Diagnostics;
using System.Text;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        string? stdin = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);
        if (fileName.EndsWith("docker.exe", StringComparison.OrdinalIgnoreCase))
            psi.Environment["DOCKER_HOST"] = "npipe:////./pipe/dockerDesktopLinuxEngine";

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
            return new ProcessResult(-1, "", "Process did not start.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (stdin != null)
            await process.StandardInput.WriteAsync(stdin).ConfigureAwait(false);
        process.StandardInput.Close();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (cancellationToken.IsCancellationRequested)
                throw;
            var so = await SafeRead(stdoutTask);
            var se = await SafeRead(stderrTask);
            return new ProcessResult(-1, so, string.IsNullOrWhiteSpace(se) ? "Timed out." : se);
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    public void StartDetached(string fileName)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true
        });
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static async Task<string> SafeRead(Task<string> task)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
            return "";
        }
    }
}
