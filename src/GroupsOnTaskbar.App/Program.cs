using GroupsOnTaskbar.App.Activation;

namespace GroupsOnTaskbar_App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (ActivationCoordinator.RedirectToMainInstance())
        {
            return;
        }

        XamlGeneratedProgram.XamlGeneratedMain();
    }
}
