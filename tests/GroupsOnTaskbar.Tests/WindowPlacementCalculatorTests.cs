using GroupsOnTaskbar.Core.Placement;

namespace GroupsOnTaskbar.Tests;

public sealed class WindowPlacementCalculatorTests
{
    [Fact]
    public void Calculate_WithBottomTaskbar_ReturnsPlacementAboveTaskbar()
    {
        var placement = WindowPlacementCalculator.Calculate(
            new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 0, 1920, 1040),
            960,
            1060,
            440,
            360,
            8);

        Assert.Equal(new ScreenRect(740, 672, 440, 360), placement);
    }

    [Fact]
    public void Calculate_WithTopTaskbar_PinsBelowWorkAreaTopEdge()
    {
        var placement = WindowPlacementCalculator.Calculate(
            new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 40, 1920, 1040),
            960,
            20,
            440,
            360,
            8);

        Assert.Equal(new ScreenRect(740, 48, 440, 360), placement);
    }

    [Fact]
    public void Calculate_WithLeftTaskbar_PinsInsideLeftEdge()
    {
        var placement = WindowPlacementCalculator.Calculate(
            new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(48, 0, 1872, 1080),
            10,
            540,
            440,
            360,
            8);

        Assert.Equal(new ScreenRect(56, 360, 440, 360), placement);
    }

    [Fact]
    public void Calculate_WithRightTaskbar_PinsInsideRightEdge()
    {
        var placement = WindowPlacementCalculator.Calculate(
            new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 0, 1872, 1080),
            1910,
            540,
            440,
            360,
            8);

        Assert.Equal(new ScreenRect(1424, 360, 440, 360), placement);
    }

    [Fact]
    public void Calculate_WhenMonitorEqualsWorkArea_UsesBottomEdgeFallback()
    {
        var placement = WindowPlacementCalculator.Calculate(
            new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 0, 1920, 1080),
            960,
            1070,
            440,
            360,
            8);

        Assert.Equal(new ScreenRect(740, 712, 440, 360), placement);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1919, 0)]
    [InlineData(0, 1079)]
    [InlineData(1919, 1079)]
    public void Calculate_WhenPointerIsNearMonitorCorners_ClampsInsideWorkArea(int pointerX, int pointerY)
    {
        var workArea = new ScreenRect(48, 0, 1872, 1080);

        var placement = WindowPlacementCalculator.Calculate(
            new ScreenRect(0, 0, 1920, 1080),
            workArea,
            pointerX,
            pointerY,
            440,
            360,
            8);

        AssertInside(workArea, placement);
    }

    [Fact]
    public void Calculate_WithNonZeroOriginWorkArea_ReturnsCoordinatesInsideThatWorkArea()
    {
        var workArea = new ScreenRect(1920, 0, 2560, 1400);

        var placement = WindowPlacementCalculator.Calculate(
            new ScreenRect(1920, 0, 2560, 1440),
            workArea,
            4470,
            1410,
            640,
            480,
            12);

        Assert.Equal(new ScreenRect(3840, 908, 640, 480), placement);
        AssertInside(workArea, placement);
    }

    [Theory]
    [InlineData(0, 360, 8)]
    [InlineData(440, 0, 8)]
    [InlineData(440, 360, -1)]
    [InlineData(2000, 360, 8)]
    [InlineData(440, 1100, 8)]
    public void Calculate_WithInvalidSizesOrOversizedWindow_ThrowsArgumentOutOfRangeException(
        int windowWidth,
        int windowHeight,
        int gap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowPlacementCalculator.Calculate(
                new ScreenRect(0, 0, 1920, 1080),
                new ScreenRect(0, 0, 1920, 1040),
                960,
                1060,
                windowWidth,
                windowHeight,
                gap));
    }

    [Theory]
    [InlineData(560, 460, 8)]
    [InlineData(840, 690, 12)]
    [InlineData(1120, 920, 16)]
    public void Calculate_WithRuntimeLauncherFootprint_KeepsPlacementInsideWorkArea(
        int windowWidth,
        int windowHeight,
        int gap)
    {
        var monitor = new ScreenRect(0, 0, 3840, 2160);
        var workArea = new ScreenRect(0, 0, 3840, 2080);

        var placement = WindowPlacementCalculator.Calculate(
            monitor,
            workArea,
            3830,
            2150,
            windowWidth,
            windowHeight,
            gap);

        Assert.Equal(windowWidth, placement.Width);
        Assert.Equal(windowHeight, placement.Height);
        Assert.Equal(workArea.Bottom - windowHeight - gap, placement.Y);
        AssertInside(workArea, placement);
    }

    private static void AssertInside(ScreenRect bounds, ScreenRect placement)
    {
        Assert.InRange(placement.X, bounds.X, bounds.Right - placement.Width);
        Assert.InRange(placement.Y, bounds.Y, bounds.Bottom - placement.Height);
        Assert.True(placement.Right <= bounds.Right, "Placement should not overflow the work-area right edge.");
        Assert.True(placement.Bottom <= bounds.Bottom, "Placement should not overflow the work-area bottom edge.");
    }
}
