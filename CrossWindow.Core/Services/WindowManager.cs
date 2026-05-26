using CrossWindow.Core.Models;
using CrossWindow.Core.Win32;
using System.Text;

namespace CrossWindow.Core.Services;

public sealed class WindowManager
{
    // ─── Public API ───────────────────────────────────────────────────────────────

    public WindowInfo? GetActiveWindow()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        return hwnd == IntPtr.Zero ? null : GetWindowInfo(hwnd);
    }

    public WindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        if (!IsValidTarget(hwnd)) return null;

        var sb = new StringBuilder(512);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);

        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return null;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);

        return new WindowInfo
        {
            Handle = hwnd,
            Title = sb.ToString(),
            Bounds = CwRect.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom),
            IsMaximized = NativeMethods.IsZoomed(hwnd),
            IsMinimized = NativeMethods.IsIconic(hwnd),
            ProcessId = pid
        };
    }

    public MoveResult MoveWindow(WindowInfo window, MonitorInfo source, MonitorInfo target)
    {
        if (window.IsMinimized)
            return MoveResult.Fail("Window is minimized — cannot move");

        var boundsToMove = window.Bounds;
        bool wasMaximized = window.IsMaximized;

        if (wasMaximized)
        {
            NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
            if (!NativeMethods.GetWindowRect(window.Handle, out var restored))
                return MoveResult.Fail("Could not read restored window bounds");
            boundsToMove = CwRect.FromLTRB(restored.Left, restored.Top, restored.Right, restored.Bottom);
        }

        var newBounds = ComputeNewBounds(boundsToMove, source, target);

        bool ok = NativeMethods.SetWindowPos(
            window.Handle,
            IntPtr.Zero,
            newBounds.X, newBounds.Y,
            newBounds.Width, newBounds.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_ASYNCWINDOWPOS);

        if (!ok)
            return MoveResult.Fail($"SetWindowPos failed (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");

        return MoveResult.Ok(newBounds);
    }

    // ─── Validation ───────────────────────────────────────────────────────────────

    public static bool IsValidTarget(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        if (!NativeMethods.IsWindowVisible(hwnd)) return false;

        // Skip titleless windows (shell, taskbar, invisible overlays)
        if (NativeMethods.GetWindowTextLength(hwnd) == 0) return false;

        return true;
    }

    // ─── Geometry ────────────────────────────────────────────────────────────────

    private static CwRect ComputeNewBounds(CwRect win, MonitorInfo source, MonitorInfo target)
    {
        // Preserve relative position within the source monitor (proportional)
        double relX = source.Bounds.Width > 0
            ? (double)(win.X - source.Bounds.X) / source.Bounds.Width : 0;
        double relY = source.Bounds.Height > 0
            ? (double)(win.Y - source.Bounds.Y) / source.Bounds.Height : 0;

        int newX = (int)(target.Bounds.X + relX * target.Bounds.Width);
        int newY = (int)(target.Bounds.Y + relY * target.Bounds.Height);

        // DPI-aware size: scale physical pixels by the DPI ratio between monitors.
        // With PerMonitorV2 awareness (app.manifest), GetWindowRect returns physical
        // pixels, so multiplying by (targetDPI / sourceDPI) preserves visual size.
        double dpiRatio = source.ScaleFactor > 0 ? target.ScaleFactor / source.ScaleFactor : 1.0;
        int newW = (int)(win.Width  * dpiRatio);
        int newH = (int)(win.Height * dpiRatio);

        // Clamp so window fits within target work area
        int maxW = Math.Max(target.WorkArea.Width, 200);
        int maxH = Math.Max(target.WorkArea.Height, 100);
        newW = Math.Clamp(newW, 100, maxW);
        newH = Math.Clamp(newH, 60,  maxH);
        newX = Math.Clamp(newX, target.WorkArea.X, target.WorkArea.Right  - newW);
        newY = Math.Clamp(newY, target.WorkArea.Y, target.WorkArea.Bottom - newH);

        return new CwRect(newX, newY, newW, newH);
    }
}
