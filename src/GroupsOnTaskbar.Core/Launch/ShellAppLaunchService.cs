using System.ComponentModel;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Core.Launch;

public sealed class ShellAppLaunchService(
    IShellExecutor shellExecutor,
    Func<string, bool>? fileExists = null) : IAppLaunchService
{
    private readonly Func<string, bool> _fileExists = fileExists ?? File.Exists;

    public LaunchResult Launch(string targetPath)
    {
        if (!Path.IsPathFullyQualified(targetPath)
            || !ShortcutTargetValidator.IsSupportedExtension(targetPath))
        {
            return new(
                LaunchStatus.LaunchFailed,
                "This shortcut is not a supported .exe or .lnk target.");
        }

        if (!_fileExists(targetPath))
        {
            return new(
                LaunchStatus.TargetMissing,
                "The shortcut target no longer exists.");
        }

        try
        {
            shellExecutor.Execute(targetPath);
            return new(LaunchStatus.Started);
        }
        catch (UnauthorizedAccessException)
        {
            return new(
                LaunchStatus.AccessDenied,
                "Windows denied access to this shortcut.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 5)
        {
            return new(
                LaunchStatus.AccessDenied,
                "Windows denied access to this shortcut.");
        }
        catch (Win32Exception)
        {
            return new(
                LaunchStatus.LaunchFailed,
                "Windows could not start this shortcut.");
        }
    }
}
