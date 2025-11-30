using Godot;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
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
using System.Runtime.Intrinsics;
using System.Reflection.Metadata;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;


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
    private float? Length = null;

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
        if (Length != null)
        {
            return (float)Length;
        }
        else
        {
            float[] f = Flat();
            Length = Player.LengthCubic(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7]);
            return (float)Length;
        }
    }

    /// <summary>trims segment and returns trimmed segment with tangent unit vectors for start and end of trimmed</summary><returns></returns>
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
    public bool Locked = true;
    public bool Selected = false;
    public bool IsInHandle = false;
    public Anchor AdjacentAnchor = null!;
    public float DistanceFromAnchor = 2;
    public float Angle = 0;

    public Handle()
    {
    }
    
    public HandlePointer Pointer()
    {
        return new(AdjacentAnchor, IsInHandle);
    }

    public Handle GetAnchorSibling()
    {
        if (IsInHandle)
        {
            return AdjacentAnchor.OutHandle;
        }
        else
        {
            return AdjacentAnchor.InHandle;
        }

    }

    public Handle GetSegmentSibling()
    {
        if (IsInHandle)
        {
            return AdjacentAnchor.PreviousAnchor().OutHandle;
        }
        else
        {
            return AdjacentAnchor.NextAnchor().InHandle;
        }
    }

    public void MakeStraight(bool trailingSegment = false)
    {
        Handle sibling = GetSegmentSibling();

        Type = SegmentType.Straight;
        DistanceFromAnchor = 1;
        sibling.Type = SegmentType.Straight;
        sibling.DistanceFromAnchor = 1;

    }

    public void MakeCubic(bool trailingSegment = false)
    {
        Handle sibling = GetSegmentSibling();

        Type = SegmentType.Cubic;
        DistanceFromAnchor = 100;
        sibling.Type = SegmentType.Cubic;
        sibling.DistanceFromAnchor = 100;
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
    public SubdivisionType SubdivType = SubdivisionType.None;
    public Handle InHandle = null!;
    public Handle OutHandle = null!;
    public bool Sselected = false;
    public bool Broken = false;
    public int anchorRounding = 20;
    public int intersectionRounding = 1;

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
                PreviousAnchor().OutHandle.Type = SegmentType.Cubic;
                PreviousAnchor().OutHandle.DistanceFromAnchor = 100;
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
        OutHandle.Init(this, false);
    }

    public void ReverseHandles()
    {
        (OutHandle, InHandle) = (InHandle, OutHandle);
    }

    public void AutoHandles()
    {
        // float ang1 = MathF.Acos(V2.Dot(Position, NextAnchor().Position) / (NextAnchor().Position.Length() * Position.Length()));
        // float ang2 = MathF.Acos(V2.Dot(Position, PreviousAnchor().Position) / (PreviousAnchor().Position.Length() * Position.Length()));
        // float ang1 = Fun.Vtv(Position).AngleToPoint(Fun.Vtv(NextAnchor().Position));
        // float ang2 = Fun.Vtv(Position).AngleToPoint(Fun.Vtv(PreviousAnchor().Position));
        // if (ang1 < 0) ang1 = (2 * MathF.PI) - ang1;
        // if (ang2 < 0) ang2 = (2 * MathF.PI) - ang2;
        // ang1 %= 2 * MathF.PI;
        // ang2 %= 2 * MathF.PI;
        // float avAng = (ang1 + ang2) / 2f + (.5f * MathF.PI);
         
        //normalize!!!
        GV2 v1 = Fun.Vtv(V2.Normalize(Position - PreviousAnchor().Position));
        GV2 v2 = Fun.Vtv(V2.Normalize(Position - NextAnchor().Position));
        GV2 rightV = ((v1 + v2) / 2f).Rotated(0.5f * MathF.PI);
        float v1a = v1.Angle();
        float v2a = v2.Angle();
        if (v1a < 0) v1a = (2 * MathF.PI) + v1a;
        if (v2a < 0) v2a = (2 * MathF.PI) + v2a;
        float avAng = ((v1 + v2) / 2f).Angle();
        if (avAng < 0) avAng = (2 * MathF.PI) + avAng;
        avAng += (0.5f * MathF.PI);

        float inhAng = 0;
        float outhAng = 0;
        if (v1.AngleTo(v2) > 0)
        {
            inhAng = avAng;
            outhAng = avAng + MathF.PI;
        }
        else
        {
            inhAng = avAng + MathF.PI;
            outhAng = avAng;
        }
        float maxLength = 200;
        if (InHandle.Type == SegmentType.Cubic)
        {
            InHandle.Angle = inhAng;
            InHandle.DistanceFromAnchor = MathF.Min(maxLength,Fun.Vtv(Position).DistanceTo(Fun.Vtv(PreviousAnchor().Position)) / 3f);
        }

        if (OutHandle.Type == SegmentType.Cubic)
        {
            OutHandle.Angle = outhAng;
            OutHandle.DistanceFromAnchor = MathF.Min(maxLength, Fun.Vtv(Position).DistanceTo(Fun.Vtv(NextAnchor().Position)) / 3f);
        }
    }

    public void AlignHandles(bool toOutHandle = false)
    {
        if (OutHandle.Type == SegmentType.Straight)
        {
            OutHandle.Angle = Fun.Vtv(Position).DirectionTo(Fun.Vtv(NextAnchor().Position)).Angle();
        }
        if (InHandle.Type == SegmentType.Straight)
        {
            InHandle.Angle = Fun.Vtv(Position).DirectionTo(Fun.Vtv(PreviousAnchor().Position)).Angle();
        }
        float epsilon = (2 * MathF.PI) / 100;
        if (toOutHandle)
        {
            if (InHandle.Type == SegmentType.Cubic)
            {
                if (!InHandle.Locked) return;
                else
                {
                    InHandle.Angle = OutHandle.Angle + MathF.PI;
                }
            }

            if (OutHandle.Type == SegmentType.Cubic)
            {
                if (!OutHandle.Locked) return;
                else
                {
                    OutHandle.Angle = InHandle.Angle + MathF.PI;
                }
            }
        }
        else
        {
            if (OutHandle.Type == SegmentType.Cubic)
            {
                if (!OutHandle.Locked) return;
                else
                {
                    OutHandle.Angle = InHandle.Angle + MathF.PI;
                }
            }
            if (InHandle.Type == SegmentType.Cubic)
            {
                if (!InHandle.Locked) return;
                else
                {
                    InHandle.Angle = OutHandle.Angle + MathF.PI;
                }
            }
        }
        float tweePi = MathF.PI * 2;
        if (InHandle.Angle < tweePi * -2)
        {
            InHandle.Angle = (tweePi) + (InHandle.Angle % -tweePi);
        }
        if (InHandle.Angle > tweePi)
        {
            InHandle.Angle = InHandle.Angle % tweePi;
        }
        if (OutHandle.Angle < tweePi * -2)
        {
            OutHandle.Angle = (tweePi) + (OutHandle.Angle % -tweePi);
        }
        if (OutHandle.Angle > tweePi)
        {
            OutHandle.Angle = OutHandle.Angle % tweePi;
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

    public bool AnchorsCached = true;
    public Segment[] Segments = [];
    public Segment[] RoundedSegments = [];
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

    public virtual SKPath ToSKPath()
    {
        SKPath retPath = new();
        retPath.MoveTo(Anchors[0].Position);
        Segment[] segs = SegList();
        foreach (Segment s in segs)
        {
            retPath.CubicTo(s.InHandle, s.OutHandle, s.OutPoint);
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

    public void AnchorsChanged()
    {
        AnchorsCached = false;
    }

    public override string ToString()
    {
        string ts = "bez: [";
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

    public virtual void AddAnchor(V2 pos, bool makeCubic = false, Anchor? insertAfter = null, bool broken = false)
    {
        Anchor a = new();
        a.Init(pos, this);
        a.Broken = broken;
        AnchorsChanged();
        a.intersectionRounding = 1 + Anchors.Count * 20; 

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
        AnchorsChanged();
    }

    public virtual Segment[] SegList(bool rounded = false)
    {
        if (AnchorsCached)
        {
            if (rounded) return RoundedSegments;
            else         return Segments;
        }

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

        Segment[] roundedSegments = Shapes.RoundCornersSegments(segments, CornerRoundings());
        Segments = segments;
        RoundedSegments = roundedSegments;
        AnchorsCached = true;

        if (rounded) return roundedSegments;
        else return segments;
    }

    public virtual int[] CornerRoundings()
    {
        int[] cornerRoundings = [];
        foreach (Anchor a in Anchors)
        {
            cornerRoundings = [.. cornerRoundings, a.anchorRounding];
        }
        return cornerRoundings;
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
}

public class HyperbezierShape: Shape
{
    public int[] BeziersPerAnchorPair = [];
    public override Segment[] SegList(bool rounded = false)
    {
        if (AnchorsCached)
        {
            if (rounded) return RoundedSegments;
            else         return Segments;
        }

        (Segment[], int[]) segmentsAndAmounts = Player.AnchorsToHyperBeziers(Anchors);
        BeziersPerAnchorPair = segmentsAndAmounts.Item2;

        Segment[] roundedSegments = Shapes.RoundCornersSegments(segmentsAndAmounts.Item1, CornerRoundings(), BeziersPerAnchorPair);
        Segments = segmentsAndAmounts.Item1;
        RoundedSegments = roundedSegments;
        AnchorsCached = true;
        if (rounded) return roundedSegments;
        else return segmentsAndAmounts.Item1;
    }

    public override string ToString()
    {
        string ts = "hyperbez: [";
        int counter = 0;
        foreach (var a in Anchors)
        {
            ts += counter + ": " + a.ToString() + " ";
            counter += 1;
        }
        ts += "]";
        return ts;
    }
}

public static class Shapes
{
    public static List<Shape> S = [];
    public static Shape NewShape()
    {
        Shape s = new HyperbezierShape();
        // Shape s = new();
        S.Add(s);
        return s;
    }
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,                        // serialize public fields
        ReferenceHandler = ReferenceHandler.Preserve, // avoid crashes on back-references
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

    public static Dictionary<V2, int> GetRoundingPerIntersection(Shape shape, Segment[][] segLists)
    {
        Dictionary<V2, int> roundingDict = [];
        List<(int roundedShapeBezIndex, V2)> intersections = [];
        foreach (Segment[] island in segLists)
        {
            intersections.AddRange(Player.ShapeShapeIntersections(island, shape.SegList(true)));
        }

        foreach ((int bezIndex, V2 intersectCoord) in intersections)
        {
            // bezindex belongs to anchor TODO
            Anchor anchorThatIntersectingBezierBelongsTo = shape.Anchors[2];
            roundingDict[intersectCoord] = anchorThatIntersectingBezierBelongsTo.intersectionRounding;
        }
        return roundingDict;
    }

    public static Segment[][] MergeShapesSkia()
    {
        // beoogd is shape -> rounded shape -> merge w stack -> round merges - |
        //           ^---                                              <------ |
        // voor elk segment van toMerge wordt gecheckt waar dit seg intersect met elke contour van de stack
        // schrijf coordinaten weg naar dict als keys met segRound als value
        // ga na het mergen alle punten langs, check of ze in de buurt liggen van coords in de dict, zoja round het met de segRound value
        Segment[] firstShapeRounded = [];
        if (S[0].Finished) firstShapeRounded = S[0].SegList(true);

        if (S.Count < 2) return firstShapeRounded.Length > 0 ? [firstShapeRounded] : [];

        SKPath currentSKPath = SegmentListsToSKPath([firstShapeRounded]);
        Segment[][] currentSegLists = SKPathToSegmentLists(currentSKPath);
        foreach (Shape shape in S[1..])
        {
            if (!shape.Finished) continue;
            SKPathOp currentOperation = shape.Negative ? SKPathOp.Difference : SKPathOp.Union;
            Segment[] roundedShape = shape.SegList(true);

            Dictionary<V2, int> intersectionDict = GetRoundingPerIntersection(shape, currentSegLists);

            SKPath pathToMerge = SegmentListsToSKPath([roundedShape]);
            currentSKPath = currentSKPath.Op(pathToMerge, currentOperation);

            currentSegLists = SKPathToSegmentLists(currentSKPath);
            for (int i = 0; i < currentSegLists.Length; i++)
            {
                currentSegLists[i] = RoundIntersections(currentSegLists[i], intersectionDict);
            }
            currentSKPath = SegmentListsToSKPath(currentSegLists);
        }
        return SKPathToSegmentLists(currentSKPath);

        Segment[][] segLists = [];
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
        return segLists;
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
                currentNewPath = new();
                currentNewPath.MoveTo(currentVerb[0]);
                paths.Add(currentNewPath);
            }
            else if (currentVerbType == SKPathVerb.Line)
            {
                if ((new V2(currentVerb[0].X, currentVerb[0].Y) - new V2(currentVerb[1].X, currentVerb[1].Y)).Length() > 5)
                {
                    currentNewPath.LineTo(currentVerb[1]);
                }
            }
            else if (currentVerbType == SKPathVerb.Quad)
            {
                currentNewPath.QuadTo(currentVerb[1], currentVerb[2]);
            }
            else if (currentVerbType == SKPathVerb.Cubic)
            {
                currentNewPath.CubicTo(currentVerb[1], currentVerb[2], currentVerb[3]);
            }
        }
        return paths;
    }

    public static SKPath SegmentListsToSKPath(Segment[][] segLists)
    {
        SKPath retPath = new();
        foreach (Segment[] segs in segLists)
        {
            Segment Start = segs[0];
            retPath.MoveTo(Start.InPoint);
            foreach (Segment s in segs)
            {
                retPath.CubicTo(s.InHandle, s.OutHandle, s.OutPoint);
            }
            retPath.Close();
        }
        return retPath;
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
                if (currentSegList.Length > 0)
                {
                    outSegmentLists = [.. outSegmentLists, currentSegList];
                    currentSegList = [];
                }
            }
            else if (currentVerbType == SKPathVerb.Line)
            {
                Segment s = new(currentVerb[0], currentVerb[0], currentVerb[1], currentVerb[1]);
                currentSegList = [.. currentSegList, s];
            }
            else if (currentVerbType == SKPathVerb.Quad)
            {
                Segment s = new(currentVerb[0], currentVerb[1], currentVerb[2], currentVerb[2]);
                currentSegList = [.. currentSegList, s];
            }
            else if (currentVerbType == SKPathVerb.Cubic)
            {
                Segment s = new(currentVerb[0], currentVerb[1], currentVerb[2], currentVerb[3]);
                currentSegList = [.. currentSegList, s];
            }
        }
        outSegmentLists = [.. outSegmentLists, currentSegList];
        return outSegmentLists;
    }


    public static Segment[] RoundIntersections(Segment[] originalShape, Dictionary<V2, int> roundingPerIntersections)
    {
        int[] cornerSizes = [];
        int[] beziersToSkip = [];
        int beziersBetween = 0;
        Segment firstSegment = originalShape[0];
        foreach ((V2 intersection, int rounding) in roundingPerIntersections)
        {
            if (V2.Distance(firstSegment.InPoint, intersection) < 5)
            {
                cornerSizes = [.. cornerSizes, rounding];
                break;
            }
        }
        if (cornerSizes.Length == 0) cornerSizes = [0];
        beziersBetween += 1;

        for (int currentIndex = 1; currentIndex < originalShape.Length; currentIndex++)
        {
            Segment currentSegment = originalShape[currentIndex];
            foreach ((V2 intersection, int rounding) in roundingPerIntersections)
            {
                if (V2.Distance(currentSegment.InPoint, intersection) < 5)
                {
                    beziersToSkip = [.. beziersToSkip, beziersBetween];
                    cornerSizes = [.. cornerSizes, rounding];
                    beziersBetween = 0;
                    break;
                }
            }
            beziersBetween += 1;
        }
        beziersToSkip = [.. beziersToSkip, beziersBetween];
        return RoundCornersSegments(originalShape, cornerSizes, beziersToSkip);
    }

    public static (Segment[], V2, V2) TrimmedSegmentGroup(Segment[] segmentGroup, float inLength, float outLength)
    { 
        V2 inTan = V2.Zero;
        V2 outTan = V2.Zero;
        List<Segment> trimmedFromFront = [];
        List<Segment> trimmedFromBack = [];
        float trimmedSoFar = 0;
        for (int index = 0; index < segmentGroup.Length; index++)
        {
            Segment currentSeg = segmentGroup[index];
            float segLength = currentSeg.LengthCubic();
            if (trimmedSoFar + segLength > inLength)
            {
                float remaining = inLength - trimmedSoFar;
                (Segment trimmed, inTan, V2 _) = currentSeg.TrimmedTangentAndPos(remaining / segLength, 1.0f);
                trimmedFromFront.Add(trimmed);
                trimmedFromFront.AddRange(segmentGroup[(index+1)..]);
                break;
            }
            trimmedSoFar += segLength;
        }
        trimmedSoFar = 0;
        trimmedFromFront.Reverse();
        for (int index = 0; index < trimmedFromFront.Count; index++)
        {
            Segment currentSeg = trimmedFromFront[index];
            float segLength = currentSeg.LengthCubic();
            if (trimmedSoFar + segLength > outLength)
            {
                float remaining = outLength - trimmedSoFar;
                (Segment trimmed, V2 _, outTan) = currentSeg.TrimmedTangentAndPos(0.0f, 1.0f - (remaining / segLength));
                trimmedFromBack.Add(trimmed);
                trimmedFromBack.AddRange(trimmedFromFront[(index+1)..]);
                break;
            }
            trimmedSoFar += segLength;
        }
        trimmedFromBack.Reverse();

        return ([.. trimmedFromBack], inTan, outTan);
    }

    public static float LengthOfSegmentGroup(Segment[] segmentGroup)
    {
        float totalLength = 0;
        foreach (Segment seg in segmentGroup)
        {
            totalLength += seg.LengthCubic();
        }
        return totalLength;
    }

    public static (float start, float end) GetCornerSizes(float length, float cornerSizeIn, float cornerSizeOut)
    {
        float start = 0;
        float end = 0;
        float margin = length / 20;
        float half = length / 2;
        start = MathF.Min(cornerSizeIn, MathF.Max(half - margin, length - (cornerSizeOut + margin)));
        end = MathF.Min(cornerSizeOut, MathF.Max(half - margin, length - (cornerSizeIn + margin)));
        return (start, end);
    }


    public static Segment[] RoundCornersSegments(Segment[] originalShape, int[] cornerSizes, int[]? beziersToSkip = null, bool rounded = true)
    {
        float roundness = 0.7f;
        const float MIN_ANGLE = 2f * (MathF.PI / 180f);
        const float MAX_ANGLE = 178f * (MathF.PI / 180f);
        float minCornerSize = 3f;

        List<(Segment[] segments, V2 inTan, V2 outTan)> trimmedAndTangentList = [];
        List<(float inCorner, float outCorner)> cornerSizeList = [];
        int currentSegIndex = 0;

        // loop trough all segments, trim appropriately
        for (int currentIndex = 0; currentIndex < cornerSizes.Length; currentIndex++)
        {
            int nextIndex = currentIndex == cornerSizes.Length - 1 ? 0 : currentIndex + 1;
            float startSize = cornerSizes[currentIndex];
            float endSize = cornerSizes[nextIndex];
            int SBCount = beziersToSkip != null ? beziersToSkip[currentIndex] : 1;
            Segment[] segsBetween = originalShape[currentSegIndex..(currentSegIndex + SBCount)];

            if (startSize == 0 && endSize == 0)
            {
                trimmedAndTangentList.Add((segsBetween, V2.Zero, V2.Zero));
                cornerSizeList.Add((0, 0));
                continue;
            }

            float lengthBetween = LengthOfSegmentGroup(segsBetween);
            (float inCorner, float outCorner) = GetCornerSizes(lengthBetween, startSize, endSize);
            trimmedAndTangentList.Add(TrimmedSegmentGroup(segsBetween, inCorner, outCorner));

            cornerSizeList.Add((inCorner, outCorner));
            currentSegIndex += SBCount;
        }

        List<Segment> roundedSegments = [];
        for (int currentIndex = 0; currentIndex < trimmedAndTangentList.Count; currentIndex++)
        {
            int previousIndex = currentIndex == 0 ? trimmedAndTangentList.Count - 1 : currentIndex - 1;
            Segment[] trimmed = trimmedAndTangentList[currentIndex].segments;
            Segment[] prevTrimmed = trimmedAndTangentList[previousIndex].segments;
            var currentCorners = cornerSizeList[currentIndex];
            var previousCorners = cornerSizeList[previousIndex];
            V2 startTan = trimmedAndTangentList[currentIndex].inTan;
            V2 endTan = trimmedAndTangentList[currentIndex].outTan;
            V2 prevEndTan = trimmedAndTangentList[previousIndex].outTan;

            if (currentCorners.inCorner == 0 && previousCorners.outCorner == 0)
            {
                roundedSegments.AddRange(trimmed);
                continue;
            }

            (V2 inPoint, V2 outPoint) = (prevTrimmed.Last().OutPoint, trimmed.First().InPoint);
            float angle = Mathf.Acos(Math.Clamp(V2.Dot(prevEndTan, -startTan), -1f, 1f));
            if (
                rounded
                && cornerSizeList[currentIndex].inCorner >= minCornerSize
                && angle >= MIN_ANGLE
                && angle <= MAX_ANGLE
                && endTan != V2.Zero
                && startTan != V2.Zero
            )
            {
                V2 inHandlePos  = inPoint  + prevEndTan * (roundness * previousCorners.outCorner);
                V2 outHandlePos = outPoint - startTan   * (roundness * currentCorners.inCorner);
                roundedSegments.Add(new(inPoint, inHandlePos, outHandlePos, outPoint));
            }
            else
            {
                roundedSegments.Add(
                    new(inPoint, inPoint, outPoint, outPoint));
            }

            roundedSegments.AddRange(trimmed);
        }
        return [.. roundedSegments];
    }

}
