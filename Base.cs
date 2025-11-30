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
using System.Net;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
    public static readonly V2 WindowSize = new V2(120, 80) * GridSize;
    // public static readonly V2 WindowSize;
    public static V2 OriginOff = new(300, 0);
    public static V2 Origin = WindowSize / 2f - OriginOff;

    const float BaseLineHeight = 24 * GridSize;
    const float XLineHeight = -24 * GridSize;
    const float CapitalLineHeight = -48 * GridSize;
    const float AscenderLineHeight = -48 * GridSize;
    const float DescenderLineHeight = 48 * GridSize;
    const float LeftWidthLine = -24 * GridSize;
    const float RightWidthLine = 24 * GridSize;

    static readonly (V2, V2) Borders = (V2.Zero, WindowSize);
    static readonly Color GridColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.04f);
    static readonly Color GuidesColor = Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.1f);
    static readonly Color BackgroundColor = Color.FromHtml("cccccc");
    // static readonly Color SelectingColor = Color.FromOkHsl(63 / 359f, 9 / 100f, 66 / 100f);
    static readonly Color SelectingColor = BackgroundColor;
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
    Sprite2D Tex = null!;

    public static V2 CursorOff = new(0, 0);
    public static int FrameCounter = 0;
    public static float Counter = 0;
    V2 MarkerPos = Origin;
    Shape CurrentShape = Shapes.NewShape();
    List<float> DeltaTimeList = new();
    bool StickyGuide = true;
    bool CanMoveAgain = true;
    bool CanUndoAgain = true;
    bool SelectingInHandle = true;
    bool CurvePlacing = false;
    public static bool AutoMoveMode = false;
    int CurvePlacingStage = 0;
    static Mode CurrentMode = Mode.Editing;
    static Focus CurrentFocus = Focus.Anchor;
    Timer UndoTimer = new();
    Timer MovementTimer = new();
    Timer FpsTimer = new();
    Sprite2D Cursor = null!;
    Sprite2D CursorShadow = null!;
    Label CursorLabel = null!;
    RichTextLabel FpsLabel = null!;
    ColorRect Background = null!;
    CanvasLayer ControlRoot = null!;
    Panel FocusIdentifier = null!;
    HBoxContainer PreviewContainer = null!;
    FileDialog FilePicker = null!;
    string LastFramesSvg = "";

    HashSet<Anchor> SelectedAnchors = new();
    HashSet<HandlePointer> SelectedHandles = new();

    public override void _Ready()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        AddChild(Manager);
        AddChild(UndoTimer);
        AddChild(MovementTimer);
        AddChild(FpsTimer);

        MovementTimer.WaitTime = 0.01f;
        MovementTimer.OneShot = true;
        MovementTimer.Timeout += MovementTimerTimeout;

        UndoTimer.WaitTime = 0.5f;
        UndoTimer.OneShot = true;
        UndoTimer.Timeout += DoUndoRedo;

        FpsTimer.WaitTime = 0.5f;
        FpsTimer.OneShot = true;
        FpsTimer.Start();

        Tex = GetParent().GetNode<Sprite2D>("Tex");
        Background = GetParent().GetNode<ColorRect>("Background");
        Cursor = GetParent().GetNode<Sprite2D>("Cursor");
        CursorShadow = (Sprite2D)Cursor.GetChild(0);
        CursorLabel = (Label)Cursor.GetChild(1);
        FpsLabel = GetParent().GetNode<RichTextLabel>("FpsLabel");
        ControlRoot = GetNode<CanvasLayer>("ControlRoot");
        FocusIdentifier = ControlRoot.GetNode<Panel>("LayerPanel/FocusIdentifier");
        PreviewContainer = (HBoxContainer)ControlRoot.FindChild("PreviewContainer");
        FilePicker = GetParent().GetNode<FileDialog>("FileDialog");

        GetWindow().Size = new Vector2I((int)WindowSize.X, (int)WindowSize.Y);
        Background.Color = BackgroundColor;
    }

    public override async void _Process(double doubleDelta)
    {

        float delta = (float)doubleDelta;
        Counter += delta;

        HandleInput(delta);

        if (true)
        {
            if (DeltaTimeList.Count() > 30)
                DeltaTimeList.RemoveAt(0);
            DeltaTimeList.Add(delta);
            if (FpsTimer.TimeLeft <= 0)
            {
                FpsTimer.Start();
                float totalDelta = 0f;
                float totalPoints = 0f;
                foreach (float t in DeltaTimeList)
                    totalDelta += t;
                FpsLabel.Text = Mathf.Round(1.0 / (totalDelta / DeltaTimeList.Count())).ToString() + " : " + totalPoints.ToString();
                foreach (Shape s in Shapes.S)
                    totalPoints += s.Anchors.Count;
            }
        }

        if (Zoom <= 1)
            Cursor.Position = Fun.Vtv(V2.Lerp(Fun.Vtv(Cursor.Position), ((MarkerPos + CursorOff) * Zoom) + (Origin * (1f - Zoom)), delta * 20f));
        else
            Cursor.Position = Fun.Vtv(Origin + CursorOff);
        Cursor.Scale = Fun.Vtv(V2.Lerp(Fun.Vtv(Cursor.Scale), new V2(.9f, .9f), delta * 10));
        if (Cursor.Scale.Length() < new V2(.85f, .85f).Length())
        {
            Cursor.Rotation = Cursor.Position.AngleToPoint(Fun.Vtv((MarkerPos * Zoom) + (Origin * (1 - Zoom))));
        }
        CursorShadow.Position = Cursor.Position + new GV2(0,5);
        CursorLabel.Position = Cursor.Position + new GV2(30, 30);
        CursorShadow.Scale = Cursor.Scale;
        CursorShadow.Rotation = Cursor.Rotation;
        CursorLabel.Text = MarkerPos.X.ToString() + ", " + MarkerPos.Y.ToString();
        CursorLabel.Size = new(0, 10);

        Panel selector = (Panel)FindChild("Selector");
        GV2 goalPos = new(5, 14 + Shapes.S.IndexOf(CurrentShape) * 98);

        selector.Position = Fun.Vtv(V2.Lerp(Fun.Vtv(selector.Position), Fun.Vtv(goalPos), delta * 30f));
        selector.Scale = Fun.Vtv(V2.Lerp(Fun.Vtv(selector.Scale), new V2(1.0f, 1.0f), delta * 30));
        ((RichTextLabel)selector.GetNode("PointAmountLabel")).Text = CurrentShape.Anchors.Count.ToString("D2") + "/24";

        FocusIdentifier.Scale = Fun.Vtv(V2.Lerp(Fun.Vtv(FocusIdentifier.Scale), new V2(1.0f, 1.0f), delta * 30));

        // SVG code goes here
        
        // Task<Texture2D> taskje = Task.Run(() =>
        // {
        //     Im.LoadSvgFromString(LastFramesSvg);
        //     Texture2D finalTexture = ImageTexture.CreateFromImage(Im);
        //     return finalTexture;
        // });
        Im.LoadSvgFromString(LastFramesSvg);
        Texture2D finalTexture = ImageTexture.CreateFromImage(Im);

        SvgString.ClearString(Zoom, Origin, WindowSize, MarkerPos);

        if (CurrentMode == Mode.Editing) DrawEditing();
        else if (CurrentMode == Mode.Previewing) DrawPreviewing();
        else if (CurrentMode == Mode.Selecting) DrawSelecting();

        SvgString.Finish();
        LastFramesSvg = SvgString.CurrentString;

        // Tex.Texture = await taskje;
        Tex.Texture = finalTexture;
        
        TextureRect preview1 = PreviewContainer.GetChild<TextureRect>(0);
        TextureRect tinyPreview = (TextureRect)PreviewContainer.FindChild("TinyPreview");
        TextureRect tinyPreview2 = (TextureRect)PreviewContainer.FindChild("TinyPreview2");
        TextureRect preview2 = PreviewContainer.GetChild<TextureRect>(2);

        Image thumbnail = DrawThumbnail(new(200, 320));
        preview1.Texture = ImageTexture.CreateFromImage(thumbnail);


        Image tinyThumbnail = new();
        tinyThumbnail.CopyFrom(thumbnail);

        thumbnail.AdjustBcs(0, 1, 1);
        thumbnail.FlipX();
        preview2.Texture = ImageTexture.CreateFromImage(thumbnail);

        tinyThumbnail.Resize(200 / 4, 320 / 4, Image.Interpolation.Nearest);
        tinyThumbnail.FlipY();
        tinyPreview2.Texture = ImageTexture.CreateFromImage(tinyThumbnail);
        tinyPreview2.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
        tinyThumbnail.AdjustBcs(0, 1, 1);
        tinyThumbnail.FlipY();
        tinyPreview.Texture = ImageTexture.CreateFromImage(tinyThumbnail);
        tinyPreview.StretchMode = TextureRect.StretchModeEnum.KeepCentered;


        VBoxContainer vbox = (VBoxContainer)FindChild("VBoxContainer_Layers");
        Image tempImage = new();
        int shapeCounter = 0;
        foreach (TextureRect nde in vbox.GetChildren())
        {
            string currentString = (
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"90\" >" +
                $"<g transform=\"scale(0.5) translate(-30,-30) rotate(0)\">" +
                $"<g transform=\"scale(0.1) translate(0,0) rotate(0)\">");
            if (shapeCounter < Shapes.S.Count)
            {
                if (Shapes.S[shapeCounter].Anchors.Count < 3)
                {
                    continue;
                }
                Segment[] s = Shapes.S[shapeCounter].SegList();
                float[] startSeg = s[0].Flat();
                currentString += $"<path d=\"M {startSeg[0]} {startSeg[1]} C ";
                int innerCounter = 0;
                foreach (Segment seg in s)
                {
                    float[] flatSeg = seg.Flat();
                    currentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
                    if (innerCounter != s.Length - 1)
                    {
                        currentString += ",";
                    }
                    currentString += " ";
                    innerCounter += 1;
                }
                currentString += $"Z\" ";
                currentString += " fill =\"gray\" stroke =\"black\" fill-opacity=\"0.0\" stroke-opacity=\"1.0\" stroke-width=\"30\"/>";
                currentString += Shapes.S[shapeCounter];
            }
            currentString += (
                "</g></g></svg>" 
            );
            tempImage.LoadSvgFromString(currentString);
            nde.Texture = ImageTexture.CreateFromImage(tempImage);

            shapeCounter += 1;
        }

        QueueRedraw();
    }

    // public async Task<string> SvgFromString(string s)
    // {
    //     Im.LoadSvgFromString(SvgString.CurrentString);
    //     Texture2D finalTexture = ImageTexture.CreateFromImage(Im);
    //     Tex.Texture = finalTexture;
    // }

    public override void _Draw()
    {
        if (true && CurrentMode == Mode.Editing)
        {
            if (!(Zoom < 1 && GridModifier < 2f))
            {
                // draw grid
                V2 drawnGridSize = new(Zoom * GridModifier * GridSize);
                // GV2 gridAdjustment = -Fun.Vtv(CursorOff) - new GV2(20,64);
                GV2 gridAdjustment =  - new GV2(20,64);
                if (Zoom > 1)
                {
                    gridAdjustment -= new GV2(0,128);
                }
                if (Zoom < 1)
                {
                    gridAdjustment = new(32,32);
                }
                for (int i = 0; i < (WindowSize.X / drawnGridSize.X) + 30; i++)
                {
                    DrawLine(
                        new GV2(0, i * drawnGridSize.X - gridAdjustment.Y),
                        new GV2(5000, i * drawnGridSize.X - gridAdjustment.Y), GridColor, 1.5f, true);
                }
                for (int i = 0; i < (WindowSize.Y / drawnGridSize.Y) + 30; i++)
                {
                    DrawLine(
                        new GV2(i * drawnGridSize.Y - gridAdjustment.X, 0),
                        new GV2(i * drawnGridSize.Y - gridAdjustment.X, 5000), GridColor, 1.5f, true);
                }
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
                    Color charColor = Colors.Black;
                    Color shadowColor = Colors.Black;
                    Color bcol = BackgroundColor;
                    if (SelectedAnchors.Contains(a))
                    {
                        // charColor = Colors.;
                        bcol = Colors.Orange;
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
                    DrawRect(new(Fun.Vtv(p - new V2(15, 18)), new GV2(30, 36)), bcol);
                    // DrawChar(MediumFont, Fun.Vtv(p + letterOffset + new V2(0, 3)), c.ToString(), 30, shadowColor);
                    DrawChar(MediumFont, Fun.Vtv(p + letterOffset), c.ToString(), 30, charColor);
                    c = (char)((int)c + 1);
                }
            }
            else if (CurrentFocus == Focus.Handle)
            {
                char c = 'a';
                // if straight, label in middle of segment, 2 labels on handles
                var segList = CurrentShape.SegList();
                for (int index = 0; index < CurrentShape.Anchors.Count; index++)
                {
                    Color charColor = Colors.Black;
                    Color shadowColor = Colors.Black;
                    Color bcol = BackgroundColor;
                    if (CurrentShape.Anchors[index].OutHandle.Type == SegmentType.Cubic)
                    {
                        if (SelectedHandles.Contains(CurrentShape.Anchors[index].OutHandle.Pointer()))
                        {
                            // charColor = Colors.;
                            bcol = Colors.Orange;
                            shadowColor = Colors.Red;
                        }
                        V2 p = CurrentShape.Anchors[index].OutHandle.Position();
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
                        DrawRect(new(Fun.Vtv(p - new V2(15, 18)), new GV2(30, 36)), bcol);
                        // DrawChar(MediumFont, Fun.Vtv(p + letterOffset + new V2(0, 3)), c.ToString(), 30, shadowColor);
                        DrawChar(MediumFont, Fun.Vtv(p + letterOffset), c.ToString(), 30, charColor);

                        charColor = Colors.Black;
                        shadowColor = Colors.Black;
                        bcol = BackgroundColor;

                        if (SelectedHandles.Contains(CurrentShape.Anchors[index].NextAnchor().InHandle.Pointer()))
                        {
                            // charColor = Colors.;
                            bcol = Colors.Orange;
                            shadowColor = Colors.Red;
                        }
                        p = CurrentShape.Anchors[index].NextAnchor().InHandle.Position();
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
                        DrawRect(new(Fun.Vtv(p - new V2(15, 18)), new GV2(30, 36)), bcol);
                        DrawChar(MediumFont, Fun.Vtv(p + letterOffset), c.ToString().ToUpper(), 30, charColor);

                        c = (char)((int)c + 1);

                    }
                    else
                    {
                        Segment s = segList[index];
                        var f = s.Flat();
                        float[] middlePos = Player.PointAlongCubicParametric(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], 0.5f);

                        if (SelectedHandles.Contains(CurrentShape.Anchors[index].OutHandle.Pointer()) ||
                            SelectedHandles.Contains(CurrentShape.Anchors[index].NextAnchor().InHandle.Pointer()))
                        {
                            // charColor = Colors.;
                            bcol = Colors.Orange;
                            shadowColor = Colors.Red;
                        }
                        V2 p = new(middlePos[0],middlePos[1]);
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
                        DrawRect(new(Fun.Vtv(p - new V2(15, 18)), new GV2(30, 36)), bcol);
                        // DrawChar(MediumFont, Fun.Vtv(p + letterOffset + new V2(0, 3)), c.ToString(), 30, shadowColor);
                        DrawChar(MediumFont, Fun.Vtv(p + letterOffset), c.ToString(), 30, charColor);
                        c = (char)((int)c + 1);
                    }
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
                Manager.PlaySound("click.wav", 0.15f, 0.7f, 0.8f);
                int shapeToPick = 0;
                if (at == "0") shapeToPick = 9;
                else shapeToPick = Int32.Parse(at) - 1;
                if (CurrentShape == Shapes.S[shapeToPick])
                {
                    SelectedAnchors = [.. SelectedAnchors, .. CurrentShape.Anchors];
                }
                else
                {
                    Panel selector = (Panel)FindChild("Selector");
                    selector.Scale = new Godot.Vector2(0.3f, 1.6f);
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
            if (!SelectedAnchors.Remove(a))
            {
                SelectedAnchors.Add(a);
            }
        }
        else if (CurrentFocus == Focus.Handle)
        {
            HandlePointer hp;
            if (CurrentShape.GetAnchorFromLabel(s.ToLower()).OutHandle.Type == SegmentType.Straight)
            {
                hp = new(CurrentShape.GetAnchorFromLabel(s.ToLower()), false);
                if (!SelectedHandles.Remove(hp))
                {
                    SelectedHandles.Add(hp);
                }
                hp = new(CurrentShape.GetAnchorFromLabel(s.ToLower()).NextAnchor(), true);
                if (!SelectedHandles.Remove(hp))
                {
                    SelectedHandles.Add(hp);
                }
            }
            else
            {
                if (s[0] >= 97)
                {
                    hp = new(CurrentShape.GetAnchorFromLabel(s.ToLower()), false);
                }
                else
                {
                    hp = new(CurrentShape.GetAnchorFromLabel(s.ToLower()).NextAnchor(), true);
                }
                if (!SelectedHandles.Remove(hp))
                {
                    SelectedHandles.Add(hp);
                }
            }
        }
    }


    public void HiRotation(float delta)
    {
        float rotationAmount = 0.05f;
        float ang = rotationAmount * 2 * MathF.PI * delta;
        if (Input.IsActionPressed(Snl.rotate_cw_points) || Input.IsActionPressed(Snl.rotate_ccw_points))
        {
            Uts();
            if (Input.IsActionPressed(Snl.rotate_cw_points))
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
        if (Input.IsActionPressed(Snl.xscale_up_points) || Input.IsActionPressed(Snl.xscale_down_points))
        {
            if (Input.IsActionPressed(Snl.xscale_down_points))
            {
                xScalingAmount *= -1;
            }
            xScalar += xScalingAmount;
        }
        if (Input.IsActionPressed(Snl.yscale_up_points) || Input.IsActionPressed(Snl.yscale_down_points))
        {
            if (Input.IsActionPressed(Snl.yscale_down_points))
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
                    a.MyShape.AnchorsChanged();
                }
            }
            else if (CurrentFocus == Focus.Handle && SelectedHandles.Count > 0)
            {
                Uts();
                foreach (HandlePointer hp in SelectedHandles)
                {
                    Handle h = hp.h();
                    Handle sibling = h.GetAnchorSibling();
                    if (h.Type == SegmentType.Straight)
                    {
                        continue;
                    }
                    h.DistanceFromAnchor -= (movingSelected.Y) * .1f;
                    if (Input.IsKeyPressed(Key.Shift))
                    {
                        //TODO stepped rotation
                    }
                    else
                    {
                        float movingAmount = 1.5f;
                        if (!h.Locked)
                        {
                            h.Angle += (movingSelected.X / 1000f) * movingAmount;
                        }
                        else if (sibling.Type == SegmentType.Cubic)
                        {
                            h.Angle += (movingSelected.X / 1000f) * movingAmount;
                            if (!SelectedHandles.Contains(sibling.Pointer()))
                            {
                                sibling.Angle += (movingSelected.X / 1000f) * movingAmount;
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
        if (Input.IsActionJustPressed(Snl.add_new_point) || Input.IsActionJustPressed(Snl.add_sharp_point))
        {
            Uts();
            if (CurrentShape.Finished)
            {
                CurrentShape = Shapes.NewShape();
            }

            if (false && CurvePlacing)
            {
                switch (CurvePlacingStage)
                {
                    case 0:
                        CurrentShape.AddAnchor(MarkerPos);
                        break;
                    case 1:
                        break;
                    case 2:
                        CurvePlacingStage = 0;
                        break;
                }
                CurvePlacingStage += 1;
            }
            else
            {
                if (Input.IsActionJustPressed(Snl.add_sharp_point))
                {
                    CurrentShape.AddAnchor(MarkerPos, broken: true);
                }
                else
                {
                    CurrentShape.AddAnchor(MarkerPos);
                }
            }


    
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

        if (Input.IsActionJustPressed(Snl.switch_segment_style) && CurrentFocus == Focus.Handle)
        {
            Uts();
            List<Handle> doneAllready = new();
            foreach (HandlePointer hp in SelectedHandles)
            {
                Handle h = hp.h();
                if (doneAllready.Contains(h))
                {
                    continue;
                }
                if (h.Type == SegmentType.Straight)
                {
                    Handle sibling = h.GetSegmentSibling();
                    h.MakeCubic();
                    sibling.MakeCubic();
                    doneAllready.Add(h);
                    doneAllready.Add(sibling);
                    h.AdjacentAnchor.AlignHandles();
                }
                else
                {
                    Handle sibling = h.GetSegmentSibling();
                    h.MakeStraight();
                    sibling.MakeStraight();
                    doneAllready.Add(h);
                    doneAllready.Add(sibling);
                }
            }
        }

        if (Input.IsActionJustPressed(Snl.auto_move_mode))
        {
            AutoMoveMode = !AutoMoveMode;
            GD.Print(AutoMoveMode);
        }

        if (Input.IsActionJustPressed(Snl.switch_point_style) && CurrentFocus == Focus.Handle)
        {
            Uts();
            List<Handle> doneAllready = new();
            foreach (HandlePointer hp in SelectedHandles)
            {
                Handle h = hp.h();
                if (!doneAllready.Contains(h))
                {
                    Handle sibling = h.GetAnchorSibling();
                    h.Locked = !h.Locked;
                    sibling.Locked = !sibling.Locked;
                    doneAllready.Add(h);
                    doneAllready.Add(sibling);
                    h.AdjacentAnchor.AlignHandles(!h.IsInHandle);
                }
            }

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

    public async Task HandleInput(float delta)
    {
        if (Input.IsActionJustPressed("ui_accept"))
        {
            CurvePlacing = !CurvePlacing;
            // CursorOff = Fun.Vtv(Cursor.Position) - Origin;
            // MarkerPos = Origin;
        }

        if (Input.IsKeyPressed(Key.Backspace))
        {
            GetTree().Quit();
        }

        if (Input.IsActionJustPressed(Snl.save_project))
        {
            FilePicker.FileMode = FileDialog.FileModeEnum.SaveFile;
            FilePicker.Visible = true;
            string file = (string)(await ToSignal(FilePicker, FileDialog.SignalName.FileSelected))[0];
            var fileAc = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Write);
            fileAc.StoreString(Shapes.SaveState());
            fileAc.Close();
        }

        if (Input.IsActionJustPressed(Snl.load_project))
        {
            FilePicker.FileMode = FileDialog.FileModeEnum.OpenFile;
            FilePicker.Visible = true;
            string file = (string)(await ToSignal(FilePicker, FileDialog.SignalName.FileSelected))[0];
            var fileAc = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Read);
            Shapes.LoadState(fileAc.GetAsText());
            CurrentShape = Shapes.S[0]; 
        }

        if (Input.IsActionJustPressed(Snl.switch_focus))
        {
            Manager.PlaySound("mine_2.wav", 0.07f, 2.0f, 2.3f);
            if (CurrentFocus == Focus.Anchor)
            {
                ((Sprite2D)FocusIdentifier.GetChild(0)).Visible = false;
                ((Sprite2D)FocusIdentifier.GetChild(1)).Visible = true;
                FocusIdentifier.Scale = new GV2(0.3f, 3f);
                CurrentFocus = Focus.Handle;
            }
            else if (CurrentFocus == Focus.Handle)
            {
                ((Sprite2D)FocusIdentifier.GetChild(0)).Visible = true;
                ((Sprite2D)FocusIdentifier.GetChild(1)).Visible = false;
                FocusIdentifier.Scale = new GV2(0.3f, 3f);
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

        if (Input.IsActionJustPressed(Snl.toggle_preview))
        {
            if (CurrentMode == Mode.Editing)
            {
                CurrentMode = Mode.Previewing;
                Background.Color = PreviewColor;
                ControlRoot.Visible = false;
                Cursor.Visible = false;
            }
            else if (CurrentMode == Mode.Previewing)
            {
                CurrentMode = Mode.Editing;
                Background.Color = BackgroundColor;
                ControlRoot.Visible = true;
                Cursor.Visible = true;
            }
        }

        if (Input.IsActionJustPressed(Snl.select_mode))
        {
            if (CurrentMode == Mode.Editing)
            {
                CurrentMode = Mode.Selecting;
                Background.Color = SelectingColor;
                Cursor.Visible = false;
            }
            else if (CurrentMode == Mode.Selecting)
            {
                CurrentMode = Mode.Editing;
                Background.Color = BackgroundColor;
                Cursor.Visible = true;
            }
        }
        if (CurrentMode == Mode.Editing || CurrentMode == Mode.Previewing)
        {
            HiMovement(delta);
            HiPointAdding(delta);
            // HiScaling(delta);
            // HiRotation(delta);
            if (AutoMoveMode)
            {
                foreach (Anchor a in SelectedAnchors)
                {
                    a.AutoHandles();
                    a.PreviousAnchor().AutoHandles();
                    a.NextAnchor().AutoHandles();
                }
            }
            foreach (Shape s in Shapes.S)
            {
                s.AlignAllHandles();
            }
        }
    }

    public bool OnGuide()
    {
        if ((int)MarkerPos.X == Origin.X + LeftWidthLine ||
            (int)MarkerPos.X == Origin.X + RightWidthLine ||
            (int)MarkerPos.Y == Origin.Y + XLineHeight ||
            (int)MarkerPos.Y == Origin.Y + BaseLineHeight ||
            (int)MarkerPos.Y == Origin.Y + AscenderLineHeight ||
            (int)MarkerPos.Y == Origin.Y + DescenderLineHeight)
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
        V2 gridAdjustment =  new V2(-20,-64);
        int totalSize = (int)GridModifier * GridSize;
        MarkerPos.X = (int)(MarkerPos.X / totalSize) * totalSize;
        MarkerPos.Y = (int)(MarkerPos.Y / totalSize) * totalSize;
        MarkerPos -= gridAdjustment;
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

    public void DrawPreviewing(bool white = false)
    {
        if (white)
        {
            SvgString.SetStyle(Style.ShapePreviewWhite);
        }
        else
        {
            SvgString.SetStyle(Style.ShapePreview);
        }
        // SvgString.AddSegmentsGroup(Shapes.MergeShapesSkia());
        var mgs = Shapes.MergeShapesSkia();
        if (mgs.Length > 0)
            SvgString.AddSegmentsDebug(Shapes.MergeShapesSkia()[0]);
    }

    public void DrawGuides(float opac = 0.5f, float lesserOpac = 0.12f, float fwi = 2.0f)
    {
        // draw guides
        V2 orig = Origin;
        if (Zoom > 1)
        {
            fwi /= 2; 
        }
        else if (Zoom < 1)
        {
            fwi *= 1.5f; 
        }
        float zero = -5000;
        // SvgString.AddLine(new V2(0, Origin.Y + CapitalLineHeight), new V2(5000, Origin.Y + CapitalLineHeight), sWidth: fwi, sOpacity: opac/3f);
        SvgString.AddLine(new V2(zero, orig.Y + AscenderLineHeight), new V2(5000, orig.Y + AscenderLineHeight), sWidth: fwi, sOpacity: lesserOpac);
        SvgString.AddLine(new V2(zero, orig.Y + DescenderLineHeight), new V2(5000, orig.Y + DescenderLineHeight), sWidth: fwi, sOpacity: lesserOpac);

        SvgString.AddLine(new V2(orig.X + LeftWidthLine, zero), new V2(orig.X + LeftWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(orig.X + LeftWidthLine, zero), new V2(orig.X + LeftWidthLine, 5000), sWidth: fwi, sOpacity: opac);
        SvgString.AddLine(new V2(orig.X + RightWidthLine, zero), new V2(orig.X + RightWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(orig.X + RightWidthLine, zero), new V2(orig.X + RightWidthLine, 5000), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(zero, orig.Y + XLineHeight), new V2(5000, orig.Y + XLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(zero, orig.Y + XLineHeight), new V2(5000, orig.Y + XLineHeight), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(zero, orig.Y + BaseLineHeight), new V2(5000, orig.Y + BaseLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(zero, orig.Y + BaseLineHeight), new V2(5000, orig.Y + BaseLineHeight), sWidth: fwi, sOpacity: opac);
    }

    public void DrawEditing()
    {
        DrawGuides(opac: 0.3f, fwi: 3f);
        float[] radiusSizes = [6, 16];
        float[] widths = [1, 3];
        if (Zoom > 1)
        {
            radiusSizes = [3,4];
            widths = [0.5f, 1.0f];
        }
        else if (Zoom < 1)
        {
            radiusSizes = [12,32];
            widths = [2f, 6f];
        }
        // draw unmerged shape underlays
        if (CurrentShape.Anchors.Count == 0)
        {
            return;
        }
        foreach (Shape s in Shapes.S)
        {
            if (s.Anchors.Count < 3) continue;

            if (s.Negative)
            {
                SvgString.SetStyle(Style.ShapeNegative);
            }
            else
            {
                SvgString.SetStyle(Style.ShapeUnchanged);
            }

            SvgString.AddSegments(s.SegList());
            // SvgString.AddSegmentsDebug(s.SegList());
        }

        // draw merged shapes
        SvgString.SetStyle(Style.ShapePositive);
        Segment[][] sss = Shapes.MergeShapesSkia();
        var lot = Shapes.ListOfTangents(sss);
        SvgString.AddSegmentsGroup(sss);


        // draw ui overlays (anchors and handles)
        foreach (Shape s in Shapes.S)
        {
            // if (s != CurrentShape) continue;

            if (s == CurrentShape)
            {
                SvgString.SetStyle(Style.ShapeSelected);
                if (s.Anchors.Count > 2)
                {
                    SvgString.AddSegments(s.SegList(), false);
                }
            }
            foreach (Anchor a in s.Anchors)
            {

                // draw anchors
                // visualize anchor selection
                // visualize anchor type
                if (SelectedAnchors.Contains(a))
                {
                    // SvgString.AddCircle(a.Position, 12 + Mathf.Sin(Counter*4)*2, fOpacity: 0.0f, sOpacity: 1.0f, sWidth: 6, fill:"red", stroke:"black");
                    SvgString.AddCircle(a.Position, radiusSizes[1], fOpacity: 0.4f, sOpacity: 1.0f, sWidth: widths[1]);
                }
                else
                {
                    SvgString.AddCircle(a.Position, radiusSizes[0], fOpacity: 0, sOpacity: 1.0f, sWidth: widths[0]);
                }

                // draw handles
                // visualize handle selection
                // visualize handle type
                if (CurrentFocus == Focus.Handle && s == CurrentShape)
                {
                    if (a.InHandle.Type == SegmentType.Cubic)
                    {
                        SvgString.AddLine(a.Position, a.InHandle.Position(), sOpacity: .5f);
                        if (SelectedHandles.Contains(a.InHandle.Pointer()))
                            SvgString.AddCircle(a.InHandle.Position(), radiusSizes[1], fill: "blue", fOpacity: .3f, sOpacity: 1.0f, sWidth: widths[1]);
                        else
                            SvgString.AddCircle(a.InHandle.Position(), radiusSizes[0], fill: "blue", fOpacity: .3f, sOpacity: 0f, sWidth: widths[0]);
                    }
                    if (a.OutHandle.Type == SegmentType.Cubic)
                    {
                        SvgString.AddLine(a.Position, a.OutHandle.Position(), sOpacity: .5f);
                        if (SelectedHandles.Contains(a.OutHandle.Pointer()))
                            SvgString.AddCircle(a.OutHandle.Position(), radiusSizes[1], fill: "red", fOpacity: .3f, sOpacity: 1.0f, sWidth: widths[1]);
                        else
                            SvgString.AddCircle(a.OutHandle.Position(), radiusSizes[0], fill: "red", fOpacity: .3f, sOpacity: 0f, sWidth: widths[0]);
                    }
                }
            }
        }
    }

    public Image DrawThumbnail(V2 size)
    {
        SvgString.ClearString(0.2f, size / 2f - new V2(230,180), size, MarkerPos);

        DrawPreviewing(true);
        SvgString.Finish();
        Image thumbnail = new();
        thumbnail.LoadSvgFromString(SvgString.CurrentString);
        // Im.Resize((int)size.X, (int)size.Y);
        return thumbnail;
    }
    public void DrawSelecting()
    {
        DrawGuides(0.04f, 0.04f, 8);
        float[] radiusSizes = [6, 16];
        float[] widths = [1, 3];
        if (Zoom > 1)
        {
            radiusSizes = [3,4];
            widths = [0.5f, 1.0f];
        }
        else if (Zoom < 1)
        {
            radiusSizes = [12,32];
            widths = [2f, 6f];
        }
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
            SvgString.SetStyle(Style.ShapeSelected);
            SvgString.AddSegments(s.SegList(), false);

            foreach (Anchor a in s.Anchors)
            {

                // draw anchors
                // visualize anchor selection
                // visualize anchor type
                SvgString.AddCircle(a.Position, radiusSizes[0], fOpacity: 0, sOpacity: 1.0f, sWidth: 1);

                // draw handles
                // visualize handle selection
                // visualize handle type
                if (CurrentFocus == Focus.Handle)
                {
                    if (a.InHandle.Type == SegmentType.Cubic)
                    {
                        SvgString.AddLine(a.Position, a.InHandle.Position(), sOpacity: .5f);
                        SvgString.AddCircle(a.InHandle.Position(), radiusSizes[0], fill: "blue", fOpacity: .3f, sOpacity: 0f);
                    }
                    if (a.OutHandle.Type == SegmentType.Cubic)
                    {
                        SvgString.AddLine(a.Position, a.OutHandle.Position(), sOpacity: .5f);
                        SvgString.AddCircle(a.OutHandle.Position(), radiusSizes[0], fill: "blue", fOpacity: .3f, sOpacity: 0f);
                    }
                }
            }
        }
    }
}
