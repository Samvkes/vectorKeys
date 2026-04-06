using Godot;
using System;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;

public partial class PointCanvas : ColorRect
{
    bool drawn = false;
    Texture2D t = GD.Load<Texture2D>("res://test.png");
    public override void _Ready()
    {
        // QueueRedraw();
    }

    // public override void _Draw()
    // {
    //     // if (!drawn)
    //     {
    //         for (int i = 0; i < 150; i++)
    //         {
    //             for (int j = 0; j < 150; j++)
    //             {
    //                 // DrawTexture(t, new(20 + i * 80, j * 80), Color.Color8(0,0,0,255));
    //                 DrawCircle(new(20 + i * 64, j * 64), 2, Color.Color8(0,0,0,90));
    //             }
    //         }
    //         // drawn = true;
    //     }
    // }
}
