﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OxyPlot
{
    public static class RenderingExtensions
    {
        /// <summary>
        /// Draws a line specified by coordinates.
        /// </summary>
        /// <param name="rc">The rc.</param>
        /// <param name="x0">The x0.</param>
        /// <param name="y0">The y0.</param>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="aliased">Aliased line if set to <c>true</c>.</param>
        public static void DrawLine(this IRenderContext rc, double x0, double y0, double x1, double y1, OxyPen pen, bool aliased = true)
        {
            if (pen == null)
            {
                return;
            }

            rc.DrawLine(new[]
                            {
                                new ScreenPoint(x0, y0), 
                                new ScreenPoint(x1, y1)
                            }, pen.Color, pen.Thickness, pen.DashArray, pen.LineJoin, aliased);
        }

        public static void DrawBox(this IRenderContext rc, OxyRect rect, OxyColor fill, OxyColor stroke, double thickness)
        {
            var sp0 = new ScreenPoint(rect.Left, rect.Top);
            var sp1 = new ScreenPoint(rect.Right, rect.Top);
            var sp2 = new ScreenPoint(rect.Right, rect.Bottom);
            var sp3 = new ScreenPoint(rect.Left, rect.Bottom);
            rc.DrawPolygon(new[] { sp0, sp1, sp2, sp3 }, fill, stroke, thickness, null, OxyPenLineJoin.Miter, true);
        }

        /// <summary>
        /// Draws the line segments.
        /// </summary>
        /// <param name="rc">The rc.</param>
        /// <param name="points">The points.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="aliased">if set to <c>true</c> [aliased].</param>
        public static void DrawLineSegments(this IRenderContext rc, IList<ScreenPoint> points, OxyPen pen, bool aliased = true)
        {
            if (pen == null)
            {
                return;
            }

            rc.DrawLineSegments(points, pen.Color, pen.Thickness, pen.DashArray, pen.LineJoin, aliased);
        }

        /// <summary>
        /// Draws a list of markers.
        /// </summary>
        /// <param name="rc">The render context.</param>
        /// <param name="markerPoints">The marker points.</param>
        /// <param name="clippingRect">The clipping rect.</param>
        /// <param name="markerType">Type of the marker.</param>
        /// <param name="markerOutline">The marker outline.</param>
        /// <param name="markerSize">Size of the marker.</param>
        /// <param name="markerFill">The marker fill.</param>
        /// <param name="markerStroke">The marker stroke.</param>
        /// <param name="markerStrokeThickness">The marker stroke thickness.</param>
        /// <param name="resolution">The resolution.</param>
        public static void DrawMarkers(this IRenderContext rc, ScreenPoint[] markerPoints, OxyRect clippingRect,
            MarkerType markerType, IList<ScreenPoint> markerOutline, double[] markerSize,
            OxyColor markerFill, OxyColor markerStroke, double markerStrokeThickness, int resolution = 0)
        {
            // todo: Markers should be rendered to a DrawingContext for performance.
            if (markerType == MarkerType.None)
                return;

            var clipping = new CohenSutherlandClipping(clippingRect);

            var ellipses = new List<OxyRect>(markerPoints.Length);
            var rects = new List<OxyRect>(markerPoints.Length);
            var polygons = new List<IEnumerable<ScreenPoint>>(markerPoints.Length);
            var lines = new List<ScreenPoint>(markerPoints.Length);

            var hashset = new HashSet<uint>();

            int i = 0;

            double minx = clippingRect.Left;
            double maxx = clippingRect.Right;
            double miny = clippingRect.Top;
            double maxy = clippingRect.Bottom;

            foreach (var p in markerPoints)
            {
                if (resolution > 1)
                {
                    int x = (int)(p.X / resolution);
                    int y = (int)(p.Y / resolution);
                    uint hash = (uint)(x << 16) + (uint)y;
                    if (hashset.Contains(hash))
                    {
                        i++;
                        continue;
                    }
                    hashset.Add(hash);
                }

                bool outside = p.x < minx || p.x > maxx ||
                               p.y < miny || p.y > maxy;
                if (!outside)
                {
                    int j = i < markerSize.Length ? i : 0;
                    AddMarkerGeometry(p, markerType, markerOutline, markerSize[j], ellipses, rects, polygons, lines);
                }
                i++;
            }

            if (ellipses.Count > 0)
                rc.DrawEllipses(ellipses, markerFill, markerStroke, markerStrokeThickness);
            if (rects.Count > 0)
                rc.DrawRectangles(rects, markerFill, markerStroke, markerStrokeThickness);
            if (polygons.Count > 0)
                rc.DrawPolygons(polygons, markerFill, markerStroke, markerStrokeThickness);
            if (lines.Count > 0)
                rc.DrawLineSegments(lines, markerStroke, markerStrokeThickness);


        }

        private static void AddMarkerGeometry(ScreenPoint p, MarkerType type, IEnumerable<ScreenPoint> outline, double size, IList<OxyRect> ellipses, IList<OxyRect> rects, IList<IEnumerable<ScreenPoint>> polygons, IList<ScreenPoint> lines)
        {
            if (type == MarkerType.Custom)
            {
                Debug.Assert(outline != null, "MarkerOutline should be set if MarkerType=Custom.");
                var poly = outline.Select(o => new ScreenPoint(p.X + o.x * size, p.Y + o.y * size)).ToList();
                polygons.Add(poly);
                return;
            }

            switch (type)
            {
                case MarkerType.Circle:
                    {
                        ellipses.Add(new OxyRect(p.x - size, p.y - size, size * 2, size * 2));
                        break;
                    }

                case MarkerType.Square:
                    {
                        rects.Add(new OxyRect(p.x - size, p.y - size, size * 2, size * 2));
                        break;
                    }

                case MarkerType.Diamond:
                    {
                        polygons.Add(new[]
                                      {
                                          new ScreenPoint(p.x, p.y - M2*size), 
                                          new ScreenPoint(p.x + M2*size, p.y), 
                                          new ScreenPoint(p.x, p.y + M2*size), 
                                          new ScreenPoint(p.x - M2*size, p.y)
                                      });
                        break;
                    }

                case MarkerType.Triangle:
                    {
                        polygons.Add(new[]
                                      {
                                          new ScreenPoint(p.x - size, p.y + M1*size), 
                                          new ScreenPoint(p.x + size, p.y + M1*size), 
                                          new ScreenPoint(p.x, p.y - M2*size)
                                      });
                        break;
                    }

                case MarkerType.Plus:
                case MarkerType.Star:
                    {
                        lines.Add(new ScreenPoint(p.x - size, p.y));
                        lines.Add(new ScreenPoint(p.x + size, p.y));
                        lines.Add(new ScreenPoint(p.x, p.y - size));
                        lines.Add(new ScreenPoint(p.x, p.y + size));
                        break;
                    }
            }

            switch (type)
            {
                case MarkerType.Cross:
                case MarkerType.Star:
                    {
                        lines.Add(new ScreenPoint(p.x - size * M3, p.y - size * M3));
                        lines.Add(new ScreenPoint(p.x + size * M3, p.y + size * M3));
                        lines.Add(new ScreenPoint(p.x - size * M3, p.y + size * M3));
                        lines.Add(new ScreenPoint(p.x + size * M3, p.y - size * M3));
                        break;
                    }
            }
        }

        private static readonly double M1 = Math.Tan(Math.PI / 6);
        private static readonly double M2 = Math.Sqrt(1 + M1 * M1);
        private static readonly double M3 = Math.Tan(Math.PI / 4);

        /// <summary>
        /// Renders the marker.
        /// </summary>
        /// <param name="rc">The render context.</param>
        /// <param name="type">The marker type.</param>
        /// <param name="p">The center point of the marker.</param>
        /// <param name="size">The size of the marker.</param>
        /// <param name="fill">The fill color.</param>
        /// <param name="stroke">The stroke color.</param>
        /// <param name="strokeThickness">The stroke thickness.</param>
        public static void DrawMarker(this IRenderContext rc, ScreenPoint p, OxyRect clippingRect, MarkerType type, IList<ScreenPoint> outline, double size,
                                    OxyColor fill, OxyColor stroke, double strokeThickness)
        {
            rc.DrawMarkers(new[] { p }, clippingRect, type, outline, new[] { size }, fill, stroke, strokeThickness);
        }

        /// <summary>
        /// Draws the clipped line.
        /// </summary>
        /// <param name="rc">The render context.</param>
        /// <param name="points">The points.</param>
        /// <param name="clippingRectangle">The clipping rectangle.</param>
        /// <param name="minDistSquared">The min dist squared.</param>
        /// <param name="stroke">The stroke.</param>
        /// <param name="strokeThickness">The stroke thickness.</param>
        /// <param name="lineStyle">The line style.</param>
        /// <param name="lineJoin">The line join.</param>
        /// <param name="aliased">if set to <c>true</c> [aliased].</param>
        public static void DrawClippedLine(this IRenderContext rc, ScreenPoint[] points,
           OxyRect clippingRectangle, double minDistSquared,
           OxyColor stroke, double strokeThickness, LineStyle lineStyle, OxyPenLineJoin lineJoin, bool aliased)
        {
            var clipping = new CohenSutherlandClipping(clippingRectangle.Left, clippingRectangle.Right, clippingRectangle.Top, clippingRectangle.Bottom);

            int n;
            var pts = new List<ScreenPoint>();
            n = points.Length;
            if (n > 0)
            {
                var s0 = points[0];
                var last = points[0];

                if (n == 1)
                    pts.Add(s0);

                for (int i = 1; i < n; i++)
                {
                    var s1 = points[i];

                    // Clipped version of this and next point.
                    var s0c = s0;
                    var s1c = s1;
                    bool isInside = clipping.ClipLine(ref s0c, ref s1c);
                    s0 = s1;

                    if (!isInside)
                    {
                        // keep the previous coordinate
                        continue;
                    }

                    // render from s0c-s1c
                    double dx = s1c.x - last.x;
                    double dy = s1c.y - last.y;

                    if (dx * dx + dy * dy > minDistSquared || i == 1)
                    {
                        if (!s0c.Equals(last) || i == 1)
                        {
                            pts.Add(s0c);
                        }

                        pts.Add(s1c);
                        last = s1c;
                    }

                    // render the line if we are leaving the clipping region););
                    if (!clipping.IsInside(s1))
                    {
                        if (pts.Count > 0)
                            rc.DrawLine(pts, stroke, strokeThickness, LineStyleHelper.GetDashArray(lineStyle), lineJoin, aliased);
                        pts.Clear();
                    }
                }

                // Check if the line contains two points and they are at the same point
                if (pts.Count == 2)
                {
                    if (pts[0].DistanceTo(pts[1]) < 1)
                    {
                        // Modify to a small horizontal line to make sure it is being rendered
                        pts[1] = new ScreenPoint(pts[0].X + 1, pts[0].Y);
                        pts[0] = new ScreenPoint(pts[0].X - 1, pts[0].Y);
                    }
                }

                // Check if the line contains a single point
                if (pts.Count == 1)
                {
                    // Add a second point to make sure the line is being rendered
                    pts.Add(new ScreenPoint(pts[0].X + 1, pts[0].Y));
                    pts[0] = new ScreenPoint(pts[0].X - 1, pts[0].Y);
                }

                if (pts.Count > 0)
                {
                    rc.DrawLine(pts, stroke, strokeThickness, LineStyleHelper.GetDashArray(lineStyle), lineJoin, aliased);
                }
            }
        }
    }
}