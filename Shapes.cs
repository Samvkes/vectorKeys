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

public struct Segment(V2 inPoint, V2 outPoint, V2 inHandle, V2 outHandle)
{
    V2 InPoint = inPoint;
    V2 OutPoint = outPoint;
    V2 InHandle = inHandle;
    V2 OutHandle = outHandle;
}

public class Handle
{
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
        if (IsInHandle)
            return AdjacentAnchor.Position + V2.Transform(V2.One * DistanceFromAnchor, Matrix3x2.CreateRotation(Angle));
        else
            return AdjacentAnchor.Position + V2.Transform(V2.One * DistanceFromAnchor, Matrix3x2.CreateRotation(-Angle));
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

    public override string ToString()
    {
        string ts = "";
        ts += "[" + SelectionIndex + ": " + Position.X + "  " + Position.Y + "]";
        return ts;
    }
}


public class Shape
{
    public bool Selected = false;
    public List<Anchor> Anchors = [];
    // public bool Closed = false;

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
        V2 B = Anchors[aIndex - 1].Position;
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

    public void Close()
    {
        if (Anchors.Count < 3)
        {
            Delete();
            return;
        }
        MakeClockwise();
    }

    public Segment[] Segments()
    {
        Segment[] segments = new Segment[Anchors.Count];
        for (int i = 0; i < Anchors.Count; i+=1)
        {

        }
        return segments;
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
        // toReturn.Close();
        return toReturn;
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













