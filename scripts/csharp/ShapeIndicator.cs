using Godot;
using System;

public partial class ShapeIndicator : Node2D
{
    TextureRect Tex = null!;
    RichTextLabel Label = null!;

    public override void _Ready()
    {
        Tex = GetChild<TextureRect>(0);
        Label = GetChild<RichTextLabel>(1);
    }

    public void SetAngle(float ang)
    {
        Tex.Rotation = ang - (MathF.PI / 2);
    }

    public void SetNumber(int num)
    {
        Label.Text = num.ToString();
    }

}
