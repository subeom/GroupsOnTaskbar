using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Configuration;

public sealed class SettingsSession
{
    private readonly IGroupStore _groupStore;
    private readonly Func<string, bool> _fileExists;
    private readonly LauncherConfiguration _originalConfiguration;
    private ConfigurationEditor _editor;

    public SettingsSession(
        LauncherConfiguration configuration,
        IGroupStore groupStore,
        Func<string, bool>? fileExists = null)
    {
        _originalConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _fileExists = fileExists ?? File.Exists;
        _editor = new ConfigurationEditor(configuration, _fileExists);
    }

    public string? ErrorMessage { get; private set; }

    public LauncherConfiguration Snapshot => _editor.Snapshot;

    public Guid? AddGroup(string name)
        => ExecuteGuidMutation(() => _editor.AddGroup(name));

    public bool RenameGroup(Guid groupId, string name)
        => ExecuteBooleanMutation(() =>
        {
            _editor.RenameGroup(groupId, name);
            return true;
        });

    public bool DeleteGroup(Guid groupId)
        => ExecuteBooleanMutation(() =>
        {
            _editor.DeleteGroup(groupId);
            return true;
        });

    public bool MoveGroup(Guid groupId, int offset)
        => ExecuteBooleanMutation(() =>
        {
            _editor.MoveGroup(groupId, offset);
            return true;
        });

    public Guid? AddShortcut(Guid groupId, string displayName, string targetPath)
        => ExecuteGuidMutation(() => _editor.AddShortcut(groupId, displayName, targetPath));

    public bool UpdateShortcut(Guid groupId, Guid shortcutId, string displayName, string targetPath)
        => ExecuteBooleanMutation(() =>
        {
            _editor.UpdateShortcut(groupId, shortcutId, displayName, targetPath);
            return true;
        });

    public bool DeleteShortcut(Guid groupId, Guid shortcutId)
        => ExecuteBooleanMutation(() =>
        {
            _editor.DeleteShortcut(groupId, shortcutId);
            return true;
        });

    public bool MoveShortcut(Guid groupId, Guid shortcutId, int offset)
        => ExecuteBooleanMutation(() =>
        {
            _editor.MoveShortcut(groupId, shortcutId, offset);
            return true;
        });

    public void Cancel()
    {
        _editor = new ConfigurationEditor(_originalConfiguration, _fileExists);
        ClearError();
    }

    public async Task<LauncherConfiguration> SaveAsync(CancellationToken cancellationToken = default)
    {
        ClearError();
        var snapshot = _editor.Snapshot;

        try
        {
            await _groupStore.SaveAsync(snapshot, cancellationToken);
            return snapshot;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            throw;
        }
    }

    private Guid? ExecuteGuidMutation(Func<Guid> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        try
        {
            var result = mutation();
            ClearError();
            return result;
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = ToUserFacingMessage(exception);
            return null;
        }
        catch (KeyNotFoundException exception)
        {
            ErrorMessage = exception.Message;
            return null;
        }
    }

    private bool ExecuteBooleanMutation(Func<bool> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        try
        {
            var result = mutation();
            ClearError();
            return result;
        }
        catch (ArgumentException exception)
        {
            ErrorMessage = ToUserFacingMessage(exception);
            return false;
        }
        catch (KeyNotFoundException exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private void ClearError()
    {
        ErrorMessage = null;
    }

    private static string ToUserFacingMessage(ArgumentException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (string.IsNullOrWhiteSpace(exception.ParamName))
        {
            return exception.Message;
        }

        var suffix = $" (Parameter '{exception.ParamName}')";
        return exception.Message.EndsWith(suffix, StringComparison.Ordinal)
            ? exception.Message[..^suffix.Length]
            : exception.Message;
    }
}
