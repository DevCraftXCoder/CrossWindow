using CrossWindow.Core.Enums;
using CrossWindow.Core.Models;
using CrossWindow.Core.Services;

var monitors = new MonitorManager();
var windows = new WindowManager();
var engine = new MovementEngine(monitors, windows);

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "info";

switch (command)
{
    case "info":
        PrintInfo(monitors, windows);
        break;

    case "move":
        var dir = ParseDirection(args.Length > 1 ? args[1] : "right");
        var result = engine.MoveActiveWindow(dir);
        Console.WriteLine(result.IsSuccess
            ? $"[OK]  Moved to {result.NewBounds}"
            : $"[ERR] {result.ErrorMessage}");
        break;

    case "swap":
        var swapDir = ParseDirection(args.Length > 1 ? args[1] : "right");
        var swap = engine.SwapActiveWindow(swapDir);
        if (swap.IsSuccess)
        {
            Console.WriteLine(swap.WasActualSwap
                ? $"[OK]  Swapped: A→{swap.NewBoundsA}  B→{swap.NewBoundsB}"
                : $"[OK]  Moved (no window to swap with): {swap.NewBoundsA}");
        }
        else
        {
            Console.WriteLine($"[ERR] {swap.ErrorMessage}");
        }
        break;

    case "snap":
        var snapZone = ParseSnapZone(args.Length > 1 ? args[1] : "left");
        var snapResult = engine.SnapActiveWindow(snapZone);
        Console.WriteLine(snapResult.IsSuccess
            ? $"[OK]  Snapped {snapZone} → {snapResult.NewBounds}"
            : $"[ERR] {snapResult.ErrorMessage}");
        break;

    case "listen":
        RunListen(engine);
        break;

    default:
        PrintUsage();
        break;
}

static void PrintInfo(MonitorManager monitors, WindowManager windows)
{
    Console.WriteLine("=== Monitors ===");
    var list = monitors.EnumerateMonitors();
    if (list.Count == 0)
    {
        Console.WriteLine("  (none detected)");
    }
    else
    {
        foreach (var (m, i) in list.Select((m, i) => (m, i + 1)))
            Console.WriteLine($"  {i}. {m}  scale={m.ScaleFactor:F2}x");
    }

    Console.WriteLine();
    Console.WriteLine("=== Active Window ===");
    var win = windows.GetActiveWindow();
    if (win == null)
        Console.WriteLine("  (no foreground window)");
    else
        Console.WriteLine($"  {win}  pid={win.ProcessId}");
}

static SnapZone ParseSnapZone(string s) => s.ToLowerInvariant() switch
{
    "left"   or "l"  => SnapZone.LeftHalf,
    "right"  or "r"  => SnapZone.RightHalf,
    "top"    or "t"  => SnapZone.TopHalf,
    "bottom" or "b"  => SnapZone.BottomHalf,
    "tl" or "topleft"     => SnapZone.TopLeft,
    "tr" or "topright"    => SnapZone.TopRight,
    "bl" or "bottomleft"  => SnapZone.BottomLeft,
    "br" or "bottomright" => SnapZone.BottomRight,
    "max" or "maximize" or "m" => SnapZone.Maximize,
    "center" or "c"    => SnapZone.Center,
    _ => SnapZone.LeftHalf
};

static Direction ParseDirection(string s) => s.ToLowerInvariant() switch
{
    "left" or "l" => Direction.Left,
    "right" or "r" => Direction.Right,
    "up" or "u" => Direction.Up,
    "down" or "d" => Direction.Down,
    "ul" or "upleft" => Direction.UpperLeft,
    "ur" or "upright" => Direction.UpperRight,
    "dl" or "downleft" => Direction.LowerLeft,
    "dr" or "downright" => Direction.LowerRight,
    "opposite" or "o" => Direction.Opposite,
    "next" or "n" => Direction.Next,
    "prev" or "p" => Direction.Previous,
    _ => Direction.Right
};

static void RunListen(MovementEngine engine)
{
    var bindings = HotkeyConfig.Load();

    Console.WriteLine("Hotkey bindings:");
    int n = 1;
    foreach (var b in bindings)
    {
        string target = b.Action switch
        {
            HotkeyAction.Snap           => b.Zone?.ToString() ?? "?",
            HotkeyAction.SwapModeToggle => "(toggle)",
            _                           => b.Direction.ToString()
        };
        Console.WriteLine($"  {n++,2}. {b.Action,-15}  {target,-12}  [{b.Label}]");
    }
    Console.WriteLine();

    using var listener = new HotkeyListener(engine);
    bool ok = listener.Register(bindings);
    if (!ok)
        Console.Error.WriteLine("[WARN] Some hotkeys could not be registered — they may be claimed by another app.");

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        listener.Stop();
    };

    Console.WriteLine("CrossWindow listening. Press Ctrl+C to stop.");
    listener.Run();
    Console.WriteLine("Stopped.");
}

static void PrintUsage()
{
    Console.WriteLine("CrossWindow CLI");
    Console.WriteLine();
    Console.WriteLine("  crosswindow info                    List monitors + active window");
    Console.WriteLine("  crosswindow move <dir>              Move active window to adjacent monitor");
    Console.WriteLine("  crosswindow swap <dir>              Swap active window with window on target monitor");
    Console.WriteLine("  crosswindow snap <zone>             Snap active window to a zone on its current monitor");
    Console.WriteLine("  crosswindow listen                  Start hotkey daemon (Ctrl+C to stop)");
    Console.WriteLine();
    Console.WriteLine("  Directions: left right up down ul ur dl dr opposite next prev");
    Console.WriteLine("  Zones:      left right top bottom tl tr bl br max center");
    Console.WriteLine();
    Console.WriteLine("  Examples:");
    Console.WriteLine("    crosswindow move right");
    Console.WriteLine("    crosswindow swap left");
    Console.WriteLine("    crosswindow snap left");
    Console.WriteLine("    crosswindow snap tl");
    Console.WriteLine("    crosswindow snap center");
    Console.WriteLine("    crosswindow listen");
}
