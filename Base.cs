using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using Snl = Vectordrawing.StringNamesList;
using System.Globalization;
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

public record Children(
    Sprite2D Tex,
    Sprite2D Cursor,
    Label CursorLabel,
    ColorRect Background,
    Control ControlRoot,
    Label LayerSelector,
    HBoxContainer PreviewContainer,
    TextureRect BigPreview,
    TextureRect TinyPreviewUp,
    TextureRect TinyPreviewDown,
    FileDialog SerafFilePicker,
    VBoxContainer VBox
);

// public record UiState
// {
//     public V2 CursorOff = V2.Zero;
//     public Image CanvasImage = new();
//     public (V2, string)[] MeasurementText = [];
//     public float GridModifier = 4;
//     public float Zoom = 1f;
//     public float SelectionFadeOutTime = 1;
//     public float SinceLastSelected = 0;
//     public float CanvasScale = 1;
//     public float CanvasScaleGoal = 1;
// };

public record InputState
{
    public V2 MarkerPos = UIConfig.Default.Origin;
    public Timer UndoTimer = new();
    public Mode CurrentMode = Mode.Editing;
    public Focus CurrentFocus = Focus.Anchor;
    public Anker? FocussedAnchor = null;
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

public partial class Base : Control
{
    public static readonly UIConfig config = UIConfig.Default;
    public InputState input = new();
    public Children children = null!;
    public WorkbenchUi ui = null!;
    Manager Manager = null!;
    Editor Ed = null!;

    PackedScene IndicatorScene = GD.Load<PackedScene>("res://shape_indicator.tscn");
    ShapeIndicator[] Indicators = new ShapeIndicator[10];

    public Shapes Shapes = new();
    public UndoRedo UndoRedo = null!;
    public Shape CurrentShape = null!;
    HashSet<Anker> SelectedAnchors = new();
    HashSet<HandlePointer> SelectedHandles = new();
    public Texture2D PreviewTex = null!;
    public bool JustUnpaused = false;

    Task<byte[]>? PreviewTask = null;
    RichTextLabel S0 = null!;
    RichTextLabel S1 = null!;
    RichTextLabel S2 = null!;
    RichTextLabel S3 = null!;
    bool appliedOnce = false;
    string a = "asdf";
    RawInput R = null!;

    FontFile LatestGlyphPreviewTtf = new();
    // TODO
    // PythonFontWorker fontWorker = new("/Users/sam/Documents/vectorkeys/vectorKeys/.venv/bin/python3",
    //                                   "/Users/sam/Documents/vectorkeys/vectorKeys/font_worker.py");

    public override void _Ready()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        Ed = (Editor)(GetParent());
        R = Ed.R;
        Manager = ((Editor)GetParent()).Manager;
        AddChild(input.UndoTimer);
        input.UndoTimer.Timeout += DoUndoRedo;
        // AddChild(input.MovementTimer);

        S0 = (RichTextLabel)FindChild("Size0");
        S1 = (RichTextLabel)FindChild("Size1");
        S2 = (RichTextLabel)FindChild("Size2");
        S3 = (RichTextLabel)FindChild("Size3");

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

        Sprite2D _cursor = GetNode<Sprite2D>("Cursor");
        Control _controlroot = GetNode<Control>("ControlRoot");
        HBoxContainer _previewcontainer = (HBoxContainer)_controlroot.FindChild("PreviewContainer");
        children = new(
            GetNode<Sprite2D>("Tex"),
            _cursor,
            (Label)_cursor.GetChild(0),
            GetNode<ColorRect>("Background"),
            _controlroot,
            (Label)FindChild("Selector"),
            _previewcontainer,
            _previewcontainer.GetChild<TextureRect>(0),
            (TextureRect)_previewcontainer.FindChild("TinyPreview"),
            (TextureRect)_previewcontainer.FindChild("TinyPreview2"),
            GetNode<FileDialog>("SerafFileDialog"),
            (VBoxContainer)FindChild("VBoxContainer_Layers")
        );

        GetWindow().Size = new Vector2I((int)config.WindowSize.X, (int)config.WindowSize.Y);
        children.Background.Color = config.BackgroundColor;
        // Fun.Repeatedly(this, 0.5f, () => {UpdatePreviews();});
    }

    public WorkbenchUi NewUI()
    {
        return new WorkbenchUi(children, Shapes, this);
    }

    public void UpdateUI()
    {
    }

    public void Initialize(Shapes shapes)
    {
        PreviewTask?.Dispose();
        ui = NewUI();
        input.MarkerPos = UIConfig.Default.Origin;
        input.CurrentMode = Mode.Editing;
        input.CurrentFocus = Focus.Anchor;
        input.FocussedAnchor = null;
        input.CanUndoAgain = true;
        input.AngledMoveMode = false;

        SelectedAnchors.Clear();
        SelectedHandles.Clear();

        PreviewTex = new();
        Shapes = shapes;
        Shapes.ShapesCached = false;
        UndoRedo = new(Shapes);
        if (Shapes.S.Count == 0)
            CurrentShape = Shapes.NewShape();
        else
            CurrentShape = Shapes.S[0];
        UpdateUI();
        JustUnpaused = true;
    }

    // public void _OnVisibilityChanged()
    // {
    //     children.ControlRoot.Visible = !children.ControlRoot.Visible;    
    // }

    public override void _Process(double doubleDelta)
    {
        // TODO svg
        // Task<Texture2D> createSvg = CreateSvgImage();
        HandleInput((float)doubleDelta);
        float delta = (float)doubleDelta;

        UpdateUI();
        if (Ed.Throttling)
            return;

        // SVG code goes here
        // TODO svg
        // SvgString.ClearString(ui.Zoom, config.Origin, config.WindowSize, input.MarkerPos, ui.CursorOff);
        // if (input.CurrentMode == Mode.Editing) DrawEditing();
        // else if (input.CurrentMode == Mode.Previewing) DrawPreviewing();
        // else if (input.CurrentMode == Mode.Selecting) DrawSelecting();

        SvgString.Finish();

        // TODO svg
        // createSvg.Wait();
        // children.Tex.Texture = createSvg.Result;
        // LastFramesSvg = SvgString.CurrentString.ToString();
        // LastFramesTexture = children.Tex.Texture;

        QueueRedraw();
    }

    void HandleLayerSwitching()
    {
        if (R.NumberJustPressed is not null)
        {
            Manager.PlaySound("click.wav", 0.15f, 0.7f, 0.8f);
            int shapeToPick = 0;
            if (R.NumberPressed == "0") shapeToPick = 9;
            else shapeToPick = Int32.Parse(R.NumberJustPressed) - 1;
            if (CurrentShape == Shapes.S[shapeToPick])
            {
                SelectedAnchors = [.. SelectedAnchors, .. CurrentShape.Anchors];
            }
            else
            {
                CurrentShape = Shapes.S[shapeToPick];
                Label sel = children.LayerSelector;
                CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quint).TweenProperty(sel, "position", new GV2(-100, -3 + Shapes.S.IndexOf(CurrentShape) * 98), .2f);
                CreateTween().TweenProperty(sel, "scale", new GV2(1, 1), .2).From(new GV2(.7f, 1.3f));
                children.LayerSelector.Text = CurrentShape.Anchors.Count.ToString("D2") + "\n24";
            }
        }

    }

    public void HandleSelectionText(string s)
    {
        Anker a = CurrentShape.GetAnchorFromLabel(s.ToLower());
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
        foreach (Anker a in SelectedAnchors)
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
            foreach (Anker a in SelectedAnchors)
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
        if (Input.IsKeyPressed(Key.A))
        {
            movementAmount = 1;
        }

        V2 movingSelected = Ed.GetMovementInput(delta, OnGuide: OnGuide()) * movementAmount;

        if (movingSelected != V2.Zero)
        {
            // input.CanMoveAgain = false;
            // if (!OnGuide())
            // {
            //     input.StickyGuide = true;
            // }
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
                    foreach (Anker a in SelectedAnchors)
                    {
                        a.anchorRounding = Math.Clamp(a.anchorRounding + (int)(-movingSelected.Y) / 5, 1, 600);
                        a.intersectionRounding = Math.Clamp(a.intersectionRounding + (int)(movingSelected.X) / 5, 1, 600);
                        a.MyShape.AnchorsChanged();
                    }
                }
                else
                {
                    foreach (Anker a in SelectedAnchors)
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
                                ang = MathF.Round((Fun.Vtv(lap).AngleToPoint(Fun.Vtv(input.MarkerPos)) - (tan % fr)) / fr) * fr + (tan % fr);
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

        // if (OnGuide() && input.StickyGuide)
        // {
        //     input.MovementTimer.WaitTime = .2f;
        //     input.MovementTimer.Start();
        //     input.CanMoveAgain = false;
        //     input.StickyGuide = false;
        // }
        // else
        // {
        //     input.MovementTimer.WaitTime = .01f;
        // }

    }

    public void HiPointAdding(float delta)
    {
        // TODO split up
        if (input.CurrentFocus == Focus.Anchor)
        {
            if (Input.IsActionJustPressed(Snl.switch_segment_style) || Input.IsActionJustPressed(Snl.finish_shape))
            {
                Anker fanchor = null!;
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
                foreach (Anker a in SelectedAnchors)
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

    public async void HandleInput(float delta)
    {
        if (TextInput.BeingEdited) return;
        if (JustUnpaused)
        {
            Fun.DelayOneFrame(this, () => { JustUnpaused = false; });
            return;
        }

        // if (Input.IsActionJustPressed(Snl.save_project))
        // {
        //     children.SerafFilePicker.FileMode = FileDialog.FileModeEnum.SaveFile;
        //     children.SerafFilePicker.Visible = true;
        //     string file = (string)(await ToSignal(children.SerafFilePicker, FileDialog.SignalName.FileSelected))[0];
        //     var fileAc = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Write);
        //     fileAc.StoreString(Shapes.SaveState());
        //     fileAc.Close();
        //     fileAc.Dispose();
        // }

        // if (Input.IsActionJustPressed(Snl.load_project))
        // {
        //     children.SerafFilePicker.FileMode = FileDialog.FileModeEnum.OpenFile;
        //     children.SerafFilePicker.Visible = true;
        //     string file = (string)(await ToSignal(children.SerafFilePicker, FileDialog.SignalName.FileSelected))[0];
        //     var fileAc = Godot.FileAccess.Open(file, Godot.FileAccess.ModeFlags.Read);
        //     Shapes.LoadState(fileAc.GetAsText());
        //     CurrentShape = Shapes.S[0]; 
        // }


        if (Input.IsActionJustPressed(Snl.shape_negative))
        {
            Uts();
            CurrentShape.Negative = !CurrentShape.Negative;
            Shapes.ShapesCached = false;
        }

        if (Input.IsActionJustPressed(Snl.undo))
        {
            UndoRedo.Undo();
            SelectedAnchors.Clear();
            SelectedHandles.Clear();
            CurrentShape.AnchorsCached = false;
            CurrentShape = Shapes.S.Last();
            CurrentShape.AnchorsCached = false;
            Shapes.ShapesCached = false;
        }

        if (Input.IsActionJustPressed(Snl.redo))
        {
            UndoRedo.Redo();
            SelectedAnchors.Clear();
            SelectedHandles.Clear();
            CurrentShape.AnchorsCached = false;
            CurrentShape = Shapes.S.Last();
            CurrentShape.AnchorsCached = false;
            Shapes.ShapesCached = false;
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

        HandleLayerSwitching();

        if (input.CurrentMode == Mode.Editing || input.CurrentMode == Mode.Previewing)
        {
            HiMovement(delta);
            HiPointAdding(delta);
            // HiScaling(delta);
            // HiRotation(delta, (Input.IsActionPressed(Snl.rotate_cw_points) || 
            //                    Input.IsActionPressed(Snl.rotate_ccw_points)));

            {
                foreach (Anker a in CurrentShape.Anchors)
                {
                    a.AutoHandles();
                }
                foreach (Anker a in CurrentShape.Anchors)
                {
                    a.AutoHandles();
                }
            }
            foreach (Shape s in Shapes.S)
            {
                // s.AlignAllHandles();
            }
        }
        else if (input.CurrentMode == Mode.Selecting)
        {
            if (R.LetterJustPressed is not null)
                HandleSelectionText(R.LetterJustPressed);
        }

        if (Input.IsActionJustPressed(Snl.semicolon))
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
    }

    public bool OnGuide()
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
        V2 gridAdjustment = new V2(-20, -64);
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
    // M undoredo
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
    // M undoredo
    public void DoUndoRedo()
    {
        input.CanUndoAgain = true;
    }

}
