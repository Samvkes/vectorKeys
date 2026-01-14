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

// TODO track more state (selection, currentshape)
public class UndoRedo
{
    Shapes Shapes = null!;
    const int MAX_STACK_SIZE = 100;
    readonly List<string> UndoStack = [];
    readonly List<string> RedoStack = [];
    static private string CurrentState = "";

    public UndoRedo(Shapes shapes)
    {
        Shapes = shapes;
    }

    public void Undo()
    {
        if (UndoStack.Count <= 0)
        {
            GD.Print("UndoStack is empty");
            return;
        }
        RedoStack.Add(Shapes.SaveState());
        if (RedoStack.Count > MAX_STACK_SIZE) RedoStack.RemoveAt(0);
        Shapes.LoadState(UndoStack.Last());
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
        Shapes.LoadState(RedoStack.Last());
        RedoStack.RemoveAt(RedoStack.Count - 1);
    }

    public void ClearRedoStack()
    {
        RedoStack.Clear();
    }

    public void CurrentShapesToUndoStack()
    {
        AddToUndoStack(Shapes.SaveState());
    }

    public void AddToUndoStack(string serialized)
    {
        UndoStack.Add(serialized);
        if (UndoStack.Count > MAX_STACK_SIZE)
        {
            UndoStack.RemoveAt(0);
        }
    }
}