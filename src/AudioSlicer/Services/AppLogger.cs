using System.IO;
using System.Text;

namespace AudioSlicer.Services;

public static class AppLogger
{
    private static readonly Lock SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AudioSlicer",
        "logs");

    public static string LogPath => Path.Combine(LogDirectory, "app.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(LogDirectory);
                var builder = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(' ')
                    .Append(level)
                    .Append(' ')
                    .AppendLine(message);
                if (exception is not null)
                {
                    builder.AppendLine(exception.ToString());
                }

                File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the editor.
        }
    }
}

