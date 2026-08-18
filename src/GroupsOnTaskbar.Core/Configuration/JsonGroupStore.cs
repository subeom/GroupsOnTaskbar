using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Core.Configuration;

public sealed class JsonGroupStore : IGroupStore
{
    private static readonly JsonTypeInfo<LauncherConfiguration> LauncherConfigurationTypeInfo =
        LauncherConfigurationJsonContext.Default.LauncherConfiguration;

    private readonly string _rootPath;
    private readonly string _settingsPath;
    private readonly TimeProvider _timeProvider;

    public const string SettingsFileName = "settings-v1.json";

    public JsonGroupStore(string rootPath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _rootPath = rootPath;
        _settingsPath = Path.Combine(rootPath, SettingsFileName);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LauncherConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return LauncherConfiguration.Empty;
        }

        try
        {
            await using var stream = new FileStream(
                _settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var configuration = await JsonSerializer.DeserializeAsync(
                stream,
                LauncherConfigurationTypeInfo,
                cancellationToken);

            if (configuration is null)
            {
                throw new CorruptConfigurationException(
                    _settingsPath,
                    ["document: The configuration document is null."]);
            }

            var issues = ConfigurationValidator.Validate(configuration);
            if (issues.Count > 0)
            {
                throw new CorruptConfigurationException(
                    _settingsPath,
                    [.. issues.Select(issue => $"{issue.Field}: {issue.Message}")]);
            }

            return configuration;
        }
        catch (CorruptConfigurationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CorruptConfigurationException(_settingsPath, [exception.Message], exception);
        }
    }

    public async Task SaveAsync(LauncherConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var issues = ConfigurationValidator.Validate(configuration);
        if (issues.Count > 0)
        {
            throw new ArgumentException(
                $"The configuration is invalid: {string.Join("; ", issues.Select(issue => $"{issue.Field}: {issue.Message}"))}",
                nameof(configuration));
        }

        Directory.CreateDirectory(_rootPath);

        string? temporaryPath = null;

        try
        {
            temporaryPath = Path.Combine(
                _rootPath,
                $"{Path.GetFileNameWithoutExtension(SettingsFileName)}.{Guid.NewGuid():N}.tmp");

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    configuration,
                    LauncherConfigurationTypeInfo,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<string> BackUpAndResetAsync(CancellationToken cancellationToken = default)
    {
        string backupPath = string.Empty;

        if (File.Exists(_settingsPath))
        {
            Directory.CreateDirectory(_rootPath);

            var timestamp = _timeProvider
                .GetUtcNow()
                .UtcDateTime
                .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            backupPath = Path.Combine(_rootPath, $"settings-v1.{timestamp}.corrupt.json");
            File.Move(_settingsPath, backupPath);
        }

        await SaveAsync(LauncherConfiguration.Empty, cancellationToken);
        return backupPath;
    }
}
