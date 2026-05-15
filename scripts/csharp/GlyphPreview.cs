using Godot;
using System;
using System.Collections.Concurrent;
using System.Text;
using GV2 = Godot.Vector2;
using V2 = System.Numerics.Vector2;

namespace Vectordrawing;

public partial class GlyphPreview : TextureRect
{
    static int c = 0;
    Panel Background = null!;
    Panel BackgroundSelected = null!;
    bool Selected = false;
    char MyGlyph = 'j';
    string alfabet = "abcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()-=_+[]{};:\\|,.<>/?01234567890";
    bool ShowPlaceholder = true;
    FontFile MediumFontFile = GD.Load<FontFile>("res://assets/fonts/DraftingMono/DraftingMono-Regular.otf");
    Font MediumFont = GD.Load<Font>("res://assets/fonts/DraftingMono/DraftingMono-Regular.otf");
    Godot.Vector2I FontSize = new(60, 0);
    Color placeholderColor = Color.Color8(120,120,120,255);
    Color placeholderColorSelected = Color.Color8(250,180,180,255);


    public override void _Ready()
    {
        MyGlyph = alfabet[(int)GD.RandRange(0,alfabet.Length)];
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
            GV2 heightOffset = new(0,0);
            Color charCol = Selected ? placeholderColorSelected : placeholderColor;
            DrawChar(MediumFontFile, (pos - baselineOffset) + heightOffset, MyGlyph.ToString(), FontSize.X, charCol);
        }
    }

    public void ToggleSelected()
    {
        Selected = !Selected;
        Manager.PlaySound("Rattle3.wav",.15f, 1.0f, 1.5f);
        CreateTween().SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Elastic)
            .TweenProperty(this, "position:y", Position.Y, 1.5f).From(Position.Y + 30);
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

    public void SetPreviewTexture(Texture2D? tex = null)
    {
        ShowPlaceholder = tex is null;
        Texture = tex;
        QueueRedraw();
    }
}
