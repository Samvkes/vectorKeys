using Godot;
using System;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using Snl = Vectordrawing.StringNamesList;
using Vectordrawing;
using System.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;

public struct FamilyConfig
{
    public string Name;
    public string[] Styles;

    public FamilyConfig()
    {
        Name = "testFamily";
        Styles = ["light", "regular", "bold"];
    }
}

public partial class Editor : Node2D
{
    Node2D DrawingBase = null!; 
    LetterMenu LetterMenu = null!; 
    public Manager Manager = GD.Load<PackedScene>("res://Manager.tscn").Instantiate<Manager>();
    public FileDialog UfoFilePicker = null!;
    public FamilyConfig CurrentFamily = new();

    public override void _Ready()
    {
        AddChild(Manager);
        GetTree().Paused = true;
        DrawingBase = (Node2D)FindChild("DrawingBase");
        LetterMenu = (LetterMenu)FindChild("LetterMenu");
        UfoFilePicker = (FileDialog)FindChild("UfoFilePicker");
        DrawingBase.Visible = false;
        LetterMenu.Visible = true;
        DrawingBase.ProcessMode = ProcessModeEnum.Pausable;
        LetterMenu.ProcessMode = ProcessModeEnum.WhenPaused;
    }

    public override async void _Process(double delta)
    {
        if (Input.IsActionJustPressed(Snl.escape))
        {
            DrawingBase.Visible = false;
            LetterMenu.Visible = true;
            DrawingBase.ProcessMode = ProcessModeEnum.Pausable;
            LetterMenu.ProcessMode = ProcessModeEnum.WhenPaused;
            Texture2D t = ((Base)DrawingBase.FindChild("BaseTest")).PreviewTex;
            LetterMenu.CurrentlySelected.SetPreviewTexture(t);
            Base b = (Base)DrawingBase.FindChild("BaseTest");
            LetterMenu.ShapeDict[LetterMenu.CurrentlySelected.GetGlyph()] = (b.Shapes,b.UndoRedo);
        }

        if (Input.IsActionJustPressed(Snl.export_ufo))
        {
            ExportUfo();
        }
    }

    async void ExportUfo()
    {
        UfoFilePicker.FileMode = FileDialog.FileModeEnum.SaveFile;
        UfoFilePicker.Visible = true;
        string file = (string)(await ToSignal(UfoFilePicker, FileDialog.SignalName.FileSelected))[0];
        UfoWriterReader.ExportUfo(file, CurrentFamily);
    }

    public void OpenDrawingScene(GlyphPreview preview)
    {
        Godot.Collections.Array a = [];
        OS.Execute("python3", ["-h"], a);
        GD.Print(a[0]);
        LetterMenu.ProcessMode = ProcessModeEnum.Pausable;
        Base b = (Base)DrawingBase.FindChild("BaseTest");
        (b.Shapes, b.UndoRedo) = LetterMenu.ShapeDict[LetterMenu.CurrentlySelected.GetGlyph()];
        if (b.Shapes.S.Count == 0) b.Initialize();
        DrawingBase.ProcessMode = ProcessModeEnum.WhenPaused;
        b.JustUnpaused = true;
        Fun.Delayed(this, 0.1f, () =>
        {
            DrawingBase.Visible = true;
            LetterMenu.Visible = false;
        });
    }
}
