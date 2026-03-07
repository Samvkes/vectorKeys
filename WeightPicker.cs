using Godot;
using System;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using System.Collections.Generic;
using System.Linq;
using Snl = Vectordrawing.StringNamesList;
using System.Diagnostics;
using System.Runtime;

namespace Vectordrawing;

public class Axis(int index, Label l, WeightPicker wp, string name = "")
{
    readonly Color yella = new(.9f,.9f,.9f);
    readonly WeightPicker Wp = wp;

    Label Lab = l;
    public string Name = name;
    public bool TurnedOn = false;
    public bool Defined = false;
    public int Index = index;

    public void Define(string name)
    {
        Defined = true;
        ChangeName(name);
        Lab.AddThemeColorOverride("font_color", yella);
        StyleBoxFlat sbf = Lab.GetThemeStylebox("normal").Duplicate() as StyleBoxFlat;
        sbf.BorderColor = yella;
        Lab.AddThemeStyleboxOverride("normal", sbf);
    }

    public void ChangeName(string name)
    {
        Name = name;
        Lab.Text = $"{Index + 1} {Name}";
    }

    public void Switch()
    {
        if (!Defined)
        {
           return; 
        } 
        if (!TurnedOn)
        {
            Manager.PlaySound("Rattle3.wav",.2f, 0.95f, 1.0f);
            Lab.PivotOffsetRatio = new GV2(.5f,.5f);
            StyleBoxFlat sbf = Lab.GetThemeStylebox("normal").Duplicate() as StyleBoxFlat;
            sbf.BgColor = yella;
            Wp.CreateTween().SetTrans(Tween.TransitionType.Expo).TweenProperty(sbf, "border_color", yella, .1f);
            Wp.CreateTween().SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine).TweenProperty(sbf, "bg_color:a", 1f, .1f);
            Wp.CreateTween().SetTrans(Tween.TransitionType.Expo).TweenProperty(Lab, "theme_override_colors/font_color", new Color(0,0,0), .1f);
            // Wp.CreateTween().SetTrans(Tween.TransitionType.Sine).TweenProperty(Lab, "position", Lab.Position, .4f).From(Lab.Position + new GV2(0,10f));
            Wp.CreateTween().SetTrans(Tween.TransitionType.Sine).TweenProperty(Lab, "scale", new GV2(1,1), .2f).From(new GV2(1.10f,1.10f));
            Lab.AddThemeStyleboxOverride("normal", sbf);
            GD.Print($"{TurnedOn}");
            TurnedOn = true;
            GD.Print($"{TurnedOn}");
        }
        else
        {
            Manager.PlaySound("Rattle3.wav",.2f, 0.7f, 0.75f);
            Lab.PivotOffsetRatio = new GV2(.5f,.5f);
            StyleBoxFlat sbf = Lab.GetThemeStylebox("normal").Duplicate() as StyleBoxFlat;
            Wp.CreateTween().SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Expo).TweenProperty(sbf, "border_color", yella, .2f);
            Wp.CreateTween().SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Expo).TweenProperty(sbf, "bg_color:a", 0f, .1f);
            Wp.CreateTween().SetTrans(Tween.TransitionType.Expo).TweenProperty(Lab, "theme_override_colors/font_color", new Color(1,1,1), .1f);
            Wp.CreateTween().SetTrans(Tween.TransitionType.Sine).TweenProperty(Lab, "scale", new GV2(1,1), .2f).From(new GV2(1.10f,1.10f));
            // Wp.CreateTween().SetTrans(Tween.TransitionType.Sine).TweenProperty(Lab, "position", Lab.Position, .2f).From(Lab.Position + new GV2(0,10f));
            Lab.AddThemeStyleboxOverride("normal", sbf);
            TurnedOn = false;
        }
    }
}

public partial class WeightPicker : Node
{
    ShaderMaterial blur1 = null!;
    ShaderMaterial blur2 = null!;
    CanvasLayer blurLayer1 = null!;
    CanvasLayer blurLayer2 = null!;
    CanvasLayer pickerLayer = null!;
    Editor Ed = null!;
    VBoxContainer Vbox = null!;
    bool t = false;
    List<Panel> Clicks = [];
    HSeparator First = null!;
    HSeparator Last = null!;
    bool canSoundAgain = true;
    Timer soundTimer = null!;
    Tween? theTween = null;
    List<Tween> runningTweens = [];
    int step = 108;
    float target = 0;
    bool ab = false;
    bool Active = false;
    FontVariation rec = null!;
    int weight = 600;
    int count = 500;
    Label currentWeightLabel = null!;
    Label weightNameLabel = null!;
    Label weightNameHalfLabel = null!;
    Label glyphsDoneLabel = null!;
    Panel showsWhatsPicked = null!;
    LetterMenu letterMenu = null!;
    bool huh = false;
    bool justActive = false;
    Color yella = new(.9f,.9f,.9f);
    public List<Axis> Axes = [];
    public string[] WeightNames = [
        "Hairline",
        "Hairline",
        "Thin",
        "Thin",
        "Light",
        "Light",
        "Slight",
        "Slight",
        "Medium",
        "Semi",
        "Semi",
        "Bold",
        "Bold",
        "Heavy",
        "Heavy",
        "Black",
        "Black",
        "Ultra",
        "Ultra",
    ];

    public override void _Ready()
    {
        TextServer t = TextServerManager.GetPrimaryInterface();
        rec = GD.Load<FontVariation>("res://recursive_var.tres");
        Font mediumF =  GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Medium.otf");
        FontVariation a = new();

        blur1 = (ShaderMaterial)((ColorRect)FindChild("firstBlurShader")).Material;
        blur2 = (ShaderMaterial)((ColorRect)FindChild("secondBlurShader")).Material;
        blurLayer1 = (CanvasLayer)FindChild("firstBlur");
        blurLayer2 = (CanvasLayer)FindChild("secondBlur");
        pickerLayer = (CanvasLayer)FindChild("Picker");

        currentWeightLabel = (Label)FindChild("CurrentWeight");
        weightNameLabel = (Label)FindChild("WeightName");
        weightNameHalfLabel = (Label)FindChild("WeightNameHalf");
        glyphsDoneLabel = (Label)FindChild("GlyphsDone");
        showsWhatsPicked = (Panel)FindChild("ShowsWhatsPicked");

        soundTimer = new();
        AddChild(soundTimer);
        soundTimer.WaitTime = 0.10f;
        soundTimer.Timeout += () => {canSoundAgain = true;};
        soundTimer.OneShot = true;
        soundTimer.Start();

        Ed = GetParent<Editor>();
        letterMenu = (LetterMenu)Ed.FindChild("LetterMenu");
        Vbox = (VBoxContainer)FindChild("vbox1");
        Vbox.GlobalPosition = new(Vbox.GlobalPosition.X, - step * 6);
        StyleBoxFlat weightFlat = GD.Load<StyleBoxFlat>("res://weightLineFlat.tres");
        StyleBoxFlat labelSb = GD.Load<StyleBoxFlat>("res://axisLabelStyleBox.tres");
        GridContainer LabelContainer = (GridContainer)FindChild("LabelContainer");
        
        for (int i = 0; i < 9; i++)
        {
            Label l = new();
            l.CustomMinimumSize = new(380, 0);
            l.AddThemeStyleboxOverride("normal", labelSb);
            l.AddThemeFontSizeOverride("font_size", 30);
            l.AddThemeFontOverride("font", mediumF);
            l.AddThemeColorOverride("font_color", new Color(0.5f,0.5f,0.5f));
            l.Name = $"AxisLabel{i}";
            LabelContainer.AddChild(l);
            Axis ax = new(i, l, this);
            if (i == 0)
                ax.Define("italic");
            else
                l.Text = (i+1).ToString();
            Axes.Add(ax);
        }

        for (int i = 0; i < 10; i++)
        {
            Panel h = new();
            h.CustomMinimumSize = new(i == 5 ? 45 : 30, 8);
            h.AddThemeStyleboxOverride("panel", weightFlat);
            h.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            Vbox.AddChild(h);
            h.PivotOffsetRatio = new(0.5f, 0.5f);
            Clicks.Add(h);
        }
        target = Vbox.Position.Y;
    }

    public async void Define(int i)
    {
        PackedScene tiScene = GD.Load<PackedScene>("text_input.tscn");
        TextInput ti = tiScene.Instantiate<TextInput>();
        pickerLayer.AddChild(ti);
        // GD.Print($"AxisLabel{Ed.R.NumberJustPressed.ToInt() -1}");
        GridContainer LabelContainer = (GridContainer)FindChild("LabelContainer");
        ti.Position = ((Label)LabelContainer.GetChild(i)).GlobalPosition - new Godot.Vector2(ti.Size.X / 4f, ti.Size.Y + 15);
        ti.Edit();
        string a = (string)(await ToSignal(ti.Le, LineEdit.SignalName.TextSubmitted))[0];
        GD.Print($"huh {a}"); 
        Axes[i].Define(a);
    }

    public override void _Process(double delta)
    {
        if (Ed.CurrentFocus != EditorFocus.WeightPicker || TextInput.BeingEdited)
            return;
        
        if (Input.IsActionJustPressed(Snl.add_new_point) && pickerLayer.Visible == true)
            SwitchActive();
        if (Ed.R.NumberJustPressed is not null)
        {
            if (!Axes[Ed.R.NumberJustPressed.ToInt() - 1].Defined)
            {
                Define(Ed.R.NumberJustPressed.ToInt() - 1);
            }
            else
                Axes[Ed.R.NumberJustPressed.ToInt() - 1].Switch();
        }
        currentWeightLabel.Text = count.ToString();
        weightNameLabel.Text = WeightNames[(int)((count - 100) / 50)];
        if (count % 100 != 0)
        {
            weightNameHalfLabel.Text = "Semi";
        }
        else
        {
            weightNameHalfLabel.Text = "";
        }
        GV2 vpos = Vbox.Position;
        GV2 tpos = new(vpos.X, target + 570 );
        if (vpos.DistanceTo(tpos) < 2 || justActive)
        {
            justActive = false;
            Vbox.Position = tpos;
        }
        else
            Vbox.Position += (tpos - Vbox.Position) * (1 - MathF.Exp( -(float)delta * 14f));
            // Vbox.Position = vpos.Lerp(tpos, (float)delta * 12f);
        weight = Math.Max(Math.Min(1000, weight), 300);
        rec.VariationOpentype = new(){{"weight", weight}, {"custom_CASL", 1}, {"custom_CRSV", 1}};

        V2 mov = Ed.GetMovementInput((float)delta, wait: 0.05f);
        if (mov.Y > 0 )
        {
            if (count > 100)
            {
                weight -= 80;
                count -= 100;
                target -= step;
                Rattle();
            }
        }
        else if (mov.Y < 0)
        {
            if (count < 1000)
            {
                weight += 80;
                count += 100;
                target += step;
                Rattle();
            }
        }
        else
        {
        }
        float h = GetWindow().Size.Y;
        float fallof = .05f;
        float fromBottom = 390;
        float fromTop = 60;
        foreach (Panel click in Clicks)
        {
            click.Modulate = new(1,1,1,
                MathF.Min(
                    MathF.Min((click.GlobalPosition.Y - fromTop) / (fallof*h), (MathF.Abs(click.GlobalPosition.Y - h) - fromBottom) / (fallof*h))
                , 1)
            );
            // click.Scale = new GV2(1,
            //     MathF.Max(
            //         MathF.Min(
            //             MathF.Min((click.GlobalPosition.Y - fromTop*1.0f) / (0.4f*h), (MathF.Abs(click.GlobalPosition.Y - h) - fromBottom*1.0f) / (0.4f*h))
            //         , 1)
            //     , .5f)
            // );
        }
        if (Input.IsActionJustPressed(Snl.down)) 
        {
            CreateTween().TweenProperty(showsWhatsPicked, "modulate:a", 0.0f, .2);
        }
        if (Input.IsActionJustPressed(Snl.up)) 
        {
            CreateTween().TweenProperty(showsWhatsPicked, "modulate:a", 0.0f, .2);
        }
        if (Input.IsActionJustReleased(Snl.up) || Input.IsActionJustReleased(Snl.down)) 
        {
            CreateTween().TweenProperty(showsWhatsPicked, "modulate:a", 1.0f, .2f);
        }
        letterMenu.CurrentWeight = count;
        List<Axis> activeAxes= [];
        foreach (Axis a in Axes)
            if (a.TurnedOn) activeAxes.Add(a);
        letterMenu.CurrentAxes = activeAxes;
    }

    public void SwitchActive()
    {
        if (!Active)
        {
            Ed.CurrentFocus = EditorFocus.WeightPicker;
            blurLayer1.Visible = true;
            blurLayer2.Visible = true;
            theTween?.Kill();
            theTween = CreateTween();
            theTween.TweenMethod(Callable.From((int s) =>
            {
                blur1.SetShaderParameter("blurSize", s);
            }), 0, 15, .1f);
            theTween.Parallel().TweenMethod(Callable.From((int s) =>
            {
                blur2.SetShaderParameter("blurSize", s);
            }), 0, 15, .1f);
            theTween.TweenCallback(Callable.From(()=>{pickerLayer.Visible = true;}));
            justActive = true;
        }
        else
        {
            Ed.ResetEditorFocus();
            theTween?.Kill();
            blurLayer1.Visible = false;
            blurLayer2.Visible = false;
            pickerLayer.Visible = false;
            string weightString = count.ToString();
            List<Axis> filledA = [.. Axes.Where(a => a.Name != "" && a.TurnedOn)];
            foreach (Axis a in filledA)
            {
                weightString += " - ";
                weightString += a.Name;
            }
            EmitSignal(SignalName.PickedWeight, weightString);
        }
        Active = !Active;
    }

    public (int, List<string>) GetWeightAndAxes()
    {
        return (count, Axes.Where(o => o.Name != "" && o.TurnedOn).Select(o => o.Name).ToList());
    }

    void Rattle()
    {
        if (canSoundAgain)
        {
            Manager.PlaySound("Rattle3.wav",(float)GD.RandRange(0.1,0.7), 1.5f, 2.0f);
            canSoundAgain = false;
            soundTimer.WaitTime = .07f;
            soundTimer.Start();
        }
    }

    [Signal]
    public delegate void PickedWeightEventHandler();
}
