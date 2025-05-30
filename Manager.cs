using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using WordTools;

public partial class Manager : Node
{
	FastNoiseLite fnl = new();
	Camera2D cam = null!;
	Tween shakeTween = null!;
	bool cameraShouldShake = false;
	const int PLAYER_AMOUNT = 16;
	AudioStreamPlayer[] streams = new AudioStreamPlayer[PLAYER_AMOUNT];
	Dictionary<string, AudioStream> soundFiles = new Dictionary<string, AudioStream>();	
	int counter = 0;
	float noiseI = 0.0f;
	float shakeStrength = 0.0f;

	float slowI = 0.0f;

	public override void _Ready()
	{
		// shakeTween = CreateTween();
		// cam = GetTree().Root.GetNode<Game>("Game").GetNode<Camera2D>("Camera");
		List<string> files = DirContents("res://assets/sound_effects");
		foreach (string file in files)
		{
			if (file.EndsWith("wav"))
			{
				soundFiles.Add(file, GD.Load<AudioStream>("res://assets/sound_effects/"+file));
				GD.Print("loaded: " + file);
			}
		}
		for (int i=0; i < PLAYER_AMOUNT; i++)
		{
			AudioStreamPlayer asp = new AudioStreamPlayer();
			streams[i] = asp;	
			AddChild(asp);
		}
	}

	// public void ScreenShake()
	// {
	// 	shakeStrength = 0.0f;
	// 	shakeTween?.Kill();
	// 	shakeTween = CreateTween();
	// 	shakeTween.TweenProperty(this, "shakeStrength", 8, .05);
	// 	this.Delayed(0.10f, () => {
	// 		shakeTween?.Kill(); 
	// 		shakeTween = CreateTween(); 
	// 		shakeTween.TweenProperty(this, "shakeStrength", 0, .08);});
	// }

	// public Vector2 GetNoiseOffset(float delta)
	// {
	// 	noiseI += delta * 150;
	// 	return new Vector2(
	// 		fnl.GetNoise2D(1,noiseI) * 1.0f * shakeStrength,
	// 		fnl.GetNoise2D(100,noiseI)*1.0f * shakeStrength);
	// }

	public override void _Process(double delta)
	{
		// cam.Offset = GetNoiseOffset((float)delta);
	}

	public void Test()
	{
		GD.Print("ditwerkt!!");
	}

	public void PlaySound(string audioName, float volume=1.0f, float minPitch = 1.0f, float maxPitch = 1.0f)
	{
		GD.Print(audioName);
		if (!soundFiles.ContainsKey(audioName))
		{
			GD.Print(audioName);
			GD.PushError("\nAudio File not found!!\n");
		}
		else
		{
			float pitch = (float)GD.RandRange(minPitch, maxPitch);
			streams[counter].Stream = soundFiles[audioName];
			streams[counter].PitchScale = pitch;
			streams[counter].VolumeDb = Mathf.LinearToDb(volume);
			streams[counter].Play();
		}
		if (counter >= PLAYER_AMOUNT - 1)
		{
			counter = 0;
		}
		else
		{
			counter += 1;
		}
	}

	public List<string> DirContents(string path)
	{
		List<string> files = new List<string>();
		using var dir = DirAccess.Open(path);
		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();
			while (fileName != "")
			{
				if (dir.CurrentIsDir())
				{
					GD.Print($"Found directory: {fileName}");
					files.Add(fileName);
				}
				else
				{
					GD.Print($"Found file: {fileName}");
					files.Add(fileName);
				}
				fileName = dir.GetNext();
			}
			return files;
		}
		else
		{
			GD.Print("An error occurred when trying to access the path.");
			return files;
		}
	}
}
