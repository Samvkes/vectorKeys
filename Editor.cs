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

public enum EditorFocus
{
    Letters,
    Workbench,
    WeightPicker,
}

public partial class Editor : CanvasLayer
{
    Control DrawingBase = null!; 
    LetterMenu LetterMenu = null!; 
    public Manager Manager = GD.Load<PackedScene>("res://manager.tscn").Instantiate<Manager>();
    public FileDialog UfoFilePicker = null!;
    public FamilyConfig CurrentFamily = new();
    Timer FpsTimer = null!;
    float ThrottleWaitTime = 1f;
    public bool Throttling = false;
    public Timer MovementTimer = new();
    bool CanMoveAgain = true;
    float MovementHeldTime = 0;
    float ValidHoldTime = .2f;
    bool StickyGuide = true;
    ShaderMaterial blur1 = null!;
    ShaderMaterial blur2 = null!;
    CanvasLayer blurLayer1 = null!;
    CanvasLayer blurLayer2 = null!;
    CanvasLayer weightP = null!;
    Tween? theTween = null;
    public EditorFocus CurrentFocus = EditorFocus.Letters;

    public override void _Ready()
    {
        weightP = (CanvasLayer)FindChild("WeightP");
        blur1 = (ShaderMaterial)((ColorRect)FindChild("firstBlurShader")).Material;
        blur2 = (ShaderMaterial)((ColorRect)FindChild("secondBlurShader")).Material;
        blurLayer1 = (CanvasLayer)FindChild("firstBlur");
        blurLayer2 = (CanvasLayer)FindChild("secondBlur");
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
        DrawingBase = (Control)FindChild("DrawingBase");
        LetterMenu = (LetterMenu)FindChild("LetterMenu");
        UfoFilePicker = (FileDialog)FindChild("UfoFilePicker");
        DrawingBase.Visible = false;
        LetterMenu.Visible = true;
        DrawingBase.ProcessMode = ProcessModeEnum.Pausable;
        LetterMenu.ProcessMode = ProcessModeEnum.WhenPaused;
    }

    public override void _Process(double delta)
    {
        if (CurrentFocus == EditorFocus.Letters && Input.IsActionJustPressed(Snl.select_mode))
        {
            // weightP.Visible = true;
            GetTree().Paused = true;
            CurrentFocus = EditorFocus.WeightPicker;
            blurLayer1.Visible = true;
            blurLayer2.Visible = true;
            theTween?.Kill();
            theTween = CreateTween();
            theTween.TweenMethod(Callable.From((int s) =>
            {
                blur1.SetShaderParameter("blurSize", s);
            }), 0, 15, .1f);
            theTween.Parallel().TweenMethod(Callable.From((int s) =>
            {
                blur2.SetShaderParameter("blurSize", s);
            }), 0, 15, .1f);
            theTween.TweenCallback(Callable.From(()=>{weightP.Visible = true;}));
            theTween.TweenCallback(Callable.From(()=>{GetTree().Paused = true;}));

        }
        if (CurrentFocus == EditorFocus.WeightPicker && Input.IsActionJustReleased(Snl.select_mode))
        {
            CurrentFocus = EditorFocus.Letters;
            theTween?.Kill();
            blurLayer1.Visible = false;
            blurLayer2.Visible = false;
            // blur1.SetShaderParameter("blurSize", 0);
            // blur2.SetShaderParameter("blurSize", 0);
            weightP.Visible = false;
        }
        if (Input.IsKeyPressed(Key.Backspace))
        {
			((TextureRect)FindChild("Peace")).Visible = true;
            GetTree().Quit();
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

        if (CurrentFocus == EditorFocus.Workbench && Input.IsActionJustPressed(Snl.escape))
        {
            CurrentFocus = EditorFocus.Letters;
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
        
        if (Input.IsActionJustPressed(Snl.f1))
        {
            if (Engine.MaxFps == 0)
                Engine.MaxFps = 30;
            else
                Engine.MaxFps = 0;
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
        Base b = (Base)DrawingBase;
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

    public void _OnButtonDown()
    {
        GetWindow().AlwaysOnTop = !GetWindow().AlwaysOnTop;
    }

    public void MovementTimerTimeout()
    {
        CanMoveAgain = true;
    }

    public V2 GetMovementInput(float delta, float wait = 0.01f, bool OnGuide = true, float guideWait = 0.2f)
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
}
