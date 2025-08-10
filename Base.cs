
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

public enum Mode
{
    Editing,
    Selecting,
    Previewing,
}

public partial class Base : Node
{
    const int GridSize = 16;
    const int FarMoveBorder = 2 * GridSize;
    const float ValidHoldTime = .2f;
    const int DefaultFontSize = 14;
    static readonly V2 Borders = new(0, 0);
    static readonly V2 WindowSize = new V2(96, 112) * GridSize;
    static readonly V2 Origin = WindowSize / 2f;
    static readonly Color BackgroundColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 70 / 100f);
    static readonly Color SelectingColor = Color.FromOkHsl(63 / 359f, 9 / 100f, 66 / 100f);
    static readonly Color PreviewColor = Color.FromOkHsl(10 / 359f, 75 / 100f, 90 / 100f);
    // static Manager Manager = null!; 
    // static Font LightFont = null!;
    // static Font MediumFont = null!;
    // static Font BoldFont = null!;
    static Font LightFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Light.otf");
    static Font MediumFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Medium.otf");
    static Font BoldFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Bold.otf");
    static Manager Manager = GD.Load<PackedScene>("res://Manager.tscn").Instantiate<Manager>();

    float GridModifier = 8;
    float Zoom = 1f;
    float MovementHeldTime = 0f;
    float VisualGridSize = GridSize;
    Image Im = new();
    Texture Tex = null!;
    int FrameCounter = 0;
    V2 MarkerPos = Origin;
    Shape CurrentShape = Shapes.NewShape();
    List<string> KeysPressed = new();
    List<float> DeltaTimeList = new();
    bool StickyBorder = true;
    bool CanMoveAgain = true;
    bool CanUndoAgain = true;
    Timer UndoTimer = new();
    Timer MovementTimer = new();

    public override void _Ready()
    {
        AddChild(Manager);
        AddChild(UndoTimer);
        AddChild(MovementTimer);

        MovementTimer.WaitTime = 0.01f;
        MovementTimer.OneShot = true;
        MovementTimer.Timeout += MovementTimerTimeout;

        UndoTimer.WaitTime = 0.1f;
        UndoTimer.OneShot = true;
        UndoTimer.Timeout += DoUndoRedo;
        // FIXME
        Tex = GetChild<Texture>(1);
        GetWindow().Size = new Vector2I((int)WindowSize.X, (int)WindowSize.Y);
    }

    public override void _Process(double delta)
    {
        if (DeltaTimeList.Count() > 3)
        {
            DeltaTimeList.RemoveAt(0);
        }
        DeltaTimeList.Add((float)delta);
        float totalDelta = 0f;
        float totalPoints = 0f;
        foreach (float t in DeltaTimeList)
        {
            totalDelta += t;
        }
        foreach (Shape s in Shapes.S)
        {
            totalPoints += s.Anchors.Count;
        }
        // FIXME
        GetChild<RichTextLabel>(0).Text = (Mathf.Round(1.0 / (totalDelta / DeltaTimeList.Count()))).ToString() + " : " + totalPoints.ToString();

        if (Zoom <= 1)
        {
            
        }
    }

    public void DoUndoRedo()
    { 

    }

    public void MovementTimerTimeout()
    {

    }
}