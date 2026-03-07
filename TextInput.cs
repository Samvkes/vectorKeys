using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Vectordrawing;

public partial class TextInput : MarginContainer
{
    public LineEdit Le = null!;
    public static bool BeingEdited = false;
    public string SubmittedText = "";

    public override void _Ready()
    {
        Le = (LineEdit)FindChild("LineEdit");
        PivotOffsetRatio = new Godot.Vector2(.5f,1.0f);
    }

    public override void _Process(double delta)
    {
    }

    public void Edit()
    {
        Le.Edit();
    }

    public static TextInput AddToScene(Node nde, Godot.Vector2 pos)
    {
        PackedScene tiScene = GD.Load<PackedScene>("text_input.tscn");
        TextInput ti = tiScene.Instantiate<TextInput>();
        nde.AddChild(ti);
        ti.Edit();
        ti.Position = pos;
        return ti;
    }

    public void _OnEditingToggled(bool on)
    {
        if (BeingEdited)
        {
            Manager.PlaySound("Rattle3.wav",.4f, 1.95f, 2.0f);
            SubmittedText = Le.Text;
            GD.Print(SubmittedText);
            Tween t = CreateTween();
            t.SetTrans(Tween.TransitionType.Quint).TweenProperty(this, "scale", new Godot.Vector2(0,0), 0.3);
        }
        else
        {
            CreateTween().SetTrans(Tween.TransitionType.Expo).TweenProperty(this, "scale", new Godot.Vector2(1,1), .3).From(new Godot.Vector2(0,0));
            SubmittedText = "";
        }
        BeingEdited = !BeingEdited;
    }
}
