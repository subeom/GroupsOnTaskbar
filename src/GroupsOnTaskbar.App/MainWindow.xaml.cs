using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GroupsOnTaskbar_App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public ContentControl RootHost => RootHostControl;

    public KeyboardAccelerator EscapeKeyboardAccelerator => EscapeKeyboardAcceleratorControl;
}
