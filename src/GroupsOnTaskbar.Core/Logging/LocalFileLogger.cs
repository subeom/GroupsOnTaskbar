using System.Text;

namespace GroupsOnTaskbar.Core.Logging;

public sealed class LocalFileLogger(string localDataRoot) : IAppLogger
{
    internal const long MaxLogLengthInBytes = 1024 * 1024;

    private const string CurrentLogFileName = "taskbar-groups.log";
    private const string PreviousLogFileName = "taskbar-groups.previous.log";
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly string _logsDirectoryPath = Path.Combine(localDataRoot, "Logs");
    private readonly string _currentLogPath = Path.Combine(localDataRoot, "Logs", CurrentLogFileName);
    private readonly string _previousLogPath = Path.Combine(localDataRoot, "Logs", PreviousLogFileName);
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public Task WriteAsync(
        string category,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(exception);

        return WriteLineAsync(
            CreateLine(
                category,
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.HResult,
                exception.Message),
            cancellationToken);
    }

    public Task WriteAsync(
        string category,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return WriteLineAsync(
            CreateLine(category, "None", 0, message),
            cancellationToken);
    }

    private async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        var nextLineLengthInBytes = Utf8WithoutBom.GetByteCount(line);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(_logsDirectoryPath);
            RotateIfNeeded(nextLineLengthInBytes);
            await File.AppendAllTextAsync(_currentLogPath, line, Utf8WithoutBom, cancellationToken).ConfigureAwait(false);
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

    private void RotateIfNeeded(int nextLineLengthInBytes)
    {
        if (!File.Exists(_currentLogPath))
        {
            return;
        }

        var currentLogLengthInBytes = new FileInfo(_currentLogPath).Length;
        if (currentLogLengthInBytes + nextLineLengthInBytes <= MaxLogLengthInBytes)
        {
            return;
        }

        if (File.Exists(_previousLogPath))
        {
            File.Delete(_previousLogPath);
        }

        File.Move(_currentLogPath, _previousLogPath);
    }

    private static string CreateLine(string category, string exceptionType, int hresult, string message)
    {
        return $"{DateTimeOffset.UtcNow:O} [{Sanitize(category)}] {Sanitize(exceptionType)} HRESULT=0x{hresult:X8} {SanitizeMessage(message)}{Environment.NewLine}";
    }

    private static string Sanitize(string value)
        => value.ReplaceLineEndings(" ").Trim();

    private static string SanitizeMessage(string message)
    {
        var sanitizedMessage = Sanitize(message);
        return LooksLikeSerializedJson(sanitizedMessage)
            ? "[redacted-json]"
            : sanitizedMessage;
    }

    private static bool LooksLikeSerializedJson(string value)
    {
        var trimmedValue = value.Trim();
        return (trimmedValue.StartsWith('{') && trimmedValue.EndsWith('}'))
            || (trimmedValue.StartsWith('[') && trimmedValue.EndsWith(']'));
    }
}
