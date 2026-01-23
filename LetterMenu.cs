using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using Vectordrawing;
using Snl = Vectordrawing.StringNamesList;
using V2 = System.Numerics.Vector2;


public partial class LetterMenu : CanvasLayer
{
    public GlyphPreview CurrentlySelected = null!;
    Editor Ed = null!;
    ScrollContainer Scroll = null!;
    static string numberGlyphs = "1234567890";
    static string letterGlyphs = "abcdefghijklmnopqrstuvwxyz";
    static string punctuationGlyphs = ".,!?'\":;";
    static string specialGlyphs = "-+_=@#$%^&*(){}[]<>\\/|";
    int MaxGridColumns = 10;
    V2 CurrentPos = new(0,0);
    List<string> allGlyphs = [numberGlyphs, punctuationGlyphs, letterGlyphs, letterGlyphs.ToUpper(), specialGlyphs];
    List<string> GroupTitles = ["numbers", "punctuation", "lowers", "uppers", "friends"];
    List<List<GlyphPreview>> Previews = [];
    List<List<int>> Rows = [];
    List<int> RowsFlat = [];
    List<PreviewGrid> PreviewGrids = [];
    public static Dictionary<char, (Shapes, Vectordrawing.UndoRedo)> ShapeDict = [];
    PackedScene PreviewGridScene = GD.Load<PackedScene>("res://preview_grid.tscn");

    public override void _Ready()
    {
        Ed = (Editor)GetParent();
        VBoxContainer glyphContainer = (VBoxContainer)FindChild("VBoxContainer");
        PackedScene glyphScene = GD.Load<PackedScene>("res://glyph_preview.tscn");
        Scroll = (ScrollContainer)FindChild("ScrollContainer");
        foreach (string glyphs in allGlyphs)
        {
            PreviewGrid pg = PreviewGridScene.Instantiate<PreviewGrid>();
            glyphContainer.AddChild(pg);
            PreviewGrids = [.. PreviewGrids, pg];
            pg.SetTitleText(GroupTitles[allGlyphs.IndexOf(glyphs)]);
            Previews.Add([]);
            List<int> currentRowGroup = [];
            int counter = 0;
            foreach (char glyph in glyphs)
            {
                counter += 1;
                GlyphPreview glyphPreview = glyphScene.Instantiate<GlyphPreview>();
                glyphPreview.SetMyGlyph(glyph);
                Previews.Last().Add(glyphPreview);
                pg.AddPreview(glyphPreview);
                Shapes shapes = new();
                ShapeDict[glyph] = (shapes, new(shapes));
                
                if (counter % MaxGridColumns == 0 || counter == glyphs.Count())
                    currentRowGroup.Add(counter % MaxGridColumns == 0 ? MaxGridColumns : counter % MaxGridColumns);
            }
            Rows.Add(currentRowGroup);

        }
        foreach (List<int> r in Rows)
            RowsFlat.AddRange(r);
        CurrentlySelected = Previews.First().First();
        CurrentlySelected.ToggleSelected();
        foreach (List<GlyphPreview> prevs in Previews)
        {
            if (prevs.Count > MaxGridColumns && !prevs.Contains(CurrentlySelected))
            {
                foreach (GlyphPreview p in prevs[MaxGridColumns..])
                {
                    p.Visible = false;
                }
            }
        }
    }
        
    public int CeilDiv(int num, int den)
    {
        return (int)MathF.Ceiling((float)num / den);
    }


    public override void _Process(double delta)
    {
        int oldGroup = GetCurrentGroup().currentGroup;
        if (Input.IsActionJustPressed(Snl.left))
            CurrentPos.X = CurrentPos.X == 0 ? RowsFlat[(int)CurrentPos.Y] - 1 : CurrentPos.X - 1;
        if (Input.IsActionJustPressed(Snl.right))
            CurrentPos.X = CurrentPos.X == RowsFlat[(int)CurrentPos.Y] - 1 ? 0 : CurrentPos.X + 1;
        if (Input.IsActionJustPressed(Snl.up))
            CurrentPos.Y = CurrentPos.Y == 0 ? RowsFlat.Count() - 1 : CurrentPos.Y - 1;

        if (Input.IsActionJustPressed(Snl.down))
            CurrentPos.Y = CurrentPos.Y == RowsFlat.Count() - 1? 0 : CurrentPos.Y + 1;

        CurrentlySelected.ToggleSelected();
        CurrentlySelected = GetCurrentPreview();
        CurrentlySelected.ToggleSelected();

        int newGroup = GetCurrentGroup().currentGroup;
        if (newGroup != oldGroup)
            OpenCloseGroups(oldGroup, newGroup);
        if (GetWindow().Size.Y - CurrentlySelected.GlobalPosition.Y < 200)
            Scroll.ScrollVertical += 14;
        if (CurrentlySelected.GlobalPosition.Y < 200)
            Scroll.ScrollVertical -= 14;
        if (CurrentlySelected.GlobalPosition.Y < 0)
            Scroll.ScrollVertical -= 80;
        if (CurrentlySelected.GlobalPosition.Y > GetWindow().Size.Y)
            Scroll.ScrollVertical += 80;

        if (Input.IsActionJustPressed(Snl.add_new_point))
            Ed.OpenDrawingScene(CurrentlySelected);
    }


    GlyphPreview GetCurrentPreview()
    {
        (int currentGroup, int rowInGroup) = GetCurrentGroup();
        List<int> r = Rows[currentGroup];
        if (CurrentPos.X >= r[rowInGroup]) CurrentPos.X = r[rowInGroup] - 1;
        return Previews[currentGroup][(int)rowInGroup * MaxGridColumns + (int)CurrentPos.X];
    }

    (int currentGroup, int rowInGroup) GetCurrentGroup()
    {
        int total = 0;
        for(int i = 0; i < Rows.Count; i++)
        {
            total += Rows[i].Count;
            if (total > CurrentPos.Y)
                return (i, Rows[i].Count - (total - (int)CurrentPos.Y));
        }
        return (Rows.Count - 1, Rows.Last().Count - 1);
    }
    
    void OpenCloseGroups(int oldGroup, int newGroup)
    {
        PreviewGrid o = PreviewGrids[oldGroup];
        PreviewGrid n = PreviewGrids[newGroup];

        o.SetTitleColor(new("#666666"));
        n.SetTitleColor(new("#000000"));
        o.ClosePreviews();
        n.OpenPreviews();
        o.SetFSep(30);
        n.SetFSep(100, true);

    }


}