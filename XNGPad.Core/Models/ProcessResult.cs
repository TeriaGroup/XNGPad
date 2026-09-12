namespace XNGPad.Core.Models;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;

    public string Combined
    {
        get
        {
            if (string.IsNullOrWhiteSpace(StandardError))
                return StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(StandardOutput))
                return StandardError.Trim();
            return (StandardOutput.Trim() + Environment.NewLine + StandardError.Trim()).Trim();
        }
    }
}
