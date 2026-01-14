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

    public override void _Draw()
    {
        if (ShowPlaceholder)
        {
            int glyphIndex = MediumFontFile.GetGlyphIndex(FontSize.X, MyGlyph, 0);
            GV2 baselineOffset = MediumFontFile.GetGlyphOffset(0, FontSize, glyphIndex);
            GV2 glyphSize = MediumFontFile.GetGlyphSize(0, FontSize, glyphIndex);
            GV2 pos = (Size - glyphSize) / 2;
            Color charCol = Selected ? placeholderColorSelected : placeholderColor;
            DrawChar(MediumFontFile, pos - baselineOffset, MyGlyph.ToString(), FontSize.X, charCol);
        }
    }

    public void ToggleSelected()
    {
        Selected = !Selected;
        if (Selected)
        {
            BackgroundSelected.Visible = true;
            Scale = new(1.1f, 1.1f);
        }
        else
        {
            BackgroundSelected.Visible = false;
            Scale = Vector2.One;
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
    public override void _Process(double delta)
    {
    }
}
