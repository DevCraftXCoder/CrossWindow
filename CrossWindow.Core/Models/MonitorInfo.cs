namespace CrossWindow.Core.Models;

public sealed record MonitorInfo
{
    public required IntPtr Handle { get; init; }
    public required string DeviceName { get; init; }
    public required CwRect Bounds { get; init; }
    public required CwRect WorkArea { get; init; }
    public required bool IsPrimary { get; init; }
    public double ScaleFactor { get; init; } = 1.0;

    public CwPoint Center => Bounds.Center;

    public override string ToString() =>
        $"{DeviceName} [{Bounds.X},{Bounds.Y} {Bounds.Width}x{Bounds.Height}]{(IsPrimary ? " (primary)" : "")}";
}
