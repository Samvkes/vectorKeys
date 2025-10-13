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
using System.Diagnostics.SymbolStore;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SkiaSharp;
using System.IO;

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
    public static readonly V2 WindowSize = new V2(200, 120) * GridSize;
    // public static readonly V2 WindowSize;
    public static readonly V2 Origin = WindowSize / 2f;

    const float BaseLineHeight = 28 * GridSize;
    const float XLineHeight = -20 * GridSize;
    const float CapitalLineHeight = -48 * GridSize;
    const float AscenderLineHeight = -44 * GridSize;
    const float DescenderLineHeight = 52 * GridSize;
    const float LeftWidthLine = -44 * GridSize;
    const float RightWidthLine = 4 * GridSize;

    static readonly (V2, V2) Borders = (V2.Zero, WindowSize);
    static readonly Color GridColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.04f);
    static readonly Color GuidesColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.1f);
    static readonly Color BackgroundColor = Color.FromHtml("cecece");
    static readonly Color SelectingColor = Color.FromOkHsl(63 / 359f, 9 / 100f, 66 / 100f);
    static readonly Color PreviewColor = Color.FromOkHsl(10 / 359f, 75 / 100f, 90 / 100f);
    static Font LightFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Light.otf");
    static Font MediumFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Medium.otf");
    static Font BoldFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Bold.otf");
    static Manager Manager = GD.Load<PackedScene>("res://Manager.tscn").Instantiate<Manager>();

    float GridModifier = 4;
    public float Zoom = 1f;
    float MovementHeldTime = 0f;
    float VisualGridSize = GridSize;
    Image Im = new();
    Image[] LayerImages = new Image[10];
    Sprite2D Tex = null!;

    public static int FrameCounter = 0;
    public static float Counter = 0;
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

    HashSet<Anchor> SelectedAnchors = new();
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

        UndoTimer.WaitTime = 0.5f;
        UndoTimer.OneShot = true;
        UndoTimer.Timeout += DoUndoRedo;

        Tex = GetParent().GetNode<Sprite2D>("Tex");
        Background = GetParent().GetNode<ColorRect>("Background");
        Cursor = GetParent().GetNode<Sprite2D>("Cursor");
        FpsLabel = GetParent().GetNode<RichTextLabel>("FpsLabel");

        GetWindow().Size = new Vector2I((int)WindowSize.X, (int)WindowSize.Y);
        Background.Color = BackgroundColor;
    }

    public override void _Process(double doubleDelta)
    {
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
        SvgString.ClearString(Zoom, Origin, WindowSize, MarkerPos);

        if (CurrentMode == Mode.Editing) DrawEditing();
        else if (CurrentMode == Mode.Previewing) DrawPreviewing();
        else if (CurrentMode == Mode.Selecting) DrawSelecting();

        SvgString.Finish();
        Im.LoadSvgFromString(SvgString.CurrentString);
        Tex.Texture = ImageTexture.CreateFromImage(Im);
        QueueRedraw();
    }


    public override void _Draw()
    {
        if (true && CurrentMode == Mode.Editing || CurrentMode == Mode.Selecting)
        {
            if (GridModifier >= .5f)
            {
                // draw grid
                V2 drawnGridSize = new(Zoom * GridModifier * GridSize);
                GV2 gridAdjustment = new(0, 0);
                if (Zoom > 1)
                {
                    gridAdjustment = new(128, 256);
                }
                for (int i = 0; i < (WindowSize.X / drawnGridSize.X) + 30; i++)
                {
                    DrawLine(
                        new GV2(0, i * drawnGridSize.X - gridAdjustment.X),
                        new GV2(5000, i * drawnGridSize.X - gridAdjustment.X), GridColor, 1.5f, true);
                }
                for (int i = 0; i < (WindowSize.Y / drawnGridSize.Y) + 30; i++)
                {
                    DrawLine(
                        new GV2(i * drawnGridSize.Y - gridAdjustment.Y, 0),
                        new GV2(i * drawnGridSize.Y - gridAdjustment.Y, 5000), GridColor, 1.5f, true);
                }
            }

            // draw marker position
            if (Zoom <= 1)
            {
                DrawString(LightFont, Fun.Vtv((MarkerPos * Zoom) + (Origin * (1f - Zoom)) + new V2(30, 30)), (MarkerPos / 16).ToString(), HorizontalAlignment.Left, -1f, DefaultFontSize + 8, Colors.Black);
            }
            else
            {
                DrawString(LightFont, Fun.Vtv(Origin + new V2(30, 30)), MarkerPos.ToString(), HorizontalAlignment.Left, -1f, DefaultFontSize + 8, Colors.Black);
            }

        }

        // draw anchor letters
        if (CurrentMode == Mode.Selecting)
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
                    V2 p = a.Position;
                    if (Zoom <= 1)
                    {
                        p = (p * Zoom) + Origin * (1f - Zoom);
                    }
                    else
                    {
                        p -= Origin - 1.3333f * (Origin - MarkerPos);
                        p *= Zoom;
                        p += Origin - 1.3333f * (Origin - MarkerPos);
                        p -= new V2(4, -5);
                    }
                    V2 letterOffset = new(-9, 7);
                    DrawChar(MediumFont, Fun.Vtv(p + letterOffset + new V2(0, 3)), c.ToString(), 30, shadowColor);
                    DrawChar(MediumFont, Fun.Vtv(p + letterOffset), c.ToString(), 30, charColor);
                    c = (char)((int)c + 1);
                }
            }
        }
    }


    public override void _Input(InputEvent ev)
    {
        string at = ev.AsText();
        if (ev is InputEventKey && ev.IsPressed())
        {
            if ("0123456789".Contains(at))
            {
                int shapeToPick = 0;
                if (at == "0") shapeToPick = 9;
                else shapeToPick = Int32.Parse(at) - 1;
                if (CurrentShape == Shapes.S[shapeToPick])
                {
                    SelectedAnchors = [.. SelectedAnchors, .. CurrentShape.Anchors];
                }
                else
                {
                    CurrentShape = Shapes.S[shapeToPick];
                }
            }
            else if (at == "Semicolon")
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
            SelectedAnchors.Add(a);
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


    public void HiRotation(float delta)
    {
        float rotationAmount = 0.05f;
        float ang = rotationAmount * 2 * MathF.PI * delta;
        if (Input.IsKeyPressed(Key.Key1) || Input.IsKeyPressed(Key.Key2))
        {
            Uts();
            if (Input.IsKeyPressed(Key.Key1))
            {
                ang *= -1;
            }
            foreach (Anchor a in SelectedAnchors)
            {
                V2 spot = a.Position - MarkerPos;
                V2 displacement = new(MathF.Cos(ang) * spot[0] - MathF.Sin(ang) * spot[1], MathF.Sin(ang) * spot[0] + MathF.Cos(ang) * spot[1]);
                a.Position += displacement - spot;
            }
        }
    }

    public void HiScaling(float delta)
    {
        float scalingAmount = .01f;
        float xScalingAmount = scalingAmount;
        float yScalingAmount = scalingAmount;
        float xScalar = 1;
        float yScalar = 1;
        if (Input.IsKeyPressed(Key.Key5) || Input.IsKeyPressed(Key.Key6))
        {
            if (Input.IsKeyPressed(Key.Key5))
            {
                xScalingAmount *= -1;
            }
            xScalar += xScalingAmount;
        }
        if (Input.IsKeyPressed(Key.Key3) || Input.IsKeyPressed(Key.Key4))
        {
            if (Input.IsKeyPressed(Key.Key3))
            {
                yScalingAmount *= -1;
            }
            yScalar += yScalingAmount;
        }
        if (xScalar != 1 || yScalar != 1)
        {
            Uts();
            foreach (Anchor a in SelectedAnchors)
            {
                V2 spot = a.Position - MarkerPos;
                V2 displacement = new(spot[0] * xScalar, spot[1] * yScalar);
                a.Position += displacement - spot;
            }
        }
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
                Uts();
                foreach (Anchor a in SelectedAnchors)
                {
                    a.Position += movingSelected;
                    a.AlignHandles();
                }
            }
            else if (CurrentFocus == Focus.Handle && SelectedHandles.Count > 0)
            {
                Uts();
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
                        float movingAmount = 1.5f;
                        if (h.AdjacentAnchor.Type == AnchorType.Broken)
                        {
                            h.Angle += (movingSelected.X / 1000f) * movingAmount;
                        }
                        else
                        {
                            if (h.IsInHandle)
                            {
                                if (h.AdjacentAnchor.OutHandle.Type == SegmentType.Cubic)
                                {
                                    h.Angle += (movingSelected.X / 1000f) * movementAmount;
                                    h.AdjacentAnchor.OutHandle.Angle = h.Angle + MathF.PI;
                                }
                            }
                            else
                            {
                                if (h.AdjacentAnchor.InHandle.Type == SegmentType.Cubic)
                                {
                                    h.Angle += (movingSelected.X / 1000f) * movingAmount;
                                    h.AdjacentAnchor.InHandle.Angle = h.Angle + MathF.PI;
                                }
                            }
                        }
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
            Uts();
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
                    Uts();
                    CurrentShape.AddAnchor(MarkerPos, insertAfter: a);

                    Cursor.Scale = new(.8f, .8f);
                    Manager.PlaySound("Laptop_Keystroke_82.wav", 0.2f, 1.3f, 1.8f);
                }
            }
        }
        if (Input.IsActionJustPressed(Snl.finish_shape) && !CurrentShape.Finished)
        {
            Uts();
            CurrentShape.Finish();
            Manager.PlaySound("camera.wav", 0.4f, 1.3f, 1.8f);
        }
        if (Input.IsActionJustPressed(Snl.snap_selected))
        {
            Uts();
            SnapSelectedPos();
        }
        if (Input.IsActionJustPressed(Snl.switch_segment_style))
        {
            Uts();
            foreach (Anchor a in SelectedAnchors)
            {
                if (SelectedAnchors.Contains(a.NextAnchor()))
                {
                    a.SwitchSegmentType();
                }
            }
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

    public void HandleInput(float delta)
    {
        if (Input.IsKeyPressed(Key.Backspace))
        {
            GetTree().Quit();
        }

        if (Input.IsActionJustPressed(Snl.switch_focus))
        {
            if (CurrentFocus == Focus.Anchor)
            {
                CurrentFocus = Focus.Handle;
            }
            else if (CurrentFocus == Focus.Handle)
            {
                CurrentFocus = Focus.Anchor;
            }
        }

        if (Input.IsActionJustPressed(Snl.shape_negative))
        {
            Uts();
            CurrentShape.Negative = !CurrentShape.Negative;
        }

        if (Input.IsActionJustPressed(Snl.undo))
        {
            UndoRedo.Undo();
            SelectedAnchors.Clear();
            SelectedHandles.Clear();
            CurrentShape = Shapes.S.Last();
        }

        if (Input.IsActionJustPressed(Snl.redo))
        {
            UndoRedo.Redo();
            SelectedAnchors.Clear();
            SelectedHandles.Clear();
            CurrentShape = Shapes.S.Last();
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
            HiRotation(delta);
            foreach (Shape s in Shapes.S)
            {
                s.AlignAllHandles();
            }
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

    public void Uts()
    {
        if (CanUndoAgain)
        {
            UndoRedo.CurrentShapesToUndoStack();
            UndoRedo.ClearRedoStack();
            CanUndoAgain = false;
        }
        if (UndoTimer.IsStopped())
        {
            UndoTimer.Start();
        }
    }

    public void DoUndoRedo()
    {
        CanUndoAgain = true;
    }

    public void MovementTimerTimeout()
    {
        CanMoveAgain = true;
    }

    public void DrawPreviewing()
    {
        SvgString.SetStyle(Style.ShapePreview);
        SvgString.AddSegmentsGroup(Shapes.MergeShapesSkia());
    }

    public void DrawEditing()
    {
        // draw guides
        float opac = 0.5f;
        float fwi = 2.0f;
        // SvgString.AddLine(new V2(0, Origin.Y + CapitalLineHeight), new V2(5000, Origin.Y + CapitalLineHeight), sWidth: fwi, sOpacity: opac/3f);
        SvgString.AddLine(new V2(0, Origin.Y + AscenderLineHeight), new V2(5000, Origin.Y + AscenderLineHeight), sWidth: fwi, sOpacity: opac/3f);
        SvgString.AddLine(new V2(0, Origin.Y + DescenderLineHeight), new V2(5000, Origin.Y + DescenderLineHeight), sWidth: fwi, sOpacity: opac/3f);

        SvgString.AddLine(new V2(Origin.X + LeftWidthLine, 0), new V2(Origin.X + LeftWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(Origin.X + LeftWidthLine, 0), new V2(Origin.X + LeftWidthLine, 5000), sWidth: fwi, sOpacity: opac);
        SvgString.AddLine(new V2(Origin.X + RightWidthLine, 0), new V2(Origin.X + RightWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(Origin.X + RightWidthLine, 0), new V2(Origin.X + RightWidthLine, 5000), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(0, Origin.Y + XLineHeight), new V2(5000, Origin.Y + XLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(0, Origin.Y + XLineHeight), new V2(5000, Origin.Y + XLineHeight), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(0, Origin.Y + BaseLineHeight), new V2(5000, Origin.Y + BaseLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(0, Origin.Y + BaseLineHeight), new V2(5000, Origin.Y + BaseLineHeight), sWidth: fwi, sOpacity: opac);


        if (false)
        {
            V2 drawnGridSize = new(Zoom * GridModifier * GridSize / 2.0f);
            V2 gridAdjustment = drawnGridSize;
            float opa = 1.0f;
            float swi = 1.0f;
            int sze = 8;
            if (GridModifier > 4)
            {
                sze = 24;
            }
            else if (GridModifier > 2)
            {
                sze = 14;
            }
            for (int i = 0; i < (WindowSize.X / drawnGridSize.X) + 10; i++)

            {
                SvgString.AddDashed(
                    new V2(-sze / 2.0f, i * drawnGridSize.X - gridAdjustment.X),
                    new V2(3000, i * drawnGridSize.X - gridAdjustment.X), sze + " " + (drawnGridSize.X * 2f - sze) + "\"", sWidth: swi, sOpacity: opa, stroke: "gray");
            }
            for (int i = 0; i < (WindowSize.Y / drawnGridSize.Y) + 10; i++)
            {
                SvgString.AddDashed(
                    new V2(i * drawnGridSize.Y - gridAdjustment.Y - drawnGridSize.Y, -sze / 2.0f),
                    new V2(i * drawnGridSize.Y - gridAdjustment.Y - drawnGridSize.Y * 2.0f, 3000), sze + " " + (drawnGridSize.Y * 2 - sze) + "\"", sWidth: swi, sOpacity: opa, stroke: "gray");
            }
        }

        // draw unmerged shape underlays
        foreach (Shape s in Shapes.S)
        {
            if (s.Anchors.Count <= 1) continue;

            if (s.Negative)
            {
                SvgString.SetStyle(Style.ShapeNegative);
            }
            else
            {
                SvgString.SetStyle(Style.ShapeUnchanged);
            }

            SvgString.AddSegments(s.SegList(), false);
        }

        // draw merged shapes
        SvgString.SetStyle(Style.ShapePositive);
        Segment[][] sss = Shapes.MergeShapesSkia();
        var lot = Shapes.ListOfTangents(sss);
        SvgString.AddSegmentsGroup(sss);


        // draw ui overlays (anchors and handles)
        foreach (Shape s in Shapes.S)
        {
            if (s != CurrentShape) continue;

            foreach (Anchor a in s.Anchors)
            {
                if (s == CurrentShape)
                {
                    SvgString.SetStyle(Style.ShapeSelected);
                    SvgString.AddSegments(s.SegList(), false);
                }

                // draw anchors
                // visualize anchor selection
                // visualize anchor type
                SvgString.AddCircle(a.Position, 6, fOpacity: 0, sOpacity: 1.0f, sWidth: 1);

                // draw handles
                // visualize handle selection
                // visualize handle type
                if (a.InHandle.Type == SegmentType.Cubic)
                {
                    SvgString.AddLine(a.Position, a.InHandle.Position(), sOpacity: .5f);
                    SvgString.AddCircle(a.InHandle.Position(), 6, fill: "blue", fOpacity: .3f, sOpacity: 0f);
                }
                if (a.OutHandle.Type == SegmentType.Cubic)
                {
                    SvgString.AddLine(a.Position, a.OutHandle.Position(), sOpacity: .5f);
                    SvgString.AddCircle(a.OutHandle.Position(), 6, fill: "blue", fOpacity: .3f, sOpacity: 0f);
                }
            }
        }
    }

    public void DrawSelecting()
    {
        foreach (Shape s in Shapes.S)
        {
            if (s.Anchors.Count <= 1) continue;

            if (s.Negative)
            {
                SvgString.SetStyle(Style.ShapeNegative);
            }
            else
            {
                SvgString.SetStyle(Style.ShapeUnchanged);
            }

            SvgString.AddSegments(s.SegList(), false);
        }

        foreach (Shape s in Shapes.S)
        {
            if (s != CurrentShape) continue;

            foreach (Anchor a in s.Anchors)
            {
                if (s == CurrentShape)
                {
                    SvgString.SetStyle(Style.ShapeSelected);
                    SvgString.AddSegments(s.SegList(), false);
                }

                // draw anchors
                // visualize anchor selection
                // visualize anchor type
                SvgString.AddCircle(a.Position, 6, fOpacity: 0, sOpacity: 1.0f, sWidth: 1);

                // draw handles
                // visualize handle selection
                // visualize handle type
                if (a.InHandle.Type == SegmentType.Cubic)
                {
                    SvgString.AddLine(a.Position, a.InHandle.Position(), sOpacity: .5f);
                    SvgString.AddCircle(a.InHandle.Position(), 6, fill: "blue", fOpacity: .3f, sOpacity: 0f);
                }
                if (a.OutHandle.Type == SegmentType.Cubic)
                {
                    SvgString.AddLine(a.Position, a.OutHandle.Position(), sOpacity: .5f);
                    SvgString.AddCircle(a.OutHandle.Position(), 6, fill: "blue", fOpacity: .3f, sOpacity: 0f);
                }
            }
        }
    }
}
