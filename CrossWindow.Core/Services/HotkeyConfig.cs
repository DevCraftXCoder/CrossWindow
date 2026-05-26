using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrossWindow.Core.Enums;
using CrossWindow.Core.Models;

namespace CrossWindow.Core.Services;

/// <summary>
/// Loads hotkey bindings from crosswindow.json.
/// Search order: %APPDATA%\CrossWindow\crosswindow.json → exe directory.
/// If neither exists, writes defaults to %APPDATA%\CrossWindow\ and returns them.
/// </summary>
public static class HotkeyConfig
{
    private const string FileName = "crosswindow.json";

    // ─── Default binding table (id 1-39) ─────────────────────────────────────────

    private static readonly HotkeyBinding[] Defaults =
    [
        // Arrow keys — Move
        new() { Id = 1,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x25, Action = HotkeyAction.Move, Direction = Direction.Left     },
        new() { Id = 2,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x27, Action = HotkeyAction.Move, Direction = Direction.Right    },
        new() { Id = 3,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x26, Action = HotkeyAction.Move, Direction = Direction.Up       },
        new() { Id = 4,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x28, Action = HotkeyAction.Move, Direction = Direction.Down     },

        // NumPad aliases — Move (VK_NUMPAD4=0x64, VK_NUMPAD6=0x66, VK_NUMPAD8=0x68, VK_NUMPAD2=0x62)
        new() { Id = 5,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x64, Action = HotkeyAction.Move, Direction = Direction.Left     },
        new() { Id = 6,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x66, Action = HotkeyAction.Move, Direction = Direction.Right    },
        new() { Id = 7,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x68, Action = HotkeyAction.Move, Direction = Direction.Up       },
        new() { Id = 8,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x62, Action = HotkeyAction.Move, Direction = Direction.Down     },

        // Arrow keys — Swap
        new() { Id = 9,  Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x25, Action = HotkeyAction.Swap, Direction = Direction.Left  },
        new() { Id = 10, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x27, Action = HotkeyAction.Swap, Direction = Direction.Right },
        new() { Id = 11, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x26, Action = HotkeyAction.Swap, Direction = Direction.Up    },
        new() { Id = 12, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x28, Action = HotkeyAction.Swap, Direction = Direction.Down  },

        // N / P — Move Next / Prev  (VK_N=0x4E, VK_P=0x50)
        new() { Id = 13, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x4E, Action = HotkeyAction.Move, Direction = Direction.Next     },
        new() { Id = 14, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x50, Action = HotkeyAction.Move, Direction = Direction.Previous },

        // N / P — Swap Next / Prev
        new() { Id = 15, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x4E, Action = HotkeyAction.Swap, Direction = Direction.Next     },
        new() { Id = 16, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x50, Action = HotkeyAction.Swap, Direction = Direction.Previous },

        // NumPad4 / NumPad6 — Swap Left / Right (extra aliases)
        new() { Id = 17, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x64, Action = HotkeyAction.Swap, Direction = Direction.Left  },
        new() { Id = 18, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x66, Action = HotkeyAction.Swap, Direction = Direction.Right },

        // NumPad corners — Move diagonal  (NP7=0x67, NP9=0x69, NP1=0x61, NP3=0x63)
        new() { Id = 19, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x67, Action = HotkeyAction.Move, Direction = Direction.UpperLeft  },
        new() { Id = 20, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x69, Action = HotkeyAction.Move, Direction = Direction.UpperRight },
        new() { Id = 21, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x61, Action = HotkeyAction.Move, Direction = Direction.LowerLeft  },
        new() { Id = 22, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x63, Action = HotkeyAction.Move, Direction = Direction.LowerRight },

        // O — Move Opposite  (VK_O=0x4F)
        new() { Id = 23, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKey = 0x4F, Action = HotkeyAction.Move, Direction = Direction.Opposite },

        // NumPad corners — Swap diagonal
        new() { Id = 24, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x67, Action = HotkeyAction.Swap, Direction = Direction.UpperLeft  },
        new() { Id = 25, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x69, Action = HotkeyAction.Swap, Direction = Direction.UpperRight },
        new() { Id = 26, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x61, Action = HotkeyAction.Swap, Direction = Direction.LowerLeft  },
        new() { Id = 27, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x63, Action = HotkeyAction.Swap, Direction = Direction.LowerRight },

        // O — Swap Opposite
        new() { Id = 28, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x4F, Action = HotkeyAction.Swap, Direction = Direction.Opposite },

        // ── Snap zones — Ctrl+Shift+Win + arrow/numpad/letter ─────────────────────
        // Arrow keys → halves
        new() { Id = 29, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x25, Action = HotkeyAction.Snap, Zone = SnapZone.LeftHalf    },
        new() { Id = 30, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x27, Action = HotkeyAction.Snap, Zone = SnapZone.RightHalf   },
        new() { Id = 31, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x26, Action = HotkeyAction.Snap, Zone = SnapZone.TopHalf     },
        new() { Id = 32, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x28, Action = HotkeyAction.Snap, Zone = SnapZone.BottomHalf  },

        // NumPad corners → quarters
        new() { Id = 33, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x67, Action = HotkeyAction.Snap, Zone = SnapZone.TopLeft     },
        new() { Id = 34, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x69, Action = HotkeyAction.Snap, Zone = SnapZone.TopRight    },
        new() { Id = 35, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x61, Action = HotkeyAction.Snap, Zone = SnapZone.BottomLeft  },
        new() { Id = 36, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x63, Action = HotkeyAction.Snap, Zone = SnapZone.BottomRight },

        // M / C → maximize / center
        new() { Id = 37, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x4D, Action = HotkeyAction.Snap, Zone = SnapZone.Maximize    },
        new() { Id = 38, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKey = 0x43, Action = HotkeyAction.Snap, Zone = SnapZone.Center      },

        // ── Swap mode toggle — Ctrl+Shift+Alt+S ───────────────────────────────────
        new() { Id = 39, Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, VirtualKey = 0x53, Action = HotkeyAction.SwapModeToggle },
    ];

    // ─── Public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Full path to the user-editable config file in %APPDATA%\CrossWindow\.
    /// The file may not exist yet — call EnsureDefaultsWritten() or Load() to create it.
    /// </summary>
    public static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrossWindow", FileName);

    /// <summary>
    /// Creates the default config at ConfigPath if it does not already exist.
    /// Safe to call multiple times.
    /// </summary>
    public static void EnsureDefaultsWritten()
    {
        var path = ConfigPath;
        if (!File.Exists(path))
            WriteDefaults(path);
    }

    public static IReadOnlyList<HotkeyBinding> Load()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CrossWindow", FileName);

        var exePath = Path.Combine(
            AppContext.BaseDirectory, FileName);

        if (File.Exists(appDataPath))
            return ParseFile(appDataPath) ?? Defaults;

        if (File.Exists(exePath))
            return ParseFile(exePath) ?? Defaults;

        // Neither exists — write defaults to %APPDATA%\CrossWindow\ and return them
        WriteDefaults(appDataPath);
        return Defaults;
    }

    // ─── Serialisation helpers ────────────────────────────────────────────────────

    private static IReadOnlyList<HotkeyBinding>? ParseFile(string path)
    {
        try
        {
            var json  = File.ReadAllText(path);
            var root  = JsonNode.Parse(json);
            var array = root?["hotkeys"]?.AsArray();
            if (array == null) return null;

            var bindings = new List<HotkeyBinding>(array.Count);
            foreach (var node in array)
            {
                if (node == null) continue;

                var id        = node["id"]?.GetValue<int>() ?? 0;
                var modStr    = node["modifiers"]?.GetValue<string>() ?? "";
                var vk        = node["vk"]?.GetValue<uint>() ?? 0;
                var actionStr = node["action"]?.GetValue<string>() ?? "Move";
                var dirStr    = node["direction"]?.GetValue<string>() ?? "Right";
                var zoneStr   = node["zone"]?.GetValue<string>();

                var mods = ParseModifiers(modStr);

                if (!Enum.TryParse<HotkeyAction>(actionStr, ignoreCase: true, out var action))
                    action = HotkeyAction.Move;

                if (!Enum.TryParse<Direction>(dirStr, ignoreCase: true, out var dir))
                    dir = Direction.Right;

                SnapZone? zone = null;
                if (zoneStr != null && Enum.TryParse<SnapZone>(zoneStr, ignoreCase: true, out var z))
                    zone = z;

                bindings.Add(new HotkeyBinding
                {
                    Id         = id,
                    Modifiers  = mods,
                    VirtualKey = vk,
                    Action     = action,
                    Direction  = dir,
                    Zone       = zone
                });
            }

            return bindings.Count > 0 ? bindings : null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Could not parse {path}: {ex.Message}");
            return null;
        }
    }

    private static HotkeyModifiers ParseModifiers(string s)
    {
        var result = HotkeyModifiers.None;
        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<HotkeyModifiers>(part, ignoreCase: true, out var flag))
                result |= flag;
        }
        return result;
    }

    private static void WriteDefaults(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var options = new JsonSerializerOptions
            {
                WriteIndented            = true,
                DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull
            };
            var records = Defaults.Select(b => new
            {
                id        = b.Id,
                modifiers = string.Join(",", GetModifierNames(b.Modifiers)),
                vk        = b.VirtualKey,
                action    = b.Action.ToString(),
                direction = b.Action is HotkeyAction.Snap or HotkeyAction.SwapModeToggle
                                ? (string?)null
                                : b.Direction.ToString(),
                zone      = b.Zone?.ToString()
            }).ToArray();

            var wrapper = new { hotkeys = records };
            File.WriteAllText(path, JsonSerializer.Serialize(wrapper, options));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Could not write default config to {path}: {ex.Message}");
        }
    }

    private static IEnumerable<string> GetModifierNames(HotkeyModifiers mods)
    {
        if (mods.HasFlag(HotkeyModifiers.Control))  yield return "Control";
        if (mods.HasFlag(HotkeyModifiers.Alt))       yield return "Alt";
        if (mods.HasFlag(HotkeyModifiers.Shift))     yield return "Shift";
        if (mods.HasFlag(HotkeyModifiers.Win))       yield return "Win";
        if (mods.HasFlag(HotkeyModifiers.NoRepeat))  yield return "NoRepeat";
    }
}
