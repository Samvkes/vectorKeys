using Godot;
using System;
using System.Collections.Generic;
using Vectordrawing;
using GV2 = Godot.Vector2;
using V2 = System.Numerics.Vector2;

public partial class PreviewGrid : VBoxContainer
{
    HSeparator FrontSep = null!;
    RichTextLabel Title = null!;
    GridContainer Grid = null!;
    List<GlyphPreview> Previews = [];

    float FSepTarget = 30;
    float opentime = .3f;
    float closetime = .3f;
    

    public override void _Ready()
    {
        FrontSep = (HSeparator)FindChild("FrontSep");
        Title = (RichTextLabel)FindChild("Title");
        Grid = (GridContainer)FindChild("Grid");
    }


    public override void _Process(double delta)
    {
    }

    public void AddPreview(GlyphPreview p)
    {
        Previews.Add(p);
        Grid.AddChild(p);
    }

    public void OpenPreviews()
    {
        if (Previews.Count <= Grid.Columns)
            return;
        foreach(GlyphPreview p in Previews)
        {
            p.Visible = true;
            // p.ZIndex = 0;
            CreateTween().TweenProperty(p, "modulate:a", 1, opentime*1.5);
            CreateTween().SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.In).TweenProperty(Grid, "theme_override_constants/v_separation", 20, opentime);
        }
    }

    public void SetFSep(float target, bool fromRight = false)
    {
        if (fromRight)
        {
            HBoxContainer hb = (HBoxContainer)FindChild("HBoxContainer");
            FSepTarget = hb.Size.X - (target + Title.Size.X);
            CreateTween().SetTrans(Tween.TransitionType.Quint).TweenProperty(FrontSep, "custom_minimum_size:x", FSepTarget, 0.3);
        }
        else
        {
            FSepTarget = target;
            CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic).TweenProperty(FrontSep, "custom_minimum_size:x", target, 0.5);
        }
    }

    public void ClosePreviews()
    {
        if (Previews.Count <= Grid.Columns)
            return;
        foreach(GlyphPreview p in Previews[Grid.Columns..])
        {
            CreateTween().SetEase(Tween.EaseType.Out).TweenProperty(p, "modulate:a", 0, closetime*1.5);
            // // p.ZIndex = -1;
            CreateTween().SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.In).TweenProperty(Grid, "theme_override_constants/v_separation", -100, closetime);
            // // CreateTween().TweenProperty(Grid, "v_separation", -100, 1);
            p.Visible = false;
        }
    }

    public void SetTitleText(string title)
    {
        Title.Text = title;
    }

    public void SetTitleColor(Color col)
    {
        Title.AddThemeColorOverride("default_color", col);
    }
}
