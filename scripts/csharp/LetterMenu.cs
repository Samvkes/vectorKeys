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
using GV2 = Godot.Vector2;
using System.IO;

// what to cache for lettermenu
// last 5 dicts of glyphs?
// last 5 previews of glyph families?
// what of Shapes or Shape state should get saved?
// Shape needs references back to Shapes, voorzichtig serializeren!!

public class Glyph
{
    public char G;
    public Shapes Shapes = null!;
    public DateTime lastEdit = DateTime.Now;
    public int minutesSpent = 0;
    public int anchorCount = 0;
}

public partial class LetterMenu : Control
{
    public Dictionary<char, GlyphPreview> PreviewDict = [];
    public GlyphPreview CurrentlySelected = null!;
    public static Dictionary<char, Glyph> ShapeDict = [];
    public int CurrentWeight = 500;
    public List<Axis> CurrentAxes = [];
    public List<string> allGlyphs = [numberGlyphs, punctuationGlyphs, letterGlyphs, letterGlyphs.ToUpper(), specialGlyphs];

    static string numberGlyphs = "1234567890";
    static string letterGlyphs = "abcdefghijklmnopqrstuvwxyz";
    static string punctuationGlyphs = ".,!?'\":;";
    static string specialGlyphs = "-+_=@#$%^&*(){}[]<>\\/|";

    Editor Ed = null!;
    ScrollContainer Scroll = null!;
    Panel Selector = null!;
    Control CurrentTitle = null!;
    Timer weightPickerSwitchTimer = null!;

    List<string> GroupTitles = ["numbers", "punctuation", "lowers", "uppers", "friends"];
    List<List<int>> Rows = [];
    List<int> RowsFlat = [];
    List<PreviewGrid> PreviewGrids = [];
    List<List<GlyphPreview>> Previews = [];

    GV2 SelectorGoalPos = GV2.Zero;
    int MaxGridColumns = 10;
    V2 CurrentPos = new(0,0);
    PackedScene PreviewGridScene = GD.Load<PackedScene>("res://scenes/preview_grid.tscn");

    public override void _Ready()
    {
        Ed = (Editor)GetParent();
        Selector = (Panel)FindChild("Selector");
        Selector.PivotOffsetRatio = new GV2(.5f,.5f);
        VBoxContainer glyphContainer = (VBoxContainer)FindChild("GlyphContainer");
        PackedScene glyphScene = GD.Load<PackedScene>("res://scenes/glyph_preview.tscn");
        CurrentTitle = (Control)FindChild("CurrentTitle");
        weightPickerSwitchTimer = new();
        weightPickerSwitchTimer.WaitTime = .5f;
        weightPickerSwitchTimer.OneShot = true;
        AddChild(weightPickerSwitchTimer);
        GlyphNames.PrepareNames();
        
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
                PreviewDict[glyph] = glyphPreview;
                glyphPreview.SetMyGlyph(glyph);
                Previews.Last().Add(glyphPreview);
                pg.AddPreview(glyphPreview);
                Shapes shapes = new();
                ShapeDict[glyph] = new();
                ShapeDict[glyph].G = glyph;
                ShapeDict[glyph].Shapes = shapes;
                if (counter % MaxGridColumns == 0 || counter == glyphs.Count())
                    currentRowGroup.Add(counter % MaxGridColumns == 0 ? MaxGridColumns : counter % MaxGridColumns);
            }
            Rows.Add(currentRowGroup);
        }
        foreach (List<int> r in Rows)
            RowsFlat.AddRange(r);
        CurrentlySelected = Previews.First().First();
        CurrentlySelected.ToggleSelected();
        
        Fun.DelayOneFrame(this, () => {PreviewGrids.First().SetFSep(100, false);});
        PreviewGrids.First().SetTitleColor(new("#000000"));
        foreach (List<GlyphPreview> prevs in Previews)
        {
            if (prevs.Count > MaxGridColumns && !prevs.Contains(CurrentlySelected))
            {
                foreach (GlyphPreview p in prevs[MaxGridColumns..])
                {
                    // p.Visible = false;
                }
            }
        }
    }

    public void ClearPlaceholderPreviews()
    {
        foreach (GlyphPreview g in PreviewDict.Values)
        {
            g.SetPreviewTexture();
        }
    }

    public void ClearShapeDict()
    {
        foreach (char c in ShapeDict.Keys)
        {
            ShapeDict[c] = new();
            ShapeDict[c].G = c;
            ShapeDict[c].Shapes = new();
        }
    }

    public override void _Process(double delta)
    {
        if (TextInput.BeingEdited) return;

        if (Ed.CurrentFocus != EditorFocus.Letters)
            return;
        float spd = delta < 0.03 ? 20 : 20;

        if (Input.IsActionJustPressed(Snl.select_mode))
        {
            weightPickerSwitchTimer.Start();
            Ed.weightP.SwitchOn();
        }

        if (Input.IsActionJustReleased(Snl.select_mode) && weightPickerSwitchTimer.TimeLeft <= 0)
        {
            Ed.weightP.SwitchOff();
        }

        Selector.Position += (CurrentlySelected.GlobalPosition - Selector.Position) * (1 - MathF.Exp( -(float)delta * spd));
        // Selector.Position = Selector.Position.Lerp(CurrentlySelected.GlobalPosition, (float)delta * 20);
        int oldGroup = GetCurrentGroup().currentGroup;
        V2 normalizedMovement = Ed.GetMovementInput((float)delta, 0.06f, 
            CurrentPos.Y == 0 || CurrentPos.Y == RowsFlat.Count()-1 || CurrentPos.X == 0 || CurrentPos.X == RowsFlat[(int)CurrentPos.Y]-1,
            0.3f);

        if (normalizedMovement.X < 0)
            CurrentPos.X = CurrentPos.X == 0 ? RowsFlat[(int)CurrentPos.Y] - 1 : CurrentPos.X - 1;
        if (normalizedMovement.X > 0)
            CurrentPos.X = CurrentPos.X == RowsFlat[(int)CurrentPos.Y] - 1 ? 0 : CurrentPos.X + 1;
        if (normalizedMovement.Y < 0)
            CurrentPos.Y = CurrentPos.Y == 0 ? RowsFlat.Count() - 1 : CurrentPos.Y - 1;
        if (normalizedMovement.Y > 0)
            CurrentPos.Y = CurrentPos.Y == RowsFlat.Count() - 1? 0 : CurrentPos.Y + 1;

        if (CurrentlySelected != GetCurrentPreview())
        {
            CurrentlySelected.ToggleSelected();
            CurrentlySelected = GetCurrentPreview();
            CurrentlySelected.ToggleSelected();
            // SelectorGoalPos = CurrentlySelected.GlobalPosition;
            // Selector.Position = CurrentlySelected.Position;

            // CreateTween().TweenProperty(Selector, "position", CurrentlySelected.GlobalPosition, 0.2f);
        }

        int newGroup = GetCurrentGroup().currentGroup;
        if (newGroup != oldGroup)
            OpenCloseGroups(oldGroup, newGroup);
        if (GetWindow().Size.Y - Selector.GlobalPosition.Y < 500)
            Scroll.ScrollVertical += (int)(delta * 1800);
        if (Selector.GlobalPosition.Y < 500)
            Scroll.ScrollVertical -= (int)(delta * 1800);
        if (Selector.GlobalPosition.Y < 0)
            Scroll.ScrollVertical -= (int)(delta * 4000);
        if (Selector.GlobalPosition.Y > GetWindow().Size.Y)
            Scroll.ScrollVertical += (int)(delta * 4000);

        if (Input.IsActionJustPressed(Snl.add_new_point))
        {
            Ed.CurrentGlyph = ShapeDict[CurrentlySelected.GetGlyph()];
            Ed.SwitchToWorkbench(ShapeDict[CurrentlySelected.GetGlyph()].Shapes);
        }
        
        var c = CurrentTitle.GetChildren();

        if (((Label)c[0]).Text != CurrentWeight.ToString())
        if (CurrentAxes.Count > 0)
        {
            ((Label)c[2]).Visible = true;
            string axesString = "";
            foreach (Axis a in CurrentAxes)
            {
                axesString += a.Name;
                if (a != CurrentAxes.Last())
                    axesString += ", ";
            }
            if (((Label)c[2]).Text != axesString)
                ((Label)c[2]).Text = axesString;
        }
        else
            ((Label)c[2]).Visible = false;
        if (((Label)c[1]).Text != CurrentWeight.ToString())
            ((Label)c[1]).Text = CurrentWeight.ToString();
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
        // o.ClosePreviews();
        n.OpenPreviews();
        o.SetFSep(30);
        n.SetFSep(100, false);

    }
}