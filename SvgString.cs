using Godot;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using System;
using System.Collections;
using System.Collections.Generic;
using Vectordrawing;
using System.Numerics;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Data;
using System.Diagnostics.Metrics;
using SkiaSharp;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace Vectordrawing;

public enum Style  {
    ShapePositive,
    ShapeOpen,
    ShapePreview,
    ShapePreviewWhite,
    ShapeUnchanged,
    ShapeNegative,
    ShapeSelected,
    ShapeShadow,
    ShapeUnderlay,
    ShapeOverShadow,
    ShapeShadowOutside,
    ShapeUnchangedSelected,
    ShapeNegativeSelected,
}

static class Styles
{
    public enum Width
    {
        thin,
        medium,
        thick,
    }
    public static float zoom = 1;

    public static string GetWidth(Width width)
    {
        string[] widths = ["1", "3", "5", "30"];
        if (zoom > 1)
            widths = ["0.5", "1", "2", "8"];
        else if (zoom < 1)
            widths = ["3", "5", "8", "32"];

        return widths[(int)width];
    }

    public static Dictionary<string, string>[] S =
    [
        new()
        {
        // shape positive
            ["fill"] = "gray",
            ["stroke"] = "black",
            ["fill-opacity"] = "0.0",
            ["stroke-opacity"] = "1.0",
            ["stroke-width"] = "3",
        },
        new()
        {
        // shape open
            ["fill"] = "red",
            ["stroke"] = "black",
            ["fill-opacity"] = "0.0",
            ["stroke-opacity"] = "1",
            ["stroke-width"] = "1",
        },
        new()
        {
        // shape preview
            ["fill"] = "black",
            ["stroke"] = "black",
            ["fill-opacity"] = "1",
            ["stroke-opacity"] = "0",
            ["stroke-width"] = "0",
        },
        new()
        {
        // shape preview
            ["fill"] = "lightgray",
            ["stroke"] = "lightgray",
            ["fill-opacity"] = "1",
            ["stroke-opacity"] = "0",
            ["stroke-width"] = "0",
        },
        new()
        {
        // shape unchanged
            ["fill"] = "gray",
            ["stroke"] = "#636363",
            ["fill-opacity"] = "0.0",
            ["stroke-opacity"] = "0.6",
            ["stroke-width"] = "2",
        },
        new()
        {
        // shape negative
            // ["fill"] = "#d69c85",
            // ["fill"] = "#e08a85",
            ["fill"] = "#CCCCCC",
            ["stroke"] = "#3363ff",
            ["fill-opacity"] = "0.6",
            ["stroke-opacity"] = "0.8",
            ["stroke-width"] = "2",
        },
        new()
        {
        // shape selected 
            ["fill"] = "blue",
            ["stroke"] = "blue",
            ["fill-opacity"] = "0.15",
            ["stroke-opacity"] = "0.0",
            ["stroke-width"] = "3",
        },
        new()
        {
        // shape shadow 
            ["fill"] = "blue",
            ["stroke"] = "#CCCCCC",
            // ["stroke"] = "#bbbbbb",
            ["fill-opacity"] = "0.00",
            ["stroke-opacity"] = "1.0",
            ["stroke-width"] = "4",

        },
        new()
        {
        // shape underlay 
            ["fill"] = "#bbbbbb",
            ["stroke"] = "#CCCCCC",
            ["fill-opacity"] = "1.00",
            ["stroke-opacity"] = "0.0",
            ["stroke-width"] = "0",

        },
        new()
        {
        // shape overshadow 
            ["fill"] = "#ff3333",
            ["stroke"] = "black",
            ["fill-opacity"] = "0.20",
            ["stroke-opacity"] = "1.00",
            ["stroke-width"] = "2",

        },
        new()
        {
        // shape overshadow outside
            ["fill"] = "#ff3333",
            ["stroke"] = "#CCCCCC",
            ["fill-opacity"] = "0.00",
            ["stroke-opacity"] = "1.00",
            ["stroke-width"] = "3",

        },
        new()
        {
        // shape unchanged selected
            ["fill"] = "#ff8800",
            ["stroke"] = "#636363",
            ["fill-opacity"] = "0.2",
            ["stroke-opacity"] = "0.0",
            ["stroke-width"] = "2",
        },
        new()
        {
        // shape negative selected
            // ["fill"] = "#d69c85",
            // ["fill"] = "#e08a85",
            ["fill"] = "#808af5",
            // ["fill"] = "#CCCCCC",
            ["stroke"] = "#3363ff",
            ["fill-opacity"] = "0.0",
            ["stroke-opacity"] = "1.0",
            ["stroke-width"] = "2",
        },
    ];
}

public class SvgString
{
    // public static string CurrentString = "";
    static Dictionary<string, string> CurrentStyle = Styles.S[0];
    public static readonly StringBuilder CurrentString = new();
    UIConfig Config = UIConfig.Default;
    Editor Ed = null!;

    public SvgString(Editor ed)
    {
        Ed = ed;
    }

    public static void SetStyle(Style s)
    {
        CurrentStyle = Styles.S[(int)s];
    }

    static void StyleCurrentString()
    {
        foreach ((string key, string val) in CurrentStyle)
        {
            if (key == "stroke-width" && val != "0")
            {
                CurrentString.Append(key)
                             .Append( "=\"")
                             .Append(Styles.GetWidth((Styles.Width)(int.Parse(val)-1)))
                             .Append("\" ");
            }
            else
            {
                CurrentString.Append(key)
                             .Append("=\"")
                             .Append(val)
                             .Append("\" ");
            }
        }
    }

    public static void AddSKPath(SKPath path)
    {
        CurrentString.Append("<path d=\" ")
                     .Append(path.ToSvgPathData())
                     .Append("Z\" ");
        StyleCurrentString();
        CurrentString.Append("/>");
    }

    public static void AddSegmentsDebug(Segment[] s, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        string color1 = "red";
        string color2 = "white";
        if (Shapes.IsSegmentListClockwise(s))
        {
            color1 = "blue";
            color2 = "yellow";
        }
        float[] startSeg = s[0].Flat();
        int counter = 0;
        Span<float> flatSeg = stackalloc float[8];
        foreach (Segment seg in s)
        {
            string color = color1;
            if (counter % 2 == 0)
            {
                color = color2;
            }
            seg.Flat(flatSeg);
            AddCircle(new(flatSeg[2], flatSeg[3]), 2, color, color, fOpacity = 0.2f, sWidth = 0.5f);
            AddCircle(new(flatSeg[4], flatSeg[5]), 2, color, color, fOpacity = 0.2f, sWidth = 0.5f);
            CurrentString.Append("<path d=\"M ")
                         .Append(flatSeg[0]).Append(' ')
                         .Append(flatSeg[1]).Append(' ')
                         .Append(flatSeg[2]).Append(' ')
                         .Append(flatSeg[3]).Append(' ')
                         .Append(flatSeg[4]).Append(' ')
                         .Append(flatSeg[5]).Append(' ')
                         .Append(flatSeg[6]).Append(' ')
                         .Append(flatSeg[7]).Append(' ')
                         .Append("\" stroke=\"{color}\" fill-opacity=\"0.0\" stroke-width=\"2\"/>");
            counter += 1;
        }
    }

    public static void AddSegments(Segment[] s, bool debugInfo = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        Span<float> f = stackalloc float[8];  
        s[0].Flat(f);
        CurrentString.Append("<path d=\"M ")
                     .Append(f[0]).Append(' ')
                     .Append(f[1]).Append(' ')
                     .Append("C ");
        // CurrentString += $"<path d=\"M {start[0]} {start[1]} C ";
        // int counter = 0;
        foreach (Segment seg in s)
        {
            seg.Flat(f);
            CurrentString.Append(f[2]).Append(' ')
                         .Append(f[3]).Append(' ')
                         .Append(f[4]).Append(' ')
                         .Append(f[5]).Append(' ')
                         .Append(f[6]).Append(' ')
                         .Append(f[7]).Append(' ');
            // CurrentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
            // if (counter != s.Length - 1)
            // {
            //     CurrentString.Append("C ");
            // }
            // CurrentString.Append(' ');
            // CurrentString += " ";
            // counter += 1;
        }
        // CurrentString += $"Z\" ";
        CurrentString.Append("Z\" ");
        if (debugInfo)
        {
        //     if (Shapes.IsSegmentListClockwise(s))
        //     {
        //         CurrentString += " fill =\"red\" stroke =\"red\" fill-opacity=\"1.2\" stroke-opacity=\"0.0\" stroke-width=\"2\"/>";
        //     }
        //     else
        //     {
        //         CurrentString += " fill =\"blue\" stroke =\"blue\" fill-opacity=\"1.2\" stroke-opacity=\"0.0\" stroke-width=\"2\"/>";
        //     }
        //     // CurrentString += " fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
        }
        else
        {
            StyleCurrentString();
        }
        CurrentString.Append("/>");
    }

    public static void AddSegmentsShadow(Segment[] s, int shadow, bool debugInfo = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        // GD.Print(s[0]);
        float[] startSeg = s[0].Flat();
        CurrentString.Append("<path d=\"M ")
                     .Append(startSeg[0]).Append(' ')
                     .Append(startSeg[1]).Append(" C ");
        int counter = 0;
        Span<float> f = stackalloc float[8];
        foreach (Segment seg in s)
        {
            seg.Flat(f);
            CurrentString.Append(f[2]).Append(' ')
                         .Append(f[3]).Append(' ')
                         .Append(f[4]).Append(' ')
                         .Append(f[5]).Append(' ')
                         .Append(f[6]).Append(' ')
                         .Append(f[7]);
            // CurrentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
            if (counter != s.Length - 1)
            {
                CurrentString.Append("C ");
            }
            CurrentString.Append(' ');
            counter += 1;
        }
        CurrentString.Append("Z\" ");
        if (debugInfo)
        {
            if (Shapes.IsSegmentListClockwise(s))
            {
                CurrentString.Append(" fill =\"red\" stroke =\"red\" fill-opacity=\"1.2\" stroke-opacity=\"0.0\" stroke-width=\"2\"/>");
            }
            else
            {
                CurrentString.Append(" fill =\"blue\" stroke =\"blue\" fill-opacity=\"1.2\" stroke-opacity=\"0.0\" stroke-width=\"2\"/>");
            }
            // CurrentString += " fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
        }
        else
        {
            StyleCurrentString();
        }
        CurrentString.Append("/>");
    }

    public static void AddSegmentsGroup_Debug(Segment[][] sGroup, bool currentShape = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        Span<float> f = stackalloc float[8];
        foreach (Segment[] s in sGroup)
        {
            CurrentString.Append("<path d=\"");
            s[0].Flat(f);
            CurrentString.Append(" M ")
                         .Append(f[0])
                         .Append(f[1]).Append(" C ");
            int counter = 0;
            foreach (Segment seg in s)
            {
                seg.Flat(f);
                CurrentString.Append(f[2]).Append(' ')
                            .Append(f[3]).Append(' ')
                            .Append(f[4]).Append(' ')
                            .Append(f[5]).Append(' ')
                            .Append(f[6]).Append(' ')
                            .Append(f[7]);
                // float[] flatSeg = seg.Flat();
                // CurrentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
                if (counter != s.Length - 1)
                {
                    CurrentString.Append(",");
                }
                CurrentString.Append(" ");
                counter += 1;
            }
            CurrentString.Append("Z ");
            CurrentString.Append("\"");
            if (Shapes.IsSegmentListClockwise(s))
            {
                CurrentString.Append("fill =\"red\" stroke =\"red\" fill-opacity=\"0.0\" stroke-opacity=\"1.0\" stroke-width=\"4\"");
            }
            else
            {
                CurrentString.Append("fill =\"blue\" stroke =\"blue\" fill-opacity=\"0.0\" stroke-opacity=\"1.0\" stroke-width=\"4\"");
            }
            // Style(currentShape);
            CurrentString.Append("/>");
        }
        //   fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
    }

    public static void AddSegmentsGroup(Segment[][] sGroup, bool currentShape = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        CurrentString.Append("<path d=\"");
        Span<float> f = stackalloc float[8]; 
        foreach (Segment[] s in sGroup)
        {
            s[0].Flat(f);
            // float[] startSeg = s[0].Flat();
            CurrentString.Append(" M ")
                         .Append(f[0]).Append(' ')
                         .Append(f[1]).Append(" C ");
            int counter = 0;
            foreach (Segment seg in s)
            {
                seg.Flat(f);
                CurrentString.Append(f[2]).Append(' ')
                            .Append(f[3]).Append(' ')
                            .Append(f[4]).Append(' ')
                            .Append(f[5]).Append(' ')
                            .Append(f[6]).Append(' ')
                            .Append(f[7]);
                // CurrentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
                if (counter != s.Length - 1)
                {
                    CurrentString.Append(",");
                }
                CurrentString.Append(' ');
                counter += 1;
            }
            CurrentString.Append("Z ");
        }
        //   fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
        CurrentString.Append("\"");
        StyleCurrentString();
        CurrentString.Append("/>");
    }

    public static void AddCircle(V2 position, float radius, string fill = "black", string stroke = "black", float fOpacity = 1.0f, float sOpacity = 1.0f, float sWidth = 10f)
    { 
        CurrentString.Append($"<circle cx=\"{position.X}\" cy=\"{position.Y}\" r=\"{radius}\" ")
                     .Append($"fill=\"{fill}\" stroke=\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>");
    }

    public static void AddLine(V2 start, V2 end, string stroke = "red", float sWidth = 1f, float sOpacity = 1f)
    {
        CurrentString.Append($"<path d=\"M {start.X} {start.Y} L {end.X} {end.Y}\"")
                     .Append($"stroke =\"{stroke}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>");
    }
    
    public static void AddDashed(V2 start, V2 end, string dash, string stroke = "black", float sWidth = 1f, float sOpacity = 1f)
    {
        CurrentString.Append($"<line x1=\"{start.X}\" y1=\"{start.Y}\"  x2=\"{end.X}\" y2=\"{end.Y}\"")
                     .Append($"stroke =\"{stroke}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\" ")
                     .Append("stroke-dasharray=\"" + dash + "\" stroke-linecap=\"round\"")
                     .Append("/>");
    }

    public static void ClearString(float zoom, V2 origin, V2 windowSize, V2 markerPos, V2 cursorOff)
    {
        CurrentString.Clear();
        var tslating = origin;
        if (zoom > 1)
        {
            tslating = origin + 1.33333f * (markerPos - origin);
        }
        Styles.zoom = zoom;

        CurrentString.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{windowSize.X}\" height=\"{windowSize.Y}\" >")
            .Append($"<g transform=\"scale({1}) translate({tslating.X + cursorOff.X},{tslating.Y + cursorOff.Y}) rotate({0})\">")
            .Append($"<g transform=\"scale({zoom:N3}) translate({-tslating.X},{-tslating.Y}) rotate({0})\">");
    }

    public static void ClearStringPreview(float zoom, V2 origin, V2 windowSize, V2 markerPos, V2 cursorOff)
    {
        CurrentString.Clear();
        var tslating = origin;
        if (zoom > 1)
        {
            tslating = origin + 1.33333f * (markerPos - origin);
        }
        Styles.zoom = zoom;

        CurrentString.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{windowSize.X}\" height=\"{windowSize.Y}\" >")
            .Append($"<g transform=\"scale({1}) translate({tslating.X + cursorOff.X},{tslating.Y + cursorOff.Y}) rotate({0})\">")
            .Append($"<g transform=\"scale({zoom:N3}) translate({-tslating.X},{-tslating.Y}) rotate({0})\">");
    }

    public static void Add(string s)
    {
        CurrentString.Append(s);
    }

    public static void Finish()
    {
        CurrentString.Append("</g></g></svg>");
    }
    // M svgstring
    public void DrawPreviewing(Shapes shapes, bool white = false, bool debug = false)
    {
        if (white)
        {
            SvgString.SetStyle(Style.ShapePreviewWhite);
        }
        else
        {
            SvgString.SetStyle(Style.ShapePreview);
        }
        if (debug)
        {
            var mgs = shapes.GetMergedShapes();
            if (mgs.Length > 0)
                SvgString.AddSegmentsDebug(shapes.GetMergedShapes()[0]);
        }
        else
            SvgString.AddSegmentsGroup(shapes.GetMergedShapes());
    }
    // M svgstring
    public void DrawGuides(float fwi = 2.0f, float opac = 0.5f, float lesserOpac = 0.12f)
    {
        // draw guides
        V2 orig = Config.Origin;
        // TODO
        // if (zoom > 1)
        // {
        //     fwi /= 2;
        // }
        // else if (zoom < 1)
        // {
        //     fwi *= 1.5f;
        // }
        float zero = -5000;
        SvgString.AddLine(new V2(zero, orig.Y + Config.AscenderLineHeight), new V2(5000, orig.Y + Config.AscenderLineHeight), sWidth: fwi, sOpacity: lesserOpac);
        SvgString.AddLine(new V2(zero, orig.Y + Config.DescenderLineHeight), new V2(5000, orig.Y + Config.DescenderLineHeight), sWidth: fwi, sOpacity: lesserOpac);

        SvgString.AddLine(new V2(orig.X + Config.LeftWidthLine, zero), new V2(orig.X + Config.LeftWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(orig.X + Config.LeftWidthLine, zero), new V2(orig.X + Config.LeftWidthLine, 5000), sWidth: fwi, sOpacity: opac);
        SvgString.AddLine(new V2(orig.X + Config.RightWidthLine, zero), new V2(orig.X + Config.RightWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(orig.X + Config.RightWidthLine, zero), new V2(orig.X + Config.RightWidthLine, 5000), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(zero, orig.Y + Config.XLineHeight), new V2(5000, orig.Y + Config.XLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(zero, orig.Y + Config.XLineHeight), new V2(5000, orig.Y + Config.XLineHeight), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(zero, orig.Y + Config.BaseLineHeight), new V2(5000, orig.Y + Config.BaseLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(zero, orig.Y + Config.BaseLineHeight), new V2(5000, orig.Y + Config.BaseLineHeight), sWidth: fwi, sOpacity: opac);
    }

    public void DrawEditing(Shapes shapes, Shape currentShape, float zoom, V2 markerPos, Focus focus, HashSet<Anker> selectedAnchors, HashSet<HandlePointer> selectedHandles)
    {
        DrawGuides(opac: 0.3f, fwi: 3f);

        if (currentShape.Anchors.Count == 0) return;

        Segment[][] mergedShapes = shapes.GetMergedShapes();

        // underlay shapes
        foreach (Shape s in shapes.S)
        {
            if (s.Anchors.Count < 3) continue;

            if (s.Negative) SvgString.SetStyle(Style.ShapeNegative);
            else            SvgString.SetStyle(Style.ShapeUnchanged);

            if (Config.Debug) SvgString.AddSegmentsDebug(s.SegList());
            else              SvgString.AddSegments(s.SegList());
        }

        // overlay shapes
        foreach (Shape s in shapes.S)
        {
            if (s.Anchors.Count < 3) continue;

            if (s.Negative)
            {
                SetStyle(Style.ShapeShadow);
                AddSegments(s.SegList(true));
                SetStyle(Style.ShapeNegative);
            }
            else
            {
                SetStyle(Style.ShapeShadow);
                AddSegments(s.SegList(true));
                SetStyle(Style.ShapeUnchanged);
            }

            if (Config.Debug) AddSegmentsDebug(s.SegList());
            else              AddSegments(s.SegList(true));
        }

        SetStyle(Style.ShapeOverShadow);
        AddSegmentsGroup(mergedShapes);
        if (currentShape.Anchors.Count >= 3)
        {
            SetStyle(Style.ShapeUnchangedSelected);
            AddSegments(currentShape.SegList(true));
        }

        if (currentShape.Finished && false)
        {
            DrawMeasureLine(mergedShapes, markerPos);
        }

        DrawAnchorsHandles(shapes, currentShape, zoom, focus, selectedAnchors, selectedHandles);
    }

    // TODO fix measurementText: seperate drawing, label and calculating
    public void DrawMeasureLine(Segment[][] mergedShapes, V2 markerPos)
    {
        V2 tan = Fun.Vtv(Player.ProjectOnShapeTangent(mergedShapes[0], markerPos));
        tan = V2.Transform(tan, Matrix3x2.CreateRotation(MathF.PI * .5f));
        V2 lineStart = markerPos - tan * 2000;
        V2 lineEnd = markerPos + tan * 2000;
        // ui.MeasurementText = [];
        int pointcount = 0;
        foreach (Segment[] s in mergedShapes)
        {
            pointcount += s.Length;
            Segment measure = new(lineStart, lineStart, lineEnd, lineEnd);
            GV2[] inters = Player.SegmentShapeIntersections(s, measure);
            List<V2> il = [];
            foreach (GV2 gv in inters)
                il.Add(Fun.Vtv(gv));
            il.Sort(Fun.CompareVectors);
            for (int i = 0; i < il.Count; i++)
            {
                V2 current = il[i];
                if (i < il.Count - 1)
                {
                    V2 next = il[i + 1];
                    int d = (int)V2.Distance(current, next);
                    V2 halfway = (current + next) / 2.0f;
                    // ui.MeasurementText = [.. ui.MeasurementText, new(halfway, d.ToString())];
                }
                AddCircle(current, 7, fill: "red", fOpacity: 0.5f, sWidth: 0);
            }
        }
        AddLine(lineStart, lineEnd);
    }

    // draw anchors, visualize anchor selection, visualize anchor type
    public void DrawAnchor(Anker a, bool isSelected, HashSet<HandlePointer> selectedHandles, float[] radiusSizes, float[] widths)
    {
        if (isSelected)
        {
            AddCircle(a.Position, radiusSizes[1] + 8, fill: "red", fOpacity: 0.2f, sOpacity: 1.0f, sWidth: widths[0]);
        }
        else if (selectedHandles.Contains(a.InHandle.Pointer()) || selectedHandles.Contains(a.OutHandle.Pointer()))
        {
            AddCircle(a.Position, radiusSizes[1], fill: "white", fOpacity: 0.3f, sOpacity: 1.0f, sWidth: widths[0]);
        }
        else
        {
            AddCircle(a.Position, radiusSizes[0], fOpacity: 0, sOpacity: 1.0f, sWidth: widths[0]);
        }
    }

    // draw handles, visualize handle selection, visualize handle type
    public void DrawHandle(Anker a, bool focusIsHandle, HashSet<HandlePointer> selectedHandles, float[] radiusSizes, float[] widths)
    {
        if (selectedHandles.Contains(a.InHandle.Pointer()))
        {
            AddLine((2 * a.Position + a.InHandle.Position()) / 3, a.InHandle.Position(), stroke: "black", sOpacity: .5f);
            AddCircle(a.InHandle.Position(), radiusSizes[1] - 2, fill: "white", stroke: "black", fOpacity: 1.0f, sOpacity: 1.0f, sWidth: widths[1]);
        }
        else
        {
            if (focusIsHandle)
            {
                AddLine((2 * a.Position + a.InHandle.Position()) / 3, a.InHandle.Position(), stroke: "black", sOpacity: .5f);
                AddCircle(a.InHandle.Position(), radiusSizes[0] + 1, fill: "black", stroke: "white", fOpacity: .4f, sOpacity: 1f, sWidth: widths[0]);
            }
            else AddCircle(a.InHandle.Position(), radiusSizes[0] + 1, fill: "black", stroke: "white", fOpacity: .2f, sOpacity: .5f, sWidth: widths[0]);
        }

        if (selectedHandles.Contains(a.OutHandle.Pointer()))
        {
            AddLine((2 * a.Position + a.OutHandle.Position()) / 3, a.OutHandle.Position(), stroke: "black", sOpacity: .5f);
            AddCircle(a.OutHandle.Position(), radiusSizes[1] - 2, fill: "white", stroke: "black", fOpacity: 1.0f, sOpacity: 1.0f, sWidth: widths[1]);
        }
        else
        {
            if (focusIsHandle)
            {
                AddLine((2 * a.Position + a.OutHandle.Position()) / 3, a.OutHandle.Position(), stroke: "black", sOpacity: .5f);
                AddCircle(a.OutHandle.Position(), radiusSizes[0] + 1, fill: "black", stroke: "white", fOpacity: .4f, sOpacity: 1f, sWidth: widths[0]);
            }
            else AddCircle(a.OutHandle.Position(), radiusSizes[0] + 1, fill: "black", stroke: "white", fOpacity: .2f, sOpacity: .5f, sWidth: widths[0]);
        }
    }

    public void DrawAnchorsHandles(Shapes shapes, Shape currentShape, float zoom, Focus focus, HashSet<Anker> selectedAnchors, HashSet<HandlePointer> selectedHandles)
    {
        float[] radiusSizes = [6, 12];
        float[] widths = [1, 3];
        if (zoom > 1)
        {
            radiusSizes = [3, 4];
            widths = [0.5f, 1.0f];
        }

        else if (zoom < 1)
        {
            radiusSizes = [12, 32];
            widths = [2f, 6f];
        }

        // draw ui overlays (anchors and handles)
        foreach (Shape s in shapes.S)
        {
            foreach (Anker a in s.Anchors)
            {
                DrawAnchor(a, selectedAnchors.Contains(a), selectedHandles, radiusSizes, widths);
                if (s == currentShape)
                {
                    DrawHandle(a, focus == Focus.Handle, selectedHandles, radiusSizes, widths);
                }
            }
        }
    }
    // M svgstring
    // public Image DrawThumbnail(V2 size)
    // {
    //     float f = size.Y / Config.WindowSize.Y;
    //     float margin = .02f;
    //     f -= margin;

    //     SvgString.ClearString(f, (size * (margin / f)) / 2, size, input.MarkerPos, ui.CursorOff);

    //     DrawPreviewing(true);
    //     SvgString.Finish();
    //     Image thumbnail = new();
    //     thumbnail.LoadSvgFromString(SvgString.CurrentString.ToString());
    //     return thumbnail;
    // }
    // M svgstring
    public void DrawSelecting(Shapes shapes, Shape currentShape, float zoom)
    {
        DrawGuides(0.04f, 0.04f, 8);
        float[] radiusSizes = [6, 16];
        float[] widths = [1, 3];
        if (zoom > 1)
        {
            radiusSizes = [3, 4];
            widths = [0.5f, 1.0f];
        }
        else if (zoom < 1)
        {
            radiusSizes = [12, 32];
            widths = [2f, 6f];
        }
        foreach (Shape s in shapes.S)
        {
            if (s.Anchors.Count <= 1) continue;

            if (s.Negative)
            {
                SvgString.SetStyle(Style.ShapeNegative);
            }
            else
            {
                SvgString.SetStyle(Style.ShapeUnchanged);
            }

            SvgString.AddSegments(s.SegList(), false);
        }

        foreach (Shape s in shapes.S)
        {
            if (s != currentShape) continue;
            SvgString.SetStyle(Style.ShapeSelected);
            SvgString.AddSegments(s.SegList(), false);

            foreach (Anker a in s.Anchors)
            {

                // draw anchors
                // visualize anchor selection
                // visualize anchor type
                SvgString.AddCircle(a.Position, radiusSizes[0], fOpacity: 0, sOpacity: 1.0f, sWidth: 1);

                // draw handles
                // visualize handle selection
                // visualize handle type
            }
        }
    }

    // M svgstring
    // public void RenderThumbnails(float delta)
    // {
    //     V2 size = new(260, 260);
    //     Image thumbnail = DrawThumbnail(size);
    //     Image prevthumb = new();
    //     prevthumb.CopyFrom(thumbnail);

    //     children.BigPreview.Texture = ImageTexture.CreateFromImage(thumbnail);
    //     children.BigPreview.StretchMode = TextureRect.StretchModeEnum.KeepCentered;

    //     prevthumb.Resize((int)size.X / 3, (int)size.Y / 3);
    //     prevthumb.AdjustBcs(0.2f, 1, 1);
    //     PreviewTex = ImageTexture.CreateFromImage(prevthumb);
    // }
}

