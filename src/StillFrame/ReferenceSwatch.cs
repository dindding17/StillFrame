using System;
using System.Windows;
using System.Windows.Media;

namespace StillFrame;

public sealed class ReferenceSwatch : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(ReferenceSwatch), new FrameworkPropertyMetadata("仅计时", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; if (w < 1 || h < 1) return;
        var p = new Preferences { LineInset = .12, RingRadius = 9 };
        ReferenceLayout.SelectPreset(p, Kind);
        var ink = new SolidColorBrush(Color.FromRgb(40, 111, 135)); ink.Freeze();
        var muted = new Pen(new SolidColorBrush(Color.FromRgb(198, 215, 225)), .8);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(236, 245, 249)), muted, new Rect(0, 0, w, h), 5, 5);
        var pen = new Pen(ink, 1.1);
        foreach (var s in ReferenceLayout.Create(p, w, h)) dc.DrawLine(pen, new Point(s.X1,s.Y1), new Point(s.X2,s.Y2));
        foreach (var c in ReferenceLayout.CreateCircles(p,w,h)) dc.DrawEllipse(null, pen, new Point(c.X,c.Y), c.Radius,c.Radius);
        foreach (var m in ReferenceLayout.CreateMarkers(p,w,h)) dc.DrawGeometry(null,pen,ReferenceDrawing.MarkerGeometry(m));
        if (p.Dot) dc.DrawEllipse(ink, null, new Point(w/2,h/2), 1.5,1.5);
        if (Kind == "仅计时")
        {
            double r = h * .26; var center = new Point(w/2,h/2);
            dc.DrawEllipse(null, pen, center, r,r); dc.DrawLine(pen,center,new Point(w/2,h/2-r*.65)); dc.DrawLine(pen,center,new Point(w/2+r*.55,h/2));
        }
    }
}
