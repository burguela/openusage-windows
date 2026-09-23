using System;
using System.IO;

namespace OpenUsage.Windows.Services;

/// <summary>
/// The tray app's log, beside the engine's own <c>OpenUsage.log</c> in
/// <c>%LOCALAPPDATA%\OpenUsage\Logs</c>. Capped at 5 MB with one archive, like the engine's log.
/// Never logs credentials: the engine keeps those to itself, and this app never sees them.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 5_000_000;
    private static readonly object Gate = new();

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenUsage", "Logs");

    public static string FilePath { get; } = Path.Combine(Directory, "OpenUsage.Windows.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ} [{level}] {message}{Environment.NewLine}";
        lock (Gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Move(FilePath, Path.Combine(Directory, "OpenUsage.Windows.1.log"), overwrite: true);
                }
                File.AppendAllText(FilePath, line);
            }
            catch (IOException error)
            {
                // Logging must never take the app down; surface the problem where a developer sees it.
                System.Diagnostics.Debug.WriteLine($"OpenUsage log write failed: {error.Message}");
            }
            catch (UnauthorizedAccessException error)
            {
                System.Diagnostics.Debug.WriteLine($"OpenUsage log write failed: {error.Message}");
            }
        }
    }
}
