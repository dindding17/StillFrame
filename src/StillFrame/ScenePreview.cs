using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StillFrame;

public sealed class ScenePreview : Grid
{
    private readonly OverlayCanvas overlay = new();
    private Preferences preferences = new();
    private bool original;
    public ScenePreview()
    {
        ClipToBounds = true; IsHitTestVisible = false;
        Children.Add(new SceneDrawing()); Children.Add(overlay);
        SizeChanged += (_, _) => Paint();
    }
    public void Update(Preferences p) { preferences = Store.Snapshot(p); Paint(); }
    public void ShowOriginal(bool value) { original = value; Paint(); }
    private void Paint() { overlay.Refresh(); overlay.Update(original ? new Preferences() : preferences, 0, null); }
}

internal sealed class SceneDrawing : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; if (w < 1 || h < 1) return;
        static SolidColorBrush B(string hex) { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; brush.Freeze(); return brush; }
        var sky = new LinearGradientBrush(Color.FromRgb(83, 110, 137), Color.FromRgb(178, 187, 188), 90); sky.Freeze();
        dc.DrawRectangle(sky, null, new Rect(0, 0, w, h));
        dc.DrawEllipse(B("#CAD0C9"), null, new Point(w * .76, h * .22), h * .064, h * .064);
        void Polygon(string color, params (double x, double y)[] points)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open()) { ctx.BeginFigure(new Point(points[0].x * w, points[0].y * h), true, true); for (int i = 1; i < points.Length; i++) ctx.LineTo(new Point(points[i].x * w, points[i].y * h), true, false); }
            geometry.Freeze(); dc.DrawGeometry(B(color), null, geometry);
        }
        Polygon("#6C8390", (0,.58),(.16,.26),(.29,.45),(.43,.22),(.64,.49),(.83,.31),(1,.57),(1,1),(0,1));
        Polygon("#536C7A", (0,.64),(.1,.49),(.21,.57),(.34,.43),(.46,.64),(.68,.46),(.84,.55),(1,.43),(1,1),(0,1));
        Polygon("#46545F", (0,.70),(1,.70),(1,1),(0,1));
        Polygon("#77838A", (.45,.67),(.55,.67),(.83,1),(.15,1));
        var seams = new Pen(B("#637078"), .8);
        for (int i = 1; i < 5; i++) { double y = .72 + i * i * .011; dc.DrawLine(seams, new Point(w * (.46 - i * .052), h * y), new Point(w * (.54 + i * .05), h * y)); }
        // A static illustrated scene provides bright and dark areas without capturing a game.
        Polygon("#34444F", (0,.21),(.14,.21),(.14,1),(0,1));
        Polygon("#4B5D66", (.025,.22),(.055,.22),(.055,1),(.025,1));
        Polygon("#293A47", (0,.19),(.16,.19),(.18,.24),(0,.24));
        Polygon("#344A55", (.83,.57),(.94,.49),(1,.60),(1,1),(.82,1));
        Polygon("#49616B", (.84,.57),(.94,.51),(.92,.71),(.83,.76));
        dc.DrawEllipse(B("#3B4850"), null, new Point(w * .5, h * .87), w * .055, h * .018);
        var character = B("#263744");
        dc.DrawEllipse(character, null, new Point(w * .5, h * .6), h * .023, h * .026);
        Polygon("#293B48", (.48,.63),(.52,.63),(.533,.77),(.503,.82),(.468,.77));
        var limb = new Pen(character, Math.Max(2, h * .018)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(limb, new Point(w * .49, h * .76), new Point(w * .483, h * .86));
        dc.DrawLine(limb, new Point(w * .511, h * .76), new Point(w * .52, h * .86));
        dc.DrawLine(new Pen(B("#A9B7BA"), Math.Max(.7, h * .004)), new Point(w * .478, h * .71), new Point(w * .545, h * .56));
    }
}
