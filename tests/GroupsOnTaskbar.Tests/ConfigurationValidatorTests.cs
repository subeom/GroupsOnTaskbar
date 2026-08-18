using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Tests;

public sealed class ConfigurationValidatorTests
{
    [Fact]
    public void Validate_WhenConfigurationIsEmpty_ReturnsNoIssues()
    {
        var configuration = LauncherConfiguration.Empty;

        var issues = ConfigurationValidator.Validate(configuration);

        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_WhenSchemaVersionIsUnsupported_ReturnsSingleSchemaVersionIssue()
    {
        var configuration = new LauncherConfiguration(99, []);

        var issues = ConfigurationValidator.Validate(configuration);

        var issue = Assert.Single(issues);
        Assert.Equal("schemaVersion", issue.Field);
    }

    [Fact]
    public void Validate_WhenGroupNameAndTargetAreInvalid_ReturnsIssuesForBothFields()
    {
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(
                    Guid.NewGuid(),
                    "   ",
                    0,
                    [
                        new AppShortcut(Guid.NewGuid(), "Paint", "relative.cmd", 0)
                    ])
            ]);

        var issues = ConfigurationValidator.Validate(configuration);

        Assert.Contains(issues, issue => issue.Field.EndsWith(".name", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Field.EndsWith(".targetPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenGroupContainsDuplicateTargets_ReturnsDuplicateIssue()
    {
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(
                    Guid.NewGuid(),
                    "Utilities",
                    0,
                    [
                        new AppShortcut(Guid.NewGuid(), "Paint", @"C:\Apps\Paint.exe", 0),
                        new AppShortcut(Guid.NewGuid(), "Paint Link", @"C:\Apps\.\Paint.exe", 1)
                    ])
            ]);

        var issues = ConfigurationValidator.Validate(configuration);

        Assert.Contains(
            issues,
            issue => issue.Field == "groups[0].shortcuts[1].targetPath"
                && issue.Message == "The target already exists in this group.");
    }

    [Fact]
    public void Validate_WhenNamesExceedMaximumLengths_ReturnsNameLengthIssues()
    {
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(
                    Guid.NewGuid(),
                    new string('G', ConfigurationValidator.MaximumGroupNameLength + 1),
                    0,
                    [
                        new AppShortcut(
                            Guid.NewGuid(),
                            new string('S', ConfigurationValidator.MaximumShortcutNameLength + 1),
                            @"C:\Apps\Paint.exe",
                            0)
                    ])
            ]);

        var issues = ConfigurationValidator.Validate(configuration);

        Assert.Contains(issues, issue => issue.Field == "groups[0].name");
        Assert.Contains(issues, issue => issue.Field == "groups[0].shortcuts[0].displayName");
    }
}
