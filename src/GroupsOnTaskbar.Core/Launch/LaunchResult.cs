namespace GroupsOnTaskbar.Core.Launch;

public enum LaunchStatus
{
    Started,
    TargetMissing,
    AccessDenied,
    LaunchFailed
}

public sealed record LaunchResult(LaunchStatus Status, string? UserMessage = null);
