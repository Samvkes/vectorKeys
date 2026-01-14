using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vectordrawing;
using Snl = Vectordrawing.StringNamesList;

public partial class LetterMenu : CanvasLayer
{
    public GlyphPreview CurrentlySelected = null!;
    Editor Ed = null!;
    int GridColumns = 0;
    int GridRows = 0;
    static string numberGlyphs = "1234567890";
    static string letterGlyphs = "abcdefghijklmnopqrstuvwxyz";
    static string punctuationGlyphs = ".,!?'\":;";
    static string specialGlyphs = "-+_=@#$%^&*(){}[]<>\\/|";
    string allGlyphs = numberGlyphs + punctuationGlyphs + letterGlyphs + letterGlyphs.ToUpper() + specialGlyphs;
    List<GlyphPreview> Previews = [];
    public static Dictionary<char, (Shapes, Vectordrawing.UndoRedo)> ShapeDict = [];

    public override void _Ready()
    {
        Ed = (Editor)GetParent();
        GridContainer glyphContainer = (GridContainer)FindChild("GlyphContainer");
        GridColumns = glyphContainer.Columns;
        GridRows = (int)Math.Ceiling(allGlyphs.Length / (double)GridColumns);
        PackedScene glyphScene = GD.Load<PackedScene>("res://glyph_preview.tscn");
        foreach (char glyph in allGlyphs)
        {
            GlyphPreview glyphPreview = glyphScene.Instantiate<GlyphPreview>();
            glyphPreview.SetMyGlyph(glyph);
            Previews.Add(glyphPreview);
            glyphContainer.AddChild(glyphPreview);
            Shapes shapes = new();
            ShapeDict[glyph] = (shapes, new(shapes));
        }
        CurrentlySelected = Previews.First();
        CurrentlySelected.ToggleSelected();
    }

    public override void _Process(double delta)
    {
        int containerLength = allGlyphs.Length;
        int selectedIndex = Previews.IndexOf(CurrentlySelected);
        int oldIndex = selectedIndex;
        if (Input.IsActionJustPressed(Snl.left))
            selectedIndex = selectedIndex % GridColumns == 0 ? selectedIndex + GridColumns - 1 : selectedIndex - 1;
        if (Input.IsActionJustPressed(Snl.right))
            selectedIndex = (selectedIndex+1) % GridColumns == 0 ? selectedIndex - GridColumns + 1 : selectedIndex + 1;
        if (Input.IsActionJustPressed(Snl.up))
            selectedIndex = selectedIndex < GridColumns ? containerLength  + selectedIndex + (GridRows-1) * GridColumns : selectedIndex - GridColumns;
        if (Input.IsActionJustPressed(Snl.down))
            selectedIndex = selectedIndex + GridColumns > containerLength ? selectedIndex % GridColumns: selectedIndex + GridColumns; 
        if (selectedIndex != oldIndex)
        {
            CurrentlySelected.ToggleSelected();
            CurrentlySelected = Previews[selectedIndex];
            CurrentlySelected.ToggleSelected();
        }

        if (Input.IsActionJustPressed(Snl.add_new_point))
            Ed.OpenDrawingScene(CurrentlySelected);
    }

}
