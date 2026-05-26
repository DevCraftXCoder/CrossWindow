using CrossWindow.Core.Services;

namespace CrossWindow.Tray;

/// <summary>
/// Owns a dedicated STA thread that runs the Win32 hotkey message pump.
/// RegisterHotKey + GetMessage must share the same thread — this isolates
/// that requirement from the WPF dispatcher thread.
/// </summary>
public sealed class HotkeyThread : IDisposable
{
    private readonly MovementEngine _engine;
    private Thread?                 _thread;
    private volatile HotkeyListener? _listener;
    private bool                    _disposed;
    private int                     _lastRegisteredCount;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Relayed from HotkeyListener — fires on the pump thread. Payload is the new swap mode state.
    /// </summary>
    public event Action<bool>? SwapModeChanged;

    public HotkeyThread(MovementEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Loads config, registers all hotkeys, starts the message pump.
    /// Returns the number of bindings loaded from config.
    /// Blocks until all hotkeys are registered before returning.
    /// </summary>
    public int Start()
    {
        if (IsRunning) Stop();

        var bindings = HotkeyConfig.Load();
        var ready    = new ManualResetEventSlim(false);

        _thread = new Thread(() =>
        {
            _listener = new HotkeyListener(_engine);
            _listener.SwapModeChanged += active => SwapModeChanged?.Invoke(active);
            _listener.Register(bindings);
            _lastRegisteredCount = _listener.RegisteredCount;
            ready.Set();
            _listener.Run();
            _listener.Dispose();
            _listener = null;
        })
        {
            Name         = "CrossWindow-HotkeyPump",
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);

        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(3));
        IsRunning = true;
        return _lastRegisteredCount;
    }

    /// <summary>
    /// Posts WM_QUIT to the pump thread and waits for it to exit.
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;
        _listener?.Stop();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread   = null;
        IsRunning = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
