using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker.Services;

public interface IDiagnosticsService
{
    string LogPath { get; }
    void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception);
    string CreateSnapshot(string destinationDirectory);
}

public class DiagnosticsService : IDiagnosticsService
{
    public const long MaximumLogBytes = 2 * 1024 * 1024;
    public const int ArchiveCount = 4;

    public object FileLock = new();
    public string LogPath { get; }

    public DiagnosticsService(string logPath)
    {
        LogPath = logPath;
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
    }

    public void Write(
        LogLevel level,
        string category,
        EventId eventId,
        string message,
        Exception? exception)
    {
        var eventText = eventId.Id == 0 ? "" : $" [{eventId.Id}:{eventId.Name}]";
        var line =
            $"{DateTimeOffset.UtcNow:O} {level,-11} {category}{eventText} {message}";
        if (exception != null)
            line += $"{Environment.NewLine}{exception}";

        lock (FileLock)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch (IOException ioException)
            {
                Debug.WriteLine($"Diagnostics write failed: {ioException.Message}");
            }
            catch (UnauthorizedAccessException accessException)
            {
                Debug.WriteLine($"Diagnostics write denied: {accessException.Message}");
            }
        }
    }

    public string CreateSnapshot(string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(
            destinationDirectory,
            $"health_goals_diagnostics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");

        lock (FileLock)
        {
            if (File.Exists(LogPath))
                File.Copy(LogPath, destination, true);
            else
                File.WriteAllText(destination, "No diagnostic events have been recorded.");
        }

        return destination;
    }

    public void RotateIfNeeded()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < MaximumLogBytes)
            return;

        var oldest = $"{LogPath}.{ArchiveCount}";
        if (File.Exists(oldest))
            File.Delete(oldest);

        for (var index = ArchiveCount - 1; index >= 1; index--)
        {
            var source = $"{LogPath}.{index}";
            if (File.Exists(source))
                File.Move(source, $"{LogPath}.{index + 1}");
        }

        File.Move(LogPath, $"{LogPath}.1");
    }
}

public class FileLoggerProvider : ILoggerProvider
{
    public IDiagnosticsService DiagnosticsService;

    public FileLoggerProvider(IDiagnosticsService diagnosticsService)
    {
        DiagnosticsService = diagnosticsService;
    }

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, DiagnosticsService);

    public void Dispose()
    {
    }
}

public class FileLogger : ILogger
{
    public string CategoryName;
    public IDiagnosticsService DiagnosticsService;

    public FileLogger(string categoryName, IDiagnosticsService diagnosticsService)
    {
        CategoryName = categoryName;
        DiagnosticsService = diagnosticsService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= LogLevel.Information &&
        CategoryName.StartsWith("HealthGoalsTracker", StringComparison.Ordinal);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        DiagnosticsService.Write(
            logLevel,
            CategoryName,
            eventId,
            formatter(state, exception),
            exception);
    }
}
