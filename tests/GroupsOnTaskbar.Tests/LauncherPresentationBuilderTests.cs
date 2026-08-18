using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Presentation;

namespace GroupsOnTaskbar.Tests;

public sealed class LauncherPresentationBuilderTests
{
    [Fact]
    public void Create_WhenConfigurationHasGroups_OrdersGroupsAndShortcutsAndSelectsFirstGroupByDefault()
    {
        var selectedGroupId = Guid.Parse("3D465112-E9E4-46E3-BD13-5B0A9DDB8D3A");
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(
                    selectedGroupId,
                    "Utilities",
                    1,
                    [
                        new AppShortcut(
                            Guid.Parse("F93F88F5-2467-4F86-88D7-2B6F424E9034"),
                            "Paint",
                            @"C:\Apps\Paint.exe",
                            2),
                        new AppShortcut(
                            Guid.Parse("44C0EE32-B257-4FCC-81DF-1CE6D56F94BF"),
                            "Calculator",
                            @"C:\Apps\Calc.exe",
                            0)
                    ]),
                new AppGroup(
                    Guid.Parse("4499A613-D530-499D-B5D6-76AF46B1297D"),
                    "Browsers",
                    3,
                    [
                        new AppShortcut(
                            Guid.Parse("B3DA977D-3E32-4CBE-8DCE-C3453126F4A2"),
                            "Edge",
                            @"C:\Apps\Edge.exe",
                            0)
                    ])
            ]);

        var presentation = LauncherPresentationBuilder.Create(
            configuration,
            fileExists: path => path.EndsWith("Calc.exe", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("Edge.exe", StringComparison.OrdinalIgnoreCase));

        Assert.Collection(
            presentation.Groups,
            group =>
            {
                Assert.Equal(selectedGroupId, group.Id);
                Assert.Collection(
                    group.Shortcuts,
                    shortcut =>
                    {
                        Assert.Equal("Calculator", shortcut.DisplayName);
                        Assert.True(shortcut.IsAvailable);
                    },
                    shortcut =>
                    {
                        Assert.Equal("Paint", shortcut.DisplayName);
                        Assert.False(shortcut.IsAvailable);
                    });
            },
            group => Assert.Equal("Browsers", group.Name));
        Assert.Equal(selectedGroupId, presentation.SelectedGroup?.Id);
        Assert.True(presentation.HasShortcuts);
    }

    [Fact]
    public void Create_WhenPreviousSelectionStillExists_PreservesSelectedGroup()
    {
        var browsersId = Guid.Parse("A219180E-31B4-44DA-A0D6-999441DAE0F1");
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(Guid.Parse("FF036C76-3FB6-4E74-BCA3-366C8B926538"), "Utilities", 0, []),
                new AppGroup(
                    browsersId,
                    "Browsers",
                    1,
                    [
                        new AppShortcut(
                            Guid.Parse("1A2E339F-048C-4A41-A309-ADDB72A4EF2C"),
                            "Edge",
                            @"C:\Apps\Edge.exe",
                            0)
                    ])
            ]);

        var presentation = LauncherPresentationBuilder.Create(configuration, browsersId, _ => true);

        Assert.Equal(browsersId, presentation.SelectedGroup?.Id);
        Assert.True(presentation.HasShortcuts);
    }

    [Fact]
    public void Create_WhenPreviousSelectionIsMissing_FallsBackToFirstOrderedGroup()
    {
        var firstGroupId = Guid.Parse("0BB0E890-18D7-4206-BB3C-39AC1F84EFEB");
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(Guid.Parse("792B0595-6D57-4A8E-AF01-825B0965974D"), "Browsers", 1, []),
                new AppGroup(firstGroupId, "Utilities", 0, [])
            ]);

        var presentation = LauncherPresentationBuilder.Create(
            configuration,
            Guid.Parse("D52F1E27-578A-4493-9F76-B6852170286F"),
            _ => true);

        Assert.Equal(firstGroupId, presentation.SelectedGroup?.Id);
        Assert.False(presentation.HasShortcuts);
    }

    [Fact]
    public void Create_WhenSelectedGroupHasNoShortcuts_HasShortcutsIsFalse()
    {
        var emptyGroupId = Guid.Parse("F8421B98-BD0E-4E73-A4DD-7F4DE55CC650");
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(emptyGroupId, "Utilities", 0, []),
                new AppGroup(
                    Guid.Parse("E0DD291F-1F98-47E9-B4D8-4C2EA2C6FC32"),
                    "Browsers",
                    1,
                    [
                        new AppShortcut(
                            Guid.Parse("47FC4505-0090-4A7A-B4FB-D7A349369653"),
                            "Edge",
                            @"C:\Apps\Edge.exe",
                            0)
                    ])
            ]);

        var presentation = LauncherPresentationBuilder.Create(configuration, emptyGroupId, _ => true);

        Assert.Equal(emptyGroupId, presentation.SelectedGroup?.Id);
        Assert.False(presentation.HasShortcuts);
    }

    [Fact]
    public void Create_WhenConfigurationHasNoGroups_ReturnsEmptyPresentation()
    {
        var presentation = LauncherPresentationBuilder.Create(LauncherConfiguration.Empty, fileExists: _ => true);

        Assert.Empty(presentation.Groups);
        Assert.Null(presentation.SelectedGroup);
        Assert.False(presentation.HasShortcuts);
    }
}
