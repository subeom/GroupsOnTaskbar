using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Configuration;

public interface IGroupStore
{
    Task<LauncherConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LauncherConfiguration configuration, CancellationToken cancellationToken = default);

    Task<string> BackUpAndResetAsync(CancellationToken cancellationToken = default);
}
