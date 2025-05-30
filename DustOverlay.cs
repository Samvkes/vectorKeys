using Godot;
using System;

public partial class DustOverlay : Sprite2D
{
	Vector2 startPos;
	double counter = 0;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		startPos = Position;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		counter += delta; 
		if (counter > .4f)
		{
			Position = new Vector2(startPos.X + GD.Randf()*30, startPos.Y + GD.Randf()*20);
			counter = 0;
		}
	}
}
