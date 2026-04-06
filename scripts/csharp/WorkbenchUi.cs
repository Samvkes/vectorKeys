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
        LightFont: GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Light.otf"),
        MediumFont: GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Medium.otf"),
        BoldFont: GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Bold.otf")
    );
}

// pass along a ui state object?
// try simplifying first -> all changes to an object / child should happen at the end / in the same place
// simplify and group, make order clear. 
public partial class WorkbenchUi : Node2D
{
    string LastFramesSvg = "";
    Texture2D LastFramesTexture = new();
    public V2 CursorOff = V2.Zero;
    public Image CanvasImage = new();
    public (V2, string)[] MeasurementText = [];
    public float GridModifier = 4;
    public float Zoom = 1f;
    public float SelectionFadeOutTime = 1;
    public float SinceLastSelected = 0;
    public float CanvasScale = 1;
    public float CanvasScaleGoal = 1;
    public Editor Ed = null!;
    public SvgString svgString = null!;
    public UIConfig config = UIConfig.Default;
    public InputState input = null!;
    public Children children = null!;
    public Workbench workbench = null!;
    ShapeIndicator[] Indicators = null!;
    public Shapes Shapes;
    public Shape CurrentShape = null!;
    public HashSet<Anker> SelectedAnchors = new();

    public WorkbenchUi(Children c, Shapes s, Workbench b, ShapeIndicator[] indicators)
    {
        children = c;
        Shapes = s;
        workbench = b; 
        Indicators = indicators;
    }

    public void UpdateUI(float delta, Shape currentShape, HashSet<Anker> sa)
    {
        CurrentShape = currentShape;
        SelectedAnchors = sa;
        ProcessCursor(delta);
        if (Ed.Throttling)
            return;
        DrawLayers(delta);
        UpdateShapeIndicators();
    }

    public void UpdateShapeIndicators()
    {
        if (input.CurrentMode != Mode.Editing)
        {
            foreach (ShapeIndicator indicator in Indicators)
                indicator.Visible = false;
            return;
        }

        int c = -1;
        foreach (ShapeIndicator indicator in Indicators)
        {
            c += 1;
            if (c > Shapes.S.Count - 1 || Shapes.S[c].Anchors.Count < 3)
            {
                indicator.Visible = false;
                continue;
            }
            Shape s = Shapes.S[c];
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
        children.Cursor.Position = Zoom <= 1 
            ? children.Cursor.Position.Lerp(
                Fun.Vtv((input.MarkerPos + CursorOff) * Zoom + config.Origin * (1f - Zoom)),
                MathF.Min(delta, 0.1f) * 20f)
            : Fun.Vtv(config.Origin + CursorOff);

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
        workbench.DrawLine(Fun.Vtv(input.MarkerPos), Fun.Vtv(l), Colors.Red, 1);
        workbench.DrawLine(Fun.Vtv(l), Fun.Vtv(l + a * d), Colors.Red, 1);
        workbench.DrawArc(Fun.Vtv(l), d, tanAng, mAng, (int)(2 + 8 * MathF.Abs(diff / MathF.Tau)), Colors.Red, 2);
        workbench.DrawString(config.MediumFont, Fun.Vtv(l + a * (d + 30)), MathF.Round((diff / MathF.Tau) * 360).ToString(), HorizontalAlignment.Center, fontSize: 32, modulate: Colors.Black);
    }

    private void _DrawGrid()
    {
        V2 drawnGridSize = new(Zoom * GridModifier * config.GridSize);
        GV2 gridAdjustment = -new GV2(20, 64);
        if (Zoom > 1)
        {
            gridAdjustment -= new GV2(0, 128);
        }
        if (Zoom < 1)
        {
            gridAdjustment = new(32, 32);
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
                svgString.AddCircle(current, 7, fill: "red", fOpacity: 0.5f, sWidth: 0);
            }
        }
        svgString.AddLine(lineStart, lineEnd);

        Color bcol = config.BackgroundColor;
        foreach ((V2 pos, string text) t in MeasurementText)
        {
            DrawRect(new(t.pos.X - 24, t.pos.Y - 22, 10 + 13 * t.text.Length, 30), bcol);
            DrawString(config.MediumFont, Fun.Vtv(t.pos) - new GV2(20, 0), t.text, fontSize: 24, modulate: Colors.Black);
        }
    }

    private void _DrawLettersAtAnchors()
    {
        char c = 'a';
        string tallLetters = "htldfiklb";
        string deepLetters = "qypg";
        foreach (Vectordrawing.Anker a in CurrentShape.Anchors)
        {
            Color charColor = Colors.Black;
            Color shadowColor = Colors.Black;
            Color bcol = config.BackgroundColor;
            if (SelectedAnchors.Contains(a))
            {
                bcol = Colors.Orange;
                shadowColor = Colors.Red;
            }
            bcol.A = SinceLastSelected / SelectionFadeOutTime;
            charColor.A = SinceLastSelected / SelectionFadeOutTime;
            V2 p = a.Position;
            if (Zoom <= 1)
            {
                p = (p * Zoom) + config.Origin * (1f - Zoom);
            }
            else
            {
                p -= config.Origin - 1.3333f * (config.Origin - input.MarkerPos);
                p *= Zoom;
                p += config.Origin - 1.3333f * (config.Origin - input.MarkerPos);
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
            DrawChar(config.MediumFont, Fun.Vtv(p + letterOffset + totalOffset), c.ToString(), 30, charColor);
            c = (char)((int)c + 1);
        }
    }

    public void _DrawCommands()
    {
        if (input.CurrentMode == Mode.Editing)
        {
            if (!(Zoom < 1 && GridModifier < 2f)) _DrawGrid();
            if (input.AngledMoveMode) _DrawAngledMoveGuide();
            _DrawMeasurements(Shapes.GetMergedShapes(), input.MarkerPos);
        }

        else if (input.CurrentMode != Mode.Previewing)
        {
            _DrawLettersAtAnchors();
        }
    }

    public void DrawLayers(float delta)
    {
        Image tempImage = new();
        int shapeCounter = 0;
        V2 size = new(90, 90);
        float f = size.Y / config.WindowSize.Y;

        foreach (TextureRect nde in children.VBox.GetChildren())
        {
            string currentString = (
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size.X}\" height=\"{size.Y}\" >" +
                $"<g transform=\"scale({f}) translate(0,0) rotate(0)\">" +
                $"<g transform=\"scale(1) translate(0,0) rotate(0)\">");

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