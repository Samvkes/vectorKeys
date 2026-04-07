using Godot;
using System;
using System.Collections;
using System.Text.Json;
using System.Collections.Generic;
using Vectordrawing;
using System.Numerics;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using System.Linq;
using System.Diagnostics;

namespace Vectordrawing;

// TODO rewrite stack adding
public partial class WorkbenchUndoRedo : Node
{
    Shapes shapes = null!;
    const int MAX_STACK_SIZE = 100;
    readonly List<string> UndoStack = [];
    readonly List<string> RedoStack = [];
    static private string CurrentState = "";
    Timer UndoTimer = new();
    bool CanUndoAgain = true;

    public override void _Ready()
    {
        AddChild(UndoTimer);
        UndoTimer.Timeout += ()=>{CanUndoAgain = true;};
    }

    public void Initialize(Shapes s)
    {
        shapes = s;
    }

    public void Undo()
    {
        if (UndoStack.Count <= 0)
        {
            GD.Print("UndoStack is empty");
            return;
        }
        RedoStack.Add(shapes.SaveState());
        if (RedoStack.Count > MAX_STACK_SIZE) RedoStack.RemoveAt(0);
        shapes.LoadState(UndoStack.Last());
        UndoStack.RemoveAt(UndoStack.Count - 1);
    }

    public void Redo()
    {
        if (RedoStack.Count <= 0)
        {
            GD.Print("Redostack is empty");
            return;
        }
        CurrentShapesToUndoStack();
        shapes.LoadState(RedoStack.Last());
        RedoStack.RemoveAt(RedoStack.Count - 1);
    }

    public void ClearRedoStack()
    {
        RedoStack.Clear();
    }

    public void CurrentShapesToUndoStack()
    {
        AddToUndoStack(shapes.SaveState());
    }

    public void AddToUndoStack(string serialized)
    {
        UndoStack.Add(serialized);
        if (UndoStack.Count > MAX_STACK_SIZE)
        {
            UndoStack.RemoveAt(0);
        }
    }

    public void UndoCheckpoint()
    {
        if (CanUndoAgain)
        {
            CurrentShapesToUndoStack();
            ClearRedoStack();
            CanUndoAgain = false;
        }
        if (UndoTimer.IsStopped())
        {
            UndoTimer.Start();
        }
    }
}