namespace CrossWindow.Core.Models;

public readonly record struct CwPoint(int X, int Y);

public readonly record struct CwSize(int Width, int Height);

public readonly record struct CwRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public CwPoint Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(CwPoint p) =>
        p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;

    public bool IsEmpty => Width == 0 || Height == 0;

    public static CwRect FromLTRB(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);
}
