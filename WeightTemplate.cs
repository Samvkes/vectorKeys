using Godot;
using System;
using Vectordrawing;

public partial class WeightTemplate : HBoxContainer
{
    Label WeightName = null!;
    Label LastEditDate = null!;
    Label GlyphsCount = null!;
    ProjectWeight weight = null!;

    public override void _Ready()
    {
        WeightName = (Label)FindChild("Name");
        LastEditDate = (Label)FindChild("LastEditDate");
        GlyphsCount = (Label)FindChild("GlyphsCount");
    }

    public override void _Process(double delta)
    {
    }

    public void SetWeight(ProjectWeight w)
    {
        weight = w;
        UpdateInfo();
    }

    public ProjectWeight ProjectWeight()
    {
        return weight; 
    }

    public void UpdateInfo()
    {
        DateTime led = weight.LastEdit;
        string name = weight.Name();
        string gc = weight.GlyphCount.ToString();
        LastEditDate.Text = $"{led.Day} / {led.Month} / {led.Year - 2000}";
        if (name != "")
            WeightName.Text = name;
        if (gc != "")
            GlyphsCount.Text = gc;
    }
}
