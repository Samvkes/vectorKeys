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

public record struct InputState()
{
    public V2 MarkerPos = UIConfig.Default.Origin;
    public InputMode CurrentMode = InputMode.Editing;
    public EditingFocus CurrentFocus = EditingFocus.Anchor;
    public TransformMode CurrentTransformMode = TransformMode.Moving;
    public bool AngledMoveMode = false;
    public bool JustUnpaused = false;
    public bool Measuring = false;
    public float GridModifier = 4;
};

public enum TransformMode
{
    Moving,
    Scaling,
    Rotating,
}

public enum InputMode
{
    Editing,
    Selecting,
    Previewing,
    Pasting,
}

public enum EditingFocus
{
    Anchor,
    Handle,
    Outline,
}

public partial class Workbench : Control
{
    public InputState input = new();
    public UIConfig config = null!;

    Shapes Shapes = new();
    Shape CurrentShape = null!;
    HashSet<Anker> SelectedAnchors = new();
    HashSet<HandlePointer> SelectedHandles = new();
    WorkbenchUndoRedo UndoRedo = null!;
    WorkbenchUi Ui = null!;
    Manager Manager = null!;
    Editor Ed = null!;

    RawInput R = null!;
    // TODO

    public override void _Ready()
    {
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        Ed = (Editor)GetParent();
        R = Ed.R;

        Manager = ((Editor)GetParent()).Manager;
        UndoRedo = GetNode<WorkbenchUndoRedo>("WorkbenchUndoRedo");
        Ui = GetNode<WorkbenchUi>("WorkbenchUi"); 
        config = Ui.Config;
    }

    public void Initialize(Shapes shapes)
    {
        // PreviewTask?.Dispose();
        Ui.ResetUiState();
        input.MarkerPos = UIConfig.Default.Origin;
        input.CurrentMode = InputMode.Editing;
        input.CurrentFocus = EditingFocus.Anchor;
        input.AngledMoveMode = false;

        SelectedAnchors.Clear();
        SelectedHandles.Clear();

        // PreviewTex = new();
        Shapes = shapes;
        Shapes.ShapesCached = false;
        UndoRedo.Initialize(Shapes);
        if (Shapes.S.Count == 0)
            CurrentShape = Shapes.NewShape();
        else
            CurrentShape = Shapes.S[0];
        input.JustUnpaused = true;
    }

    public Shapes Close()
    {
        return Shapes;
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

        Ui.UpdateUI(delta, CurrentShape, Shapes, SelectedAnchors, SelectedHandles, input);
        if (Ed.Throttling)
            return;

        // SVG code goes here
        // TODO svg
        // SvgString.ClearString(ui.Zoom, config.Origin, config.WindowSize, input.MarkerPos, ui.CursorOff);
        // if (input.CurrentMode == Mode.Editing) DrawEditing();
        // else if (input.CurrentMode == Mode.Previewing) DrawPreviewing();
        // else if (input.CurrentMode == Mode.Selecting) DrawSelecting();

        // SvgString.Finish();

        // TODO svg
        // createSvg.Wait();
        // children.Tex.Texture = createSvg.Result;
        // LastFramesSvg = SvgString.CurrentString.ToString();
        // LastFramesTexture = children.Tex.Texture;
    }

    void HandleShapeSwitching()
    {
        Manager.PlaySound("click.wav", 0.15f, 0.7f, 0.8f);
        int shapeToPick = R.NumberJustPressed == "0" ? 9 : Int32.Parse(R.NumberJustPressed) - 1;
        if (CurrentShape == Shapes.S[shapeToPick])
        {
            SelectedAnchors = [.. SelectedAnchors, .. CurrentShape.Anchors];
        }
        else
        {
            CurrentShape = Shapes.S[shapeToPick];
        }
    }

    public Texture2D GetCurrentPreviewTexture()
    {
        return Ui.CreatePreviewTex(Shapes);
    }

    public Texture2D CreatePreviewTexture(Shapes shapes)
    {
        return Ui.CreatePreviewTex(shapes);
    }

    public void HandleSelectionText(string s)
    {
        Anker a = CurrentShape.GetAnchorFromLabel(s.ToLower());
        if (!SelectedAnchors.Remove(a))
        {
            if (input.CurrentFocus == EditingFocus.Handle)
                SelectedHandles = [new(a, true)];
            else if (input.CurrentFocus == EditingFocus.Outline)
                SelectedAnchors = [a];
            else
                SelectedAnchors.Add(a);
        }
    }

    public void HiRotation(float delta, V2 movementInput)
    {
        float rotationAmount = 0.05f;
        float ang = rotationAmount * 2 * MathF.PI * delta * movementInput.X;
        Uts();
        foreach (Anker a in SelectedAnchors)
        {
            V2 spot = a.Position - input.MarkerPos;
            V2 displacement = new(MathF.Cos(ang) * spot[0] - MathF.Sin(ang) * spot[1], MathF.Sin(ang) * spot[0] + MathF.Cos(ang) * spot[1]);
            a.Position += displacement - spot;
            a.MyShape.AnchorsChanged();
        }
    }

    public void HiScaling(float delta, V2 movementInput)
    {
        float scalingAmount = (Input.IsKeyPressed(Key.A) ? .1f : 1.0f) * delta;
        float xScalar = 1;
        float yScalar = 1;
        xScalar += movementInput.X * scalingAmount;
        yScalar += -movementInput.Y * scalingAmount;
        if (xScalar != 1 || yScalar != 1)
        {
            Uts();
            foreach (Anker a in SelectedAnchors)
            {
                V2 spot = a.Position - input.MarkerPos;
                V2 displacement = new(spot[0] * xScalar, spot[1] * yScalar);
                a.Position += displacement - spot;
                a.MyShape.AnchorsChanged();
            }
        }
    }

    public void HiMovement(float delta, V2 movementInput)
    {
        int movementAmount = (int)(config.GridSize * input.GridModifier);
        if (Input.IsKeyPressed(Key.A))
        {
            movementAmount = 1;
        }

        float radGA = config.GridAngle / 360f * MathF.Tau;
        float xInput = movementInput.X;
        if (movementInput.Y > 0)
        {
            movementInput = V2.Transform(new(0,1), Matrix3x2.CreateRotation(radGA));
            movementInput*= 1/movementInput.Y;
            movementInput.X += xInput;
        }
        if (movementInput.Y < 0)
        {
            movementInput = V2.Transform(new(0,-1), Matrix3x2.CreateRotation(radGA));
            movementInput*= -1/movementInput.Y;
            movementInput.X += xInput;
        }
        V2 movingSelected = movementInput * movementAmount;

        if (movingSelected != V2.Zero)
        {
            // input.CanMoveAgain = false;
            // if (!OnGuide())
            // {
            //     input.StickyGuide = true;
            // }
            // move anchors
            if ((input.CurrentFocus == EditingFocus.Anchor || input.CurrentFocus == EditingFocus.Outline) && SelectedAnchors.Count > 0)
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
                    float tan = CurrentShape.Anchors.Count == 1 ? 0 : Fun.Vtv(CurrentShape.SegList()[^2].TangentAt(0.99f)).Angle();
                    if (Input.IsKeyPressed(Key.A))
                    {
                        input.MarkerPos = (lap + V2.Transform(input.MarkerPos - lap, Matrix3x2.CreateRotation(MathF.Sign(movingSelected.X) * .01f)));
                    }
                    else
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
                    V2 t = V2.One;

                    t = -Fun.Vtv(Fun.Vtv(lap).DirectionTo(Fun.Vtv(input.MarkerPos)));
                    input.MarkerPos += t * movingSelected.Y;
                }
                else
                {
                    input.MarkerPos += movingSelected;
                    input.MarkerPos.X = MathF.Abs(float.Round(input.MarkerPos.X) - input.MarkerPos.X) < 0.1 ? float.Round(input.MarkerPos.X) : float.Round(input.MarkerPos.X, 2);
                    input.MarkerPos.Y = MathF.Abs(float.Round(input.MarkerPos.Y) - input.MarkerPos.Y) < 0.1 ? float.Round(input.MarkerPos.Y) : float.Round(input.MarkerPos.Y, 2);
                }
                // TODO abstract cursor?
                // input.MarkerPos += movingSelected;
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
        if (input.CurrentFocus == EditingFocus.Anchor)
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
                input.CurrentFocus = EditingFocus.Handle;
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

            if (input.CurrentFocus == EditingFocus.Anchor && (Input.IsActionJustReleased(Snl.switch_segment_style) || Input.IsActionJustReleased(Snl.finish_shape)))
            {
                SelectedHandles = [];
            }

        }
        else if (input.CurrentFocus == EditingFocus.Handle)
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
                input.CurrentFocus = EditingFocus.Outline;
                SelectedAnchors = [SelectedHandles.Last().A];
                SelectedHandles = [];
            }

            if (Input.IsActionJustPressed(Snl.add_sharp_point))
            {
                SelectedHandles.Last().A.Broken = !SelectedHandles.Last().A.Broken;
            }
        }
        else if (input.CurrentFocus == EditingFocus.Outline)
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
                input.CurrentFocus = EditingFocus.Handle;
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
            Fun.Delayed(this, 0.05f,
            () =>
            {
                if (Ui.Zoom >= 1 && Ui.Zoom < 4) Ui.Zoom *= 4;
                else if (Ui.Zoom < 1) Ui.Zoom *= 2;
            });
        }
        if (Input.IsActionJustPressed(Snl.decrease_zoom))
        {
            Fun.Delayed(this, 0.05f,
            () =>
            {
                if (Ui.Zoom <= 1 && Ui.Zoom > .5f) Ui.Zoom /= 2;
                else if (Ui.Zoom > 1) Ui.Zoom /= 4;
            });
        }
        if (Input.IsActionJustPressed(Snl.increase_grid_modifier))
        {
            if (input.GridModifier < 6)
            {
                input.GridModifier *= 2;
                SnapMarkerPos();
            }
        }
        if (Input.IsActionJustPressed(Snl.decrease_grid_modifier))
        {
            if (input.GridModifier > 1)
            {
                input.GridModifier /= 2;
                SnapMarkerPos();
            }
        }
    }

    public async void HandleInput(float delta)
    {
        if (TextInput.BeingEdited) return;
        if (input.JustUnpaused)
        {
            Fun.DelayOneFrame(this, () => { input.JustUnpaused = false; });
            return;
        }

        if (Input.IsActionJustPressed(Snl.paste_toggle))
        {
            if (input.CurrentMode == InputMode.Editing)
            {
                input.CurrentMode = InputMode.Pasting;
            }
            else if (input.CurrentMode == InputMode.Pasting)
            {
                input.CurrentMode = InputMode.Editing;
            }
        }
        if (Input.IsActionJustPressed(Snl.shape_negative))
        {
            Uts();
            CurrentShape.Negative = !CurrentShape.Negative;
            CurrentShape.AnchorsChanged();
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
            if (input.CurrentMode == InputMode.Editing)
            {
                input.CurrentMode = InputMode.Previewing;
            }
            else if (input.CurrentMode == InputMode.Previewing)
            {
                input.CurrentMode = InputMode.Editing;
            }
        }

        if (Input.IsActionJustPressed(Snl.toggle_transform))
        {
            TransformMode ct = input.CurrentTransformMode;
            input.CurrentTransformMode = ct == TransformMode.Rotating ? TransformMode.Moving : ct + 1; 
        }

        if (Input.IsActionJustPressed(Snl.select_mode))
        {
            input.CurrentMode = InputMode.Selecting;
        }
        if (Input.IsActionJustReleased(Snl.select_mode))
        {
            input.CurrentMode = InputMode.Editing;
        }

        if (R.NumberJustPressed is not null)
        {
            HandleShapeSwitching();
        }

        if (input.CurrentMode == InputMode.Editing || input.CurrentMode == InputMode.Previewing)
        {
            V2 movementInput = Ed.GetMovementInput(delta, OnGuide: OnGuide());
            HiPointAdding(delta);
            if (input.CurrentTransformMode == TransformMode.Moving)
            {
                HiMovement(delta, movementInput);
            }
            else if (input.CurrentTransformMode == TransformMode.Scaling)
            {
                HiScaling(delta, movementInput);
            }
            else if (input.CurrentTransformMode == TransformMode.Rotating)
            {
                HiRotation(delta, movementInput); 
            }
            if (Input.IsActionJustPressed(Snl.yank))
            {
                Ui.Yank(CurrentShape);
            }

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
        else if (input.CurrentMode == InputMode.Selecting)
        {
            if (R.LetterJustPressed is not null)
                HandleSelectionText(R.LetterJustPressed);
        }
        else if (input.CurrentMode == InputMode.Pasting)
        {
            Pastable? toPaste = Ui.HandlePasteInput(delta);
            if (toPaste is not null)
            {
                Shapes.LoadOneShapeState(toPaste.ToPaste);
            }
        }

        if (Input.IsActionJustPressed(Snl.semicolon))
        {
            if (input.CurrentFocus == EditingFocus.Outline || input.CurrentFocus == EditingFocus.Handle)
            {
                SelectedHandles = [];
                SelectedAnchors = [];
                input.CurrentFocus = EditingFocus.Anchor;
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
        int totalSize = (int)input.GridModifier * config.GridSize;
        input.MarkerPos.X = (int)(input.MarkerPos.X / totalSize) * totalSize;
        input.MarkerPos.Y = (int)(input.MarkerPos.Y / totalSize) * totalSize;
        input.MarkerPos -= gridAdjustment;
    }
    //TODO implement
    public void SnapSelectedPos()
    {
        int totalSize = (int)input.GridModifier * config.GridSize;
    }

    public void Uts() 
    {
        UndoRedo.UndoCheckpoint();
    }
}