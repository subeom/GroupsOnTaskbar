using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Tests;

public sealed class ShortcutTargetValidatorTests
{
    [Theory]
    [InlineData(@"C:\Apps\Paint.exe", true)]
    [InlineData(@"C:\Apps\Paint.lnk", true)]
    [InlineData(@"C:\Apps\Paint.cmd", false)]
    public void IsSupportedExtension_ReturnsExpectedResult(string path, bool expected)
    {
        var result = ShortcutTargetValidator.IsSupportedExtension(path);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidateForAdd_WhenFileDoesNotExist_ReturnsMissingFileIssue()
    {
        var issues = ShortcutTargetValidator.ValidateForAdd(
            @"C:\Apps\Paint.exe",
            [],
            _ => false);

        var issue = Assert.Single(issues);
        Assert.Equal("targetPath", issue.Field);
        Assert.Equal("The selected target does not exist.", issue.Message);
    }

    [Fact]
    public void ValidateForAdd_WhenTargetAlreadyExists_ReturnsDuplicateIssue()
    {
        var issues = ShortcutTargetValidator.ValidateForAdd(
            @"C:\Apps\.\Paint.exe",
            [@"C:\Apps\Paint.exe"],
            _ => true);

        var issue = Assert.Single(issues);
        Assert.Equal("targetPath", issue.Field);
        Assert.Equal("The selected target is already in this group.", issue.Message);
    }

    [Fact]
    public void ValidateForAdd_WhenPathIsRelative_ReturnsAbsolutePathIssue()
    {
        var issues = ShortcutTargetValidator.ValidateForAdd(
            @"Paint.exe",
            [],
            _ => true);

        var issue = Assert.Single(issues);
        Assert.Equal("targetPath", issue.Field);
        Assert.Equal("The selected target must use an absolute path.", issue.Message);
    }
}
