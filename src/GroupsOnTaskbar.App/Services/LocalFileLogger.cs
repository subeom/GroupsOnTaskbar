using System.Text;

namespace GroupsOnTaskbar.App.Services;

public sealed class LocalFileLogger(string localDataRoot) : IAppLogger
{
    private const long MaxLogLengthInBytes = 1024 * 1024;
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly string _logsDirectoryPath = Path.Combine(localDataRoot, "Logs");
    private readonly string _currentLogPath = Path.Combine(localDataRoot, "Logs", "taskbar-groups.log");
    private readonly string _previousLogPath = Path.Combine(localDataRoot, "Logs", "taskbar-groups.previous.log");
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task WriteAsync(
        string category,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(exception);

        var sanitizedCategory = category.ReplaceLineEndings(" ");
        var sanitizedMessage = exception.Message.ReplaceLineEndings(" ");
        var line = $"{DateTimeOffset.UtcNow:O} [{sanitizedCategory}] {exception.GetType().FullName} HRESULT=0x{exception.HResult:X8} {sanitizedMessage}{Environment.NewLine}";

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(_logsDirectoryPath);
            RotateIfNeeded();
            await File.AppendAllTextAsync(_currentLogPath, line, Utf8WithoutBom, cancellationToken);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_currentLogPath))
        {
            return;
        }

        var currentLog = new FileInfo(_currentLogPath);

        if (currentLog.Length <= MaxLogLengthInBytes)
        {
            return;
        }

        if (File.Exists(_previousLogPath))
        {
            File.Delete(_previousLogPath);
        }

        File.Move(_currentLogPath, _previousLogPath);
    }
}
