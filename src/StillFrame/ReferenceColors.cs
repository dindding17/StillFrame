using System;

namespace StillFrame;

public static class ReferenceColors
{
    public static string NormalizeHex(string? value)
    {
        string hex = (value ?? "").Trim();
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6) return "";
        foreach (char digit in hex)
            if (!Uri.IsHexDigit(digit)) return "";
        return "#" + hex.ToUpperInvariant();
    }
    public static string Resolve(Preferences p, ReferenceSide side = ReferenceSide.Center)
    {
        string perSide = side switch { ReferenceSide.Top => p.TopColor, ReferenceSide.Bottom => p.BottomColor, ReferenceSide.Left => p.LeftColor, ReferenceSide.Right => p.RightColor, _ => "" };
        string custom = NormalizeHex(perSide);
        if (custom.Length != 0) return custom;
        custom = NormalizeHex(p.CustomColor);
        return custom.Length != 0 ? custom : p.DotColor switch { "暖白" => "#FAF8E9", "琥珀黄" => "#FFCD70", "雾蓝" => "#B5D7ED", _ => "#B1ECCE" };
    }
    public static bool Enabled(Preferences p, ReferenceSide side) => side switch
    { ReferenceSide.Top => p.TopEnabled, ReferenceSide.Bottom => p.BottomEnabled, ReferenceSide.Left => p.LeftEnabled, ReferenceSide.Right => p.RightEnabled, _ => true };
}
