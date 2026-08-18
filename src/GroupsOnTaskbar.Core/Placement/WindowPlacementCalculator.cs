namespace GroupsOnTaskbar.Core.Placement;

public static class WindowPlacementCalculator
{
    public static ScreenRect Calculate(
        ScreenRect monitor,
        ScreenRect workArea,
        int pointerX,
        int pointerY,
        int windowWidth,
        int windowHeight,
        int gap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);

        if (windowWidth > workArea.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(windowWidth), "The requested window width must fit inside the work area.");
        }

        if (windowHeight > workArea.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(windowHeight), "The requested window height must fit inside the work area.");
        }

        var edge = InferTaskbarEdge(monitor, workArea);

        var x = edge switch
        {
            TaskbarEdge.Left => workArea.X + gap,
            TaskbarEdge.Right => workArea.Right - gap - windowWidth,
            _ => pointerX - (windowWidth / 2)
        };

        var y = edge switch
        {
            TaskbarEdge.Top => workArea.Y + gap,
            TaskbarEdge.Bottom => workArea.Bottom - gap - windowHeight,
            _ => pointerY - (windowHeight / 2)
        };

        x = Math.Clamp(x, workArea.X, workArea.Right - windowWidth);
        y = Math.Clamp(y, workArea.Y, workArea.Bottom - windowHeight);

        return new ScreenRect(x, y, windowWidth, windowHeight);
    }

    private static TaskbarEdge InferTaskbarEdge(ScreenRect monitor, ScreenRect workArea)
    {
        var leftInset = workArea.X - monitor.X;
        var topInset = workArea.Y - monitor.Y;
        var rightInset = monitor.Right - workArea.Right;
        var bottomInset = monitor.Bottom - workArea.Bottom;

        var largestInset = 0;
        var edge = TaskbarEdge.Bottom;

        if (leftInset > largestInset)
        {
            largestInset = leftInset;
            edge = TaskbarEdge.Left;
        }

        if (topInset > largestInset)
        {
            largestInset = topInset;
            edge = TaskbarEdge.Top;
        }

        if (rightInset > largestInset)
        {
            largestInset = rightInset;
            edge = TaskbarEdge.Right;
        }

        if (bottomInset > largestInset)
        {
            edge = TaskbarEdge.Bottom;
        }

        return edge;
    }
}
