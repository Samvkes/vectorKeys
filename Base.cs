using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using Vectordrawing;
using System.Numerics;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Data;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using System.Diagnostics.Metrics;
using System.ComponentModel;
using System.Diagnostics;
using Snl = Vectordrawing.StringNamesList;
using System.Text;
using System.Net.Sockets;
using System.Globalization;
using System.Xml.XPath;

namespace Vectordrawing;


public enum Mode
{
    Editing,
    Selecting,
    Previewing,
}

public enum Focus
{
    Anchor,
    Handle,
}

public partial class Base : Node2D
{
    const int GridSize = 16;
    const int FarMoveBorder = 2 * GridSize;
    const float ValidHoldTime = .2f;
    const int DefaultFontSize = 14;
    const int RotationStepSizeDegrees = 15;
    public static readonly V2 WindowSize = new V2(96, 112) * GridSize;
    public static readonly V2 Origin = WindowSize / 2f;
    static readonly (V2, V2) Borders = (V2.Zero, WindowSize);
    static readonly Color GridColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.05f);
    static readonly Color GuidesColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.1f);
    static readonly Color BackgroundColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 70 / 100f);
    static readonly Color SelectingColor = Color.FromOkHsl(63 / 359f, 9 / 100f, 66 / 100f);
    static readonly Color PreviewColor = Color.FromOkHsl(10 / 359f, 75 / 100f, 90 / 100f);
    static Font LightFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Light.otf");
    static Font MediumFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Medium.otf");
    static Font BoldFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Bold.otf");
    static Manager Manager = GD.Load<PackedScene>("res://Manager.tscn").Instantiate<Manager>();

    float GridModifier = 8;
    public float Zoom = 1f;
    float MovementHeldTime = 0f;
    float VisualGridSize = GridSize;
    Image Im = new();
    Sprite2D Tex = null!;
    int FrameCounter = 0;
    float Counter = 0;
    V2 MarkerPos = Origin;
    Shape CurrentShape = Shapes.NewShape();
    List<float> DeltaTimeList = new();
    bool StickyGuide = true;
    bool CanMoveAgain = true;
    bool CanUndoAgain = true;
    bool SelectingInHandle = true;
    static Mode CurrentMode = Mode.Editing;
    static Focus CurrentFocus = Focus.Anchor;
    Timer UndoTimer = new();
    Timer MovementTimer = new();
    Sprite2D Cursor = null!;
    RichTextLabel FpsLabel = null!;
    ColorRect Background = null!;

    List<Anchor> SelectedAnchors= new();
    List<HandlePointer> SelectedHandles = new();


    public override void _Ready()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        AddChild(Manager);
        AddChild(UndoTimer);
        AddChild(MovementTimer);

        MovementTimer.WaitTime = 0.01f;
        MovementTimer.OneShot = true;
        MovementTimer.Timeout += MovementTimerTimeout;

        UndoTimer.WaitTime = 0.1f;
        UndoTimer.OneShot = true;
        UndoTimer.Timeout += DoUndoRedo;

        Tex = GetParent().GetNode<Sprite2D>("Tex");
        Background = GetParent().GetNode<ColorRect>("Background");
        Cursor = GetParent().GetNode<Sprite2D>("Cursor");
        FpsLabel = GetParent().GetNode<RichTextLabel>("FpsLabel");

        GetWindow().Size = new Vector2I((int)WindowSize.X, (int)WindowSize.Y);
        Background.Color = BackgroundColor;
    }

    // 4 functions - svg - drawing
    public override void _Process(double doubleDelta)
    {
        // return;
        float delta = (float)doubleDelta;
        Counter += delta;

        HandleInput(delta);

        if (true)
        {
            if (DeltaTimeList.Count() > 3)
                DeltaTimeList.RemoveAt(0);
            DeltaTimeList.Add(delta);
            float totalDelta = 0f;
            float totalPoints = 0f;
            foreach (float t in DeltaTimeList)
                totalDelta += t;
            FpsLabel.Text = Mathf.Round(1.0 / (totalDelta / DeltaTimeList.Count())).ToString() + " : " + totalPoints.ToString();
            foreach (Shape s in Shapes.S)
                totalPoints += s.Anchors.Count;
        }

        if (Zoom <= 1)
            Cursor.Position = Fun.Vtv(V2.Lerp(Fun.Vtv(Cursor.Position), (MarkerPos * Zoom) + (Origin * (1f - Zoom)), delta * 20f));
        else
            Cursor.Position = Fun.Vtv(Origin);
        Cursor.Scale = Fun.Vtv(V2.Lerp(Fun.Vtv(Cursor.Scale), new V2(.5f, .5f), delta * 10));
        Cursor.Rotation = Cursor.Position.AngleToPoint(Fun.Vtv((MarkerPos * Zoom) + (Origin * (1 - Zoom))));

        // SVG code goes here
        SvgString.ClearString(Zoom, Origin, WindowSize);
        if (CurrentMode == Mode.Editing)
        {
            float firstLine = 256 * 1.5f;
            float secondLine = 256 * 2.5f;
            SvgString.AddLine(new V2(-100, Origin.Y - firstLine), new V2(3000, Origin.Y - firstLine), sWidth:1, sOpacity: .3f);
            SvgString.AddLine(new V2(0, Origin.Y + firstLine), new V2(3000, Origin.Y + firstLine), sWidth:1, sOpacity: .3f);
            SvgString.AddLine(new V2(-100, Origin.Y - secondLine), new V2(3000, Origin.Y - secondLine), sWidth:1, sOpacity: .3f);
            SvgString.AddLine(new V2(0, Origin.Y + secondLine), new V2(3000, Origin.Y + secondLine), sWidth:1, sOpacity: .3f);
            // draw guidelines
        }
        
        // round corners of individual shapes

        if (Shapes.S.Count > 1)
        {
            // boolean merge shapes
            // round corners of transitions between shapes
        }

        // draw unrounded shapes / unmerged shapes for clarity in editing mode?
        // foreach (Shape s in Shapes.S)
        // {
        //     if (s.Anchors.Count <= 1)
        //     {
        //         continue;
        //     }

        //     if (CurrentMode == Mode.Editing && s == CurrentShape)
        //     {
        //         SvgString.SetStyle(Style.ShapeUnchanged);
        //         SvgString.AddSegments(s.Segments());
        //     }

        //     if (CurrentMode == Mode.Previewing) SvgString.SetStyle(Style.ShapePreview);
        //     else
        //     {
        //         if (s.Finished) SvgString.SetStyle(Style.ShapeClosed);
        //         else SvgString.SetStyle(Style.ShapeOpen);
        //     }

        //     Segment[] seg = s.RoundCornersSegments();
        //     SvgString.AddSegments(seg, s == CurrentShape);

        // }
        if (CurrentMode == Mode.Editing) SvgString.SetStyle(Style.ShapeClosed);
        if (CurrentMode == Mode.Previewing) SvgString.SetStyle(Style.ShapePreview);
        if (Shapes.S.Count > 1)
        {
            Segment[][] flatShapes = Shapes.MergeAllShapes();
            foreach (Segment[] flatShape in flatShapes)
            {
                SvgString.AddSegments(flatShape);
            }
        }

        if (CurrentMode == Mode.Editing)
        {
            foreach (Shape s in Shapes.S)
            {
                if (s != CurrentShape) continue;

                foreach (Anchor a in s.Anchors)
                {
                    SvgString.AddCircle(a.Position, 6, fOpacity:0, sOpacity: 1.0f, sWidth:1);
                    // draw anchors
                    // visualize anchor selection
                    // visualize anchor type
                    if (a.InHandle.Type == SegmentType.Cubic)
                    {
                        SvgString.AddLine(a.Position, a.InHandle.Position(), sOpacity: .5f);
                        SvgString.AddLine(a.Position, a.OutHandle.Position(), sOpacity: .5f);
                    }
                    if (a.OutHandle.Type == SegmentType.Cubic)
                    {
                        SvgString.AddCircle(a.InHandle.Position(), 6, fill:"blue", fOpacity: .3f, sOpacity:0f);
                        SvgString.AddCircle(a.OutHandle.Position(), 6, fill:"blue", fOpacity: .3f, sOpacity:0f);
                    }
                    // draw handles
                    // visualize handle selection
                    // visualize handle type
                }
            }
        }

        SvgString.Finish();
        Im.LoadSvgFromString(SvgString.CurrentString);
        Tex.Texture = ImageTexture.CreateFromImage(Im);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (CurrentMode == Mode.Editing)
        {
            foreach (Shape s in Shapes.S)
            {
                int cc = 0;
                foreach (Anchor a in s.Anchors)
                {
                    DrawChar(MediumFont, Fun.Vtv(a.Position), cc.ToString(), 22);
                    cc += 1;
                }
            }
            if (GridModifier >= .5f)
            {
                V2 drawnGridSize = new(Zoom * GridModifier * GridSize);
                GV2 gridAdjustment = new(0, 0);
                if (Zoom > 1)
                {
                    gridAdjustment = new(256, 128);
                }
                for (int i = 0; i < (WindowSize.X / drawnGridSize.X) + 10; i++)
                {
                    DrawLine(
                        new GV2(0, i * drawnGridSize.X - gridAdjustment.X),
                        new GV2(3000, i * drawnGridSize.X - gridAdjustment.X), GridColor, 1.5f, true); 
                }
                for (int i = 0; i < (WindowSize.Y / drawnGridSize.Y) + 10; i++)
                {
                    DrawLine(
                        new GV2(i * drawnGridSize.Y - gridAdjustment.Y, 0),
                        new GV2(i * drawnGridSize.Y - gridAdjustment.Y, 3000), GridColor, 1.5f, true); 
                }
            }

            if (Zoom <= 1)
                {
                    DrawString(LightFont, Fun.Vtv((MarkerPos * Zoom) + (Origin * (1f - Zoom)) + new V2(30, 30)), (MarkerPos/16).ToString(), HorizontalAlignment.Left, -1f, DefaultFontSize + 8);
                }
                else
                {
                    DrawString(LightFont, Fun.Vtv(Origin + new V2(30, 30)), MarkerPos.ToString(), HorizontalAlignment.Left, -1f, DefaultFontSize + 8);
                }

        }
        else if (CurrentMode == Mode.Selecting)
        {
            if (CurrentFocus == Focus.Anchor)
            {
                char c = 'a';

                foreach (Anchor a in CurrentShape.Anchors)
                {
                    Color charColor = Colors.White;
                    Color shadowColor = Colors.Black;
                    if (SelectedAnchors.Contains(a))
                    {
                        charColor = Colors.Yellow;
                        shadowColor = Colors.Red;
                    }
                    // DrawCircle(Fun.Vtv(a.Position), 14, Colors.Black);
                    V2 letterOffset = new(-9, 7);
                    DrawChar(MediumFont, Fun.Vtv(a.Position + letterOffset + new V2(0,3)), c.ToString(), 30, shadowColor);
                    DrawChar(MediumFont, Fun.Vtv(a.Position + letterOffset), c.ToString(), 30, charColor);
                    c = (char)((int)c + 1);
                }
            }
            // draw letters
        }
    }


    public override void _Input(InputEvent ev)
    {
        string at = ev.AsText();
        if (ev is InputEventKey && ev.IsPressed())
        {
            if (at == "Semicolon")
            {
                if (CurrentFocus == Focus.Anchor)
                {
                    SelectedAnchors.Clear();
                }
                else if (CurrentFocus == Focus.Handle)
                {
                    SelectedHandles.Clear();
                }
            }
            else if (CurrentMode == Mode.Selecting && !(at == "Shift+Space" || at == "Space" || at == "Semicolon") && at.Length > 0)
                {
                if (at.StartsWith("Shift"))
                {
                    HandleSelectionText(at.Substr(6, 1));
                }
                else
                {
                    HandleSelectionText(at.ToLower());
                }
            }
        }
    }

    public void HandleSelectionText(string s)
    {
        if (CurrentFocus == Focus.Anchor)
        {
            Anchor a = CurrentShape.GetAnchorFromLabel(s.ToLower());
            if (!SelectedAnchors.Remove(a))
            {
                SelectedAnchors.Add(a);
            }
        }
        else if (CurrentFocus == Focus.Handle)
        {
            HandlePointer hp = new(CurrentShape.GetAnchorFromLabel(s.ToLower()), s[0] < 97);
            if (!SelectedHandles.Remove(hp))
            {
                SelectedHandles.Add(hp);
            }
        }
    }


    public void HiScaling(float delta)
    {
    }

    public void HiMovement(float delta)
    {
        int movementAmount = (int)(GridSize * GridModifier);
        if (MovementTimer.IsStopped())
        {
            MovementTimer.Start();
        }
        V2 movingSelected = new(0, 0);
        if (Input.IsKeyPressed(Key.A))
        {
            movementAmount = 1;
        }

        {
            if (Input.IsActionJustPressed(Snl.left))
            {
                movingSelected.X -= movementAmount;
            }
            if (Input.IsActionJustPressed(Snl.right))
            {
                movingSelected.X += movementAmount;
            }
            if (Input.IsActionJustPressed(Snl.up))
            {
                movingSelected.Y -= movementAmount;
            }
            if (Input.IsActionJustPressed(Snl.down))
            {
                movingSelected.Y += movementAmount;
            }
        }

        {
            bool pressingMovementKey = false;
            if (Input.IsActionPressed(Snl.left))
            {
                pressingMovementKey = true;
                if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                {
                    movingSelected.X -= movementAmount;
                }
                else
                {
                    MovementHeldTime += delta;
                }
            }
            if (Input.IsActionPressed(Snl.right))
            {
                pressingMovementKey = true;
                if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                {
                    movingSelected.X += movementAmount;
                }
                else
                {
                    MovementHeldTime += delta;
                }
            }
            if (Input.IsActionPressed(Snl.up))
            {
                pressingMovementKey = true;
                if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                {
                    movingSelected.Y -= movementAmount;
                }
                else
                {
                    MovementHeldTime += delta;
                }
            }
            if (Input.IsActionPressed(Snl.down))
            {
                pressingMovementKey = true;
                if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                {
                    movingSelected.Y += movementAmount;
                }
                else
                {
                    MovementHeldTime += delta;
                }
            }
            if (!pressingMovementKey)
            {
                MovementHeldTime = 0;
            }
        }

        if (movingSelected != V2.Zero)
        {
            CanMoveAgain = false;
            if (!OnGuide())
            {
                StickyGuide = true;
            }
            if (CurrentFocus == Focus.Anchor && SelectedAnchors.Count > 0)
            {
                foreach (Anchor a in SelectedAnchors)
                {
                    a.Position += movingSelected;
                }
            }
            else if (CurrentFocus == Focus.Handle && SelectedHandles.Count > 0)
            {
                foreach (HandlePointer hp in SelectedHandles)
                {
                    Handle h = hp.h();
                    h.DistanceFromAnchor -= (movingSelected.Y) * .1f;
                    if (Input.IsKeyPressed(Key.Shift))
                    {
                        //TODO stepped rotation
                    }
                    else
                    {
                        h.Angle += movingSelected.X / 100f;
                        Manager.PlaySound("Rattle3.wav", 0.07f, 1.5f, 2.5f);
                    }
                    //TODO align handles?
                }
            }
            else
            {
                MarkerPos += movingSelected;
                //TODO abstract cursor?
                Cursor.Scale = new(.7f, .3f);
            }
        }

        //TODO: make clockwise?
        MarkerPos = V2.Clamp(MarkerPos, Borders.Item1, Borders.Item2);

        if (OnGuide() && StickyGuide)
        {
            MovementTimer.WaitTime = .2f;
            MovementTimer.Start();
            CanMoveAgain = false;
            StickyGuide = false;
        }
        else
        {
            MovementTimer.WaitTime = .01f;
        }

    }

    public void HiPointAdding(float delta)
    {
        if (Input.IsActionJustPressed(Snl.add_new_point))
        {
            if (CurrentShape.Finished)
            {
                CurrentShape = Shapes.NewShape();
            }

            CurrentShape.AddAnchor(MarkerPos);

            Cursor.Scale = new(.8f, .8f);
            Manager.PlaySound("Laptop_Keystroke_82.wav", 0.2f, 1.3f, 1.8f);
        }
        if (Input.IsActionJustPressed(Snl.insert_point))
        {
            foreach (Anchor a in SelectedAnchors)
            {
                if (SelectedAnchors.Contains(a.NextAnchor()))
                {
                    CurrentShape.AddAnchor(MarkerPos, insertAfter: a);

                    Cursor.Scale = new(.8f, .8f);
                    Manager.PlaySound("Laptop_Keystroke_82.wav", 0.2f, 1.3f, 1.8f);
                }
            }
        }
        if (Input.IsActionJustPressed(Snl.finish_shape))
        {
            CurrentShape.Finish();
            Manager.PlaySound("camera.wav", 0.4f, 1.3f, 1.8f);
        }
        if (Input.IsActionJustPressed(Snl.snap_selected))
        {
            SnapSelectedPos();
        }
        if (Input.IsActionJustPressed(Snl.switch_segment_style))
        {
            //TODO implement
        }
        if (Input.IsActionJustPressed(Snl.switch_point_style))
        {
            //TODO implement
        }
        if (Input.IsActionJustPressed(Snl.increase_zoom))
        {
            if (Zoom >= 1 && Zoom < 4) Zoom *= 4;
            else if (Zoom < 1) Zoom *= 2;
        }
        if (Input.IsActionJustPressed(Snl.decrease_zoom))
        {
            if (Zoom <= 1 && Zoom > .5f) Zoom /= 2;
            else if (Zoom > 1) Zoom /= 4;
        }
        if (Input.IsActionJustPressed(Snl.increase_grid_modifier))
        {
            if (GridModifier < 6)
            {
                GridModifier *= 2;
                SnapMarkerPos();
            }
        }
        if (Input.IsActionJustPressed(Snl.decrease_grid_modifier))
        {
            if (GridModifier > 1)
            {
                GridModifier /= 2;
                SnapMarkerPos();
            }
        }
    }

    //TODO implement

    //TODO implement

    public void HandleInput(float delta)
    {
        if (Input.IsKeyPressed(Key.Backspace))
        {
            GetTree().Quit();
        }

        if (Input.IsActionJustPressed("toggle_preview"))
        {
            if (CurrentMode == Mode.Editing)
            {
                CurrentMode = Mode.Previewing;
                Background.Color = PreviewColor;
            }
            else if (CurrentMode == Mode.Previewing)
            {
                CurrentMode = Mode.Editing;
                Background.Color = BackgroundColor;
            }
        }

        if (Input.IsActionJustPressed("select_mode"))
        {
            if (CurrentMode == Mode.Editing)
            {
                CurrentMode = Mode.Selecting;
                Background.Color = SelectingColor;
            }
            else if (CurrentMode == Mode.Selecting)
            {
                CurrentMode = Mode.Editing;
                Background.Color = BackgroundColor;
            }
        }
        if (CurrentMode == Mode.Editing || CurrentMode == Mode.Previewing)
        {
            HiMovement(delta);
            HiPointAdding(delta);
            HiScaling(delta);
        }
    }

    public bool OnGuide()
    {
        if ((int)MarkerPos.X == 384 || (int)MarkerPos.X == 1152 || (int)MarkerPos.Y == 512 || (int)MarkerPos.Y == 1280)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public void SnapMarkerPos()
    {
        int totalSize = (int)GridModifier * GridSize;
        MarkerPos.X = (int)(MarkerPos.X / totalSize) * totalSize;
        MarkerPos.Y = (int)(MarkerPos.Y / totalSize) * totalSize;
    }

    //TODO implement
    public void SnapSelectedPos()
    {
        int totalSize = (int)GridModifier * GridSize;
    }

    public void DoUndoRedo()
    {
        CanUndoAgain = true;
    }

    public void MovementTimerTimeout()
    {
        CanMoveAgain = true;
    }
}
