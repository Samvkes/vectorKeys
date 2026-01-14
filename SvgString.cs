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

namespace Vectordrawing;

enum Style  {
    ShapePositive,
    ShapeOpen,
    ShapePreview,
    ShapePreviewWhite,
    ShapeUnchanged,
    ShapeNegative,
    ShapeSelected,
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
        string[] widths = ["1", "3", "5"];
        if (zoom > 1)
            widths = ["0.5", "1", "2"];
        else if (zoom < 1)
            widths = ["3", "5", "8"];

        return widths[(int)width];
    }

    public static Dictionary<string, string>[] S =
    [
        new()
        {
        // shape positive
            ["fill"] = "blue",
            ["stroke"] = "black",
            ["fill-opacity"] = "0.1",
            ["stroke-opacity"] = "1.0",
            ["stroke-width"] = "2",
        },
        new()
        {
        // shape open
            ["fill"] = "red",
            ["stroke"] = "black",
            ["fill-opacity"] = "0.2",
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
            ["stroke"] = "white",
            ["fill-opacity"] = "0.4",
            ["stroke-opacity"] = "1.0",
            ["stroke-width"] = "1",
        },
        new()
        {
        // shape negative
            // ["fill"] = "#d69c85",
            ["fill"] = "#e08a85",
            ["stroke"] = "black",
            ["fill-opacity"] = "0.7",
            ["stroke-opacity"] = "0.8",
            ["stroke-width"] = "1",
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
    ];
}

static class SvgString
{
    public static string CurrentString = "";

    static Dictionary<string, string> CurrentStyle = Styles.S[0];

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
                CurrentString += key + "=\"" + Styles.GetWidth((Styles.Width)(int.Parse(val)-1)) + "\" ";
            }
            else
            {
                CurrentString += key + "=\"" + val + "\" ";
            }
        }
    }

    public static void AddSKPath(SKPath path)
    {
        CurrentString += $"<path d=\" ";
        CurrentString += path.ToSvgPathData();
        CurrentString += $"Z\" ";
        //   fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
        Style();
        CurrentString += "/>";
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
        foreach (Segment seg in s)
        {
            string color = color1;
            if (counter % 2 == 0)
            {
                color = color2;
            }
            float[] flatSeg = seg.Flat();
            AddCircle(new(flatSeg[2], flatSeg[3]), 2, color, color, fOpacity = 0.2f, sWidth = 0.5f);
            AddCircle(new(flatSeg[4], flatSeg[5]), 2, color, color, fOpacity = 0.2f, sWidth = 0.5f);
            CurrentString += $"<path d=\"M {flatSeg[0]} {flatSeg[1]} C {flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]} \" stroke=\"{color}\" fill-opacity=\"0.0\" stroke-width=\"2\"/>";
            counter += 1;
        }
    }
    public static void AddSegments(Segment[] s, bool debugInfo = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        // GD.Print(s[0]);
        float[] startSeg = s[0].Flat();
        CurrentString += $"<path d=\"M {startSeg[0]} {startSeg[1]} C ";
        int counter = 0;
        foreach (Segment seg in s)
        {
            float[] flatSeg = seg.Flat();
            CurrentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
            if (counter != s.Length - 1)
            {
                CurrentString += "C ";
            }
            CurrentString += " ";
            counter += 1;
        }
        CurrentString += $"Z\" ";
        if (debugInfo)
        {
            if (Shapes.IsSegmentListClockwise(s))
            {
                CurrentString += " fill =\"red\" stroke =\"red\" fill-opacity=\"1.2\" stroke-opacity=\"0.0\" stroke-width=\"2\"/>";
            }
            else
            {
                CurrentString += " fill =\"blue\" stroke =\"blue\" fill-opacity=\"1.2\" stroke-opacity=\"0.0\" stroke-width=\"2\"/>";
            }
            // CurrentString += " fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
        }
        else
        {
            Style();
        }
        CurrentString += "/>";
    }

    public static void AddSegmentsGroup_Debug(Segment[][] sGroup, bool currentShape = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        foreach (Segment[] s in sGroup)
        {
            CurrentString += $"<path d=\"";
            float[] startSeg = s[0].Flat();
            CurrentString += $" M {startSeg[0]} {startSeg[1]} C ";
            int counter = 0;
            foreach (Segment seg in s)
            {
                float[] flatSeg = seg.Flat();
                CurrentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
                if (counter != s.Length - 1)
                {
                    CurrentString += ",";
                }
                CurrentString += " ";
                counter += 1;
            }
            CurrentString += $"Z ";
            CurrentString += "\"";
            if (Shapes.IsSegmentListClockwise(s))
            {
                CurrentString += "fill =\"red\" stroke =\"red\" fill-opacity=\"0.0\" stroke-opacity=\"1.0\" stroke-width=\"4\"";
            }
            else
            {
                CurrentString += "fill =\"blue\" stroke =\"blue\" fill-opacity=\"0.0\" stroke-opacity=\"1.0\" stroke-width=\"4\"";
            }
            // Style(currentShape);
            CurrentString += "/>";
        }
        //   fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
    }

    public static void AddSegmentsGroup(Segment[][] sGroup, bool currentShape = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        CurrentString += $"<path d=\"";
        foreach (Segment[] s in sGroup)
        {
            float[] startSeg = s[0].Flat();
            CurrentString += $" M {startSeg[0]} {startSeg[1]} C ";
            int counter = 0;
            foreach (Segment seg in s)
            {
                float[] flatSeg = seg.Flat();
                CurrentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
                if (counter != s.Length - 1)
                {
                    CurrentString += ",";
                }
                CurrentString += " ";
                counter += 1;
            }
            CurrentString += $"Z ";
        }
        //   fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
        CurrentString += "\"";
        Style();
        CurrentString += "/>";
    }

    public static void AddCircle(V2 position, float radius, string fill = "black", string stroke = "black", float fOpacity = 1.0f, float sOpacity = 1.0f, float sWidth = 10f)
    { 
        CurrentString += (
            $"<circle cx=\"{position.X}\" cy=\"{position.Y}\" r=\"{radius}\" " + 
            $"fill=\"{fill}\" stroke=\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>"
        );
    }

    public static void AddLine(V2 start, V2 end, string stroke = "red", float sWidth = 1f, float sOpacity = 1f)
    {
        CurrentString += (
            $"<path d=\"M {start.X} {start.Y} L {end.X} {end.Y}\"" + 
            $"stroke =\"{stroke}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>"
        );
    }
    
    public static void AddDashed(V2 start, V2 end, string dash, string stroke = "black", float sWidth = 1f, float sOpacity = 1f)
    {
        CurrentString += (
            $"<line x1=\"{start.X}\" y1=\"{start.Y}\"  x2=\"{end.X}\" y2=\"{end.Y}\"" +
            $"stroke =\"{stroke}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\" " +
            "stroke-dasharray=\"" + dash + "\" stroke-linecap=\"round\"" +
            "/>"
        );
    }

    public static void ClearString(float zoom, V2 origin, V2 windowSize, V2 markerPos)
    {
        var tslating = origin;
        if (zoom > 1)
        {
            tslating = origin + 1.33333f * (markerPos - origin);
        }
        Styles.zoom = zoom;
        CurrentString = (
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{windowSize.X}\" height=\"{windowSize.Y}\" >" +
            $"<g transform=\"scale({1}) translate({tslating.X + Base.ui.CursorOff.X},{tslating.Y + Base.ui.CursorOff.Y}) rotate({0})\">" +
            $"<g transform=\"scale({zoom:N3}) translate({-tslating.X},{-tslating.Y}) rotate({0})\">"
        );
    }

    public static void Add(string s)
    {
        CurrentString += s;
    }

    public static void Finish()
    {
        CurrentString += "</g></g></svg>";
    }
}

