using CrossWindow.Core.Enums;

namespace CrossWindow.Core.Models;

[Flags]
public enum HotkeyModifiers : uint
{
    None     = 0,
    Alt      = 0x0001,
    Control  = 0x0002,
    Shift    = 0x0004,
    Win      = 0x0008,
    NoRepeat = 0x4000
}

public enum HotkeyAction { Move, Swap, Snap, SwapModeToggle }

public sealed class HotkeyBinding
{
    public int             Id         { get; init; }
    public HotkeyModifiers Modifiers  { get; init; }
    public uint            VirtualKey { get; init; }
    public HotkeyAction    Action     { get; init; }
    public Direction       Direction  { get; init; }
    public SnapZone?       Zone       { get; init; }

    public string Label => $"{Modifiers}+{VkName(VirtualKey)}";

    private static string VkName(uint vk) => vk switch
    {
        0x61 => "NP1", 0x62 => "NP2", 0x63 => "NP3",
        0x64 => "NP4", 0x65 => "NP5", 0x66 => "NP6",
        0x67 => "NP7", 0x68 => "NP8", 0x69 => "NP9",
        0x60 => "NP0",
        _ when vk >= 0x41 && vk <= 0x5A => ((char)vk).ToString(),
        _ when vk >= 0x25 && vk <= 0x28 => vk switch
        {
            0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down", _ => $"0x{vk:X2}"
        },
        _ => $"0x{vk:X2}"
    };
}
