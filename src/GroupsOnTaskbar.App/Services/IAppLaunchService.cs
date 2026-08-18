using GroupsOnTaskbar.Core.Launch;

namespace GroupsOnTaskbar.App.Services;

public interface IAppLaunchService
{
    LaunchResult Launch(string targetPath);
}
