using Godot;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using System.Threading.Tasks;

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
    static V2 _windowsize = new V2(120, 80) * UIConfig._gridsize;
    static V2 _originoff = new(300, 0);
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
        LightFont: GD.Load<Font>("res://assets/fonts/DraftingMono/DraftingMono-Light.otf"),
        MediumFont: GD.Load<Font>("res://assets/fonts/DraftingMono/DraftingMono-Medium.otf"),
        BoldFont: GD.Load<Font>("res://assets/fonts/DraftingMono/DraftingMono-Bold.otf")
    );
}

record Children(
    Sprite2D Cursor,
    Label CursorLabel,
    ColorRect Background,
    SidePanel SidePanel,
    TextureRect Tex
);

// pass along a ui state object?
// try simplifying first -> all changes to an object / child should happen at the end / in the same place
// simplify and group, make order clear. 
public partial class WorkbenchUi : Control
{
    public Image CanvasImage = new();
    public Editor Ed = null!;
    public SvgString svgString = new();
    public Workbench workbench = null!;
    public float Zoom = 1f;
    public readonly UIConfig Config = UIConfig.Default;

    V2 CursorOff = V2.Zero;
    (V2, string)[] MeasurementText = [];
    float SelectionFadeOutTime = 1;
    float SinceLastSelected = 0;
    string LastFramesSvg = "";
    Texture2D LastFramesTexture = new();

    Func<Shape, bool> currentShapeChanged = null!;
    Func<InputMode, bool> inputModeChanged = null!;
    InputState currentInput = new();
    Children c = null!;
    PackedScene IndicatorScene = GD.Load<PackedScene>("res://scenes/shape_indicator.tscn");
    ShapeIndicator[] Indicators = new ShapeIndicator[10];
    FontFile LatestGlyphPreviewTtf = new();

    // scary
    Func<T, bool> CreateChangeChecker<T>()
    {
        T previous = default!;
        int frameCount = -1;
        bool thisFramesOutput = false;
        return current =>
        {
            if (frameCount == Ed.FrameCounter) return thisFramesOutput;
            bool changed = !EqualityComparer<T>.Default.Equals(previous,current);
            previous = current;
            frameCount = Ed.FrameCounter;
            thisFramesOutput = changed;
            return thisFramesOutput;
        };
    }

    public override void _Ready()
    {
        workbench = GetParent<Workbench>();
        Ed = workbench.GetParent<Editor>();
        currentShapeChanged = CreateChangeChecker<Shape>();
        inputModeChanged = CreateChangeChecker<InputMode>();
        Sprite2D _cursor = GetNode<Sprite2D>("Cursor");
        SidePanel _sidePanel = GetNode<SidePanel>("SidePanel");
        c = new(
            _cursor,
            (Label)_cursor.GetChild(0),
            GetNode<ColorRect>("Background"),
            _sidePanel,
            (TextureRect)FindChild("Tex")
        );
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
        c.Background.Color = Config.BackgroundColor;
    }

    public override void _Process(double delta)
    {
    }

    public void ResetUiState()
    {
        Zoom = 1f;
        CursorOff = V2.Zero;
        MeasurementText = [];
        SelectionFadeOutTime = 1;
        SinceLastSelected = 0;
        LastFramesSvg = "";
        LastFramesTexture = new();
    }

    public async Task UpdateUI(float delta, Shape currentShape, Shapes shapes, HashSet<Anker> selectedAnchors, HashSet<HandlePointer> selectedHandles, InputState i)
    {
        currentInput = i;

        if (currentShapeChanged(currentShape)) UpdateShapeLayersIndicator(currentShape);
        c.SidePanel.DrawLayers(delta, currentShape, shapes);
        ProcessInputMode(delta, currentInput.CurrentMode);
        ProcessCursor(delta);
        UpdateShapeIndicators(shapes);
        svgString.ClearString(Zoom, Config.Origin, Config.WindowSize, currentInput.MarkerPos, CursorOff);
        UpdateSvg(currentShape, shapes, selectedAnchors, selectedHandles);
        FinishSvg();

        QueueRedraw();
        await _DrawCommands(currentShape, shapes, selectedAnchors);

    }

    public void FinishSvg()
    {
        svgString.Finish();
        CanvasImage.LoadSvgFromString(svgString.String());
        c.Tex.Texture = ImageTexture.CreateFromImage(CanvasImage);
    }

    public void UpdateSvg(Shape currentShape, Shapes shapes, HashSet<Anker> selectedAnchors, HashSet<HandlePointer> selectedHandles)
    {
        if (currentInput.CurrentMode == InputMode.Editing) svgString.DrawEditing(
            currentShape, shapes, Zoom, currentInput.MarkerPos, currentInput.CurrentFocus, selectedAnchors, selectedHandles);
        else if (currentInput.CurrentMode == InputMode.Previewing) svgString.DrawPreviewing(shapes);
        else if (currentInput.CurrentMode == InputMode.Selecting) svgString.DrawSelecting(currentShape, shapes, Zoom);

    }

    public void ProcessInputMode(float delta, InputMode currentMode)
    {
        if (currentMode == InputMode.Selecting) SinceLastSelected = SelectionFadeOutTime;
        else if (SinceLastSelected > 0) SinceLastSelected -= delta;
            
        if (!inputModeChanged(currentMode)) return;

        InputMode m = currentMode;
        if (m == InputMode.Previewing)
        {
            c.Background.Color = Config.PreviewColor;
            c.SidePanel.Visible = false;
            c.Cursor.Visible = false;
        }
        else if (m == InputMode.Selecting)
        {
            c.Background.Color = Config.SelectingColor;
            c.Cursor.Visible = false;
        }
        else
        {
            c.Background.Color = Config.BackgroundColor;
            c.SidePanel.Visible = true;
            c.Cursor.Visible = true;
        }
    }

    public void UpdateShapeLayersIndicator(Shape currentShape)
    {
        c.SidePanel.SwitchLayersIndicator(currentShape.MyIndex(), currentShape.Anchors.Count);
    }

    public void UpdateShapeIndicators(Shapes shapes)
    {
        if (currentInput.CurrentMode != InputMode.Editing)
        {
            foreach (ShapeIndicator indicator in Indicators)
                indicator.Visible = false;
            return;
        }

        int c = -1;
        foreach (ShapeIndicator indicator in Indicators)
        {
            c += 1;
            if (c > shapes.S.Count - 1 || shapes.S[c].Anchors.Count < 3)
            {
                indicator.Visible = false;
                continue;
            }
            Shape s = shapes.S[c];
            V2 p0 = s.SegList()[0].InPoint;
            V2 p1 = s.SegList()[0].OutPoint;
            V2 p2 = s.SegList()[^1].InPoint;
            V2 p3 = p0 - p1;
            V2 p4 = p0 - p2;
            V2 d = (V2.Normalize(p3) + V2.Normalize(p4)) / 2f;
            d = d.Length() > 0 ? d : V2.Transform(p3, Matrix3x2.CreateRotation(MathF.PI / 2));
            p0 += V2.Normalize(d) * 30;
            indicator.Visible = true;
            indicator.Position = Fun.Vtv(p0);
            indicator.SetAngle(Fun.Vtv(d).Angle());
        }
    }

    public void ProcessCursor(float delta)
    {
        c.Cursor.Position = Zoom <= 1
            ? c.Cursor.Position.Lerp(
                Fun.Vtv((currentInput.MarkerPos + CursorOff) * Zoom + Config.Origin * (1f - Zoom)),
                MathF.Min(delta, 0.1f) * 20f)
            : Fun.Vtv(Config.Origin + CursorOff);

        c.CursorLabel.Position = c.Cursor.Position + new GV2(30, 30);
        c.CursorLabel.Text = currentInput.MarkerPos.X.ToString() + ", " + currentInput.MarkerPos.Y.ToString();
        c.CursorLabel.Size = new(0, 10);
    }

    private void _DrawAngledMoveGuide(Shape currentShape)
    {
        V2 a = currentShape.Anchors.Count == 1 ? V2.Zero : currentShape.SegList()[^2].TangentAt(0.99f);
        V2 l = currentShape.Anchors.Last().Position;
        float tanAng = Fun.Vtv(a).Angle();
        float mAng = Fun.Vtv(currentInput.MarkerPos - l).Angle();
        if (tanAng < mAng)
        {
            mAng -= MathF.Tau;
        }
        float diff = Mathf.Abs(tanAng - mAng);
        float d = MathF.Min(V2.Distance(currentInput.MarkerPos, l), 100);
        DrawLine(Fun.Vtv(currentInput.MarkerPos), Fun.Vtv(l), Colors.Red, 1);
        DrawLine(Fun.Vtv(l), Fun.Vtv(l + a * d), Colors.Red, 1);
        DrawArc(Fun.Vtv(l), d, tanAng, mAng, (int)(2 + 8 * MathF.Abs(diff / MathF.Tau)), Colors.Red, 2);
        DrawString(Config.MediumFont, Fun.Vtv(l + a * (d + 30)), MathF.Round((diff / MathF.Tau) * 360).ToString(), HorizontalAlignment.Center, fontSize: 32, modulate: Colors.Black);
    }

    private void _DrawGrid()
    {
        V2 drawnGridSize = new(Zoom * currentInput.GridModifier * Config.GridSize);
        GV2 gridAdjustment = -new GV2(20, 64);
        if (Zoom > 1)
        {
            gridAdjustment -= new GV2(0, 128);
        }
        if (Zoom < 1)
        {
            gridAdjustment = new(32, 32);
        }
        for (int i = 0; i < (Config.WindowSize.X / drawnGridSize.X) + 30; i++)
        {
            DrawLine(
                new GV2(0, i * drawnGridSize.X - gridAdjustment.Y),
                new GV2(5000, i * drawnGridSize.X - gridAdjustment.Y), Config.GridColor, 1.5f, true);
        }
        for (int i = 0; i < (Config.WindowSize.Y / drawnGridSize.Y) + 30; i++)
        {
            DrawLine(
                new GV2(i * drawnGridSize.Y - gridAdjustment.X, 0),
                new GV2(i * drawnGridSize.Y - gridAdjustment.X, 5000), Config.GridColor, 1.5f, true);
        }
    }

    private void _DrawMeasurements(Segment[][] mergedShapes, V2 markerPos)
    {
        V2 tan = Fun.Vtv(Player.ProjectOnShapeTangent(mergedShapes[0], markerPos));
        tan = V2.Transform(tan, Matrix3x2.CreateRotation(MathF.PI * .5f));
        V2 lineStart = markerPos - tan * 2000;
        V2 lineEnd = markerPos + tan * 2000;
        MeasurementText = [];
        int pointcount = 0;
        foreach (Segment[] s in mergedShapes)
        {
            pointcount += s.Length;
            Segment measure = new(lineStart, lineStart, lineEnd, lineEnd);
            GV2[] inters = Player.SegmentShapeIntersections(s, measure);
            List<V2> il = [];
            foreach (GV2 gv in inters)
                il.Add(Fun.Vtv(gv));
            il.Sort(Fun.CompareVectors);
            for (int i = 0; i < il.Count; i++)
            {
                V2 current = il[i];
                if (i < il.Count - 1)
                {
                    V2 next = il[i + 1];
                    int d = (int)V2.Distance(current, next);
                    V2 halfway = (current + next) / 2.0f;
                    MeasurementText = [.. MeasurementText, new(halfway, d.ToString())];
                }
                DrawCircle(Fun.Vtv(current), 7, new Color(1,0,0,0.5f), filled: false, width: 0);
            }
        }
        DrawLine(Fun.Vtv(lineStart), Fun.Vtv(lineEnd), Colors.Red,1);

        Color bcol = Config.BackgroundColor;
        foreach ((V2 pos, string text) t in MeasurementText)
        {
            DrawRect(new(t.pos.X - 24, t.pos.Y - 22, 10 + 13 * t.text.Length, 30), bcol);
            DrawString(Config.MediumFont, Fun.Vtv(t.pos) - new GV2(20, 0), t.text, fontSize: 24, modulate: Colors.Black);
        }
    }

    private void _DrawLettersAtAnchors(Shape currentShape, HashSet<Anker> selectedAnchors)
    {
        char c = 'a';
        string tallLetters = "htldfiklb";
        string deepLetters = "qypg";
        foreach (Vectordrawing.Anker a in currentShape.Anchors)
        {
            Color charColor = Colors.Black;
            Color shadowColor = Colors.Black;
            Color bcol = Config.BackgroundColor;
            if (selectedAnchors.Contains(a))
            {
                bcol = Colors.Orange;
                shadowColor = Colors.Red;
            }
            bcol.A = SinceLastSelected / SelectionFadeOutTime;
            charColor.A = SinceLastSelected / SelectionFadeOutTime;
            V2 p = a.Position;
            if (Zoom <= 1)
            {
                p = (p * Zoom) + Config.Origin * (1f - Zoom);
            }
            else
            {
                p -= Config.Origin - 1.3333f * (Config.Origin - currentInput.MarkerPos);
                p *= Zoom;
                p += Config.Origin - 1.3333f * (Config.Origin - currentInput.MarkerPos);
                p -= new V2(4, -5);
            }
            V2 letterOffset = new(-9, 7);
            V2 totalOffset = new(0, 0);
            var pp = a.OutHandle.Position();
            var ppp = a.InHandle.Position();
            Color bc = new(bcol.R - .2f, bcol.G - .1f, bcol.B - .1f, 0.9f);
            if (tallLetters.Contains(c)) letterOffset += new V2(0, 3);
            if (deepLetters.Contains(c)) letterOffset -= new V2(0, 3);
            DrawRect(new(Fun.Vtv(p - new V2(12, 16) + totalOffset), new GV2(24, 30)), bcol);
            DrawChar(Config.MediumFont, Fun.Vtv(p + letterOffset + totalOffset), c.ToString(), 30, charColor);
            c = (char)((int)c + 1);
        }
    }

    public async Task _DrawCommands(Shape currentShape, Shapes shapes, HashSet<Anker> selectedAnchors)
    {
        await ToSignal(this, SignalName.Draw);
        if (currentInput.CurrentMode == InputMode.Editing)
        {
            if (!(Zoom < 1 && currentInput.GridModifier < 2f)) _DrawGrid();
            if (currentInput.AngledMoveMode) _DrawAngledMoveGuide(currentShape);
            if (currentShape.Anchors.Count >= 3) _DrawMeasurements(shapes.GetMergedShapes(), currentInput.MarkerPos);
        }

        else if (currentInput.CurrentMode != InputMode.Previewing)
        {
            _DrawLettersAtAnchors(currentShape, selectedAnchors);
        }
    }

    public async Task<Texture2D> CreateSvgImage()
    {
        if (Ed.Throttling)
            return LastFramesTexture;
        if (LastFramesSvg == "") return new();
        CanvasImage.LoadSvgFromString(LastFramesSvg);
        return ImageTexture.CreateFromImage(CanvasImage);
    }

    // TODO
    // public void UpdatePreviews()
    // {
    // if (Shapes.S.Count <= 1)
    //     return;
    // PreviewTask ??= fontWorker.SendRequest('A', Shapes.GetMergedShapes()[0]);
    // if (!PreviewTask.IsCompleted)
    //     return;
    // LatestGlyphPreviewTtf.Data = PreviewTask.Result;
    // S0.AddThemeFontOverride("normal_font", LatestGlyphPreviewTtf);
    // S1.AddThemeFontOverride("normal_font", LatestGlyphPreviewTtf);
    // S2.AddThemeFontOverride("normal_font", LatestGlyphPreviewTtf);
    // S3.AddThemeFontOverride("normal_font", LatestGlyphPreviewTtf);
    // PreviewTask = fontWorker.SendRequest('A', Shapes.GetMergedShapes()[0]);
    // }

    // public Texture2D CreatePreviewTex(List<Shape> contours)
    // {
    //     V2 size = new(100, 100);
    //     float f = size.Y / config.WindowSize.Y;
    //     float margin = .02f;
    //     f -= margin;

    //     svgString.ClearString(f, (size * (margin / f)) / 2, size, input.MarkerPos, CursorOff);
    //     svgString.SetStyle(Style.ShapePreviewWhite);
    //     Shapes.S = contours;
    //     Shapes.ShapesCached = false;
    //     svgString.AddSegmentsGroup(Shapes.GetMergedShapes());
    //     Shapes.S = [];
    //     svgString.Finish();

    //     Image thumbnail = new();
    //     thumbnail.LoadSvgFromString(svgString.String());
    //     thumbnail.AdjustBcs(0.2f, 1, 1);
    //     return ImageTexture.CreateFromImage(thumbnail);
    // }
}