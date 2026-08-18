using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GroupsOnTaskbar.App.ViewModels;

public sealed class ShortcutViewModel : ObservableObject
{
    private ImageSource? _icon;

    public ShortcutViewModel(Guid id, string displayName, string targetPath, bool isAvailable)
    {
        Id = id;
        DisplayName = displayName;
        TargetPath = targetPath;
        IsAvailable = isAvailable;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string TargetPath { get; }

    public bool IsAvailable { get; }

    public string AvailabilityText => IsAvailable ? string.Empty : "Unavailable";

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (!SetProperty(ref _icon, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IconFallbackVisibility));
            OnPropertyChanged(nameof(TileOpacity));
        }
    }

    public Visibility IconFallbackVisibility => Icon is null ? Visibility.Visible : Visibility.Collapsed;

    public double TileOpacity => IsAvailable ? 1.0 : 0.65;
}
