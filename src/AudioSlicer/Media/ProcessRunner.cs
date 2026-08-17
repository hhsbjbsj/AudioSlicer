using System.Diagnostics;
using System.Text;

namespace AudioSlicer.Media;

public static class ProcessRunner
{
    public static async Task<string> RunForTextAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = CreateProcess(executable, arguments, redirectStandardOutput: true);
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"外部媒体工具返回错误代码 {process.ExitCode}：{TrimError(error)}");
        }

        return output;
    }

    public static Process CreateProcess(
        string executable,
        IEnumerable<string> arguments,
        bool redirectStandardOutput)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = redirectStandardOutput,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process { StartInfo = startInfo };
    }

    public static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort during cancellation.
        }
    }

    private static string TrimError(string error)
    {
        const int maximumLength = 2_000;
        var normalized = error.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[^maximumLength..];
    }
}

