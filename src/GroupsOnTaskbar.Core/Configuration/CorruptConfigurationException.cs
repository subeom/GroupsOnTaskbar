namespace GroupsOnTaskbar.Core.Configuration;

public sealed class CorruptConfigurationException(
    string settingsPath,
    IReadOnlyList<string> reasons,
    Exception? innerException = null) : Exception("The launcher configuration is damaged.", innerException)
{
    public string SettingsPath { get; } = settingsPath;

    public IReadOnlyList<string> Reasons { get; } = reasons;
}
