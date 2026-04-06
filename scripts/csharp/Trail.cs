using Godot;
using System;
using Vectordrawing;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;

public partial class Trail : Line2D
{
    int Length = 6;
    GV2 Point; 
    Sprite2D Cursor = null!;
    Label Lab = null!;
    Sprite2D Circle = null!;
    Timer TrailTimer = null!;

    public override void _Ready()
    {
        Cursor = (Sprite2D)GetParent();
        Lab = (Label)Cursor.FindChild("Label");
        Circle = (Sprite2D)Cursor.FindChild("Circle");
        TrailTimer = new();
        AddChild(TrailTimer);
        TrailTimer.WaitTime = 0.03f;
        TrailTimer.Timeout += TrailStuff;
        TrailTimer.Start();

    }

    public override void _Process(double delta)
    {
        GlobalPosition = new(0,0);
        GlobalRotation = 0;

        Point = Cursor.GlobalPosition;
        if (GetPointCount() > 0 && GetPointPosition(0).DistanceTo(Point) < 150)
        {
            Visible = false;
            // Circle.Visible = true;
            // Lab.Visible = true;
        }
        else
        {
            Visible = true;
            // Circle.Visible = false;
            // Lab.Visible = false;
        }
    }

    public void TrailStuff()
    {
        AddPoint(Point);
        while(GetPointCount() > Length)
        {
            RemovePoint(0);
        }
    }
}
