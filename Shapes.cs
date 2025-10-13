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
using System.Collections.Specialized;
using SkiaSharp;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Text.Json.Serialization;


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

    public int[] FlatI()
    {
        return [(int)InPoint.X, (int)InPoint.Y, (int)InHandle.X, (int)InHandle.Y, (int)OutHandle.X, (int)OutHandle.Y, (int)OutPoint.X, (int)OutPoint.Y];
    }

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

    public Segment Reverse()
    {
        return new(OutPoint, OutHandle, InHandle, InPoint);
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
    public Anchor AdjacentAnchor = null!;
    public float DistanceFromAnchor = 2;
    public float Angle = 0;

    public Handle()
    {
    }

    public void Init(Anchor adjacentAnchor, bool isInhandle, float angle = 0f, float distanceFromAnchor = 2)
    {
        IsInHandle = isInhandle;
        AdjacentAnchor = adjacentAnchor;
        DistanceFromAnchor = distanceFromAnchor;
        Angle = angle;
    }

    public V2 Position()
    {
        return AdjacentAnchor.Position + V2.Transform(new V2(1, 0) * DistanceFromAnchor, Matrix3x2.CreateRotation(Angle));
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
    public Shape MyShape = null!;
    public AnchorType Type = AnchorType.Whole;
    public bool AutoHandles = false;
    public SegmentType SType = SegmentType.Straight;
    public SubdivisionType SubdivType = SubdivisionType.None;
    public Handle InHandle = null!;
    public Handle OutHandle = null!;
    public bool Sselected = false;

    private V2 _position = new(0, 0);

    public void Init(V2 startPosition, Shape myShape)
    {
        // SelectionLabel = selectionLabel;
        MakeHandles();
        MyShape = myShape;
        Position = startPosition;
    }

    public void SwitchSegmentType(bool trailingSegment = false)
    {
        if (trailingSegment)
        {
            if (InHandle.Type == SegmentType.Straight)
            {
                InHandle.Type = SegmentType.Cubic;
                InHandle.DistanceFromAnchor = 100;
                // InHandle.Angle = -GD.Randf();
                PreviousAnchor().OutHandle.Type = SegmentType.Cubic;
                PreviousAnchor().OutHandle.DistanceFromAnchor = 100;
                // PreviousAnchor().OutHandle.Angle = -GD.Randf();
            }
            else
            {
                InHandle.Type = SegmentType.Straight;
                InHandle.DistanceFromAnchor = 1;
                PreviousAnchor().OutHandle.Type = SegmentType.Straight;
                PreviousAnchor().OutHandle.DistanceFromAnchor = 1;
            }
        }
        else
        {
            if (OutHandle.Type == SegmentType.Straight)
            {
                OutHandle.Type = SegmentType.Cubic;
                OutHandle.DistanceFromAnchor = 100;
                // OutHandle.Angle = -GD.Randf();
                NextAnchor().InHandle.Type = SegmentType.Cubic;
                NextAnchor().InHandle.DistanceFromAnchor = 100;
                // NextAnchor().InHandle.Angle = -GD.Randf();
            }
            else
            {
                OutHandle.Type = SegmentType.Straight;
                OutHandle.DistanceFromAnchor = 1;
                NextAnchor().InHandle.Type = SegmentType.Straight;
                NextAnchor().InHandle.DistanceFromAnchor = 1;
            }
        }
    }

    public void MakeHandles()
    {
        // zorg dat ze op een lijn staan
        InHandle = new();
        InHandle.Init(this, true);
        OutHandle = new();
        OutHandle.Init(this, true);
    }

    public void ReverseHandles()
    {
        (OutHandle, InHandle) = (InHandle, OutHandle);
    }

    public void AlignHandles()
    {
        if (OutHandle.Type == SegmentType.Straight)
        {
            OutHandle.Angle = Fun.Vtv(Position).DirectionTo(Fun.Vtv(NextAnchor().Position)).Angle();
        }
        if (InHandle.Type == SegmentType.Straight)
        {
            InHandle.Angle = Fun.Vtv(Position).DirectionTo(Fun.Vtv(PreviousAnchor().Position)).Angle();
        }
        if (OutHandle.Type == SegmentType.Cubic)
        {
            if (Type == AnchorType.Broken) return;
            else if (AutoHandles)
            {
                OutHandle.Angle = InHandle.Angle + MathF.PI;
            }
        }
        if (InHandle.Type == SegmentType.Cubic)
        {
            if (PreviousAnchor().Type == AnchorType.Broken) return;
            else if (AutoHandles)
            {
                GD.Print("2asdf");
                InHandle.Angle = OutHandle.Angle + MathF.PI;
            }
        }

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
    public Shape()
    {
    }

    public bool Ssselected = false;
    public List<Anchor> Anchors = [];
    public bool Finished = false;
    public bool Negative = false; 

    public void ReverseShape()
    {
        foreach (var a in Anchors)
        {
            a.ReverseHandles();
        }
        Anchors.Reverse();
    }

    public SKPath ToSKPath()
    {
        SKPath retPath = new();
        retPath.MoveTo(Anchors[0].Position);
        foreach (Anchor a in Anchors)
        {
            retPath.CubicTo(a.OutHandle.Position(), a.NextAnchor().InHandle.Position(), a.NextAnchor().Position);
        }
        retPath.Close();
        return retPath;
    }

    public string SKSVG()
    {
        if (Anchors.Count < 3)
        {
            return "";
        }
        SKPath path = ToSKPath();
        return path.ToSvgPathData();
    }

    public void AlignAllHandles()
    {
        foreach (Anchor a in Anchors)
        {
            a.AlignHandles();
        }
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

    public void MakeCounterClockWise()
    {
        if (IsClockwise())
        {
            ReverseShape();
        }
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
        Anchor a = new();
        a.Init(pos, this);

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
        // MakeClockwise();
        Finished = true;
        AlignAllHandles();
    }

    public Segment[] SegList()
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
    public int[] FlatI()
    {
        int[] flatPositions = [];
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
            flatPositions = [.. flatPositions, .. (int[])[(int)start.X, (int)start.Y, (int)h1.X, (int)h1.Y, (int)h2.X, (int)h2.Y, (int)end.X, (int)end.Y]];
        }
        return flatPositions;
    }

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
        return Player.BetterVectorBoolean(FlatI(), shapeB.FlatI(), negative);
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
        return RoundCornersSegments(SegList(), rounded, cornerSize, roundness);
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
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,                        // ➊ serialize public fields
        ReferenceHandler = ReferenceHandler.Preserve, // ➋ avoid crashes on back-references
        WriteIndented = false
    };

    public static string SaveState()
    {
        return JsonSerializer.Serialize(S, JsonOpts);
    }

    public static void LoadState(string serialized)
    {
        S = JsonSerializer.Deserialize<List<Shape>>(serialized, JsonOpts);
    }

    public static void DeleteShape(Shape s)
    {
        S.Remove(s);
    }

    public static bool IsSegmentListClockwise(Segment[] segs)
    {
        float lowestY = 100000000;
        V2 lowestSeg = segs[0].InPoint;
        int lowestIndex = 0;
        int counter = 0;
        foreach (Segment s in segs)
        {
            if (s.InPoint.Y < lowestY)
            {
                lowestY = s.InPoint.Y;
                lowestSeg = s.InPoint;
                lowestIndex = counter;
            }
            counter += 1;
        }
        V2 A = lowestSeg;
        int aIndex = lowestIndex;

        V2 B = V2.Zero;
        if (aIndex > 0)
        {
            B = segs[aIndex - 1].InPoint;
        }
        else
        {
            B = segs.Last().InPoint;
        }
        V2 C = V2.Zero;
        if (aIndex == segs.Length - 1)
        {
            C = segs[0].InPoint;
        }
        else
        {
            C = segs[aIndex + 1].InPoint;
        }
        V2 BA = B - A;
        V2 CA = C - A;
        return ((BA.X * CA.Y) - (BA.Y * CA.X)) > 0;
    }

    public static Segment[] ReverseSegmentList(Segment[] segs)
    {
        Array.Reverse(segs);
        for (int i = 0; i < segs.Length; i++)
        {
            var s = segs[i];
            (s.InPoint, s.InHandle, s.OutHandle, s.OutPoint) = (s.OutPoint, s.OutHandle, s.InHandle, s.InPoint);
            segs[i] = s;
        }
        return segs;
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

    public static (V2, V2)[] ListOfTangents(Segment[][] segList)
    {
        (V2, V2)[] lot = [];
        foreach (Segment[] segs in segList)
        {
            foreach (Segment seg in segs)
            {
                float[] f = seg.Flat();
                // Player.CurvaturePosition(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]);
            }
        }
        return lot;
    }

    public static Segment[][] MergeShapesSkia()
    {
        if (S.Count < 2)
        {
            if (S[0].Finished)  return [S[0].SegList()];
            else                return [];
        }

        SKPath currentSKPath = S[0].ToSKPath();
        foreach (Shape shape in S[1..])
        {
            SKPathOp currentOperation = shape.Negative ? SKPathOp.Difference : SKPathOp.Union;
            currentSKPath = currentSKPath.Op(shape.ToSKPath(), currentOperation);
        }

        Segment[][] segLists = [];
        // fix winding: positive shapes clockwise, negative shapes counter
        List<SKPath> splitPaths = SplitSKPathToContours(currentSKPath);
        foreach (SKPath p in splitPaths)
        {
            int containsCounter = 0;
            SKPoint testPoint = p.GetPoint(0);
            foreach (SKPath p2 in splitPaths)
            {
                if (p2 != p && p2.Contains(testPoint.X, testPoint.Y))
                {
                    containsCounter += 1;
                }
            }
            Segment[] segs = SKPathToSegmentLists(p)[0];
            if ((containsCounter % 2 == 0 && !IsSegmentListClockwise(segs)) || (containsCounter % 2 != 0 && IsSegmentListClockwise(segs)))
            {
                segs = ReverseSegmentList(segs);
            }
            segLists = [.. segLists, segs];
        }
        // return segLists;
        Segment[][] roundedShapes = [];
        foreach (Segment[] island in segLists)
        {
            roundedShapes = [.. roundedShapes, RoundCornersSegments(island, true, 20, 1.0f)];
        }
        return roundedShapes;
    }

    public static List<SKPath> SplitSKPathToContours(SKPath path)
    {
        List<SKPath> paths = [];
        var iter = path.CreateIterator(true);
        SKPoint[] currentVerb = [new(), new(), new(), new()];
        SKPathVerb currentVerbType = new();
        iter.Next(currentVerb);
        SKPath currentNewPath = new();
        currentNewPath.MoveTo(currentVerb[0]);
        paths.Add(currentNewPath);
        while (true)
        {
            currentVerbType = iter.Next(currentVerb);
            if (currentVerbType == SKPathVerb.Done)
            {
                break;
            }
            else if (currentVerbType == SKPathVerb.Move)
            {
                // GD.Print("VERB is move");
                currentNewPath = new();
                currentNewPath.MoveTo(currentVerb[0]);
                paths.Add(currentNewPath);
            }
            else if (currentVerbType == SKPathVerb.Line)
            {
                // GD.Print("VERB is line");
                currentNewPath.LineTo(currentVerb[1]);
            }
            else if (currentVerbType == SKPathVerb.Quad)
            {
                currentNewPath.QuadTo(currentVerb[1], currentVerb[2]);
                // GD.Print("VERB is quad");
            }
            else if (currentVerbType == SKPathVerb.Cubic)
            {
                currentNewPath.CubicTo(currentVerb[1], currentVerb[2], currentVerb[3]);
            }
        }
        return paths;
    }

    public static Segment[][] SKPathToSegmentLists(SKPath skpath)
    {
        Segment[][] outSegmentLists = [];
        Segment[] currentSegList = [];
        var iter = skpath.CreateIterator(true);
        SKPoint[] currentVerb = [new(), new(), new(), new()];
        SKPathVerb currentVerbType = new();
        while (true)
        {
            currentVerbType = iter.Next(currentVerb);
            if (currentVerbType == SKPathVerb.Done)
            {
                break;
            }
            else if (currentVerbType == SKPathVerb.Move)
            {
                // GD.Print("VERB is move");
                if (currentSegList.Length > 0)
                {
                    outSegmentLists = [.. outSegmentLists, currentSegList];
                    currentSegList = [];
                }
            }
            else if (currentVerbType == SKPathVerb.Line)
            {
                // GD.Print("VERB is line");
                Segment s = new(currentVerb[0], currentVerb[0], currentVerb[1], currentVerb[1]);
                currentSegList = [.. currentSegList, s];
            }
            else if (currentVerbType == SKPathVerb.Quad)
            {
                // GD.Print("VERB is quad");
                Segment s = new(currentVerb[0], currentVerb[1], currentVerb[2], currentVerb[2]);
                currentSegList = [.. currentSegList, s];
            }
            else if (currentVerbType == SKPathVerb.Cubic)
            {
                // GD.Print("VERB is cubic");
                Segment s = new(currentVerb[0], currentVerb[1], currentVerb[2], currentVerb[3]);
                currentSegList = [.. currentSegList, s];
            }
        }
        outSegmentLists = [.. outSegmentLists, currentSegList];
        return outSegmentLists;
    }

    public static Segment[] RoundCornersSegments(Segment[] originalShape, bool rounded = false, int cornerSize = 80, float roundness = 1.0f)
    {
        roundness *= 0.7f;
        List<Segment> roundedSegments = [];
        List<(Segment, V2, V2)> trimmedSegList = [];
        List<float> cornerSizeList = [];

        for (int i = 0; i < originalShape.Length; i++)
        {
            float cornerSizeToUse = cornerSize;
            float segLength = originalShape[i].LengthCubic();
            if (segLength < 2 * cornerSize)
            {
                cornerSizeToUse = segLength / 2.4f;
            }
            cornerSizeList.Add(cornerSizeToUse);
        }

        for (int i = 0; i < originalShape.Length; i++)
        {
            Segment seg = originalShape[i];
            float segLength = seg.LengthCubic();
            float amountToTrim = MathF.Min((segLength > 1e-4f) ? cornerSizeList[i] / segLength : 0f, 0.49f);
            if (!float.IsFinite(amountToTrim)) amountToTrim = 0f;
            if (cornerSizeList[i] < 3)
            {
                trimmedSegList.Add((seg, seg.InPoint, seg.OutPoint));
            }
            else
            {
                trimmedSegList.Add(seg.TrimmedTangentAndPos(amountToTrim, 1.0f - amountToTrim));
            }
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
            var t1 = SafeNormalize(tanAngle);
            var t2 = SafeNormalize(tanPos2);
            float cosA = Math.Clamp(V2.Dot(t1, -t2), -1f, 1f);
            float angle = Mathf.Acos(cosA);
            roundedSegments.Add(trimmed);
            if (true && cornerSizeList[index] >= 3 && r >= 3)
            {
                const float MIN_ANGLE = 2f * (MathF.PI / 180f);
                const float MAX_ANGLE = 178f * (MathF.PI / 180f);

                if (angle < MIN_ANGLE || angle > MAX_ANGLE || t1 == V2.Zero || t2 == V2.Zero)
                {
                    roundedSegments.Add(new(trimmed.OutPoint, trimmed.OutPoint, trimmed2.InPoint, trimmed2.InPoint));
                }
                else
                {
                    // V2 newPos1 = trimmed.OutPoint + t1 * 100;
                    // V2 newPos2 = trimmed2.InPoint - t2 * 100;
                    V2 newPos1 = trimmed.OutPoint + t1 * (roundness * cornerSizeList[index]);
                    V2 newPos2 = trimmed2.InPoint - t2 * (roundness * r);
                    roundedSegments.Add(new(trimmed.OutPoint, newPos1, newPos2, trimmed2.InPoint));
                }
            }
            else
            {
                roundedSegments.Add(new(trimmed.OutPoint, trimmed.OutPoint, trimmed2.InPoint, trimmed2.InPoint));
            }
            index += 1;
        }
        return roundedSegments.ToArray();
    }

    static V2 SafeNormalize(V2 v, float eps = 1e-6f)
    {
        float m = v.Length();
        return (m > eps) ? v / m : V2.Zero;
    }
}
