using System.Text;
using GroupsOnTaskbar.Core.Logging;

namespace GroupsOnTaskbar.Tests;

public sealed class LocalFileLoggerTests
{
    private const int MaxLogLengthInBytes = 1024 * 1024;

    [Fact]
    public async Task WriteAsync_WritesSingleUtf8LineWithRequiredFields()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logger = new LocalFileLogger(temporaryDirectory.Path);

        await logger.WriteAsync("Startup", new InvalidOperationException("Launcher failed to start."));

        var logPath = Path.Combine(temporaryDirectory.Path, "Logs", "taskbar-groups.log");
        var bytes = await File.ReadAllBytesAsync(logPath);
        var contents = Encoding.UTF8.GetString(bytes);
        var line = Assert.Single(contents.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        Assert.False(bytes.Take(Encoding.UTF8.GetPreamble().Length).SequenceEqual(Encoding.UTF8.GetPreamble()));
        Assert.Contains("[Startup]", line, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, line, StringComparison.Ordinal);
        Assert.Contains("HRESULT=0x", line, StringComparison.Ordinal);
        Assert.Contains("Launcher failed to start.", line, StringComparison.Ordinal);
        Assert.True(DateTimeOffset.TryParse(line[..line.IndexOf(' ')], out _));
    }

    [Fact]
    public async Task WriteAsync_WhenCurrentLogExceedsThreshold_RotatesToPreviousFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logsDirectoryPath = Path.Combine(temporaryDirectory.Path, "Logs");
        Directory.CreateDirectory(logsDirectoryPath);

        var currentLogPath = Path.Combine(logsDirectoryPath, "taskbar-groups.log");
        await File.WriteAllTextAsync(currentLogPath, new string('A', MaxLogLengthInBytes + 1));

        var logger = new LocalFileLogger(temporaryDirectory.Path);

        await logger.WriteAsync("Rotation", new InvalidOperationException("Rotated entry."));

        var previousLogPath = Path.Combine(logsDirectoryPath, "taskbar-groups.previous.log");

        Assert.True(File.Exists(previousLogPath));
        Assert.Equal(MaxLogLengthInBytes + 1, new FileInfo(previousLogPath).Length);

        var currentContents = await File.ReadAllTextAsync(currentLogPath);
        Assert.Contains("Rotated entry.", currentContents, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('A', 64), currentContents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WhenCalledConcurrently_DoesNotThrowOrLoseLines()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logger = new LocalFileLogger(temporaryDirectory.Path);

        var writes = Enumerable.Range(0, 200)
            .Select(index => logger.WriteAsync("Concurrent", new InvalidOperationException($"Concurrent failure {index}")));

        await Task.WhenAll(writes);

        var logPath = Path.Combine(temporaryDirectory.Path, "Logs", "taskbar-groups.log");
        var lines = await File.ReadAllLinesAsync(logPath);

        Assert.Equal(200, lines.Length);
        Assert.Equal(
            200,
            lines.Count(line => line.Contains("Concurrent failure", StringComparison.Ordinal)));
    }
}
