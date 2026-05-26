using CrossWindow.Core.Models;
using CrossWindow.Core.Win32;

namespace CrossWindow.Core.Services;

/// <summary>
/// Registers global hotkeys and runs a Win32 message pump on the calling thread.
/// Call Register() then Run() on the same thread. Call Stop() from any thread
/// (e.g. a Ctrl+C handler) to post WM_QUIT and unblock Run().
/// </summary>
public sealed class HotkeyListener : IDisposable
{
    private readonly MovementEngine       _engine;
    private readonly List<HotkeyBinding>  _registered = [];
    private volatile uint                 _pumpThreadId;
    private bool                          _swapMode;
    private bool                          _disposed;

    /// <summary>Number of hotkeys successfully registered with the OS.</summary>
    public int RegisteredCount => _registered.Count;

    private const uint WM_QUIT = 0x0012;

    /// <summary>
    /// Fires on the pump thread when swap mode is toggled. Payload is the new state.
    /// </summary>
    public event Action<bool>? SwapModeChanged;

    public HotkeyListener(MovementEngine engine)
    {
        _engine = engine;
    }

    // ─── Register ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers all supplied bindings. Returns true if all succeeded.
    /// On partial failure, prints a warning per failed binding but continues.
    /// Must be called from the same thread that will call Run().
    /// </summary>
    public bool Register(IReadOnlyList<HotkeyBinding> bindings)
    {
        bool allOk = true;

        foreach (var binding in bindings)
        {
            uint fsModifiers = (uint)binding.Modifiers | NativeMethods.MOD_NOREPEAT;
            bool ok = NativeMethods.RegisterHotKey(IntPtr.Zero, binding.Id, fsModifiers, binding.VirtualKey);
            if (ok)
            {
                _registered.Add(binding);
            }
            else
            {
                int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                Console.Error.WriteLine(
                    $"[WARN] Failed to register hotkey id={binding.Id} {binding.Label}  (Win32 error {err})");
                allOk = false;
            }
        }

        return allOk;
    }

    // ─── Run ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Blocks on a Win32 GetMessage loop. Must be called on the same thread as Register().
    /// Returns when Stop() is called or GetMessage signals quit/error.
    /// </summary>
    public void Run()
    {
        _pumpThreadId = NativeMethods.GetCurrentThreadId();

        // Build a fast lookup: hotkey id → binding
        var lookup = new Dictionary<int, HotkeyBinding>(_registered.Count);
        foreach (var b in _registered)
            lookup[b.Id] = b;

        int ret;
        while ((ret = NativeMethods.GetMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0)) > 0)
        {
            if (msg.message == NativeMethods.WM_HOTKEY)
            {
                int hotkeyId = (int)msg.wParam;
                if (lookup.TryGetValue(hotkeyId, out var binding))
                {
                    DispatchBinding(binding);
                }
            }

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        // ret == 0 → WM_QUIT received (normal exit)
        // ret == -1 → GetMessage error
        if (ret == -1)
        {
            int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Console.Error.WriteLine($"[ERR] GetMessage failed with Win32 error {err}");
        }
    }

    // ─── Stop ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Posts WM_QUIT to the pump thread, unblocking Run(). Safe to call from any thread.
    /// </summary>
    public void Stop()
    {
        uint tid = _pumpThreadId;
        if (tid != 0)
            NativeMethods.PostThreadMessage(tid, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var binding in _registered)
            NativeMethods.UnregisterHotKey(IntPtr.Zero, binding.Id);

        _registered.Clear();
    }

    // ─── Dispatch ─────────────────────────────────────────────────────────────────

    private void DispatchBinding(HotkeyBinding binding)
    {
        try
        {
            switch (binding.Action)
            {
                case HotkeyAction.SwapModeToggle:
                    _swapMode = !_swapMode;
                    SwapModeChanged?.Invoke(_swapMode);
                    break;

                case HotkeyAction.Snap:
                    if (binding.Zone.HasValue)
                    {
                        var result = _engine.SnapActiveWindow(binding.Zone.Value);
                        if (!result.IsSuccess)
                            Console.Error.WriteLine($"[ERR] Snap {binding.Zone}: {result.ErrorMessage}");
                    }
                    else
                    {
                        Console.Error.WriteLine($"[WARN] Snap hotkey id={binding.Id} has no zone configured — skipping");
                    }
                    break;

                case HotkeyAction.Move when _swapMode:
                case HotkeyAction.Swap:
                {
                    var result = _engine.SwapActiveWindow(binding.Direction);
                    if (!result.IsSuccess)
                        Console.Error.WriteLine($"[ERR] Swap {binding.Direction}: {result.ErrorMessage}");
                    break;
                }

                default: // Move (swap mode off)
                {
                    var result = _engine.MoveActiveWindow(binding.Direction);
                    if (!result.IsSuccess)
                        Console.Error.WriteLine($"[ERR] Move {binding.Direction}: {result.ErrorMessage}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERR] Hotkey dispatch exception: {ex.Message}");
        }
    }
}
