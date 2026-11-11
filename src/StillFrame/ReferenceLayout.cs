using System;
using System.Collections.Generic;

namespace StillFrame;

public enum ReferenceSide { Center, Top, Bottom, Left, Right }
public readonly record struct ReferenceSegment(double X1, double Y1, double X2, double Y2, ReferenceSide Side = ReferenceSide.Center);
public readonly record struct ReferenceCircle(double X, double Y, double Radius);
public readonly record struct ReferenceMarker(double X, double Y, double Size, ReferenceSide Side, string Shape);

public static class ReferenceLayout
{
    public static bool HasVisuals(Preferences p) => p.Corners || p.SideGuides || p.Thirds || p.Ruler || p.OpenHorizon || p.SplitCross || p.Ring || p.Dot || p.Frame || p.Mask || p.DirectionLines || p.Triangles || p.Chevrons || p.Diamonds;
    public static void SelectPreset(Preferences p, string name)
    {
        p.Mask = p.Dynamic = p.Dot = p.Frame = p.Corners = p.SideGuides = p.Thirds = p.Ruler = p.OpenHorizon = p.SplitCross = p.Ring = p.DirectionLines = p.Triangles = p.Chevrons = p.Diamonds = false;
        switch (name)
        {
            case "边缘角标": p.Corners = true; break;
            case "侧边短线": p.SideGuides = true; break;
            case "三分刻度": p.Thirds = true; break;
            case "边缘标尺": p.Ruler = true; break;
            case "留空水平线": p.OpenHorizon = true; break;
            case "中空十字": p.SplitCross = true; break;
            case "中心空心环": p.Ring = true; break;
            case "四向短线": p.DirectionLines = true; break;
            case "四向三角": p.Triangles = true; break;
            case "方向折线": p.Chevrons = true; break;
            case "菱形锚点": p.Diamonds = true; break;
            case "中心点与细框": p.Dot = p.Frame = true; break;
            default: name = "仅计时"; break;
        }
        p.Profile = name; p.SettingsVersion = 2;
    }
    public static List<ReferenceSegment> Create(Preferences p, double width, double height)
    {
        var lines = new List<ReferenceSegment>();
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1) return lines;
        double inset = Math.Clamp(double.IsFinite(p.LineInset) ? p.LineInset : .08, .03, .18);
        double l = width * inset, r = width - l, t = height * inset, b = height - t;
        void Add(double x1, double y1, double x2, double y2, ReferenceSide? explicitSide = null)
        {
            double x = (x1 + x2) / 2, y = (y1 + y2) / 2;
            double closest = Math.Min(Math.Min(y / height, 1 - y / height), Math.Min(x / width, 1 - x / width));
            var side = explicitSide ?? (closest > .3 ? ReferenceSide.Center : Math.Abs(y / height - closest) < .0001 ? ReferenceSide.Top : Math.Abs(1 - y / height - closest) < .0001 ? ReferenceSide.Bottom : x < width / 2 ? ReferenceSide.Left : ReferenceSide.Right);
            if (ReferenceColors.Enabled(p, side)) lines.Add(new(x1, y1, x2, y2, side));
        }
        if (p.Corners)
        {
            double length = Math.Min(56, Math.Min(width, height) * .1);
            Add(l, t + length, l, t); Add(l, t, l + length, t);
            Add(r - length, t, r, t); Add(r, t, r, t + length);
            Add(r, b - length, r, b); Add(r, b, r - length, b);
            Add(l + length, b, l, b); Add(l, b, l, b - length);
        }
        if (p.SideGuides)
        {
            double length = Math.Min(88, width * .075), y = height / 2, tick = Math.Min(6, height * .02);
            Add(l, y, l + length, y); Add(l, y - tick, l, y + tick);
            Add(r - length, y, r, y); Add(r, y - tick, r, y + tick);
        }
        if (p.Thirds)
        {
            double tick = Math.Min(24, Math.Min(width, height) * .05);
            for (int i = 1; i <= 2; i++)
            {
                double x = width * i / 3, y = height * i / 3;
                Add(x, t, x, t + tick); Add(x, b - tick, x, b);
                Add(l, y, l + tick, y); Add(r - tick, y, r, y);
            }
        }
        if (p.Ruler)
        {
            double tick = Math.Min(16, Math.Min(width, height) * .025);
            for (int i = 1; i < 10; i++)
            {
                double x = l + (r - l) * i / 10, length = i == 5 ? tick : tick * .55;
                Add(x, t, x, t + length); Add(x, b - length, x, b);
            }
            for (int i = 1; i < 6; i++)
            {
                double y = t + (b - t) * i / 6, length = i == 3 ? tick : tick * .55;
                Add(l, y, l + length, y); Add(r - length, y, r, y);
            }
        }
        if (p.OpenHorizon)
        { Add(l, height / 2, width * .3, height / 2); Add(width * .7, height / 2, r, height / 2); }
        if (p.SplitCross)
        {
            double gap = Math.Min(9, Math.Min(width, height) * .02), arm = Math.Min(24, Math.Min(width, height) * .06);
            double x = width / 2, y = height / 2;
            Add(x - arm, y, x - gap, y); Add(x + gap, y, x + arm, y);
            Add(x, y - arm, x, y - gap); Add(x, y + gap, x, y + arm);
        }
        if (p.DirectionLines)
        {
            double length = Math.Min((double.IsFinite(p.MarkerSize) ? Math.Clamp(p.MarkerSize,8,48) : 18) * 1.8, Math.Min(width,height) * .16);
            Add(width / 2, t, width / 2, t + length, ReferenceSide.Top);
            Add(width / 2, b - length, width / 2, b, ReferenceSide.Bottom);
            Add(l, height / 2, l + length, height / 2, ReferenceSide.Left);
            Add(r - length, height / 2, r, height / 2, ReferenceSide.Right);
        }
        if (p.Frame)
        { Add(l, t, r, t); Add(r, t, r, b); Add(r, b, l, b); Add(l, b, l, t); }
        return lines;
    }
    public static List<ReferenceMarker> CreateMarkers(Preferences p, double width, double height)
    {
        var markers = new List<ReferenceMarker>();
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1) return markers;
        double inset = Math.Clamp(double.IsFinite(p.LineInset) ? p.LineInset : .08,.03,.18);
        double size = Math.Min(double.IsFinite(p.MarkerSize) ? Math.Clamp(p.MarkerSize,8,48) : 18, Math.Min(width,height) * Math.Min(.12,inset*1.6));
        foreach (var side in new[] { ReferenceSide.Top, ReferenceSide.Bottom, ReferenceSide.Left, ReferenceSide.Right })
        {
            if (!ReferenceColors.Enabled(p,side)) continue;
            double x = side == ReferenceSide.Left ? width * inset : side == ReferenceSide.Right ? width * (1-inset) : width / 2;
            double y = side == ReferenceSide.Top ? height * inset : side == ReferenceSide.Bottom ? height * (1-inset) : height / 2;
            if (p.Triangles) markers.Add(new(x,y,size,side,"Triangle"));
            if (p.Chevrons) markers.Add(new(x,y,size,side,"Chevron"));
            if (p.Diamonds) markers.Add(new(x,y,size,side,"Diamond"));
        }
        return markers;
    }
    public static List<ReferenceCircle> CreateCircles(Preferences p, double width, double height)
    {
        var circles = new List<ReferenceCircle>();
        if (p.Ring && double.IsFinite(width) && double.IsFinite(height) && width >= 1 && height >= 1)
        {
            double radius = Math.Clamp(double.IsFinite(p.RingRadius) ? p.RingRadius : 24, 8, 64);
            circles.Add(new(width / 2, height / 2, Math.Min(radius, Math.Min(width, height) * .4)));
        }
        return circles;
    }
}
