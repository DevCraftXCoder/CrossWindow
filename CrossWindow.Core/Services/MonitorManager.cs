using CrossWindow.Core.Enums;
using CrossWindow.Core.Models;
using CrossWindow.Core.Win32;
using System.Runtime.InteropServices;

namespace CrossWindow.Core.Services;

public sealed class MonitorManager
{
    // ─── Public API ───────────────────────────────────────────────────────────────

    public List<MonitorInfo> EnumerateMonitors()
    {
        var result = new List<MonitorInfo>();

        NativeMethods.MonitorEnumDelegate callback = (IntPtr hMonitor, IntPtr _hdc, ref NativeMethods.RECT _rect, IntPtr _data) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
            };
            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                result.Add(new MonitorInfo
                {
                    Handle = hMonitor,
                    DeviceName = info.szDevice ?? string.Empty,
                    Bounds = CwRect.FromLTRB(
                        info.rcMonitor.Left, info.rcMonitor.Top,
                        info.rcMonitor.Right, info.rcMonitor.Bottom),
                    WorkArea = CwRect.FromLTRB(
                        info.rcWork.Left, info.rcWork.Top,
                        info.rcWork.Right, info.rcWork.Bottom),
                    IsPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                    ScaleFactor = GetScaleFactor(hMonitor)
                });
            }
            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return result;
    }

    public MonitorInfo? FindMonitorContaining(WindowInfo window)
    {
        var monitors = EnumerateMonitors();
        var center = window.Center;

        var containing = monitors.FirstOrDefault(m => m.Bounds.Contains(center));
        if (containing != null) return containing;

        // Fallback: closest by center distance
        return monitors
            .OrderBy(m => DistanceSq(m.Center, center))
            .FirstOrDefault();
    }

    public MonitorInfo? FindMonitorInDirection(
        MonitorInfo current,
        Direction direction,
        List<MonitorInfo>? monitors = null)
    {
        monitors ??= EnumerateMonitors();
        var cx = current.Center.X;
        var cy = current.Center.Y;

        IEnumerable<MonitorInfo> candidates = direction switch
        {
            Direction.Left => monitors.Where(m => m.Center.X < cx),
            Direction.Right => monitors.Where(m => m.Center.X > cx),
            Direction.Up => monitors.Where(m => m.Center.Y < cy),
            Direction.Down => monitors.Where(m => m.Center.Y > cy),
            Direction.UpperLeft => monitors.Where(m => m.Center.X < cx && m.Center.Y < cy),
            Direction.UpperRight => monitors.Where(m => m.Center.X > cx && m.Center.Y < cy),
            Direction.LowerLeft => monitors.Where(m => m.Center.X < cx && m.Center.Y > cy),
            Direction.LowerRight => monitors.Where(m => m.Center.X > cx && m.Center.Y > cy),
            Direction.Opposite => monitors
                .Where(m => m.Handle != current.Handle)
                .OrderByDescending(m => DistanceSq(m.Center, current.Center))
                .Take(1),
            Direction.Next => CyclicNeighbor(current, monitors, +1),
            Direction.Previous => CyclicNeighbor(current, monitors, -1),
            _ => []
        };

        return candidates
            .Where(m => m.Handle != current.Handle)
            .OrderBy(m => DistanceSq(m.Center, current.Center))
            .FirstOrDefault();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private static IEnumerable<MonitorInfo> CyclicNeighbor(
        MonitorInfo current, List<MonitorInfo> monitors, int delta)
    {
        var sorted = monitors
            .OrderBy(m => m.Bounds.X)
            .ThenBy(m => m.Bounds.Y)
            .ToList();

        var idx = sorted.FindIndex(m => m.Handle == current.Handle);
        if (idx < 0) return [];

        var next = (idx + delta + sorted.Count) % sorted.Count;
        return [sorted[next]];
    }

    private static double DistanceSq(CwPoint a, CwPoint b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static double GetScaleFactor(IntPtr hMonitor)
    {
        try
        {
            var hr = NativeMethods.GetDpiForMonitor(hMonitor, 0, out var dpiX, out _);
            if (hr == 0 && dpiX > 0)
                return dpiX / 96.0;
        }
        catch { }
        return 1.0;
    }
}
