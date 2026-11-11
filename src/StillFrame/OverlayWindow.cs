using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StillFrame;

internal sealed class OverlayWindow : Window
{
    private readonly OverlayCanvas canvas = new();
    internal OverlayWindow()
    {
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        IsHitTestVisible = false; Content = canvas; Width = 800; Height = 500;
        SourceInitialized += (_, _) => Native.MakeOverlay(new WindowInteropHelper(this).Handle);
    }
    internal void Update(Preferences p, double motion, Native.Rect bounds, string? toast)
    {
        if (!IsVisible) Show();
        Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x10);
        canvas.Update(p, motion, toast);
    }
    internal void RefreshSettings() => canvas.Refresh();
}

internal sealed class OverlayCanvas : FrameworkElement
{
    private Preferences settings = new();
    private double motion;
    private string? toast;
    private bool dirty = true;
    private LinearGradientBrush? left, right, top, bottom;
    internal void Update(Preferences p, double amount, string? message)
    {
        if (dirty || Math.Abs(amount - motion) > .003 || message != toast)
        { settings = Store.Snapshot(p); motion = amount; toast = message; dirty = false; BuildBrushes(); InvalidateVisual(); }
    }
    internal void Refresh() { dirty = true; }
    private void BuildBrushes()
    {
        byte alpha = (byte)(255 * Math.Clamp(settings.Darkness + motion * settings.DynamicGain, 0, .94));
        LinearGradientBrush Make(double sx, double sy, double ex, double ey)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(sx, sy), EndPoint = new Point(ex, ey) };
            b.GradientStops.Add(new(Color.FromArgb(alpha, 7, 15, 17), 0));
            b.GradientStops.Add(new(Color.FromArgb((byte)(alpha * .65), 7, 15, 17), .38));
            b.GradientStops.Add(new(Color.FromArgb(0, 7, 15, 17), 1)); b.Freeze(); return b;
        }
        left = Make(0, .5, 1, .5); right = Make(1, .5, 0, .5); top = Make(.5, 0, .5, 1); bottom = Make(.5, 1, .5, 0);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w < 1 || h < 1) return;
        if (settings.Mask)
        {
            double edge = Math.Clamp(settings.EdgeWidth + motion * .055, .08, .39);
            dc.DrawRectangle(left, null, new Rect(0, 0, w * edge, h));
            dc.DrawRectangle(right, null, new Rect(w * (1 - edge), 0, w * edge, h));
            dc.DrawRectangle(top, null, new Rect(0, 0, w, h * edge));
            dc.DrawRectangle(bottom, null, new Rect(0, h * (1 - edge), w, h * edge));
        }
        DashStyle pattern = settings.LinePattern switch { "虚线" => DashStyles.Dash, "点线" => DashStyles.Dot, _ => DashStyles.Solid };
        var cache = new Dictionary<ReferenceSide,(SolidColorBrush Brush,Pen Pen,Pen Outline)>();
        (SolidColorBrush Brush,Pen Pen,Pen Outline) Paint(ReferenceSide side)
        {
            if (cache.TryGetValue(side,out var cached)) return cached;
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ReferenceColors.Resolve(settings,side))) { Opacity = settings.ReferenceOpacity }; brush.Freeze();
            var stroke = new Pen(brush,settings.LineWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, DashStyle = pattern, LineJoin = PenLineJoin.Round }; stroke.Freeze();
            var shadow = new Pen(new SolidColorBrush(Color.FromArgb((byte)(105*settings.ReferenceOpacity),4,9,18)),settings.LineWidth+1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, DashStyle = pattern, LineJoin = PenLineJoin.Round }; shadow.Freeze();
            var result = (brush,stroke,shadow); cache[side] = result; return result;
        }
        var centerPaint = Paint(ReferenceSide.Center);
        var reference = centerPaint.Brush; var pen = centerPaint.Pen; var outline = centerPaint.Outline;
        foreach (var line in ReferenceLayout.Create(settings, w, h))
        {
            var a = new Point(line.X1, line.Y1); var b = new Point(line.X2, line.Y2);
            var paint = Paint(line.Side);
            dc.DrawLine(paint.Outline, a, b); dc.DrawLine(paint.Pen, a, b);
        }
        foreach (var marker in ReferenceLayout.CreateMarkers(settings,w,h))
        {
            var paint = Paint(marker.Side); var geometry = ReferenceDrawing.MarkerGeometry(marker);
            dc.DrawGeometry(null,paint.Outline,geometry);
            dc.DrawGeometry(settings.FilledMarkers && marker.Shape != "Chevron" ? paint.Brush : null,paint.Pen,geometry);
        }
        foreach (var ring in ReferenceLayout.CreateCircles(settings, w, h))
        {
            var center = new Point(ring.X, ring.Y);
            dc.DrawEllipse(null, outline, center, ring.Radius, ring.Radius); dc.DrawEllipse(null, pen, center, ring.Radius, ring.Radius);
        }
        if (settings.Dot)
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)), null, new Point(w / 2, h / 2), settings.DotSize / 2 + 1, settings.DotSize / 2 + 1);
            dc.DrawEllipse(reference, null, new Point(w / 2, h / 2), settings.DotSize / 2, settings.DotSize / 2);
        }
        if (toast != null)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var text = new FormattedText(toast, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 16, Brushes.White, dpi.PixelsPerDip) { MaxTextWidth = Math.Max(120, Math.Min(520, w - 80)) };
            double bw = text.Width + 40, bh = text.Height + 28;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(246, 26, 39, 61)), new Pen(new SolidColorBrush(Color.FromRgb(167, 200, 255)), 1), new Rect((w - bw) / 2, h * .12, bw, bh), 12, 12);
            dc.DrawText(text, new Point((w - bw) / 2 + 20, h * .12 + 14));
        }
    }
}
