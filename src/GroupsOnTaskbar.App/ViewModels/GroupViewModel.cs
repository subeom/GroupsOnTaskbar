using System.Collections.ObjectModel;

namespace GroupsOnTaskbar.App.ViewModels;

public sealed class GroupViewModel : ObservableObject
{
    public GroupViewModel(Guid id, string name, IEnumerable<ShortcutViewModel> shortcuts)
    {
        Id = id;
        Name = name;
        Shortcuts = new ObservableCollection<ShortcutViewModel>(shortcuts);
    }

    public Guid Id { get; }

    public string Name { get; }

    public ObservableCollection<ShortcutViewModel> Shortcuts { get; }

    public string AccessibleName => $"{Name} group";

    public string AccessibleHelpText => $"{Shortcuts.Count} apps";
}
