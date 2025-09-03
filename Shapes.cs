using Godot;
using V2 = System.Numerics.Vector2;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vectordrawing;
using System.Numerics;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Data;
using System.Diagnostics;


namespace Vectordrawing;

public enum AnchorType
{
    Whole,
    Broken,
}

public enum SubdivisionType
{
    None,
    Chamfer,
    Rounded,
}

public enum SegmentType
{
    Straight,
    Cubic,
}

public struct HandlePointer(Anchor a, bool inHandle)
{
    public Anchor A = a;
    public bool InHandle = inHandle;

    public Handle h()
    {
        if (InHandle)
        {
            return A.InHandle;
        }
        else
        {
            return A.OutHandle;
        }
    }
}

public struct Segment(V2 inPoint, V2 inHandle, V2 outHandle, V2 outPoint)
{
    public V2 InPoint = inPoint;
    public V2 OutPoint = outPoint;
    public V2 InHandle = inHandle;
    public V2 OutHandle = outHandle;

    public float[] Flat()
    {
        return [InPoint.X, InPoint.Y, InHandle.X, InHandle.Y, OutHandle.X, OutHandle.Y, OutPoint.X, OutPoint.Y];
    }

    public float LengthCubic()
    {
        float[] f = Flat();
        return Player.LengthCubic(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]);
    }

    public (Segment, V2, V2) TrimmedTangentAndPos(float trimStart, float trimEnd)
    {
        Debug.Assert(trimStart <= 1.0 && trimStart >= 0.0);
        Debug.Assert(trimEnd <= 1.0 && trimEnd >= 0.0);
        float[] f = Flat();
        float[] tsat = Player.TrimmedTangentParametric(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], trimStart, trimEnd);
        Segment trimmedSegment = new(new(tsat[0], tsat[1]), new(tsat[2], tsat[3]), new(tsat[4], tsat[5]), new(tsat[6], tsat[7]));
        V2 tangentStart = new(tsat[8], tsat[9]);
        V2 tangentEnd = new(tsat[10], tsat[11]);
        return (trimmedSegment, tangentStart, tangentEnd);
    }

    public V2 PointAt(float amount)
    {
        float[] f = Flat();
        float[] raw = Player.PointAlongCubicParametric(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], amount);
        return new(raw[0], raw[1]);
    }

    public V2 TangentAt(float amount)
    {
        float[] f = Flat();
        float[] raw = Player.PointAlongCubicParametric(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], amount);
        return new(raw[0], raw[1]);
    }

    public override string ToString()
    {
        string ts = "";
        ts += "[" + InPoint + " " + InHandle + "  " + OutHandle + "  " + OutPoint + "]";
        return ts;
    }

}

public class Handle
{
    public SegmentType Type = SegmentType.Straight;
    public bool Selected = false;
    public bool IsInHandle = false;
    public Anchor AdjacentAnchor;
    public float DistanceFromAnchor;
    public float Angle;

    public Handle(Anchor adjacentAnchor, bool isInhandle, float angle = 0, float distanceFromAnchor = 1)
    {
        AdjacentAnchor = adjacentAnchor;
        IsInHandle = isInhandle;
        Angle = angle;
        DistanceFromAnchor = distanceFromAnchor;
    }

    public V2 Position()
    {
        return AdjacentAnchor.Position + V2.Transform(new V2(1,0) * DistanceFromAnchor, Matrix3x2.CreateRotation(Angle));
    }

    public override string ToString()
    {
        string ts = "";
        ts += "[" + AdjacentAnchor + ": " + DistanceFromAnchor + "  " + Angle + "  " + IsInHandle + "]";
        return ts;
    }
}

public class Anchor
{
    public V2 Position
    {
        get => _position;
        set
        {
            _position = value;
        }
    }
    public int SelectionIndex;
    public Shape MyShape;
    public AnchorType Type = AnchorType.Whole;
    public SegmentType SType = SegmentType.Straight;
    public SubdivisionType SubdivType = SubdivisionType.None;
    public Handle InHandle = null!;
    public Handle OutHandle = null!;
    public bool Selected = false;

    private V2 _position = new(0, 0);

    public Anchor(V2 startPosition, Shape myShape)
    {
        // SelectionLabel = selectionLabel;
        Position = startPosition;
        MyShape = myShape;
        MakeHandles();
    }
    public void MakeHandles()
    {
        // zorg dat ze op een lijn staan
        InHandle = new(this, true);
        OutHandle = new(this, false);
    }

    public void ReverseHandles()
    {
        (OutHandle, InHandle) = (InHandle, OutHandle);
    }

    public void AlignHandles()
    {
        InHandle.Angle = Fun.Vtv(Position).DirectionTo(Fun.Vtv(PreviousAnchor().Position)).Angle();
        OutHandle.Angle = Fun.Vtv(Position).DirectionTo(Fun.Vtv(NextAnchor().Position)).Angle();
    }

    public override string ToString()
    {
        string ts = "";
        ts += "[" + "test" + ": " + Position.X + "  " + Position.Y + "]";
        return ts;
    }

    public Anchor NextAnchor()
    {
        List<Anchor> anchors = MyShape.Anchors;
        int myIndex = anchors.IndexOf(this);
        if (myIndex == anchors.Count - 1)
        {
            return anchors.First();
        }
        else
        {
            return anchors[myIndex + 1];
        }
    }

    public Anchor PreviousAnchor()
    {
        List<Anchor> anchors = MyShape.Anchors;
        int myIndex = anchors.IndexOf(this);
        if (myIndex == 0)
        {
            return anchors.Last();
        }
        else
        {
            return anchors[myIndex - 1];
        }
    }
}


public class Shape
{
    public bool Selected = false;
    public List<Anchor> Anchors = [];
    public bool Finished = false;

    public Shape()
    {
    }

    public void ReverseShape()
    {
        foreach (var a in Anchors)
        {
            a.ReverseHandles();
        }
        Anchors.Reverse();
    }

    public override string ToString()
    {
        string ts = "[";
        int counter = 0;
        foreach (var a in Anchors)
        {
            ts += counter + ": " + a.ToString() + " ";
            counter += 1;
        }
        ts += "]";
        return ts;
    }

    public bool IsClockwise()
    {
        float lowestY = 100000000;
        Anchor lowestAnchor = Anchors[0];
        foreach (var a in Anchors)
        {
            if (a.Position.Y < lowestY)
            {
                lowestY = a.Position.Y;
                lowestAnchor = a;
            }
        }
        V2 A = lowestAnchor.Position;
        int aIndex = Anchors.IndexOf(lowestAnchor);

        V2 B = V2.Zero;
        if (aIndex > 0)
        {
            B = Anchors[aIndex - 1].Position;
        }
        else
        {
            B = Anchors.Last().Position;
        }
        V2 C = V2.Zero;
        if (aIndex == Anchors.Count - 1)
        {
            C = Anchors[0].Position;
        }
        else
        {
            C = Anchors[aIndex + 1].Position;
        }
        V2 BA = B - A;
        V2 CA = C - A;
        return ((BA.X * CA.Y) - (BA.Y * CA.X)) <= 0;
    }

    public void MakeClockwise()
    {
        if (!IsClockwise())
        {
            ReverseShape();
        }
    }

    public void AddAnchor(V2 pos, bool makeCubic = false, Anchor? insertAfter = null)
    {
        Anchor a = new(pos, this);

        // early out
        if (Anchors.Count > 0 && Anchors[^1].Position == a.Position)
        {
            Anchors[^1] = a;
            return;
        }

        if (insertAfter is not null)
        {
            int whereToInsert = Anchors.IndexOf(insertAfter);
            Anchors.Insert(whereToInsert + 1, a);
        }
        else
        {
            Anchors.Add(a);
            // maybe switch point type?
        }
    }

    public V2 GetAveragePosition()
    {
        V2 average = V2.Zero;
        foreach (var a in Anchors)
        {
            average += a.Position;
        }
        average /= Anchors.Count;
        return average;
    }

    public void Delete()
    {
        Shapes.DeleteShape(this);
    }

    public void Finish()
    {
        if (Anchors.Count < 3)
        {
            Delete();
            return;
        }
        foreach (Anchor a in Anchors)
        {
            a.AlignHandles();
        }
        MakeClockwise();
        Finished = true;
    }

    public Segment[] Segments()
    {
        Segment[] segments = new Segment[Anchors.Count];
        for (int i = 0; i < Anchors.Count; i += 1)
        {
            int after = i + 1;
            if (i == Anchors.Count - 1)
            {
                after = 0;
            }
            Segment s = new(Anchors[i].Position, Anchors[i].OutHandle.Position(), Anchors[after].InHandle.Position(), Anchors[after].Position);
            segments[i] = s;
        }
        return segments;
    }

    /// <summary>Like 'Segments' but flattened</summary><returns></returns>
    public float[] Flat()
    {
        float[] flatPositions = [];
        for (int i = 0; i < Anchors.Count; i += 1)
        {
            int after = i + 1;
            if (i == Anchors.Count - 1)
            {
                i = 0;
            }
            V2 start = Anchors[i].Position;
            V2 h1 = Anchors[i].OutHandle.Position();
            V2 h2 = Anchors[i].InHandle.Position();
            V2 end = Anchors[after].Position;
            flatPositions = [.. flatPositions, .. (float[])[start.X, start.Y, h1.X, h1.Y, h2.X, h2.Y, end.X, end.Y]];
        }
        return flatPositions;
    }

    public float[] VectorBoolean(Shape shapeB, bool negative)
    {
        return Player.BetterVectorBoolean(Flat(), shapeB.Flat(), negative);
    }

    public string GetLabel(Anchor a)
    {
        int index = Anchors.IndexOf(a);
        Debug.Assert(index != -1);
        return C.Alfabet[index].ToString();
    }

    public Anchor GetAnchorFromLabel(string s)
    {
        int index = s[0] - 97;
        return Anchors[index];
    }

    public Segment[] RoundSelf(bool rounded = true, int cornerSize = 80, float roundness = 1.0f)
    {
        return RoundCornersSegments(Segments(), rounded, cornerSize, roundness);
    }

    public static Segment[] RoundCornersSegments(Segment[] originalShape, bool rounded = true, int cornerSize = 80, float roundness = 1.0f)
    {
        roundness *= 0.7f;
        List<Segment> roundedSegments = [];
        List<(Segment, V2, V2)> trimmedSegList = [];
        List<float> cornerSizeList = [];

        {
            float cornerSizeToUse = cornerSize;
            float segLength = originalShape[0].LengthCubic();
            if (segLength < 2 * cornerSize)
            {
                cornerSizeToUse = segLength / 2.1f;
            }
            cornerSizeList.Add(cornerSizeToUse);
            for (int i = 1; i < originalShape.Length; i++)
            {
                cornerSizeToUse = cornerSize;
                segLength = originalShape[i].LengthCubic();
                if (segLength < 2 * cornerSize)
                {
                    cornerSizeToUse = segLength / 2.1f;
                }
                cornerSizeList.Add(cornerSizeToUse);
            }
        }

        for (int i = 0; i < originalShape.Length; i++)
        {
            Segment seg = originalShape[i];
            float amountToTrim = cornerSizeList[i] / seg.LengthCubic();
            trimmedSegList.Add(seg.TrimmedTangentAndPos(amountToTrim, 1.0f - amountToTrim));
        }

        if (trimmedSegList.Count == 0)
        {
            return originalShape;
        }
        int index = 0;
        foreach ((Segment trimmed, V2 tanPos, V2 tanAngle) in trimmedSegList)
        {
            V2 tanPos2;
            Segment trimmed2;
            float r;
            if (index < trimmedSegList.Count - 1)
            {
                trimmed2 = trimmedSegList[index + 1].Item1;
                tanPos2 = trimmedSegList[index + 1].Item2;
                r = cornerSizeList[index + 1];
            }
            else
            {
                trimmed2 = trimmedSegList[0].Item1;
                tanPos2 = trimmedSegList[0].Item2;
                r = cornerSizeList[0];
            }
            roundedSegments.Add(trimmed);
            if (rounded)
            {
                V2 newPos1 = trimmed.OutPoint + V2.Normalize(tanPos) * roundness * cornerSizeList[index];
                V2 newPos2 = trimmed2.InPoint - V2.Normalize(tanPos2) * roundness * r;
                roundedSegments.Add(new(trimmed.OutPoint, newPos1, newPos2, trimmed2.InPoint));
            }
            else
            {
                roundedSegments.Add(new(trimmed.OutPoint, trimmed.OutPoint, trimmed2.InPoint, trimmed2.InPoint));
            }
            index += 1;
        }
        return roundedSegments.ToArray();
    }
    // public string GetLabel(HandlePointer h)
    // {
    //     int index = Anchors.IndexOf(h.A);
    //     Debug.Assert(index != -1);
    //     return C.Alfabet[index].ToString();
    // }
}

public static class Shapes
{
    public static List<Shape> S = [];
    public static Shape NewShape()
    {
        Shape s = new();
        S.Add(s);
        return s;
    }

    public static void DeleteShape(Shape s)
    {
        S.Remove(s);
    }

    public static Shape CreateRandomShape(int variation, int shapeSize)
    {
        V2 xBorder = new(300, 1500);
        V2 yBorder = new(300, 1500);
        V2 center = new((float)GD.RandRange(xBorder.X, xBorder.Y), (float)GD.RandRange(yBorder.X, yBorder.Y));
        Shape toReturn = NewShape();
        for (int i = 0; i < GD.RandRange(3, shapeSize); i += 1)
        {
            toReturn.AddAnchor(center + Fun.RandomVector(variation, variation));
        }
        toReturn.Finish();
        return toReturn;
    }


    public static Segment[][] MergeAllShapes()
    {
        Segment[][] currentShapes = [S[0].Segments()];
        for (int i = 1; i < S.Count; i++)
        {
            Segment[][] newShapes = [];
            foreach (Segment[] island in currentShapes)
            {
                newShapes = [.. newShapes, .. BooleanMergeSegments(island, S[i].Segments(), i%2 == 0)];
            }
            currentShapes = newShapes;
        }
        Segment[][] roundedShapes = [];
        return currentShapes;
        // foreach (Segment[] island in currentShapes)
        // {
        //     roundedShapes = [.. roundedShapes, Shape.RoundCornersSegments(island, true, 20, 1)];
        // }
        // return roundedShapes;
    }


    public static Segment[][] BooleanMergeSegments(Segment[] A, Segment[] B, bool negative)
    {
        float[] pointsA = [];
        foreach (Segment s in A)
        {
            pointsA = [.. pointsA, .. s.Flat()];
        }

        float[] pointsB = [];
        foreach (Segment s in B)
        {
            pointsB = [.. pointsB, .. s.Flat()];
        }
        if (pointsA.Length <= 16 || pointsB.Length <= 16)
        {
            return [A, B];
        }
        float[] merged = Player.BetterVectorBoolean(pointsA, pointsB, negative);
        GD.Print("\n");
        for (int i = 0; i < merged.Length / 4; i++)
        {
            int j = i * 4;
            // GD.Print(merged[j] + "  " + merged[j + 1] + "  " + merged[j + 2] + "  " + merged[j + 3]);
        }
        if (merged.Length == 0)
        {
            return [A, B];
        }

        Segment[][] outAr = [];
        Segment[] segAr = [];
        int cc = 0;
        for (int i = 0; i < merged.Length; i++)
        {
            if (merged[i] == -9999)
            {
                GD.Print("\ngap\n");
                cc = 0;
                outAr = [.. outAr, segAr];
                segAr = [];
            }
            else
            {
                if (cc == 7)
                {
                    cc = 0;
                    int j = i - 7;
                    V2 inA = new(merged[j], merged[j + 1]);
                    V2 inH = new(merged[j + 2], merged[j + 3]);
                    V2 outH = new(merged[j + 4], merged[j + 5]);
                    V2 outA = new(merged[j + 6], merged[j + 7]);
                    segAr = [.. segAr, new(inA, inH, outH, outA)];
                }
                else cc += 1;
            }
        }

        outAr = [.. outAr, segAr];
        return outAr;
    }
    
}

// public static class LabelMaster
// {
//     static string[] SelectLetters = "abcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".Split();
//     static string GetLabel()
//     {
//         SelectLetters.T
//     }
// }
