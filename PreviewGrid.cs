using Godot;
using System;
using System.Collections.Generic;

public partial class PreviewGrid : VBoxContainer
{
    HSeparator FrontSep = null!;
    RichTextLabel Title = null!;
    GridContainer Grid = null!;
    List<GlyphPreview> Previews = [];

    float FSepTarget = 30;
    

    public override void _Ready()
    {
        FrontSep = (HSeparator)FindChild("FrontSep");
        Title = (RichTextLabel)FindChild("Title");
        Grid = (GridContainer)FindChild("Grid");
    }

    public override void _Process(double delta)
    {
        Vector2 mins = FrontSep.CustomMinimumSize;
        if (MathF.Abs(mins.X - FSepTarget) > 10)
            FrontSep.CustomMinimumSize = new(float.Lerp(mins.X, FSepTarget, (float)delta * 20), mins.Y);
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
        foreach(GlyphPreview p in Previews[Grid.Columns..])
        {
            p.Visible = true;
        }
    }

    public void SetFSep(float target, bool fromRight = false)
    {
        if (fromRight)
        {
            HBoxContainer hb = (HBoxContainer)FindChild("HBoxContainer");
            FSepTarget = hb.Size.X - (target + Title.Size.X);
        }
        else
            FSepTarget = target;
    }

    public void ClosePreviews()
    {
        if (Previews.Count <= Grid.Columns)
            return;
        foreach(GlyphPreview p in Previews[Grid.Columns..])
        {
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
