using Godot;
using System;
using Vectordrawing;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using System.Linq;
using System.Collections.Generic;
using Snl = Vectordrawing.StringNamesList;

public class Pastable(String p)
{
    public String ToPaste = p;
    public bool Shared = false;

}


public partial class PasteMenu : Control
{
    Workbench Wb = null!;
    SidePanel Sp = null!;
    Editor Ed = null!;
    Panel Selector = null!;
    GridContainer GlyphContainer = null!;
    GlyphPreview CurrentlySelected = null!;
    List<(Pastable pastable, GlyphPreview preview)> Pastables = [];
    PackedScene glyphScene = GD.Load<PackedScene>("res://scenes/glyph_preview.tscn");
    V2 CurrentPos = new(0,0);
    int Rows = 0;
    int Columns = 0;

    public override void _Ready()
    {
        
        Wb = (Workbench)(GetParent().GetParent().GetParent());
        Ed = (Editor)Wb.GetParent();
        Sp = (SidePanel)(GetParent().FindChild("SidePanel"));
        Selector = (Panel)FindChild("Selector");
        Selector.PivotOffsetRatio = new GV2(.5f,.5f);
        GlyphContainer = (GridContainer)FindChild("GlyphContainer");
        GlyphContainer.Columns = 10;
        // for (int i = 0; i < Rows * Columns; i++)
        // {
        //     GlyphPreview g = glyphScene.Instantiate<GlyphPreview>();
        //     g.SetMyGlyph('?');
        //     GlyphContainer.AddChild(g);
        // }
        // CurrentlySelected = (GlyphPreview)GlyphContainer.GetChildren().First();
    }

    public Pastable? HandleInput(float delta)
    {
        if (Pastables.Count <= 0 || CurrentlySelected is null) return null;
        float spd = 10;
        Selector.Position += (CurrentlySelected.GlobalPosition - Selector.Position) * (1 - MathF.Exp( -(float)delta * spd));
        V2 normalizedMovement = Ed.GetMovementInput((float)delta, 0.06f);

        if (normalizedMovement.X < 0)
            CurrentPos.X = CurrentPos.X == 0 ? Columns - 1 : CurrentPos.X - 1;
        if (normalizedMovement.X > 0)
            CurrentPos.X = CurrentPos.X == Columns - 1 ? 0 : CurrentPos.X + 1;
        if (normalizedMovement.Y < 0)
            CurrentPos.Y = CurrentPos.Y == 0 ? Rows - 1 : CurrentPos.Y - 1;
        if (normalizedMovement.Y > 0)
            CurrentPos.Y = CurrentPos.Y == Rows - 1 ? 0 : CurrentPos.Y + 1;
        CurrentlySelected = (GlyphPreview)GlyphContainer.GetChildren()[CurrentIndex()];

        Pastable? toPaste = null;
        if (Input.IsActionJustPressed(Snl.add_new_point))
        {
            Pastable currentPastable = Pastables[CurrentIndex()].pastable;
            if (currentPastable.ToPaste is not null)
                toPaste = currentPastable;
        }

        return toPaste;
    }

    int CurrentIndex()
    {
        return (int)(CurrentPos.Y * Columns + CurrentPos.X);
    }

    public void AddYank(Shape toYank)
    {
        string pasteString = Shapes.SaveOneShapeState(toYank);
        for (int i = 0; i < Pastables.Count; i++)
        {
            (Pastable p, GlyphPreview g) = Pastables[i];
            if (p.ToPaste == pasteString)
            {
                Pastables.RemoveAt(i);
                Pastables.Insert(0,(p, g));
                GlyphContainer.MoveChild(g, 0);
                return;
            }
        }
        GlyphPreview glyph = glyphScene.Instantiate<GlyphPreview>();
        glyph.SetMyGlyph(' ');
        glyph.SetPreviewTexture(toYank.ShapeTexture());
        GlyphContainer.AddChild(glyph);
        GlyphContainer.MoveChild(glyph, 0);
        Pastables.Insert(0,(new(pasteString), glyph));
        CurrentlySelected = glyph;
    }
}
