using Godot;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using Godot.Collections;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualBasic;
using System.Diagnostics;

namespace Vectordrawing;

public partial class Player : Node
{
    public static Node pl = null!;

    public override void _Ready()
    {
        pl = this;
    }

    public static float LengthCubic(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        // return 0f;
        return (float)pl.Call("length_cubic", x1, y1, x2, y2, x3, y3, x4, y4);
    }

    public static float[] TrimmedTangentParametric(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4, float t1, float t2)
    {
        return (float[])pl.Call("trimmed_tangent_parametric", x1, y1, x2, y2, x3, y3, x4, y4, t1,t2);
    }

    public static float[] BetterVectorBoolean(int[] a, int[] b, bool negative)
    {
        Array<int> aa = new();
        foreach (int f in a)
        {
            aa.Add(f);
        }
        Array<int> bb = new();
        foreach (int f in b)
        {
            bb.Add(f);
        }
        return (float[])pl.Call("better_vector_boolean", aa,bb,negative);
    }

    public static bool AreShapesOverlapping(Segment[] a, Segment[] b)
    {
        Array<int> aa = new();
        foreach (Segment s in a)
        {
            aa.AddRange(s.FlatI());
        }

        Array<int> bb = new();
        foreach (Segment s in b)
        {
            bb.AddRange(s.FlatI());
        }
        return (bool)pl.Call("are_shapes_overlapping", aa,bb);
    }

    public static List<(int whichBezierFromSL2, V2)> ShapeShapeIntersections(Segment[] segList, Segment[] segList2)
    {
        Array<float> floatAr = new();
        foreach (Segment part in segList)
        {
            floatAr.AddRange(part.Flat());
        }
        Array<float> floatAr2 = new();
        foreach (Segment part in segList2)
        {
            floatAr2.AddRange(part.Flat());
        }
        // float[] f = s.Flat();
        float[] outp = (float[])pl.Call("shape_shape_intersections", floatAr, floatAr2);
        List<(int, V2)> outAr = [];
        for (int i = 0; i < outp.Length / 3; i++)
        {
            outAr.Add((
                  (int)outp[i * 3]
                , new(outp[i * 3 + 1], outp[i * 3 + 2]))
                );
        }
        return outAr;
    }

    public static (Segment[], int[]) AnchorsToHyperBeziers(List<Anker> anchors)
    {
        (Segment[], int[]) segs = ([],[]);
        Vector2 start = Fun.Vtv(anchors[0].Position);
        Array<float> inPoints = [];
        foreach (Anker a in anchors)
        {
            Anker next = a.NextAnchor();
            V2 inh = a.OutHandle.Position();
            V2 outh = next.InHandle.Position();
            
            inPoints = [.. inPoints, inh.X, inh.Y, outh.X, outh.Y,next.Position.X, next.Position.Y, (next.Broken ? 1 : 0)];
        }

        float[] hb = (float[])pl.Call("hyper_bezier", start, inPoints);

        // for (int i = 0; i < hb.Length; i++)
        int counter = 0;
        while (counter < hb.Length)
        {
            int i = counter;
            if (hb[i] == -9999)
            {
                int amount = (int)hb[i+1];
                segs.Item2 = [.. segs.Item2, amount];
                counter += 2;
            }
            else
            {
                Segment seg = new(
                    new(hb[i + 0], hb[i + 1]),
                    new(hb[i + 2], hb[i + 3]),
                    new(hb[i + 4], hb[i + 5]),
                    new(hb[i + 6], hb[i + 7])
                );
                segs.Item1 = [.. segs.Item1, seg];
                counter += 8;    
            }
        }
        return segs;
    }

    public static GV2 ProjectOnShapeTangent(Segment[] segList, V2 pos)
    {
        Array<float> floatAr = new();
        foreach (Segment part in segList)
        {
            floatAr.AddRange(part.Flat());
        }
        return (GV2)pl.Call("project_on_shape_tangent", floatAr, pos.X, pos.Y);
    }

    public static GV2 ProjectOnShape(Segment[] segList, V2 pos)
    {
        Array<float> floatAr = new();
        foreach (Segment part in segList)
        {
            floatAr.AddRange(part.Flat());
        }
        return (GV2)pl.Call("project_on_shape", floatAr, pos.X, pos.Y);
    }

    public static Vector2[] SegmentShapeIntersections(Segment[] segList, Segment s)
    {
        Array<float> floatAr = new();
        foreach (Segment part in segList)
        {
            floatAr.AddRange(part.Flat());
        }
        float[] f = s.Flat();
        return (Vector2[])pl.Call("segment_shape_intersections", floatAr, f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]);
    }

    public static float[] CurvaturePosition(float[] a)
    {
        return (float[])pl.Call("curvature_position", a);
    }

    public static float[] PointAlongCubicParametric(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4, float amount)
    {
        return (float[])pl.Call("point_along_cubic_parametric", x1, y1, x2, y2, x3, y3, x4, y4, amount);
    }

    public static float[] TangentParametric(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4, float amount)
    {
        return (float[])pl.Call("tangent_parametric", x1, y1, x2, y2, x3, y3, x4, y4, amount);
    }

}
