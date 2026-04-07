using Godot;
using System;
using Vectordrawing;
using GV2 = Godot.Vector2;
using V2 = System.Numerics.Vector2;

record Children(
    VBoxContainer VBox
);

public partial class SidePanel : Control
{
    Label layerSelector = null!;
    WorkbenchUi workbenchUi = null!;
    UIConfig config = null!;
    Children c = null!;

    public override void _Ready()
    {
        layerSelector = (Label)FindChild("LayerSelector");
        workbenchUi = GetParent<WorkbenchUi>();
        config = workbenchUi.Config;
        c = new(
            (VBoxContainer)FindChild("VBoxContainer_Layers")
        );
    }

    public override void _Process(double delta)
    {
    }

    public void SwitchLayersIndicator(int currentShapeIndex, int currentShapeAnchorsCount)
    {
        CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quint)
            .TweenProperty(layerSelector, "position", new GV2(-100, -3 + currentShapeIndex * 98), .2f);
        CreateTween().TweenProperty(layerSelector, "scale", new GV2(1, 1), .2).From(new GV2(.7f, 1.3f));
        layerSelector.Text = currentShapeAnchorsCount.ToString("D2") + "\n24";
    }

    public void DrawLayers(float delta, Shape currentShape, Shapes shapes)
    {
        Image tempImage = new();
        int shapeCounter = 0;
        V2 size = new(90, 90);
        float f = size.Y / config.WindowSize.Y;

        foreach (TextureRect nde in c.VBox.GetChildren())
        {
            string currentString = (
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size.X}\" height=\"{size.Y}\" >" +
                $"<g transform=\"scale({f}) translate(0,0) rotate(0)\">" +
                $"<g transform=\"scale(1) translate(0,0) rotate(0)\">");

            if (shapeCounter < shapes.S.Count)
            {
                Shape shape = shapes.S[shapeCounter];
                if (shape.Anchors.Count < 3)
                {
                    continue;
                }
                if (shape == currentShape)
                {
                    float l = 0;
                    currentString += (
                        $"<path d=\"M {l} {0} L {config.WindowSize.X - l} {0}\"" +
                        $"stroke =\"{"black"}\" stroke-opacity=\"{0.4f}\" stroke-width=\"{30}\"/>"
                    );
                    currentString += (
                        $"<path d=\"M {l} {config.WindowSize.Y} L {config.WindowSize.X - l} {config.WindowSize.Y}\"" +
                        $"stroke =\"{"black"}\" stroke-opacity=\"{0.4f}\" stroke-width=\"{30}\"/>"
                    );
                }
                Segment[] s = shape.SegList();
                float[] startSeg = s[0].Flat();
                currentString += $"<path d=\"M {startSeg[0]} {startSeg[1]} C ";
                int innerCounter = 0;
                foreach (Segment seg in s)
                {
                    float[] flatSeg = seg.Flat();
                    currentString += $"{flatSeg[2]} {flatSeg[3]}, {flatSeg[4]} {flatSeg[5]}, {flatSeg[6]} {flatSeg[7]}";
                    if (innerCounter != s.Length - 1)
                    {
                        currentString += ",";
                    }
                    currentString += " ";
                    innerCounter += 1;
                }
                currentString += $"Z\" ";
                if (shape.Negative)
                    currentString += " fill =\"red\" stroke =\"red\" fill-opacity=\"0.2\" stroke-opacity=\"1.0\" stroke-width=\"30\"/>";
                else
                    currentString += " fill =\"gray\" stroke =\"black\" fill-opacity=\"0.0\" stroke-opacity=\"1.0\" stroke-width=\"30\"/>";
                currentString += shapes.S[shapeCounter];
            }
            currentString += (
                "</g></g></svg>"
            );
            tempImage.LoadSvgFromString(currentString);
            nde.Texture = ImageTexture.CreateFromImage(tempImage);
            nde.StretchMode = TextureRect.StretchModeEnum.KeepCentered;

            shapeCounter += 1;
        }
    }
}
