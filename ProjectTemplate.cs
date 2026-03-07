using Godot;
using System;
using Vectordrawing;

public partial class ProjectTemplate : GridContainer
{
    Label FontName = null!;
    Label LastEditDate = null!;
    Label WeightsAxesCount = null!;
    Label GlyphsCount = null!;
    Project project = null!;

    public override void _Ready()
    {
        FontName = (Label)FindChild("Name");
        LastEditDate = (Label)FindChild("LastEditDate");
        WeightsAxesCount = (Label)FindChild("WeightsAxesCount");
        GlyphsCount = (Label)FindChild("GlyphsCount");
    }

    public override void _Process(double delta)
    {
    }

    public void SetProject(Project p)
    {
        project = p;
        UpdateInfo();
    }

    public Project Project()
    {
        return project; 
    }

    public void UpdateInfo()
    {
        DateTime led = project.LastEdit;
        string name = project.Name;
        string wac = project.Weights.Count.ToString();
        string gc = project.GlyphCount.ToString();
        LastEditDate.Text = $"{led.Day} / {led.Month} / {led.Year - 2000}";
        if (name != "")
            FontName.Text = name;
        if (wac != "")
            WeightsAxesCount.Text = wac;
        if (gc != "")
            GlyphsCount.Text = gc;
    }
}
