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

namespace Vectordrawing;

enum Style  {
    ShapeClosed,
    ShapeOpen,
    ShapePreview,
    ShapeUnchanged,
}

static class Styles
{
    public static readonly Dictionary<string, string>[] S =
    [
        new()
        {
        // shape closed
            ["fill"] = "black",
            ["stroke"] = "black",
            ["fill-opacity"] = "0.15",
            ["stroke-opacity"] = "1",
            ["stroke-width"] = "1",
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
        // shape unchanged
            ["fill"] = "black",
            ["stroke"] = "red",
            ["fill-opacity"] = "0",
            ["stroke-opacity"] = "0.5",
            ["stroke-width"] = "1",
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

    static void Style(bool currentShape)
    {
        foreach ((string key, string val) in CurrentStyle)
        {
            if (currentShape && key == "stroke-opacity")
            {
                CurrentString += key + "=\"" + "1" + "\" ";
            }
            else
            {
                CurrentString += key + "=\"" + val + "\" ";
            }
        }
    }
    public static void AddSegments(Segment[] s, bool currentShape = false, string fill = "black", string stroke = "black", float fOpacity = 1f, float sOpacity = 1f, float sWidth = 1f)
    {
        float[] startSeg = s[0].Flat();
        CurrentString += $"<path d=\"M {startSeg[0]} {startSeg[1]} C ";
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
        CurrentString += $"Z\" ";
        //   fill =\"{fill}\" stroke =\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>";
        Style(currentShape);
        CurrentString += "/>";
    }

    public static void AddCircle(V2 position, float radius, string fill = "black", string stroke = "black", float fOpacity = 1.0f, float sOpacity = 1.0f, float sWidth = 10f)
    { 
        CurrentString += (
            $"<circle cx=\"{position.X}\" cy=\"{position.Y}\" r=\"{radius}\" " + 
            $"fill=\"{fill}\" stroke=\"{stroke}\" fill-opacity=\"{fOpacity}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>"
        );
    }

    public static void AddLine(V2 start, V2 end, string stroke = "black", float sWidth = 1f, float sOpacity = 1f)
    {
        CurrentString += (
            $"<path d=\"M {start.X} {start.Y} L {end.X} {end.Y}\"" + 
            $"stroke =\"{stroke}\" stroke-opacity=\"{sOpacity}\" stroke-width=\"{sWidth}\"/>"
        );
    }

    public static void ClearString(float zoom, V2 origin, V2 windowSize)
    {
        CurrentString = (
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{windowSize.X}\" height=\"{windowSize.Y}\">" +
            $"<g transform=\"scale({1}) translate({origin.X},{origin.Y}) rotate({0})\">" +
            $"<g transform=\"scale({zoom:N3}) translate({-origin.X},{-origin.Y}) rotate({0})\">"
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

