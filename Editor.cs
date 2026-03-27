using Godot;
using System;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using Snl = Vectordrawing.StringNamesList;
using Vectordrawing;
using System.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

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

public enum EditorFocus
{
    Letters,
    Workbench,
    WeightPicker,
    ProjectPicker,
}

public record RawInput {
    public string? NumberPressed = null;
    public  string? LetterPressed = null;
    public string? NumberJustPressed = null;
    public string? LetterJustPressed = null;
    public string? Raw = null;
    public string? RawJustPressed = null;
}

public partial class Editor : CanvasLayer
{
    public Base Workbench = null!; 
    LetterMenu LetterMenu = null!; 
    ProjectPicker ProjectPicker = null!;
    public Manager Manager = GD.Load<PackedScene>("res://manager.tscn").Instantiate<Manager>();
    public FileDialog UfoFilePicker = null!;
    public FamilyConfig CurrentFamily = new();
    public static bool DebugSwitch = false;
    Timer FpsTimer = null!;
    float ThrottleWaitTime = 1f;
    public bool Throttling = false;
    public Timer MovementTimer = new();
    bool CanMoveAgain = true;
    float MovementHeldTime = 0;
    float ValidHoldTime = .2f;
    bool StickyGuide = true;
    public WeightPicker weightP = null!;
    ProjectPicker PPicker = null!;
    public Project? CurrentProject = null;
    public ProjectWeight? CurrentWeight = null;
    public Glyph? CurrentGlyph = null;
    TextInput textInput = null!;
    public RawInput R = new();
    EditorFocus currentFocus = EditorFocus.ProjectPicker;
    public EditorFocus CurrentFocus
    {
        get { return currentFocus;}
        set { 
            LastEditorFocus = currentFocus; 
            currentFocus = value;
        }
    }
    EditorFocus LastEditorFocus = EditorFocus.ProjectPicker;
    string? oldNum = null;
    string? oldLetter = null;
    string? oldRaw = null;
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };
    public static readonly JsonSerializerOptions JsonOptsRef = new()
    {
        IncludeFields = true,
        ReferenceHandler = ReferenceHandler.Preserve, // avoid crashes on back-references
        WriteIndented = true
    };

    public override void _Ready()
    {
        weightP = (WeightPicker)FindChild("WeightPicker");
        textInput = (TextInput)FindChild("TextInput");
        FpsTimer = new();
        AddChild(FpsTimer);
        FpsTimer.WaitTime = ThrottleWaitTime;
        FpsTimer.Timeout += () => {Throttling = false;};
        FpsTimer.OneShot = true;
        FpsTimer.Start();

        AddChild(MovementTimer);
        MovementTimer.WaitTime = 0.01f;
        MovementTimer.OneShot = true;
        MovementTimer.Timeout += MovementTimerTimeout;

        AddChild(Manager);
        GetTree().Paused = true;
        Workbench = (Base)FindChild("Workbench");
        LetterMenu = (LetterMenu)FindChild("LetterMenu");
        ProjectPicker = (ProjectPicker)FindChild("ProjectPicker");
        UfoFilePicker = (FileDialog)FindChild("UfoFilePicker");
        Workbench.Visible = false;
        // LetterMenu.Visible = true;
        Workbench.ProcessMode = ProcessModeEnum.Pausable;
        LetterMenu.ProcessMode = ProcessModeEnum.WhenPaused;
    }
    
    public override void _Process(double delta)
    {
        if (TextInput.BeingEdited) return;
        if (Input.IsActionJustPressed(Snl.debug))
            DebugSwitch = !DebugSwitch;

        if (Input.IsKeyPressed(Key.Backspace))
        {
            QuitEditor();
        }
        // if (Throttling)
        //     Engine.MaxFps = 45;
        // else if (Engine.MaxFps == 45)
        //     Engine.MaxFps = 0;

        if (Input.IsAnythingPressed())
        {
            Throttling = false;
            FpsTimer.WaitTime = ThrottleWaitTime;
            if (FpsTimer.IsStopped())
                FpsTimer.Start();
        }
        if (Input.IsActionJustPressed(Snl.escape))
        {
            if (CurrentFocus == EditorFocus.Workbench)
            {
                SwitchToLettermenu();
            }
            else if (CurrentFocus == EditorFocus.Letters)
            {
                SwitchToProjectPicker();
            }
            else if (CurrentFocus == EditorFocus.ProjectPicker)
            {
                QuitEditor();
            }
        }

        if (Input.IsActionJustPressed(Snl.export_ufo))
        {
            ExportUfo();
        }
        
        if (Input.IsActionJustPressed(Snl.f1))
        {
            if (Engine.MaxFps == 0)
                Engine.MaxFps = 30;
            else
                Engine.MaxFps = 0;
        }

        R.NumberJustPressed = R.NumberPressed is null || R.NumberPressed == oldNum ? null : R.NumberPressed;
        R.LetterJustPressed = R.LetterPressed is null || R.LetterPressed == oldLetter ? null : R.LetterPressed;
        R.RawJustPressed = R.Raw is null || R.Raw == oldRaw ? null : R.Raw;
        oldNum = R.NumberPressed;
        oldLetter = R.LetterPressed;
        oldRaw = R.Raw;
    }

    public override void _Input(InputEvent ev)
    {
        R.Raw = ev.AsText();
        R.NumberPressed = null;
        R.LetterPressed = null;
        if (TextInput.BeingEdited) return;
        if (ev is InputEventKey && ev.IsPressed())
        {
            if ("0123456789".Contains(R.Raw))
            {
                R.NumberPressed = R.Raw;
            }
            else if (R.Raw.Length == 1)
            {
                R.LetterPressed = R.Raw.ToLower();
            }
            else if (R.Raw.Length == 7 && R.Raw.StartsWith("Shift"))
            {
                R.LetterPressed = R.Raw.Substr(6, 1);
            }
        }
    }

    async void ExportUfo()
    {
        UfoFilePicker.FileMode = FileDialog.FileModeEnum.SaveFile;
        UfoFilePicker.Visible = true;
        string file = (string)(await ToSignal(UfoFilePicker, FileDialog.SignalName.FileSelected))[0];
        UfoWriterReader.ExportUfo(file, CurrentFamily, this);
    }

    public void _OnButtonDown()
    {
        GetWindow().AlwaysOnTop = !GetWindow().AlwaysOnTop;
    }

    public void MovementTimerTimeout()
    {
        CanMoveAgain = true;
    }

    public V2 GetMovementInput(float delta, float wait = 0.01f, bool OnGuide = false, float guideWait = 0.2f)
    {
        if (OnGuide && StickyGuide)
        {
            MovementTimer.WaitTime = guideWait;
            MovementTimer.Start();
            CanMoveAgain = false;
            StickyGuide = false;
        }
        else
        {
            MovementTimer.WaitTime = wait;
        }
        // int movementAmount = (int)(GridSize * GridModifier);
        V2 normalizedMovement = V2.Zero;
        if (MovementTimer.IsStopped())
            MovementTimer.Start();
        

        if (Input.IsActionJustPressed(Snl.left))
            normalizedMovement += new V2(-1,0);
        
        if (Input.IsActionJustPressed(Snl.right))
            normalizedMovement += new V2(1,0);

        if (Input.IsActionJustPressed(Snl.up))
            normalizedMovement += new V2(0,-1);

        if (Input.IsActionJustPressed(Snl.down))
            normalizedMovement += new V2(0,1);
        

        bool pressingMovementKey = false;
        if (Input.IsActionPressed(Snl.left))
        {
            pressingMovementKey = true;
            if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                normalizedMovement += new V2(-1,0);
            else
                MovementHeldTime += delta;
        }
        
        if (Input.IsActionPressed(Snl.right))
        {
            pressingMovementKey = true;
            if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                normalizedMovement += new V2(1,0);
            else
                MovementHeldTime += delta;
        }

        if (Input.IsActionPressed(Snl.up))
        {
            pressingMovementKey = true;
            if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                normalizedMovement += new V2(0,-1);
            else
                MovementHeldTime += delta;
        }

        if (Input.IsActionPressed(Snl.down))
        {
            pressingMovementKey = true;
            if (MovementHeldTime > ValidHoldTime && CanMoveAgain)
                normalizedMovement += new V2(0,1);
            else
                MovementHeldTime += delta;
        }

        if (!pressingMovementKey)
        {
            MovementHeldTime = 0;
        }
        
        if (normalizedMovement != V2.Zero)
        {
            CanMoveAgain = false;
            if (!OnGuide)
            {
                StickyGuide = true;
            }
        } 

    return normalizedMovement;
    }

    public void SwitchToProjectPicker()
    {
        CurrentFocus = EditorFocus.ProjectPicker;
        ProjectPicker.Visible = true;
        LetterMenu.Visible = false;
        Workbench.Visible = false;
        ProjectPicker.ProcessMode = ProcessModeEnum.WhenPaused;
        LetterMenu.ProcessMode = ProcessModeEnum.Pausable;
        Workbench.ProcessMode = ProcessModeEnum.Pausable;
    }

    public void SwitchToLettermenu()
    {
        if (CurrentFocus == EditorFocus.Workbench)
        {
            CurrentGlyph.Shapes = Workbench.Shapes;
            Texture2D t = Workbench.PreviewTex;
            LetterMenu.CurrentlySelected.SetPreviewTexture(t);
            SaveGlyph(CurrentGlyph);
        }
        foreach (string glyphs in LetterMenu.allGlyphs)
        {
            foreach (char glyph in glyphs)
            {
                // create dictionary with chars : glyphplaceholder
                // 
                string filePath = CurrentProject.Path + Path.DirectorySeparatorChar + CurrentWeight.Name() + Path.DirectorySeparatorChar + GlyphNames.Names[glyph] + ".ginfo";
                if (File.Exists(filePath))
                {
                    Glyph g = LoadGlyph(filePath);
                    LetterMenu.ShapeDict[glyph] = g;
                    Texture2D t = Workbench.CreatePreviewTex(g.Shapes.S);
                    LetterMenu.PreviewDict[glyph].SetPreviewTexture(t);
                }
            }
        }
        CurrentFocus = EditorFocus.Letters;
        ProjectPicker.ProcessMode = ProcessModeEnum.Pausable;
        LetterMenu.ProcessMode = ProcessModeEnum.WhenPaused;
        Workbench.ProcessMode = ProcessModeEnum.Pausable;
        ProjectPicker.Visible = false;
        LetterMenu.Visible = true;
        Workbench.Visible = false;
    }

    public void SwitchToWorkbench(Shapes shapes)
    {
        CurrentFocus = EditorFocus.Workbench;
        ProjectPicker.ProcessMode = ProcessModeEnum.Pausable;
        LetterMenu.ProcessMode = ProcessModeEnum.Pausable;
        Workbench.ProcessMode = ProcessModeEnum.WhenPaused;
        // (Workbench.Shapes, Workbench.UndoRedo) = (CurrentGlyph.Contours, CurrentGlyph.Undos);
        Workbench.Initialize(shapes);
        GD.Print("testing");
        Fun.Delayed(this, 0.1f, () =>
        {
            Workbench.Visible = true;
            LetterMenu.Visible = false;
            ProjectPicker.Visible = false;
        });
    }

    public void ResetEditorFocus()
    {
        CurrentFocus = LastEditorFocus;
    }

    public void SaveEverything()
    {
        foreach (ProjectTemplate p in ProjectPicker.RecentProjects)
        {
            p.Project().UpdateInfo();
        }
    }

    public void QuitEditor()
    {
        SaveEverything();
        ((TextureRect)FindChild("Peace")).Visible = true;
        GetTree().Quit();
    }

    public void SaveGlyph(Glyph g)
    {
        string filePath = CurrentProject.Path + Path.DirectorySeparatorChar + CurrentWeight.Name() + Path.DirectorySeparatorChar + GlyphNames.Names[g.G] + ".ginfo";
        var fileAc = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
        fileAc.StoreString(JsonSerializer.Serialize(g, JsonOptsRef));
        fileAc.Close();
        fileAc.Dispose();
    }

    public Glyph LoadGlyph(string filePath)
    {
        var fileAc = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read);
        Glyph? g = JsonSerializer.Deserialize<Glyph>(fileAc.GetAsText(),JsonOptsRef);
        fileAc.Close();
        fileAc.Dispose();
        if (g != null)
            return g;
        else
            throw new Exception($"\nFailed to deserialize glyph file at {filePath}\n");
    }
}
