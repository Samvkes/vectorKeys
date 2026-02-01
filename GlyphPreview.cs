using Godot;
using System;
using System.Collections.Concurrent;
using Vectordrawing;
using GV2 = Godot.Vector2;
using V2 = System.Numerics.Vector2;

public partial class GlyphPreview : TextureRect
{
    Panel Background = null!;
    Panel BackgroundSelected = null!;
    bool Selected = false;
    char MyGlyph = 'j';
    bool ShowPlaceholder = true;
    FontFile MediumFontFile = GD.Load<FontFile>("res://assets/DraftingMono/DraftingMono-Medium.otf");
    Font MediumFont = GD.Load<Font>("res://assets/DraftingMono/DraftingMono-Medium.otf");
    Godot.Vector2I FontSize = new(60, 0);
    Color placeholderColor = Color.Color8(120,120,120,255);
    Color placeholderColorSelected = Color.Color8(250,180,180,255);


    public override void _Ready()
    {
        Background = (Panel)FindChild("Background");
        BackgroundSelected = (Panel)FindChild("BackgroundSelected");

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
    }

    public override void _Draw()
    {
        if (ShowPlaceholder)
        {
            int glyphIndex = MediumFontFile.GetGlyphIndex(FontSize.X, MyGlyph, 0);
            GV2 baselineOffset = MediumFontFile.GetGlyphOffset(0, FontSize, glyphIndex);
            GV2 glyphSize = MediumFontFile.GetGlyphSize(0, FontSize, glyphIndex);
            GV2 pos = (Size - glyphSize) / 2;
            GV2 heightOffset = Selected ? new(0,-5) : new(0,0);
            Color charCol = Selected ? placeholderColorSelected : placeholderColor;
            DrawChar(MediumFontFile, (pos - baselineOffset) + heightOffset, MyGlyph.ToString(), FontSize.X, charCol);
        }
    }

    public void ToggleSelected()
    {
        Selected = !Selected;
        if (Selected)
        {
            // BackgroundSelected.Visible = true;
            // Scale = new(1.1f, 1.1f);
            // if (GD.Randf() > 0.6)
                // Manager.PlaySound("Rattle4.wav",.3f, 1.0f, 1.3f);
            // else
                Manager.PlaySound("Rattle3.wav",.15f, 1.0f, 1.5f);
            CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic).TweenProperty(this, "position:y", Position.Y, 1.5f).From(Position.Y + 30);
        }
        else
        {
            // BackgroundSelected.Visible = false;
            // Scale = Vector2.One;
        }
        QueueRedraw();
    }

    public void SetMyGlyph(char glyph)
    {
        MyGlyph = glyph;
        QueueRedraw();
    }

    public char GetGlyph()
    {
        return MyGlyph;
    }

    public void SetPreviewTexture(Texture2D tex)
    {
        ShowPlaceholder = false;
        Texture = tex;
        QueueRedraw();
    }
}
