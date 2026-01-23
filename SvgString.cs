using Godot;
using V2 = System.Numerics.Vector2;
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

enum Style  {
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

static class SvgString
{
    // public static string CurrentString = "";
    static Dictionary<string, string> CurrentStyle = Styles.S[0];
    public static readonly StringBuilder CurrentString = new();

    public static void SetStyle(Style s)
    {
        CurrentStyle = Styles.S[(int)s];
    }

    static void Style()
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
        Style();
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
                     .Append(f[0])
                     .Append(f[1])
                     .Append(" C ");
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
            //     CurrentString += "C ";
            // }
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
            Style();
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
            Style();
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
        Style();
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

    public static void ClearString(float zoom, V2 origin, V2 windowSize, V2 markerPos)
    {
        CurrentString.Clear();
        var tslating = origin;
        if (zoom > 1)
        {
            tslating = origin + 1.33333f * (markerPos - origin);
        }
        Styles.zoom = zoom;

        CurrentString.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{windowSize.X}\" height=\"{windowSize.Y}\" >")
            .Append($"<g transform=\"scale({1}) translate({tslating.X + Base.ui.CursorOff.X},{tslating.Y + Base.ui.CursorOff.Y}) rotate({0})\">")
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
}

