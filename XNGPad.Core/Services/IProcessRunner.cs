using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        string? stdin = null);

    void StartDetached(string fileName);
}
