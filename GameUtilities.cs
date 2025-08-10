using Godot;
using System;
using System.Collections.Generic;
using V2 = System.Numerics.Vector2;

namespace Vectordrawing;

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

    public static V2 ProjectPointOnLine(V2 toProject, V2 lineStart, V2 lineEnd)
    {
        V2 diff = lineEnd - lineStart;
        V2 A = toProject - lineStart;
        float t = V2.Dot(A, diff) / (diff.Length() * diff.Length());
        return lineStart + (t * diff);
    }

    public static V2 RandomVector(float xVariation, float yVariation)
    {
        return new((float)GD.RandRange(-1 * xVariation, xVariation), (float)GD.RandRange(-1* yVariation, yVariation));
    }
}
