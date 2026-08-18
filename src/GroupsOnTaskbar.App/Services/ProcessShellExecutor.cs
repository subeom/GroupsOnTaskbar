using System.Diagnostics;

namespace GroupsOnTaskbar.App.Services;

public sealed class ProcessShellExecutor : IShellExecutor
{
    public void Execute(string targetPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = true
        });
    }
}
