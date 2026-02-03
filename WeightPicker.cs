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

public partial class WeightPicker : Control
{
    Editor Ed = null!;
    VBoxContainer Vbox1 = null!;
    VBoxContainer Vbox2 = null!;
    VBoxContainer Vbox3 = null!;
    bool t = false;
    List<Panel> Clicks = [];
    HSeparator First = null!;
    HSeparator Last = null!;
    bool canSoundAgain = true;
    Timer soundTimer = null!;
    Tween? theTween = null;
    List<Tween> runningTweens = [];
    int step = 88;
    float target = 0;
    bool ab = false;
    FontVariation rec = null!;
    int weight = 600;
    int count = 500;
    Label currentWeightLabel = null!;
    Label weightNameLabel = null!;
    Label weightNameHalfLabel = null!;
    Label glyphsDoneLabel = null!;
    Panel showsWhatsPicked = null!;
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
        // rec.VariationOpentype["weight"] = 700;
        GD.Print(rec.GetSupportedVariationList());
        FontVariation a = new();
        // a.BaseFont = GD.Load<FontFile>("res://recursive_font.ttf");
        // a.VariationOpentype = new(){{"weight", 800}};
        // GD.Print(t.TagToName(2003265652));
        currentWeightLabel = (Label)FindChild("CurrentWeight");
        weightNameLabel = (Label)FindChild("WeightName");
        weightNameHalfLabel = (Label)FindChild("WeightNameHalf");
        glyphsDoneLabel = (Label)FindChild("GlyphsDone");
        showsWhatsPicked = (Panel)FindChild("ShowsWhatsPicked");
        // l.AddThemeFontOverride("font", a);
        soundTimer = new();
        AddChild(soundTimer);
        soundTimer.WaitTime = 0.10f;
        soundTimer.Timeout += () => {canSoundAgain = true;};
        soundTimer.OneShot = true;
        soundTimer.Start();
        Ed = GetParent().GetParent<Editor>();
        Vbox1 = (VBoxContainer)FindChild("vbox1");
        Vbox1.GlobalPosition = new(Vbox1.GlobalPosition.X, - step * 6);
        StyleBoxFlat weightFlat = GD.Load<StyleBoxFlat>("res://weightLineFlatSimple.tres");
        for (int i = 0; i < 60; i++)
        {
            Panel h = new();
            h.CustomMinimumSize = new((i+2) % 2f == 0 ? 50 : 30, 8);
            h.AddThemeStyleboxOverride("panel", weightFlat);
            // h.Position = new GV2(500, i * 30);
            // h.CustomMinimumSize = new(100 + (float)Math.Sin((i / 30.0) * (1 * MathF.PI)) * 100, 0);
            // h.CustomMinimumSize = new(100, 0);
            h.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            Vbox1.AddChild(h);
            Clicks.Add(h);
        }
        target = Vbox1.Position.Y;
    }
    /*
    _
    _
    _
    _
    _
    */

    public override void _Process(double delta)
    {
        if (Ed.CurrentFocus != EditorFocus.WeightPicker)
            return;
        currentWeightLabel.Text = count.ToString();
        weightNameLabel.Text = WeightNames[(int)((count - 100) / 50)];
        if (count % 100 != 0)
            weightNameHalfLabel.Text = "Half";
        else
            weightNameHalfLabel.Text = "";
        GV2 vpos = Vbox1.Position;
        GV2 tpos = new(vpos.X, target - 32);
        if (vpos.DistanceTo(tpos) < 2)
            Vbox1.Position = tpos;
        else
            Vbox1.Position = vpos.Lerp(tpos, (float)delta * 7f);
        weight = Math.Max(Math.Min(1000, weight), 300);
        rec.VariationOpentype = new(){{"weight", weight}, {"custom_CASL", 1}, {"custom_CRSV", 1}};
        if (Vbox1.GlobalPosition.Y <= -step * 18 && Vbox1.GlobalPosition.Y % step < 0.1)
        {
            Vbox1.GlobalPosition = new(Vbox1.GlobalPosition.X, - step * 12);
            target += step * 6;
        }
        if (Vbox1.GlobalPosition.Y >= -step * 6 && Vbox1.GlobalPosition.Y % step < 0.1)
        {
            Vbox1.GlobalPosition = new(Vbox1.GlobalPosition.X,-step * 12);
            target -= step * 6;
        }
        V2 mov = Ed.GetMovementInput((float)delta, wait: 0.05f);
        if (mov.Y > 0 )
        {
            if (count > 100)
            {
                weight -= 40;
                count -= 50;
                target -= step;
                Rattle();
            }
            //     theTween?.Kill();
            //     theTween = CreateTween();
            //     theTween.TweenProperty(Vbox1, "global_position:y", Vbox1.GlobalPosition.Y - step, 0.1);
            // }
        }
        else if (mov.Y < 0)
        {
            if (count < 1000)
            {
                weight += 40;
                count += 50;
                target += step;
                Rattle();
            }
            // theTween?.Kill();
            // theTween = CreateTween();
            // theTween.TweenProperty(Vbox1, "global_position:y", Vbox1.GlobalPosition.Y + step, 0.1);
            // }
        }
        else
        {
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
        int counter = 0;
        foreach (Panel c in Clicks)
        {
            // float half = GetWindow().Size.Y / 2.0f;
            // float f = MathF.Round(MathF.Max((half - MathF.Abs(c.GlobalPosition.Y - half)) / half, .3f),3);
            // c.CustomMinimumSize = new(counter % 2f == 0 ? 200*f : 100*f, 4);
            // counter += 1;
        }
    }

    void Rattle()
    {
        if (canSoundAgain)
        {
            Manager.PlaySound("Rattle3.wav",(float)GD.RandRange(0.1,0.7), 1.5f, 2.0f);
            canSoundAgain = false;
            soundTimer.WaitTime = .1f;
            soundTimer.Start();
        }
    
    }
    void Rattle2()
    {
        if (canSoundAgain)
        {
            Manager.PlaySound("Rattle3.wav",(float)GD.RandRange(0.6,0.7), 0.2f, 0.7f);
            canSoundAgain = false;
            soundTimer.WaitTime = .3f;
            soundTimer.Start();
        }
    
    }
}
