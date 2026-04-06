using Godot;
using System;

public partial class pane : Panel
{
    public override void _Ready()
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        for(int i = 0; i < 200; i++)
        {
            for(int j = 0; j < 200; j++)
            {
                DrawCircle(new(i * 20,j * 20), 3, new Color(0f, 0f, 0f, 0.1f));
            }
        }
    }
}
