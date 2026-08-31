using HealthGoalsTracker.Services;
using Microsoft.Extensions.Logging;

namespace HealthGoalsTracker.Tests;

public class DiagnosticsServiceTests
{
    [Fact]
    public void Write_PersistsStructuredUtcEvent()
    {
        var directory = CreateDirectory();
        var logPath = Path.Combine(directory, "healthgoals.log");
        var service = new DiagnosticsService(logPath);

        try
        {
            service.Write(
                LogLevel.Information,
                "HealthGoalsTracker.Tests",
                new EventId(7, "TestEvent"),
                "Application operation completed",
                null);

            var contents = File.ReadAllText(logPath);
            Assert.Contains("Information", contents);
            Assert.Contains("HealthGoalsTracker.Tests", contents);
            Assert.Contains("[7:TestEvent]", contents);
            Assert.Contains("Application operation completed", contents);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}T", contents);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CreateSnapshot_CopiesStableDiagnosticLog()
    {
        var directory = CreateDirectory();
        var logPath = Path.Combine(directory, "healthgoals.log");
        var exportDirectory = Path.Combine(directory, "export");
        var service = new DiagnosticsService(logPath);

        try
        {
            service.Write(
                LogLevel.Warning,
                "HealthGoalsTracker.Tests",
                default,
                "Snapshot event",
                null);

            var snapshotPath = service.CreateSnapshot(exportDirectory);

            Assert.True(File.Exists(snapshotPath));
            Assert.Equal(File.ReadAllText(logPath), File.ReadAllText(snapshotPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Write_RotatesLogAtConfiguredSize()
    {
        var directory = CreateDirectory();
        var logPath = Path.Combine(directory, "healthgoals.log");
        var service = new DiagnosticsService(logPath);

        try
        {
            File.WriteAllText(logPath, new string('x', (int)DiagnosticsService.MaximumLogBytes));

            service.Write(
                LogLevel.Information,
                "HealthGoalsTracker.Tests",
                default,
                "Post-rotation event",
                null);

            Assert.True(File.Exists($"{logPath}.1"));
            Assert.Contains("Post-rotation event", File.ReadAllText(logPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void FileLogger_OnlyRecordsApplicationInformationOrHigher()
    {
        var directory = CreateDirectory();
        var logPath = Path.Combine(directory, "healthgoals.log");
        var service = new DiagnosticsService(logPath);
        var applicationLogger = new FileLogger("HealthGoalsTracker.Tests", service);
        var frameworkLogger = new FileLogger("Microsoft.Maui", service);

        try
        {
            applicationLogger.LogDebug("Debug event");
            frameworkLogger.LogInformation("Framework event");
            applicationLogger.LogInformation("Application event");

            var contents = File.ReadAllText(logPath);
            Assert.DoesNotContain("Debug event", contents);
            Assert.DoesNotContain("Framework event", contents);
            Assert.Contains("Application event", contents);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RotateIfNeeded_RetainsAtMostConfiguredArchiveCount()
    {
        var directory = CreateDirectory();
        var logPath = Path.Combine(directory, "healthgoals.log");
        var service = new DiagnosticsService(logPath);

        try
        {
            for (var index = 0; index < DiagnosticsService.ArchiveCount + 2; index++)
            {
                File.WriteAllText(logPath, new string('x', (int)DiagnosticsService.MaximumLogBytes));
                service.Write(
                    LogLevel.Information,
                    "HealthGoalsTracker.Tests",
                    default,
                    $"Rotation {index}",
                    null);
            }

            Assert.True(File.Exists($"{logPath}.{DiagnosticsService.ArchiveCount}"));
            Assert.False(File.Exists($"{logPath}.{DiagnosticsService.ArchiveCount + 1}"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CreateSnapshot_WhenNoEventsExistCreatesReadableEmptySnapshot()
    {
        var directory = CreateDirectory();
        var logPath = Path.Combine(directory, "healthgoals.log");
        var service = new DiagnosticsService(logPath);

        try
        {
            var snapshotPath = service.CreateSnapshot(Path.Combine(directory, "export"));

            Assert.Equal(
                "No diagnostic events have been recorded.",
                File.ReadAllText(snapshotPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    public static string CreateDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"health-goals-diagnostics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
