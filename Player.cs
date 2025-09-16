using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;

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

    public static float[] PointAlongCubicParametric(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4, float amount)
    {
        return (float[])pl.Call("point_along_cubic_parametric", x1, y1, x2, y2, x3, y3, x4, y4, amount);
    }

    public static float[] TangentParametric(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4, float amount)
    {
        return (float[])pl.Call("tangent_parametric", x1, y1, x2, y2, x3, y3, x4, y4, amount);
    }

}
