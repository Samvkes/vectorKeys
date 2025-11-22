using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using V2 = System.Numerics.Vector2;
using GV2 = Godot.Vector2;
using System.Diagnostics;

namespace Vectordrawing;

static class C
{
    public const string Alfabet = "abcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghilmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
}

static class Fun
{
    public static async void Delayed(this Node nde, float seconds, Action fun)
    {
        await nde.ToSignal(nde.CreateTween().TweenInterval(seconds), Tween.SignalName.Finished);
        fun();
    }
    public static async void Repeatedly(this Node nde, float seconds, Action fun)
    {
        await nde.ToSignal(nde.CreateTween().TweenInterval(seconds), Tween.SignalName.Finished);
        fun();
        Repeatedly(nde, seconds, fun);
    }
    public static async void DelayOneFrame(this Node nde, Action fun)
    {
        await nde.ToSignal(nde.GetTree(), SceneTree.SignalName.ProcessFrame);
        fun();
    }

    public static T Choose<T>(params T[] a)
    {
        int choice = GD.RandRange(0, a.Length - 1);
        return a[choice];
    }

    public static T Choose<T>(List<T> a)
    {
        int choice = GD.RandRange(0, a.Count - 1);
        return a[choice];
    }

    public static void PrintCaller()
    {
        GD.Print(new StackFrame(1, true).GetMethod().Name);
    }

    public static V2 ProjectPointOnLine(V2 toProject, V2 lineStart, V2 lineEnd)
    {
        V2 diff = lineEnd - lineStart;
        V2 A = toProject - lineStart;
        float t = V2.Dot(A, diff) / (diff.Length() * diff.Length());
        return lineStart + (t * diff);
    }

    public static V2 RandomVector(float xVariation, float yVariation)
    {
        return new((float)GD.RandRange(-1 * xVariation, xVariation), (float)GD.RandRange(-1 * yVariation, yVariation));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static V2 Vtv(GV2 v1)
    {
        V2 v2 = new V2(v1.X, v1.Y);
        return v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GV2 Vtv(V2 v1)
    {
        GV2 v2 = new GV2(v1.X, v1.Y);
        return v2;
    }
}

static class StringNamesList
{
    public static readonly StringName left = "ui_left";
    public static readonly StringName right = "ui_right";
    public static readonly StringName up = "ui_up";
    public static readonly StringName down = "ui_down";
    public static readonly StringName increase_zoom = "increase_zoom";
    public static readonly StringName decrease_zoom = "decrease_zoom";
    public static readonly StringName snap_selected = "snap_selected";
    public static readonly StringName increase_grid_modifier = "increase_grid_modifier";
    public static readonly StringName decrease_grid_modifier = "decrease_grid_modifier";
    public static readonly StringName switch_segment_style = "switch_segment_style";
    public static readonly StringName switch_point_style = "switch_point_style";
    public static readonly StringName switch_focus = "switch_focus";
    public static readonly StringName insert_point = "insert_point";
    public static readonly StringName add_new_point = "add_new_point";
    public static readonly StringName finish_shape = "finish_shape";
    public static readonly StringName shape_negative = "shape_negative";
    public static readonly StringName undo = "undo";
    public static readonly StringName redo = "redo";
    public static readonly StringName xscale_up_points = "xscale_up_points";
    public static readonly StringName xscale_down_points = "xscale_down_points";
    public static readonly StringName yscale_up_points = "yscale_up_points";
    public static readonly StringName yscale_down_points = "yscale_down_points";
    public static readonly StringName rotate_cw_points = "rotate_cw_points";
    public static readonly StringName rotate_ccw_points = "rotate_ccw_points";
    public static readonly StringName move_focus = "move_focus";
    public static readonly StringName curve_points_mode = "curve_points_mode";
    public static readonly StringName auto_move_mode = "auto_move_mode";
    public static readonly StringName save_project = "save_project";
    public static readonly StringName load_project = "load_project";
}
