using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

namespace WordTools;

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
}
