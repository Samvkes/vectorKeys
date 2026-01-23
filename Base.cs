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
using System.Diagnostics.CodeAnalysis;

namespace Vectordrawing;

public record UIConfig(
    bool Debug,
    int GridSize, 
    int FarMoveBorder, 
    float ValidHoldTime, 
    int DefaultFontSize, 
    int RotationStepSizeDegrees, 
    V2 WindowSize, 
    V2 OriginOff, 
    V2 Origin, 

    float BaseLineHeight, 
    float XLineHeight, 
    float CapitalLineHeight, 
    float AscenderLineHeight, 
    float DescenderLineHeight, 
    float LeftWidthLine, 
    float RightWidthLine, 

    (V2, V2) Borders, 
    Color GridColor, 
    Color GuidesColor, 
    Color BackgroundColor, 
    Color SelectingColor, 
    Color PreviewColor, 
    Font LightFont, 
    Font MediumFont, 
    Font BoldFont
)
{
    const int _gridsize = 16;
    static V2 _windowsize = new V2(120,80) * UIConfig._gridsize;
    static V2 _originoff = new(300,0);
    public static UIConfig Default => new
    (
        Debug: false,
        GridSize: _gridsize,
        FarMoveBorder: 2 * _gridsize,
        ValidHoldTime: .2f,
        DefaultFontSize: 14,
        RotationStepSizeDegrees: 15,
        WindowSize: _windowsize,
        OriginOff: _originoff,
        Origin: _windowsize / 2f - _originoff,

        BaseLineHeight: 24 * _gridsize,
        XLineHeight: -24 * _gridsize,
        CapitalLineHeight: -48 * _gridsize,
        AscenderLineHeight: -48 * _gridsize,
        DescenderLineHeight: 48 * _gridsize,
        LeftWidthLine: -24 * _gridsize,
        RightWidthLine: 24 * _gridsize,

        Borders: (V2.Zero, _windowsize),
        GridColor: Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.04f),
        GuidesColor: Color.FromOkHsl(43 / 359f, 45 / 100f, 10 / 100f, 0.1f),
        BackgroundColor: Color.FromHtml("cccccc"),
        SelectingColor: Color.FromHtml("cccccc"),
        PreviewColor: Color.FromOkHsl(10 / 359f, 75 / 100f, 90 / 100f),
        LightFont: GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Light.otf"),
        MediumFont: GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Medium.otf"),
        BoldFont: GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Bold.otf")
    );
}

public record Children(
    Sprite2D Tex,
    Sprite2D Cursor,
    Label CursorLabel,
    ColorRect Background,
    CanvasLayer ControlRoot,
    Panel LayerSelector,
    HBoxContainer PreviewContainer,
    TextureRect BigPreview,
    TextureRect TinyPreviewUp,
    TextureRect TinyPreviewDown,
    FileDialog SerafFilePicker,
    VBoxContainer VBox  
);

public record UiState{
    public V2 CursorOff = V2.Zero;
    public Image CanvasImage = new();
    public (V2, string)[] MeasurementText = [];
    public float GridModifier = 4;
    public float Zoom = 1f;
    public float SelectionFadeOutTime = 1;
    public float SinceLastSelected = 0;
    public float CanvasScale = 1;
    public float CanvasScaleGoal = 1;
};

public record InputState{
    public V2 MarkerPos = UIConfig.Default.Origin;
    public Timer UndoTimer = new();
    public Timer MovementTimer = new();
    public Mode CurrentMode = Mode.Editing;
    public Focus CurrentFocus = Focus.Anchor;
    public Anchor? FocussedAnchor = null;
    public float MovementHeldTime = 0f;
    public bool DebugSwitch = false;
    public bool StickyGuide = true;
    public bool CanMoveAgain = true;
    public bool CanUndoAgain = true;
    public bool AngledMoveMode = false;
};

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
    Outline,
}

public partial class Base : Node2D
{
    public static readonly UIConfig config = UIConfig.Default;
    public static InputState input = new();
    public static UiState ui = new();
    public static Children children = null!;
    Manager Manager = null!;
    Editor Ed = null!;

    public static int FrameCounter = 0;
    public static float Counter = 0;
    List<float> DeltaTimeList = new();
    Timer FpsTimer = new();
    RichTextLabel FpsLabel = null!;
    PackedScene IndicatorScene = GD.Load<PackedScene>("res://shape_indicator.tscn");
    ShapeIndicator[] Indicators = new ShapeIndicator[10];

    string LastFramesSvg = "";
    public Shapes Shapes = new();
    public UndoRedo UndoRedo = null!;
    Shape CurrentShape = null!;
    HashSet<Anchor> SelectedAnchors = new();
    HashSet<HandlePointer> SelectedHandles = new();
    public Texture2D PreviewTex = null!;
    public bool JustUnpaused = false;

    public override void _Ready()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        Ed = (Editor)(GetParent().GetParent());
        Manager = ((Editor)GetParent().GetParent()).Manager;
        AddChild(input.UndoTimer);
        AddChild(input.MovementTimer);
        AddChild(FpsTimer);
        for (int i = 0; i < 10; i++)
        {
            ShapeIndicator indicator = IndicatorScene.Instantiate<ShapeIndicator>();
            AddChild(indicator);

            indicator.SetNumber(
                i < 9 ? i + 1 : 0
                );
            Indicators[i] = indicator;
            indicator.Visible = false;
        }

        input.MovementTimer.WaitTime = 0.01f;
        input.MovementTimer.OneShot = true;
        input.MovementTimer.Timeout += MovementTimerTimeout;
        input.UndoTimer.WaitTime = 0.5f;
        input.UndoTimer.OneShot = true;
        input.UndoTimer.Timeout += DoUndoRedo;
        FpsTimer.WaitTime = 0.5f;
        FpsTimer.OneShot = true;
        FpsTimer.Start();

        Sprite2D _cursor = GetParent().GetNode<Sprite2D>("Cursor");
        CanvasLayer _controlroot = GetNode<CanvasLayer>("ControlRoot");
        HBoxContainer _previewcontainer = (HBoxContainer)_controlroot.FindChild("PreviewContainer");
        FpsLabel = GetParent().GetNode<RichTextLabel>("FpsLabel");
        children = new(
            GetParent().GetNode<Sprite2D>("Tex"),
            _cursor,
            (Label)_cursor.GetChild(0),
            GetParent().GetNode<ColorRect>("Background"),
            _controlroot,
            (Panel)FindChild("Selector"),
            _previewcontainer,
            _previewcontainer.GetChild<TextureRect>(0),
            (TextureRect)_previewcontainer.FindChild("TinyPreview"),
            (TextureRect)_previewcontainer.FindChild("TinyPreview2"),
            GetParent().GetNode<FileDialog>("SerafFileDialog"),
            (VBoxContainer)FindChild("VBoxContainer_Layers")
        );

        GetWindow().Size = new Vector2I((int)config.WindowSize.X, (int)config.WindowSize.Y);
        children.Background.Color = config.BackgroundColor;
    }
    public void Initialize()
    {
        CurrentShape = Shapes.NewShape();
    }

    public void _OnVisibilityChanged()
    {
        children.ControlRoot.Visible = !children.ControlRoot.Visible;    
    }

    public override async void _Process(double doubleDelta)
    {
        HandleInput((float)doubleDelta);
        float delta = (float)doubleDelta;
        Counter += delta;

        UpdateUI(delta);
        if (Ed.Throttling)
            return;
        // SVG code goes here
        ui.CanvasImage.LoadSvgFromString(LastFramesSvg);
        Texture2D finalTexture = ImageTexture.CreateFromImage(ui.CanvasImage);
        SvgString.ClearString(ui.Zoom, config.Origin, config.WindowSize, input.MarkerPos);

        if (input.CurrentMode == Mode.Editing) DrawEditing();
        else if (input.CurrentMode == Mode.Previewing) DrawPreviewing();
        else if (input.CurrentMode == Mode.Selecting) DrawSelecting();

        SvgString.Finish();
        LastFramesSvg = SvgString.CurrentString.ToString();
        children.Tex.Texture = finalTexture;

        QueueRedraw();
    }


    public void UpdateUI(float delta)
    {
        UpdateFpsLabel(delta);
        ProcessCursor(delta);
        if (Ed.Throttling)
            return;
        RenderThumbnails(delta);
        DrawLayers(delta);
        PositionSelectorWidget(delta);
        UpdateShapeIndicators();

        // V2 newScale = V2.Lerp(Fun.Vtv(children.FocusIdentifier.Scale), new(1.0f, 1.0f), delta * 30);
        // children.FocusIdentifier.Scale = Fun.Vtv(newScale);
    }


    public void UpdateShapeIndicators()
    {
        if (input.CurrentMode != Mode.Editing)
        {
            foreach (ShapeIndicator indicator in Indicators)
                indicator.Visible = false;
            return;
        }

        int c = 1;
        foreach (Shape s in Shapes.S)
        {
            if (s.Anchors.Count < 3) continue;
            V2 p0 = s.SegList()[0].InPoint;
            V2 p1 = s.SegList()[0].OutPoint;
            V2 p2 = s.SegList()[^1].InPoint;
            V2 p3 = p0 - p1;
            V2 p4 = p0 - p2;
            V2 d = (V2.Normalize(p3) + V2.Normalize(p4)) / 2f;
            // if (d.Length() < 0.1) GD.Print(d);
            d = d.Length() > 0 ? d : V2.Transform(p3, Matrix3x2.CreateRotation(MathF.PI / 2));
            p0 += V2.Normalize(d) * 30;
            ShapeIndicator indic = Indicators[c - 1];
            indic.Visible = true;
            indic.Position = Fun.Vtv(p0);
            indic.SetAngle(Fun.Vtv(d).Angle());
            c += 1;
        }
    }


    public void UpdateFpsLabel(float delta)
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


    public void DrawLayers(float delta)
    {
        Image tempImage = new();
        int shapeCounter = 0;
        V2 size = new(90,90);
        float f = size.Y / config.WindowSize.Y;
        // float margin = .02f;
        // f -= margin;

        foreach (TextureRect nde in children.VBox.GetChildren())
        {
            string currentString = (
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size.X}\" height=\"{size.Y}\" >" +
                $"<g transform=\"scale({f}) translate(0,0) rotate(0)\">" +
                $"<g transform=\"scale(1) translate(0,0) rotate(0)\">");
    
            // currentString += (
            //     $"<circle cx=\"{config.Origin.X}\" cy=\"{config.Origin.Y}\" r=\"{400}\" " + 
            //     $"fill=\"{"black"}\" stroke=\"{"black"}\" fill-opacity=\"{0.5f}\" stroke-opacity=\"{0.5f}\" stroke-width=\"{0}\"/>"
            // );
    
            if (shapeCounter < Shapes.S.Count)
            {
                Shape currentShape = Shapes.S[shapeCounter];
                if (currentShape.Anchors.Count < 3)
                {
                    continue;
                }
                if (currentShape == CurrentShape)
                {
                    float l = 0;
                    currentString += (
                        $"<path d=\"M {l} {0} L {config.WindowSize.X - l} {0}\"" + 
                        $"stroke =\"{"black"}\" stroke-opacity=\"{0.4f}\" stroke-width=\"{30}\"/>"
                    );
                    currentString += (
                        $"<path d=\"M {l} {config.WindowSize.Y} L {config.WindowSize.X - l} {config.WindowSize.Y}\"" + 
                        $"stroke =\"{"black"}\" stroke-opacity=\"{0.4f}\" stroke-width=\"{30}\"/>"
                    );

                }
                Segment[] s = currentShape.SegList();
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
                if (currentShape.Negative)
                    currentString += " fill =\"red\" stroke =\"red\" fill-opacity=\"0.2\" stroke-opacity=\"1.0\" stroke-width=\"30\"/>";
                else
                    currentString += " fill =\"gray\" stroke =\"black\" fill-opacity=\"0.0\" stroke-opacity=\"1.0\" stroke-width=\"30\"/>";
                currentString += Shapes.S[shapeCounter];
            }
            currentString += (
                "</g></g></svg>" 
            );
            tempImage.LoadSvgFromString(currentString);
            nde.Texture = ImageTexture.CreateFromImage(tempImage);
            nde.StretchMode = TextureRect.StretchModeEnum.KeepCentered;

            shapeCounter += 1;
        }
    }


    public void RenderThumbnails(float delta)
    {
        V2 size = new(260, 260);
        Image thumbnail = DrawThumbnail(size);
        Image prevthumb = new(); 
        prevthumb.CopyFrom(thumbnail);

        children.BigPreview.Texture = ImageTexture.CreateFromImage(thumbnail);
        children.BigPreview.StretchMode = TextureRect.StretchModeEnum.KeepCentered;

        // thumbnail.Resize((int)size.X / 4, (int)size.Y / 4, Image.Interpolation.Lanczos);
        size = new(100, 100);
        thumbnail = DrawThumbnail(size);
        thumbnail.AdjustBcs(0, 1, 1);
        thumbnail.FlipY();
        children.TinyPreviewDown.Texture = ImageTexture.CreateFromImage(thumbnail);
        children.TinyPreviewDown.StretchMode = TextureRect.StretchModeEnum.KeepCentered;

        thumbnail.FlipY();
        thumbnail.FlipX();
        children.TinyPreviewUp.Texture = ImageTexture.CreateFromImage(thumbnail);
        children.TinyPreviewUp.StretchMode = TextureRect.StretchModeEnum.KeepCentered;

        prevthumb.Resize((int)size.X / 3, (int)size.Y / 3); 
        prevthumb.AdjustBcs(0.2f,1,1);
        PreviewTex = ImageTexture.CreateFromImage(prevthumb);
    }


    public void PositionSelectorWidget(float delta)
    {
        GV2 goalPos = new(98, 100 + Shapes.S.IndexOf(CurrentShape) * 98);
        children.LayerSelector.Position = Fun.Vtv(V2.Lerp(Fun.Vtv(children.LayerSelector.Position), Fun.Vtv(goalPos), delta * 30f));
        children.LayerSelector.Scale = Fun.Vtv(V2.Lerp(Fun.Vtv(children.LayerSelector.Scale), new V2(1.0f, 1.0f), delta * 30));
        ((RichTextLabel)children.LayerSelector.GetNode("PointAmountLabel")).Text = CurrentShape.Anchors.Count.ToString("D2") + "/24";
    }


    public static void ProcessCursor(float delta)
    {
        if (ui.Zoom <= 1)
        {
            children.Cursor.Position = Fun.Vtv(V2.Lerp(
                Fun.Vtv(children.Cursor.Position), 
                ((input.MarkerPos + ui.CursorOff) * ui.Zoom) + (config.Origin * (1f - ui.Zoom)),
                MathF.Min(delta, 0.1f) * 20f));
        }
        else
        {
            children.Cursor.Position = Fun.Vtv(config.Origin + ui.CursorOff);
        }
        children.Cursor.Scale = Fun.Vtv(V2.Lerp(
            Fun.Vtv(children.Cursor.Scale), 
            new V2(.9f, .9f), 
            MathF.Min(delta, 0.1f) * 10));
        if (children.Cursor.Scale.Length() < new V2(.85f, .85f).Length())
        {
            children.Cursor.Rotation = children.Cursor.Position.AngleToPoint(Fun.Vtv((input.MarkerPos * ui.Zoom) + (config.Origin * (1 - ui.Zoom))));
        }

        children.CursorLabel.Position = children.Cursor.Position + new GV2(30, 30);
        children.CursorLabel.Text = input.MarkerPos.X.ToString() + ", " + input.MarkerPos.Y.ToString();
        children.CursorLabel.Size = new(0, 10);
    }


    private void _DrawAngledMoveGuide()
    {
        V2 a = CurrentShape.Segments[^2].TangentAt(0.99f);
        V2 l = CurrentShape.Anchors.Last().Position;
        float tanAng = Fun.Vtv(a).Angle();
        float mAng = Fun.Vtv(input.MarkerPos - l).Angle();
        if (tanAng < mAng)
        {
            mAng -= MathF.Tau;
        }
        float diff = Mathf.Abs(tanAng - mAng); 
        GD.Print("tan: " + tanAng + "  mang: " + mAng + "  dif:" + diff);
        float d = MathF.Min(V2.Distance(input.MarkerPos, l), 100);
        DrawLine(Fun.Vtv(input.MarkerPos), Fun.Vtv(l), Colors.Red, 1);
        DrawLine(Fun.Vtv(l), Fun.Vtv(l + a * d), Colors.Red, 1);
        DrawArc(Fun.Vtv(l), d, tanAng, mAng, (int)(2 + 8 * MathF.Abs(diff / MathF.Tau)), Colors.Red, 2);
        DrawString(config.MediumFont, Fun.Vtv(l + a * (d+30)), MathF.Round((diff / MathF.Tau) * 360).ToString(), HorizontalAlignment.Center, fontSize: 32, modulate: Colors.Black);
    }

    private void _DrawGrid()
    {
        V2 drawnGridSize = new(ui.Zoom * ui.GridModifier * config.GridSize);
        GV2 gridAdjustment =  - new GV2(20,64);
        if (ui.Zoom > 1)
        {
            gridAdjustment -= new GV2(0,128);
        }
        if (ui.Zoom < 1)
        {
            gridAdjustment = new(32,32);
        }
        for (int i = 0; i < (config.WindowSize.X / drawnGridSize.X) + 30; i++)
        {
            DrawLine(
                new GV2(0, i * drawnGridSize.X - gridAdjustment.Y),
                new GV2(5000, i * drawnGridSize.X - gridAdjustment.Y), config.GridColor, 1.5f, true);
        }
        for (int i = 0; i < (config.WindowSize.Y / drawnGridSize.Y) + 30; i++)
        {
            DrawLine(
                new GV2(i * drawnGridSize.Y - gridAdjustment.X, 0),
                new GV2(i * drawnGridSize.Y - gridAdjustment.X, 5000), config.GridColor, 1.5f, true);
        }
    }

    private void _DrawMeasurements()
    {
        Color bcol = config.BackgroundColor;
        foreach ((V2 pos, string text) t in ui.MeasurementText)
        {
            DrawRect(new(t.pos.X-24, t.pos.Y-22, 10 + 13 * t.text.Length, 30), bcol);
            DrawString(config.MediumFont, Fun.Vtv(t.pos) - new GV2(20,0), t.text, fontSize: 24, modulate: Colors.Black);
        }
    }
    
    private void _DrawLettersAtAnchors()
    {
        char c = 'a';
        string tallLetters = "htldfiklb";
        string deepLetters = "qypg";
        foreach (Anchor a in CurrentShape.Anchors)
        {
            Color charColor = Colors.Black;
            Color shadowColor = Colors.Black;
            Color bcol = config.BackgroundColor;
            if (SelectedAnchors.Contains(a))
            {
                // charColor = Colors.;
                bcol = Colors.Orange;
                shadowColor = Colors.Red;
            }
            bcol.A = ui.SinceLastSelected / ui.SelectionFadeOutTime;
            charColor.A = ui.SinceLastSelected / ui.SelectionFadeOutTime;
            V2 p = a.Position;
            if (ui.Zoom <= 1)
            {
                p = (p * ui.Zoom) + config.Origin * (1f - ui.Zoom);
            }
            else
            {
                p -= config.Origin - 1.3333f * (config.Origin - input.MarkerPos);
                p *= ui.Zoom;
                p += config.Origin - 1.3333f * (config.Origin - input.MarkerPos);
                p -= new V2(4, -5);
            }
            V2 letterOffset = new(-9, 7);
            V2 totalOffset = new(0,0);
            var pp = a.OutHandle.Position();
            var ppp = a.InHandle.Position();
            Color bc = new(bcol.R-.2f, bcol.G-.1f, bcol.B-.1f, 0.9f);
            if (tallLetters.Contains(c)) letterOffset += new V2(0, 3);
            if (deepLetters.Contains(c)) letterOffset -= new V2(0, 3);
            DrawRect(new(Fun.Vtv(p - new V2(12, 16) + totalOffset), new GV2(24, 30)), bcol);
            DrawChar(config.MediumFont, Fun.Vtv(p + letterOffset + totalOffset), c.ToString(), 30, charColor);
            c = (char)((int)c + 1);
        }
    }


    public override void _Draw()
    {
        if (input.CurrentMode == Mode.Editing)
        {
            if (!(ui.Zoom < 1 && ui.GridModifier < 2f)) _DrawGrid();
            if (input.AngledMoveMode) _DrawAngledMoveGuide();
            _DrawMeasurements();
        }

        else if (input.CurrentMode != Mode.Previewing)
        {
            _DrawLettersAtAnchors();
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
                if (input.CurrentFocus == Focus.Outline || input.CurrentFocus == Focus.Handle)
                {
                    SelectedHandles = [];
                    SelectedAnchors = [];
                    input.CurrentFocus = Focus.Anchor;
                }
                else
                    SelectedAnchors.Clear();
            }
            else if (input.CurrentMode == Mode.Selecting && !(at == "Shift+Space" || at == "Space" || at == "Semicolon") && at.Length > 0)
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
        Anchor a = CurrentShape.GetAnchorFromLabel(s.ToLower());
        if (!SelectedAnchors.Remove(a))
        {
            if (input.CurrentFocus == Focus.Handle)
                SelectedHandles = [new(a, true)];
            else if (input.CurrentFocus == Focus.Outline)
                SelectedAnchors = [a];
            else
                SelectedAnchors.Add(a);
        }
    }


    public void HiRotation(float delta, bool rotationInputPressed)
    {
        if (!rotationInputPressed) return;
        float rotationAmount = 0.05f;
        float ang = rotationAmount * 2 * MathF.PI * delta;
        Uts();
        if (Input.IsActionPressed(Snl.rotate_cw_points)) ang *= -1;
        foreach (Anchor a in SelectedAnchors)
        {
            V2 spot = a.Position - input.MarkerPos;
            V2 displacement = new(MathF.Cos(ang) * spot[0] - MathF.Sin(ang) * spot[1], MathF.Sin(ang) * spot[0] + MathF.Cos(ang) * spot[1]);
            a.Position += displacement - spot;
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
                V2 spot = a.Position - input.MarkerPos;
                V2 displacement = new(spot[0] * xScalar, spot[1] * yScalar);
                a.Position += displacement - spot;
            }
        }
    }

    public void HiMovement(float delta)
    {
        int movementAmount = (int)(config.GridSize * ui.GridModifier);
        if (input.MovementTimer.IsStopped())
        {
            input.MovementTimer.Start();
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
                if (input.MovementHeldTime > config.ValidHoldTime && input.CanMoveAgain)
                {
                    movingSelected.X -= movementAmount;
                }
                else
                {
                    input.MovementHeldTime += delta;
                }
            }
            if (Input.IsActionPressed(Snl.right))
            {
                pressingMovementKey = true;
                if (input.MovementHeldTime > config.ValidHoldTime && input.CanMoveAgain)
                {
                    movingSelected.X += movementAmount;
                }
                else
                {
                    input.MovementHeldTime += delta;
                }
            }
            if (Input.IsActionPressed(Snl.up))
            {
                pressingMovementKey = true;
                if (input.MovementHeldTime > config.ValidHoldTime && input.CanMoveAgain)
                {
                    movingSelected.Y -= movementAmount;
                }
                else
                {
                    input.MovementHeldTime += delta;
                }
            }
            if (Input.IsActionPressed(Snl.down))
            {
                pressingMovementKey = true;
                if (input.MovementHeldTime > config.ValidHoldTime && input.CanMoveAgain)
                {
                    movingSelected.Y += movementAmount;
                }
                else
                {
                    input.MovementHeldTime += delta;
                }
            }
            if (!pressingMovementKey)
            {
                input.MovementHeldTime = 0;
            }
        }

        if (movingSelected != V2.Zero)
        {
            input.CanMoveAgain = false;
            if (!OnGuide())
            {
                input.StickyGuide = true;
            }
            // move anchors
            if ((input.CurrentFocus == Focus.Anchor || input.CurrentFocus == Focus.Outline) && SelectedAnchors.Count > 0)
            {
                Uts();
                if (Input.IsKeyPressed(Key.Apostrophe))
                {
                    if (Input.IsKeyPressed(Key.A))
                    {
                        movingSelected *= 8;
                    }
                    foreach (Anchor a in SelectedAnchors)
                    {
                        a.anchorRounding = Math.Clamp(a.anchorRounding + (int)(-movingSelected.Y) / 5, 1, 600);
                        a.intersectionRounding = Math.Clamp(a.intersectionRounding + (int)(movingSelected.X) / 5, 1, 600);
                        a.MyShape.AnchorsChanged();
                    }
                }
                else
                {
                    foreach (Anchor a in SelectedAnchors)
                    {
                        a.Position += movingSelected;
                        a.AlignHandles();
                        a.MyShape.AnchorsChanged();
                    }
                }
            }
            // move handles
            else if (SelectedHandles.Count > 0)
            {
                Uts();
                float movMod = .1f;
                if (Input.IsKeyPressed(Key.A)) movMod = .01f;
                float angleChange = (movingSelected.X / movementAmount) * movMod;
                foreach (HandlePointer hp in SelectedHandles)
                {
                    Handle h = hp.h();
                    Handle sibling = h.GetAnchorSibling();
                    hp.A.Auto = false;
                    h.DistanceFromAnchor -= (movingSelected.Y) * .1f;

                    if (hp.A.Broken)
                    {
                        h.Angle += angleChange;
                    }
                    else 
                    {
                        h.Angle += angleChange;
                        if (!SelectedHandles.Contains(sibling.Pointer()))
                        {
                            sibling.Angle += angleChange;
                        }
                    }
                    
                    hp.A.MyShape.AnchorsChanged();
                    Manager.PlaySound("Rattle3.wav", 0.07f, 1.5f, 2.5f);
                    if (!Input.IsKeyPressed(Key.A))
                    {
                        if (angleChange < 0)
                        {
                            h.Angle = MathF.Floor(h.Angle / (MathF.PI * .125f)) * (MathF.PI * .125f);
                            if (!hp.A.Broken)
                                sibling.Angle = MathF.Floor(sibling.Angle / (MathF.PI * .125f)) * (MathF.PI * .125f);

                        }
                        if (angleChange > 0)
                        {
                            h.Angle = MathF.Ceiling(h.Angle / (MathF.PI * .125f)) * (MathF.PI * .125f);
                            if (!hp.A.Broken)
                                sibling.Angle = MathF.Ceiling(sibling.Angle / (MathF.PI * .125f)) * (MathF.PI * .125f);

                        }
                    }
                    
                    //TODO align handles?
                }
            }
            // move marker
            else
            {
                if (input.AngledMoveMode)
                {
                    var lap = CurrentShape.Anchors.Last().Position;
                    float tan = Fun.Vtv(CurrentShape.Segments[^2].TangentAt(0.99f)).Angle();
                    if (!Input.IsKeyPressed(Key.A))
                    {
                        var fr = MathF.PI * .125f;
                        input.MarkerPos = (lap + V2.Transform(input.MarkerPos - lap, Matrix3x2.CreateRotation(MathF.Sign(movingSelected.X) * .20f)));
                        float ang = Fun.Vtv(input.MarkerPos - lap).Angle();
                        var dis = V2.Distance(input.MarkerPos, lap);
                        if (MathF.Abs(movingSelected.X) > 0)
                        {
                            if (dis < 30)
                                ang = tan - MathF.PI * .5f;
                            else
                                ang = MathF.Round((Fun.Vtv(lap).AngleToPoint(Fun.Vtv(input.MarkerPos)  ) - (tan % fr)) / fr) * fr  + (tan % fr);
                        }
                        input.MarkerPos = lap + Fun.Vtv(GV2.FromAngle(ang)) * MathF.Max(dis, 30);
                    }
                    else
                        input.MarkerPos = (lap + V2.Transform(input.MarkerPos - lap, Matrix3x2.CreateRotation(MathF.Sign(movingSelected.X) * .01f)));
                    V2 t = V2.One;

                    t = -Fun.Vtv(Fun.Vtv(lap).DirectionTo(Fun.Vtv(input.MarkerPos)));
                    input.MarkerPos += t * movingSelected.Y;
                }
                else
                {
                    input.MarkerPos += movingSelected;
                }
                // TODO abstract cursor?
                // input.MarkerPos += movingSelected;
                children.Cursor.Scale = new(.7f, .3f);
            }
        }

        // TODO: make clockwise?
        input.MarkerPos = V2.Clamp(input.MarkerPos, config.Borders.Item1, config.Borders.Item2);

        if (OnGuide() && input.StickyGuide)
        {
            input.MovementTimer.WaitTime = .2f;
            input.MovementTimer.Start();
            input.CanMoveAgain = false;
            input.StickyGuide = false;
        }
        else
        {
            input.MovementTimer.WaitTime = .01f;
        }

    }

    public void HiPointAdding(float delta)
    {
        // TODO split up
        if (input.CurrentFocus == Focus.Anchor)
        {
            if (Input.IsActionJustPressed(Snl.switch_segment_style) || Input.IsActionJustPressed(Snl.finish_shape))
            {
                Anchor fanchor = null!;
                if (SelectedAnchors.Count > 0)
                {
                    fanchor = SelectedAnchors.Last();
                }
                else
                    fanchor = CurrentShape.Anchors.Last();
                HandlePointer hp = new(fanchor, true);
                SelectedHandles = [hp];
                input.CurrentFocus = Focus.Handle;
            }


            if (!Input.IsKeyPressed(Key.Shift) && (Input.IsActionJustPressed(Snl.add_new_point) || Input.IsActionJustPressed(Snl.add_sharp_point)))
            {
                Uts();

                if (Input.IsActionJustPressed(Snl.add_sharp_point))
                {
                    if (CurrentShape.Finished)
                    {
                        CurrentShape = Shapes.NewShape();
                    }
                    CurrentShape.AddAnchor(input.MarkerPos);
                    Manager.PlaySound("Laptop_Keystroke_82.wav", 0.2f, 1.3f, 1.8f);
                }
                else if (Input.IsActionJustPressed(Snl.add_new_point))
                {
                    if (CurrentShape.Finished)
                    {
                        CurrentShape = Shapes.NewShape();
                    }
                    CurrentShape.AddAnchor(input.MarkerPos, broken: true);
                    Manager.PlaySound("Laptop_Keystroke_82.wav", 0.2f, 1.3f, 1.8f);
                }
                SelectedAnchors = [];
                CurrentShape.AnchorsChanged();
                children.Cursor.Scale = new(.8f, .8f);
            }

            if (Input.IsActionJustPressed(Snl.insert_point))
            {
                foreach (Anchor a in SelectedAnchors)
                {
                    if (SelectedAnchors.Contains(a.NextAnchor()))
                    {
                        Uts();
                        CurrentShape.AddAnchor(input.MarkerPos, insertAfter: a);
                        CurrentShape.AnchorsChanged();
                        children.Cursor.Scale = new(.8f, .8f);
                        Manager.PlaySound("Laptop_Keystroke_82.wav", 0.2f, 1.3f, 1.8f);
                    }
                }
            }

            if (Input.IsActionJustPressed(Snl.add_new_point) && Input.IsKeyPressed(Key.Shift) && !CurrentShape.Finished)
            {
                Uts();
                Shapes.FinishLastShape();
                Manager.PlaySound("camera.wav", 0.4f, 1.3f, 1.8f);

            }

            if (Input.IsActionJustPressed(Snl.snap_selected))
            {
                Uts();
                SnapSelectedPos();
            }

            if (input.CurrentFocus == Focus.Anchor && (Input.IsActionJustReleased(Snl.switch_segment_style) || Input.IsActionJustReleased(Snl.finish_shape)))
            {
                SelectedHandles = [];
            }

        }
        else if (input.CurrentFocus == Focus.Handle)
        {
            if (Input.IsActionJustPressed(Snl.switch_segment_style))
            {
                if (CurrentShape.IsClockwise())
                    SelectedHandles = [SelectedHandles.Last().Next()];
                else
                    SelectedHandles = [SelectedHandles.Last().Previous()];
            }
            else if (Input.IsActionJustPressed(Snl.finish_shape))
            {
                if (CurrentShape.IsClockwise())
                    SelectedHandles = [SelectedHandles.Last().Previous()];
                else
                    SelectedHandles = [SelectedHandles.Last().Next()];
            }

            if (Input.IsActionJustPressed(Snl.add_new_point))
            {
                input.CurrentFocus = Focus.Outline;
                SelectedAnchors = [SelectedHandles.Last().A];
                SelectedHandles = [];
            }

            if (Input.IsActionJustPressed(Snl.add_sharp_point))
            {
                SelectedHandles.Last().A.Broken = !SelectedHandles.Last().A.Broken;
            }
        }
        else if (input.CurrentFocus == Focus.Outline)
        {
            if (Input.IsActionJustPressed(Snl.switch_segment_style))
            {
                if (CurrentShape.IsClockwise())
                    SelectedAnchors = [SelectedAnchors.Last().NextAnchor()];
                else
                    SelectedAnchors = [SelectedAnchors.Last().PreviousAnchor()];
            }
            else if (Input.IsActionJustPressed(Snl.finish_shape))
            {
                if (CurrentShape.IsClockwise())
                    SelectedAnchors = [SelectedAnchors.Last().PreviousAnchor()];
                else
                    SelectedAnchors = [SelectedAnchors.Last().NextAnchor()];
            }

            if (Input.IsActionJustPressed(Snl.add_new_point))
            {
                input.CurrentFocus = Focus.Handle;
                HandlePointer hp = new(SelectedAnchors.Last(), true);
                SelectedHandles = [hp];
                SelectedAnchors = [];
            }

            if (Input.IsActionJustPressed(Snl.add_sharp_point))
            {
            }
        }

        if (Input.IsActionJustPressed(Snl.auto_move_mode))
        {
            input.AngledMoveMode = !input.AngledMoveMode;
        }

        if (Input.IsActionJustPressed(Snl.increase_zoom))
        {
            ui.CanvasScaleGoal = 1.5f;
            Fun.Delayed(this, 0.05f,
            () =>
            {
                if (ui.Zoom >= 1 && ui.Zoom < 4) ui.Zoom *= 4;
                else if (ui.Zoom < 1) ui.Zoom *= 2;
                ui.CanvasScale = 1;
                ui.CanvasScaleGoal = 1;
                children.Tex.Scale = GV2.One;
            });
        }
        if (Input.IsActionJustPressed(Snl.decrease_zoom))
        {
            ui.CanvasScaleGoal = 0.75f;
            Fun.Delayed(this, 0.05f,
            () =>
            {
                if (ui.Zoom <= 1 && ui.Zoom > .5f) ui.Zoom /= 2;
                else if (ui.Zoom > 1) ui.Zoom /= 4;
                ui.CanvasScale = 1;
                ui.CanvasScaleGoal = 1;
                children.Tex.Scale = GV2.One;
            });
        }
        if (Input.IsActionJustPressed(Snl.increase_grid_modifier))
        {
            if (ui.GridModifier < 6)
            {
                ui.GridModifier *= 2;
                SnapMarkerPos();
            }
        }
        if (Input.IsActionJustPressed(Snl.decrease_grid_modifier))
        {
            if (ui.GridModifier > 1)
            {
                ui.GridModifier /= 2;
                SnapMarkerPos();
            }
        }
    }

    public async Task HandleInput(float delta)
    {
        if (JustUnpaused)
        {
            Fun.DelayOneFrame(this, () => {JustUnpaused = false;});
            return;
        }

        if (Input.IsActionJustPressed(Snl.debug))
            input.DebugSwitch = !input.DebugSwitch;


        if (Input.IsActionJustPressed(Snl.save_project))
        {
            children.SerafFilePicker.FileMode = FileDialog.FileModeEnum.SaveFile;
            children.SerafFilePicker.Visible = true;
            string file = (string)(await ToSignal(children.SerafFilePicker, FileDialog.SignalName.FileSelected))[0];
            var fileAc = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Write);
            fileAc.StoreString(Shapes.SaveState());
            fileAc.Close();
        }

        if (Input.IsActionJustPressed(Snl.load_project))
        {
            children.SerafFilePicker.FileMode = FileDialog.FileModeEnum.OpenFile;
            children.SerafFilePicker.Visible = true;
            string file = (string)(await ToSignal(children.SerafFilePicker, FileDialog.SignalName.FileSelected))[0];
            var fileAc = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Read);
            Shapes.LoadState(fileAc.GetAsText());
            CurrentShape = Shapes.S[0]; 
        }

        // if (Input.IsActionJustPressed(Snl.switch_focus))
        // {
        //     Manager.PlaySound("mine_2.wav", 0.07f, 2.0f, 2.3f);
        //     if (CurrentFocus == Focus.Anchor)
        //     {
        //         FocussedAnchor = CurrentShape.Anchors.Last();
        //         HandlePointer hp = new(FocussedAnchor, true);
        //         SelectedHandles = [hp];
        //         ((Sprite2D)FocusIdentifier.GetChild(0)).Visible = false;
        //         ((Sprite2D)FocusIdentifier.GetChild(1)).Visible = true;
        //         FocusIdentifier.Scale = new GV2(0.3f, 3f);
        //         CurrentFocus = Focus.Handle;
        //     }
        //     else if (CurrentFocus == Focus.Handle)
        //     {
        //         SelectedHandles = [];
        //         ((Sprite2D)FocusIdentifier.GetChild(0)).Visible = true;
        //         ((Sprite2D)FocusIdentifier.GetChild(1)).Visible = false;
        //         FocusIdentifier.Scale = new GV2(0.3f, 3f);
        //         CurrentFocus = Focus.Anchor;
        //     }
        //}

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
            if (input.CurrentMode == Mode.Editing)
            {
                input.CurrentMode = Mode.Previewing;
                children.Background.Color = config.PreviewColor;
                children.ControlRoot.Visible = false;
                children.Cursor.Visible = false;
            }
            else if (input.CurrentMode == Mode.Previewing)
            {
                input.CurrentMode = Mode.Editing;
                children.Background.Color = config.BackgroundColor;
                children.ControlRoot.Visible = true;
                children.Cursor.Visible = true;
            }
        }

        if (Input.IsActionPressed(Snl.select_mode))
        {
            ui.SinceLastSelected = ui.SelectionFadeOutTime;
            input.CurrentMode = Mode.Selecting;
            children.Background.Color = config.SelectingColor;
            children.Cursor.Visible = false;
        }
        else
        {
            if (ui.SinceLastSelected > 0) ui.SinceLastSelected -= delta;
            if (input.CurrentMode == Mode.Selecting)
            {
                input.CurrentMode = Mode.Editing;
                children.Background.Color = config.BackgroundColor;
                children.Cursor.Visible = true;
            }

        }
        if (input.CurrentMode == Mode.Editing || input.CurrentMode == Mode.Previewing)
        {
            HiMovement(delta);
            HiPointAdding(delta);
            // HiScaling(delta);
            // HiRotation(delta, (Input.IsActionPressed(Snl.rotate_cw_points) || 
            //                    Input.IsActionPressed(Snl.rotate_ccw_points)));
        
            {
                foreach (Anchor a in CurrentShape.Anchors)
                {
                    a.AutoHandles();
                }
                foreach (Anchor a in CurrentShape.Anchors)
                {
                    a.AutoHandles();
                }
            }
            foreach (Shape s in Shapes.S)
            {
                // s.AlignAllHandles();
            }
        }
    }

    static public bool OnGuide()
    {
        if ((int)input.MarkerPos.X == config.Origin.X + config.LeftWidthLine ||
            (int)input.MarkerPos.X == config.Origin.X + config.RightWidthLine ||
            (int)input.MarkerPos.Y == config.Origin.Y + config.XLineHeight ||
            (int)input.MarkerPos.Y == config.Origin.Y + config.BaseLineHeight ||
            (int)input.MarkerPos.Y == config.Origin.Y + config.AscenderLineHeight ||
            (int)input.MarkerPos.Y == config.Origin.Y + config.DescenderLineHeight)
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
        int totalSize = (int)ui.GridModifier * config.GridSize;
        input.MarkerPos.X = (int)(input.MarkerPos.X / totalSize) * totalSize;
        input.MarkerPos.Y = (int)(input.MarkerPos.Y / totalSize) * totalSize;
        input.MarkerPos -= gridAdjustment;
    }

    //TODO implement
    public void SnapSelectedPos()
    {
        int totalSize = (int)ui.GridModifier * config.GridSize;
    }

    public void Uts()
    {
        if (input.CanUndoAgain)
        {
            UndoRedo.CurrentShapesToUndoStack();
            UndoRedo.ClearRedoStack();
            input.CanUndoAgain = false;
        }
        if (input.UndoTimer.IsStopped())
        {
            input.UndoTimer.Start();
        }
    }

    public void DoUndoRedo()
    {
        input.CanUndoAgain = true;
    }

    public void MovementTimerTimeout()
    {
        input.CanMoveAgain = true;
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
        if (config.Debug)
        {
            var mgs = Shapes.MergeShapesSkia();
            if (mgs.Length > 0)
                SvgString.AddSegmentsDebug(Shapes.MergeShapesSkia()[0]);
        }
        else
            SvgString.AddSegmentsGroup(Shapes.MergeShapesSkia());
    }

    public void DrawGuides(float opac = 0.5f, float lesserOpac = 0.12f, float fwi = 2.0f)
    {
        // draw guides
        V2 orig = config.Origin;
        if (ui.Zoom > 1)
        {
            fwi /= 2; 
        }
        else if (ui.Zoom < 1)
        {
            fwi *= 1.5f; 
        }
        float zero = -5000;
        SvgString.AddLine(new V2(zero, orig.Y + config.AscenderLineHeight), new V2(5000, orig.Y + config.AscenderLineHeight), sWidth: fwi, sOpacity: lesserOpac);
        SvgString.AddLine(new V2(zero, orig.Y + config.DescenderLineHeight), new V2(5000, orig.Y + config.DescenderLineHeight), sWidth: fwi, sOpacity: lesserOpac);

        SvgString.AddLine(new V2(orig.X + config.LeftWidthLine, zero), new V2(orig.X + config.LeftWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(orig.X + config.LeftWidthLine, zero), new V2(orig.X + config.LeftWidthLine, 5000), sWidth: fwi, sOpacity: opac);
        SvgString.AddLine(new V2(orig.X + config.RightWidthLine, zero), new V2(orig.X + config.RightWidthLine, 5000), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1.0f);
        SvgString.AddLine(new V2(orig.X + config.RightWidthLine, zero), new V2(orig.X + config.RightWidthLine, 5000), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(zero, orig.Y + config.XLineHeight), new V2(5000, orig.Y + config.XLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(zero, orig.Y + config.XLineHeight), new V2(5000, orig.Y + config.XLineHeight), sWidth: fwi, sOpacity: opac);

        SvgString.AddLine(new V2(zero, orig.Y + config.BaseLineHeight), new V2(5000, orig.Y + config.BaseLineHeight), stroke: "rgb(204,204,204)", sWidth: 20, sOpacity: 1);
        SvgString.AddLine(new V2(zero, orig.Y + config.BaseLineHeight), new V2(5000, orig.Y + config.BaseLineHeight), sWidth: fwi, sOpacity: opac);
    }

    public void DrawEditing()
    {
        DrawGuides(opac: 0.3f, fwi: 3f);
        float[] radiusSizes = [6, 12];
        float[] widths = [1, 3];
        if (ui.Zoom > 1)
        {
            radiusSizes = [3,4];
            widths = [0.5f, 1.0f];
        }
        else if (ui.Zoom < 1)
        {
            radiusSizes = [12,32];
            widths = [2f, 6f];
        }
        // draw unmerged shape underlays
        if (CurrentShape.Anchors.Count == 0)
        {
            return;
        }

        Segment[][] sss = Shapes.MergeShapesSkia();
        // SvgString.SetStyle(Style.ShapeUnderlay);
        // SvgString.AddSegmentsGroup(sss);

        foreach (Shape s in Shapes.S)
        {
            if (s.Anchors.Count < 3) continue;

            if (s.Negative)
            {
                // SvgString.SetStyle(Style.ShapeShadow);
                // SvgString.AddSegments(s.SegList());
                SvgString.SetStyle(Style.ShapeNegative);
            }
            else
            {
                // SvgString.SetStyle(Style.ShapeShadow);
                // SvgString.AddSegments(s.SegList());
                SvgString.SetStyle(Style.ShapeUnchanged);
            }

            if (config.Debug)
                SvgString.AddSegmentsDebug(s.SegList());
            else
                SvgString.AddSegments(s.SegList());
        }

        foreach (Shape s in Shapes.S)
        {
            if (s.Anchors.Count < 3) continue;

            if (s.Negative)
            {
                SvgString.SetStyle(Style.ShapeShadow);
                SvgString.AddSegments(s.SegList(true));
                SvgString.SetStyle(Style.ShapeNegative);
            }
            else
            {
                SvgString.SetStyle(Style.ShapeShadow);
                SvgString.AddSegments(s.SegList(true));
                SvgString.SetStyle(Style.ShapeUnchanged);
            }

            if (config.Debug)
                SvgString.AddSegmentsDebug(s.SegList());
            else
                SvgString.AddSegments(s.SegList(true));
        }
        SvgString.SetStyle(Style.ShapeOverShadow);
        SvgString.AddSegmentsGroup(sss);
        if (CurrentShape.Anchors.Count >= 3)
        {
            SvgString.SetStyle(Style.ShapeUnchangedSelected); 
            SvgString.AddSegments(CurrentShape.SegList(true));
        }

        if (CurrentShape.Finished && false)
        {
            V2 tan = Fun.Vtv(Player.ProjectOnShapeTangent(sss[0], input.MarkerPos));
            tan = V2.Transform(tan, Matrix3x2.CreateRotation(MathF.PI * .5f));
            V2 lineStart = input.MarkerPos - tan * 2000;
            V2 lineEnd = input.MarkerPos + tan * 2000;
            ui.MeasurementText = [];
            int pointcount = 0;
            foreach (Segment[] s in sss)
            {
                pointcount += s.Length;
                Segment measure = new(lineStart,lineStart, lineEnd, lineEnd);
                GV2[] inters = Player.SegmentShapeIntersections(s, measure);
                List<V2> il = [];
                foreach (GV2 gv in inters)
                    il.Add(Fun.Vtv(gv));
                il.Sort(CompareVectors);
                for (int i = 0; i < il.Count; i++)
                {
                    V2 current = il[i];
                    if (i < il.Count - 1)
                    {
                        V2 next = il[i+1];
                        int d = (int)V2.Distance(current, next);
                        V2 halfway = (current + next) / 2.0f;
                        ui.MeasurementText = [.. ui.MeasurementText, new(halfway, d.ToString())];
                    }
                    SvgString.AddCircle(current, 7, fill: "red", fOpacity: 0.5f, sWidth: 0);
                }
            }
            SvgString.AddLine(lineStart,lineEnd);
            // GD.Print($"point count: {pointcount}");
        }
        var lot = Shapes.ListOfTangents(sss);


        // draw ui overlays (anchors and handles)
        foreach (Shape s in Shapes.S)
        {

            if (false && s == CurrentShape)
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
                    SvgString.AddCircle(a.Position, radiusSizes[1]+8, fill: "red", fOpacity: 0.2f, sOpacity: 1.0f, sWidth: widths[0]);
                }
                else if (SelectedHandles.Contains(a.InHandle.Pointer()) || SelectedHandles.Contains(a.OutHandle.Pointer()))
                {
                    SvgString.AddCircle(a.Position, radiusSizes[1], fill: "white", fOpacity: 0.3f, sOpacity: 1.0f, sWidth: widths[0]);
                }
                else 
                {
                    SvgString.AddCircle(a.Position, radiusSizes[0], fOpacity: 0, sOpacity: 1.0f, sWidth: widths[0]);
                }

                // draw handles
                // visualize handle selection
                // visualize handle type
                if (s == CurrentShape)
                {
                    float strobing = (MathF.Sin(Counter*2) + 1) / 2; 
                    strobing = .5f;
                    
                    if (SelectedHandles.Contains(a.InHandle.Pointer()))
                    {
                        SvgString.AddLine((2 * a.Position + a.InHandle.Position()) / 3, a.InHandle.Position(), stroke: "black", sOpacity: .5f);
                        SvgString.AddCircle(a.InHandle.Position(), radiusSizes[1]-2, fill: "white", stroke: "black", fOpacity: 1.0f, sOpacity: 1.0f, sWidth: widths[1]);
                    }
                    else
                    {
                        if (input.CurrentFocus == Focus.Handle)
                        {
                            SvgString.AddLine((2 * a.Position + a.InHandle.Position()) / 3, a.InHandle.Position(), stroke: "black", sOpacity: .5f);
                            SvgString.AddCircle(a.InHandle.Position(), radiusSizes[0]+1, fill: "black", stroke: "white", fOpacity: .4f, sOpacity: 1f, sWidth: widths[0]);
                        }
                        else
                            SvgString.AddCircle(a.InHandle.Position(), radiusSizes[0]+1, fill: "black", stroke: "white", fOpacity: .4f * strobing, sOpacity: 1f * strobing, sWidth: widths[0]);
                    }

                    if (SelectedHandles.Contains(a.OutHandle.Pointer()))
                    {
                        SvgString.AddLine((2 * a.Position + a.OutHandle.Position()) / 3, a.OutHandle.Position(), stroke: "black", sOpacity: .5f);
                        SvgString.AddCircle(a.OutHandle.Position(), radiusSizes[1]-2, fill: "white", stroke: "black", fOpacity: 1.0f, sOpacity: 1.0f, sWidth: widths[1]);
                    }
                    else
                    {
                        if (input.CurrentFocus == Focus.Handle)
                        {
                            SvgString.AddLine((2 * a.Position + a.OutHandle.Position()) / 3, a.OutHandle.Position(), stroke: "black", sOpacity: .5f);
                            SvgString.AddCircle(a.OutHandle.Position(), radiusSizes[0]+1, fill: "black", stroke: "white", fOpacity: .4f, sOpacity: 1f, sWidth: widths[0]);
                        }
                        else
                            SvgString.AddCircle(a.OutHandle.Position(), radiusSizes[0]+1, fill: "black", stroke: "white", fOpacity: .4f * strobing, sOpacity: 1f * strobing, sWidth: widths[0]);
                    }
                    
                }
            }
        }
    }

    int CompareVectors(V2 one, V2 two)
    {
        if (one.X > two.X)
            return 1;
        else if (one.X < two.X)
            return -1;
        else
        {
            return (int)(one.Y - two.Y);
        }
    }

    public Image DrawThumbnail(V2 size)
    {
        float f = size.Y / config.WindowSize.Y;
        float margin = .02f;
        f -= margin;

        SvgString.ClearString(f, (size * (margin / f))/2, size, input.MarkerPos);
        
        DrawPreviewing(true);
        SvgString.Finish();
        Image thumbnail = new();
        thumbnail.LoadSvgFromString(SvgString.CurrentString.ToString());
        return thumbnail;
    }

    public void DrawSelecting()
    {
        DrawGuides(0.04f, 0.04f, 8);
        float[] radiusSizes = [6, 16];
        float[] widths = [1, 3];
        if (ui.Zoom > 1)
        {
            radiusSizes = [3,4];
            widths = [0.5f, 1.0f];
        }
        else if (ui.Zoom < 1)
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
            }
        }
    }

}
