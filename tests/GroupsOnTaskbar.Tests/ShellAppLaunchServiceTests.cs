using System.ComponentModel;
using GroupsOnTaskbar.Core.Launch;

namespace GroupsOnTaskbar.Tests;

public sealed class ShellAppLaunchServiceTests
{
    [Fact]
    public void Launch_WhenTargetPathIsRelative_ReturnsLaunchFailedWithoutInvokingExecutor()
    {
        var shellExecutor = new RecordingShellExecutor();
        var service = new ShellAppLaunchService(shellExecutor, _ => true);

        var result = service.Launch("Paint.exe");

        Assert.Equal(new LaunchResult(
            LaunchStatus.LaunchFailed,
            "This shortcut is not a supported .exe or .lnk target."), result);
        Assert.Empty(shellExecutor.Calls);
    }

    [Fact]
    public void Launch_WhenTargetExtensionIsUnsupported_ReturnsLaunchFailedWithoutInvokingExecutor()
    {
        var shellExecutor = new RecordingShellExecutor();
        var service = new ShellAppLaunchService(shellExecutor, _ => true);

        var result = service.Launch(@"C:\Apps\Paint.cmd");

        Assert.Equal(new LaunchResult(
            LaunchStatus.LaunchFailed,
            "This shortcut is not a supported .exe or .lnk target."), result);
        Assert.Empty(shellExecutor.Calls);
    }

    [Fact]
    public void Launch_WhenTargetFileDoesNotExist_ReturnsTargetMissing()
    {
        var shellExecutor = new RecordingShellExecutor();
        var service = new ShellAppLaunchService(shellExecutor, _ => false);

        var result = service.Launch(@"C:\Apps\Paint.exe");

        Assert.Equal(new LaunchResult(
            LaunchStatus.TargetMissing,
            "The shortcut target no longer exists."), result);
        Assert.Empty(shellExecutor.Calls);
    }

    [Fact]
    public void Launch_WhenShellExecutionSucceeds_ReturnsStarted()
    {
        var shellExecutor = new RecordingShellExecutor();
        var service = new ShellAppLaunchService(shellExecutor, _ => true);

        var result = service.Launch(@"C:\Apps\Paint.exe");

        Assert.Equal(new LaunchResult(LaunchStatus.Started), result);
        Assert.Equal([@"C:\Apps\Paint.exe"], shellExecutor.Calls);
    }

    [Fact]
    public void Launch_WhenShellExecutionThrowsUnauthorizedAccessException_ReturnsAccessDenied()
    {
        var shellExecutor = new RecordingShellExecutor
        {
            ExceptionToThrow = new UnauthorizedAccessException("Denied.")
        };
        var service = new ShellAppLaunchService(shellExecutor, _ => true);

        var result = service.Launch(@"C:\Apps\Paint.exe");

        Assert.Equal(new LaunchResult(
            LaunchStatus.AccessDenied,
            "Windows denied access to this shortcut."), result);
        Assert.Equal([@"C:\Apps\Paint.exe"], shellExecutor.Calls);
    }

    [Fact]
    public void Launch_WhenShellExecutionThrowsWin32ExceptionWithErrorCodeFive_ReturnsAccessDenied()
    {
        var shellExecutor = new RecordingShellExecutor
        {
            ExceptionToThrow = new Win32Exception(5)
        };
        var service = new ShellAppLaunchService(shellExecutor, _ => true);

        var result = service.Launch(@"C:\Apps\Paint.exe");

        Assert.Equal(new LaunchResult(
            LaunchStatus.AccessDenied,
            "Windows denied access to this shortcut."), result);
        Assert.Equal([@"C:\Apps\Paint.exe"], shellExecutor.Calls);
    }

    [Fact]
    public void Launch_WhenShellExecutionThrowsOtherWin32Exception_ReturnsLaunchFailed()
    {
        var shellExecutor = new RecordingShellExecutor
        {
            ExceptionToThrow = new Win32Exception(2)
        };
        var service = new ShellAppLaunchService(shellExecutor, _ => true);

        var result = service.Launch(@"C:\Apps\Paint.exe");

        Assert.Equal(new LaunchResult(
            LaunchStatus.LaunchFailed,
            "Windows could not start this shortcut."), result);
        Assert.Equal([@"C:\Apps\Paint.exe"], shellExecutor.Calls);
    }

    private sealed class RecordingShellExecutor : IShellExecutor
    {
        public List<string> Calls { get; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public void Execute(string targetPath)
        {
            Calls.Add(targetPath);

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }
        }
    }
}
