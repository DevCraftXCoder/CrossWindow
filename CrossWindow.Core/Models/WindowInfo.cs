namespace CrossWindow.Core.Models;

public sealed record WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public required CwRect Bounds { get; init; }
    public required bool IsMaximized { get; init; }
    public required bool IsMinimized { get; init; }
    public required uint ProcessId { get; init; }

    public CwPoint Center => Bounds.Center;

    public override string ToString() =>
        $"\"{Title}\" [{Bounds.X},{Bounds.Y} {Bounds.Width}x{Bounds.Height}]{(IsMaximized ? " [MAX]" : "")}";
}
