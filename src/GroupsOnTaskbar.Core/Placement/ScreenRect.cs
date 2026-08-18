namespace GroupsOnTaskbar.Core.Placement;

public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}
