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
    private volatile bool           _disposed;
    // Plain int — memory ordering is enforced explicitly via Volatile.Read/Write at
    // every cross-thread access point. Marking it volatile would cause CS0420 when
    // passed by ref to Volatile.Read/Write (the ref strips the volatile qualifier).
    private int                     _lastRegisteredCount;

    // Volatile backing field so cross-thread reads (WPF dispatcher ↔ pump thread)
    // always see the latest write without a memory barrier.
    private volatile bool _isRunning;
    public bool IsRunning => _isRunning;

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
        if (_isRunning) Stop();

        var bindings = HotkeyConfig.Load();
        // Use ManualResetEventSlim in a using block so it is always disposed
        // even if the pump thread throws before signalling.
        using var ready = new ManualResetEventSlim(false);

        _thread = new Thread(() =>
        {
            _listener = new HotkeyListener(_engine);
            _listener.SwapModeChanged += active => SwapModeChanged?.Invoke(active);
            _listener.Register(bindings);
            // Volatile write — ensures the caller's Volatile.Read below sees the
            // updated count without a torn read.
            Volatile.Write(ref _lastRegisteredCount, _listener.RegisteredCount);
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
        _isRunning = true;
        return Volatile.Read(ref _lastRegisteredCount);
    }

    /// <summary>
    /// Posts WM_QUIT to the pump thread and waits for it to exit.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        _listener?.Stop();
        bool exited = _thread?.Join(TimeSpan.FromSeconds(2)) ?? true;
        if (!exited)
            Console.Error.WriteLine("[WARN] HotkeyThread: pump thread did not exit within 2 s — it will be abandoned.");
        _thread    = null;
        _isRunning = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
