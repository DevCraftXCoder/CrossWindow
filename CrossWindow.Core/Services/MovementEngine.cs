using CrossWindow.Core.Enums;
using CrossWindow.Core.Models;
using CrossWindow.Core.Win32;

namespace CrossWindow.Core.Services;

public sealed class MovementEngine
{
    private readonly MonitorManager _monitorManager;
    private readonly WindowManager _windowManager;

    public MovementEngine(MonitorManager monitorManager, WindowManager windowManager)
    {
        _monitorManager = monitorManager;
        _windowManager = windowManager;
    }

    // ─── Move active window in a direction ────────────────────────────────────────

    public MoveResult MoveActiveWindow(Direction direction)
    {
        var window = _windowManager.GetActiveWindow();
        if (window == null) return MoveResult.Fail("No active window found");

        var monitors = _monitorManager.EnumerateMonitors();
        if (monitors.Count < 2) return MoveResult.Fail("Only one monitor connected");

        var source = _monitorManager.FindMonitorContaining(window);
        if (source == null) return MoveResult.Fail("Cannot determine current monitor");

        var target = _monitorManager.FindMonitorInDirection(source, direction, monitors);
        if (target == null) return MoveResult.Fail($"No monitor found in direction: {direction}");

        return _windowManager.MoveWindow(window, source, target);
    }

    // ─── Snap active window to a zone on its current monitor ─────────────────────

    public MoveResult SnapActiveWindow(SnapZone zone)
    {
        var window = _windowManager.GetActiveWindow();
        if (window == null) return MoveResult.Fail("No active window found");

        var monitor = _monitorManager.FindMonitorContaining(window);
        if (monitor == null) return MoveResult.Fail("Cannot determine current monitor");

        // Restore maximized windows first so SetWindowPos takes effect
        if (NativeMethods.IsZoomed(window.Handle))
            NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);

        var snapBounds = SnapEngine.ComputeSnapBounds(zone, monitor.WorkArea);

        const uint flags = NativeMethods.SWP_NOZORDER
                         | NativeMethods.SWP_SHOWWINDOW
                         | NativeMethods.SWP_ASYNCWINDOWPOS;
        bool ok = NativeMethods.SetWindowPos(window.Handle, IntPtr.Zero,
            snapBounds.X, snapBounds.Y, snapBounds.Width, snapBounds.Height, flags);

        return ok ? MoveResult.Ok(snapBounds) : MoveResult.Fail("SetWindowPos failed");
    }

    // ─── Swap two windows between monitors ─────────────────────────────────────────
    //
    // Finds the active window and the most-recently-focused window on the
    // adjacent monitor in `direction`, then swaps them: each window takes
    // the other's monitor, preserving relative position/size on the new screen.
    //
    // If no second window is found on the target monitor, moves the active
    // window there without swapping (behaves like MoveActiveWindow).

    public SwapResult SwapActiveWindow(Direction direction)
    {
        var windowA = _windowManager.GetActiveWindow();
        if (windowA == null) return SwapResult.Fail("No active window found");

        var monitors = _monitorManager.EnumerateMonitors();
        if (monitors.Count < 2) return SwapResult.Fail("Only one monitor connected");

        var monitorA = _monitorManager.FindMonitorContaining(windowA);
        if (monitorA == null) return SwapResult.Fail("Cannot determine current monitor");

        var monitorB = _monitorManager.FindMonitorInDirection(monitorA, direction, monitors);
        if (monitorB == null) return SwapResult.Fail($"No monitor found in direction: {direction}");

        // Find the foreground window on the target monitor (topmost visible window)
        var windowB = FindTopmostWindowOnMonitor(monitorB, ignoreHandle: windowA.Handle);

        if (windowB == null)
        {
            // Nothing to swap with — just move A
            var moveOnly = _windowManager.MoveWindow(windowA, monitorA, monitorB);
            return moveOnly.IsSuccess
                ? SwapResult.MovedOnly(moveOnly.NewBounds!.Value)
                : SwapResult.Fail(moveOnly.ErrorMessage!);
        }

        // Swap: A → monitorB at B's position, B → monitorA at A's position
        var aBoundsOnB = MapBoundsToMonitor(windowA.Bounds, monitorA, monitorB);
        var bBoundsOnA = MapBoundsToMonitor(windowB.Bounds, monitorB, monitorA);

        bool okA = NativeSetWindowPos(windowA.Handle, aBoundsOnB);
        bool okB = NativeSetWindowPos(windowB.Handle, bBoundsOnA);

        if (!okA && !okB) return SwapResult.Fail("SetWindowPos failed for both windows");

        return SwapResult.Swapped(windowA, windowB, aBoundsOnB, bBoundsOnA);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private WindowInfo? FindTopmostWindowOnMonitor(MonitorInfo monitor, IntPtr ignoreHandle)
    {
        WindowInfo? best = null;

        bool EnumWindowsCallback(IntPtr hwnd, IntPtr _)
        {
            if (hwnd == ignoreHandle) return true;
            var info = _windowManager.GetWindowInfo(hwnd);
            if (info == null) return true;

            var m = _monitorManager.FindMonitorContaining(info);
            if (m?.Handle == monitor.Handle)
            {
                best = info;
                return false; // stop — GetForegroundWindow ordering means first valid is topmost
            }
            return true;
        }

        NativeMethods.EnumWindows(EnumWindowsCallback, IntPtr.Zero);
        return best;
    }

    private static CwRect MapBoundsToMonitor(CwRect bounds, MonitorInfo fromMonitor, MonitorInfo toMonitor)
    {
        double relX = fromMonitor.Bounds.Width > 0
            ? (double)(bounds.X - fromMonitor.Bounds.X) / fromMonitor.Bounds.Width : 0;
        double relY = fromMonitor.Bounds.Height > 0
            ? (double)(bounds.Y - fromMonitor.Bounds.Y) / fromMonitor.Bounds.Height : 0;
        double relW = fromMonitor.Bounds.Width > 0
            ? (double)bounds.Width / fromMonitor.Bounds.Width : 0.5;
        double relH = fromMonitor.Bounds.Height > 0
            ? (double)bounds.Height / fromMonitor.Bounds.Height : 0.5;

        int newX = (int)(toMonitor.Bounds.X + relX * toMonitor.Bounds.Width);
        int newY = (int)(toMonitor.Bounds.Y + relY * toMonitor.Bounds.Height);
        int newW = (int)(relW * toMonitor.Bounds.Width);
        int newH = (int)(relH * toMonitor.Bounds.Height);

        newW = Math.Clamp(newW, 100, toMonitor.WorkArea.Width);
        newH = Math.Clamp(newH, 60, toMonitor.WorkArea.Height);
        newX = Math.Clamp(newX, toMonitor.WorkArea.X, toMonitor.WorkArea.Right - newW);
        newY = Math.Clamp(newY, toMonitor.WorkArea.Y, toMonitor.WorkArea.Bottom - newH);

        return new CwRect(newX, newY, newW, newH);
    }

    private static bool NativeSetWindowPos(IntPtr hwnd, CwRect bounds)
    {
        const uint flags = NativeMethods.SWP_NOZORDER
                         | NativeMethods.SWP_SHOWWINDOW
                         | NativeMethods.SWP_ASYNCWINDOWPOS;
        return NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, flags);
    }
}

// ─── SwapResult ──────────────────────────────────────────────────────────────

public sealed record SwapResult
{
    public bool IsSuccess { get; init; }
    public bool WasActualSwap { get; init; }
    public WindowInfo? WindowA { get; init; }
    public WindowInfo? WindowB { get; init; }
    public CwRect? NewBoundsA { get; init; }
    public CwRect? NewBoundsB { get; init; }
    public string? ErrorMessage { get; init; }

    public static SwapResult Swapped(
        WindowInfo a, WindowInfo b, CwRect newA, CwRect newB) =>
        new() { IsSuccess = true, WasActualSwap = true, WindowA = a, WindowB = b, NewBoundsA = newA, NewBoundsB = newB };

    public static SwapResult MovedOnly(CwRect newBounds) =>
        new() { IsSuccess = true, WasActualSwap = false, NewBoundsA = newBounds };

    public static SwapResult Fail(string reason) =>
        new() { IsSuccess = false, ErrorMessage = reason };
}
