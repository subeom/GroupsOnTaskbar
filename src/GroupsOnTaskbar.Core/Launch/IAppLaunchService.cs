namespace GroupsOnTaskbar.Core.Launch;

public interface IAppLaunchService
{
    LaunchResult Launch(string targetPath);
}
