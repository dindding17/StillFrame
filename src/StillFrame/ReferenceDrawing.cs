using System;
using System.Windows;
using System.Windows.Media;

namespace StillFrame;

internal static class ReferenceDrawing
{
    internal static StreamGeometry MarkerGeometry(ReferenceMarker marker)
    {
        double s = marker.Size / 2;
        Point[] local = marker.Shape switch
        {
            "Diamond" => new[] { new Point(0,-s), new Point(s,0), new Point(0,s), new Point(-s,0) },
            "Chevron" => new[] { new Point(-s,-s*.45), new Point(0,s*.55), new Point(s,-s*.45) },
            _ => new[] { new Point(-s,-s*.65), new Point(s,-s*.65), new Point(0,s*.85) }
        };
        double angle = marker.Side switch { ReferenceSide.Bottom => Math.PI, ReferenceSide.Left => -Math.PI / 2, ReferenceSide.Right => Math.PI / 2, _ => 0 };
        Point Transform(Point a) => new(marker.X + a.X * Math.Cos(angle) - a.Y * Math.Sin(angle), marker.Y + a.X * Math.Sin(angle) + a.Y * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(Transform(local[0]), marker.Shape != "Chevron", marker.Shape != "Chevron");
            for (int i=1;i<local.Length;i++) context.LineTo(Transform(local[i]),true,false);
        }
        geometry.Freeze(); return geometry;
    }
}
