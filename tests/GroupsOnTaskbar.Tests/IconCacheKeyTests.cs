using GroupsOnTaskbar.Core.Launch;

namespace GroupsOnTaskbar.Tests;

public sealed class IconCacheKeyTests
{
    [Fact]
    public void Create_WhenPathDiffersOnlyByCasing_ReturnsSameKey()
    {
        var lastWriteUtc = new DateTimeOffset(2026, 8, 18, 12, 30, 45, TimeSpan.Zero);

        var upperKey = IconCacheKey.Create(@"C:\Apps\PAINT.EXE", lastWriteUtc);
        var lowerKey = IconCacheKey.Create(@"c:\apps\paint.exe", lastWriteUtc);

        Assert.Equal(upperKey, lowerKey);
    }

    [Fact]
    public void Create_WhenLastWriteTimestampChanges_ReturnsDifferentKey()
    {
        var earlier = new DateTimeOffset(2026, 8, 18, 12, 30, 45, TimeSpan.Zero);
        var later = earlier.AddMinutes(1);

        var earlierKey = IconCacheKey.Create(@"C:\Apps\Paint.exe", earlier);
        var laterKey = IconCacheKey.Create(@"C:\Apps\Paint.exe", later);

        Assert.NotEqual(earlierKey, laterKey);
    }

    [Fact]
    public void Create_AlwaysReturnsPngFileName()
    {
        var key = IconCacheKey.Create(
            @"C:\Apps\Paint.exe",
            new DateTimeOffset(2026, 8, 18, 12, 30, 45, TimeSpan.Zero));

        Assert.EndsWith(".png", key, StringComparison.Ordinal);
    }
}
