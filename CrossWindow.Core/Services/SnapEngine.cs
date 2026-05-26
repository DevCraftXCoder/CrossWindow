using CrossWindow.Core.Enums;
using CrossWindow.Core.Models;

namespace CrossWindow.Core.Services;

public static class SnapEngine
{
    /// <summary>
    /// Computes the target CwRect for a snap zone within the given work area.
    /// All arithmetic uses remainder division so adjacent zones share edges exactly.
    /// </summary>
    public static CwRect ComputeSnapBounds(SnapZone zone, CwRect w)
    {
        int hw = w.Width  / 2;
        int hh = w.Height / 2;

        return zone switch
        {
            SnapZone.LeftHalf    => new CwRect(w.X,          w.Y,          hw,             w.Height),
            SnapZone.RightHalf   => new CwRect(w.X + hw,     w.Y,          w.Width  - hw,  w.Height),
            SnapZone.TopHalf     => new CwRect(w.X,          w.Y,          w.Width,        hh),
            SnapZone.BottomHalf  => new CwRect(w.X,          w.Y + hh,     w.Width,        w.Height - hh),
            SnapZone.TopLeft     => new CwRect(w.X,          w.Y,          hw,             hh),
            SnapZone.TopRight    => new CwRect(w.X + hw,     w.Y,          w.Width  - hw,  hh),
            SnapZone.BottomLeft  => new CwRect(w.X,          w.Y + hh,     hw,             w.Height - hh),
            SnapZone.BottomRight => new CwRect(w.X + hw,     w.Y + hh,     w.Width  - hw,  w.Height - hh),
            SnapZone.Maximize    => w,
            SnapZone.Center      => CenterRect(w, 0.70),
            _                    => w
        };
    }

    private static CwRect CenterRect(CwRect w, double fraction)
    {
        int sw = (int)(w.Width  * fraction);
        int sh = (int)(w.Height * fraction);
        int sx = w.X + (w.Width  - sw) / 2;
        int sy = w.Y + (w.Height - sh) / 2;
        return new CwRect(sx, sy, sw, sh);
    }
}
