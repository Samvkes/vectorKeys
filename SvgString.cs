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

namespace Vectordrawing;

enum Style  {
    ShapeClosed,
    ShapeOpen,
    ShapePreview,
}

static class Styles
{
    public static readonly Dictionary<string, string>[] S =
    [
        new()
        {
        // shape closed
            ["stroke"] = "black",
            ["fill"] = "black",
            ["stroke-width"] = "0",
            ["fill-opacity"] = "2",
        },
        new()
        {
        // shape open
            ["stroke"] = "black",
            ["fill"] = "black",
            ["stroke-width"] = "0",
            ["fill-opacity"] = "2",
        },
        new()
        {
        // shape preview
            ["stroke"] = "black",
            ["fill"] = "black",
            ["stroke-width"] = "0",
            ["fill-opacity"] = "2",
        }

    ];
    
}

static class SvgString
{
    static string CurrentString = "";

    static Dictionary<string, string> CurrentStyle = Styles.S[0];

    public static void SetStyle(Style s)
{
    CurrentStyle = Styles.S[(int)s];
}

    static void Style()
    {
        foreach ((string key, string val) in CurrentStyle)
        {
            CurrentString += key + "=\"" + val + "\" ";
        }
    }

    static void ClearString()
    {
        V2 windowSize = new(1, 1);
        V2 tsLating = new(1, 1);
        float zoom = 0.5f;

        CurrentString = (
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{windowSize.X}\" height=\"{windowSize.Y}\">" +
            $"<g transform=\"scale({1}) translate({tsLating.X},{tsLating.Y}) rotate({0})\">" +
            $"<g transform=\"scale({zoom}) translate({-tsLating.X},{-tsLating.Y}) rotate({0})\">"
        );
    }

    static void Add(string s)
    {
        CurrentString += s;
    }
}

