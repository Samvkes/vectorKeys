using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Vectordrawing;
using GV2 = Godot.Vector2;
using V2 = System.Numerics.Vector2;
using Snl = Vectordrawing.StringNamesList;
using System.Reflection.Metadata;
using Godot.NativeInterop;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Diagnostics;

namespace Vectordrawing;
/* 
max 1000 weights!
moving, renaming, copying, changing, opening -> check if project is well-formed -> show well-formed weights
project -> 
[name.seraf
    weights -> 
    [100, 200, 300 - italic, 400 - strong italic - wide, etc.
        letters (serafg) -> layers, anchors, undo history
        settings (weight.sertings) -> kerning, y-axis degree, undo history
    ]
    compiled fonts
    settings (project.sertings) -> axis, weights, history, edit date, counts, reference <- update when opening file
]

project file creation -> weight folder creation (do once first glyph is opened) -> correct glyph amount
saving glyphs -> other things
*/
public class ProjectGlyph {
    string name = "";
    DateTime lastEdit = DateTime.Now;
    int minutesSpent = 0;
    int anchorCount = 0;
}

public class Project {
    public string Path;
    public string Name;
    public int GlyphCount = 0;
    public DateTime LastEdit = DateTime.Now;
    public List<Axis> AvailableAxes = [
    ];
    public List<ProjectWeight> Weights = [];
    public List<Font> ReferenceFonts = [];
    public void UpdateInfo()
    {
        string pinfoFilePath = Path + System.IO.Path.DirectorySeparatorChar + "pinfo";
        StreamWriter fs = File.CreateText(pinfoFilePath);
        fs.Write(JsonSerializer.Serialize(this, ProjectPicker.JsonOpts));
        fs.Close();
    }
    public Project(string path, string name) {
        Path = path;
        Name = name;
        for (int i = 0; i < 9; i++)
        {
            AvailableAxes.Add(
                new(i, "")
            );
        }
    }
}

// has 1 weight 100 - 1000 and 0 or more axes. 
public class ProjectWeight (int weight, List<string> axes) {
    public List<ProjectGlyph> Glyphs = [];
    public int GlyphCount = 0;
    public int Weight = weight;
    public List<string> Axes = axes;
    public DateTime LastEdit = DateTime.Now.Date;
    public V2 StemAngle = new(0, .5f * MathF.PI);
    public string Name()
    {
        string n = Weight.ToString();
        if (Axes.Count > 0)
        {
            n += " - " + string.Join(" - ", Axes);
        }
        return n;
    }
    
}

public partial class ProjectPicker : Control
{
    Editor Ed = null!;
    Panel Selector = null!;
    GV2 goalPos = GV2.Zero;
    VBoxContainer ProjectContainer = null!;
    VBoxContainer WeightContainer = null!;
    FileDialog ProjectFilePicker = null!;
    PackedScene ProjectTemplateScene = GD.Load<PackedScene>("project_template.tscn");
    PackedScene WeightTemplateScene = GD.Load<PackedScene>("weight_template.tscn");
    public List<ProjectTemplate> RecentProjects = [];
    ProjectTemplate? SelectedTemplate = null;
    ProjectWeight? SelectedWeight = null;
    Godot.FileAccess ProjectPaths = null!;
    Label addProject = null!;
    Label addWeight = null!;
    List<WeightTemplate> weightTemplates = [];
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        IncludeFields = true,                        // serialize public fields
    };

    public override void _Ready()
    {
        // ClearProjectPaths();
        Ed = GetParent<Editor>();
        Selector = (Panel)FindChild("Selector");
        ProjectContainer = (VBoxContainer)FindChild("ProjectContainer");
        WeightContainer = (VBoxContainer)FindChild("WeightContainer");
        
        ProjectFilePicker = (FileDialog)FindChild("ProjectFilePicker");
        addProject = (Label)FindChild("AddProject");
        addWeight = (Label)FindChild("AddWeight");
        goalPos = Selector.Position;

        if (!Godot.FileAccess.FileExists("user://projectPaths.dat"))
        {
            using var fa = Godot.FileAccess.Open("user://projectPaths.dat", Godot.FileAccess.ModeFlags.Write);
            fa.Close();
        }
        ProjectPaths = Godot.FileAccess.Open("user://projectPaths.dat", Godot.FileAccess.ModeFlags.ReadWrite);
        string? p = ProjectPaths.GetLine();
        GD.Print(p);
        while (p != "" && p != null)
        {
            RecentProjects.Add(LoadProject(p));
            p = ProjectPaths.GetLine();
        }
    }

    public override void _Process(double delta)
    {
        if (Ed.CurrentFocus != EditorFocus.ProjectPicker || TextInput.BeingEdited) return;
        // Selecting in project list.
        ProjectTemplate? lastSelected = SelectedTemplate;
        if (Ed.CurrentProject == null)
        {
            if (RecentProjects.Count > 0)
            {
                V2 mi = Ed.GetMovementInput((float)delta);
                if (mi.X > 0 && SelectedTemplate != null)
                {
                    Ed.CurrentProject = SelectedTemplate.Project();
                }
                if (mi.Y < 0)
                    SelectedTemplate = SelectedTemplate == null ? RecentProjects[0] : 
                        RecentProjects.IndexOf(SelectedTemplate) == RecentProjects.Count-1 ? null 
                            : RecentProjects[RecentProjects.IndexOf(SelectedTemplate) + 1];
                if (mi.Y > 0)
                    SelectedTemplate = SelectedTemplate == null ? RecentProjects[^1] : 
                        RecentProjects.IndexOf(SelectedTemplate) == 0 ? null 
                            : RecentProjects[RecentProjects.IndexOf(SelectedTemplate) - 1];
            }
            if (Input.IsActionJustPressed(Snl.add_new_point) && SelectedTemplate == null)
            {
                CreateProject();
            }
            goalPos = SelectedTemplate == null ? addProject.GlobalPosition : SelectedTemplate.GlobalPosition;
            goalPos.X += 850;
        }
        else
        {
            Project p = SelectedTemplate!.Project();
            List<ProjectWeight> weights = p.Weights;
            V2 mi = Ed.GetMovementInput((float)delta);
            if (mi.X > 0)
            {
                OpenWeight();
            }
            if (mi.X < 0)
            {
                Ed.CurrentProject = null;
                SelectedWeight = null;
            }
            if (mi.Y < 0)
            {
                SelectedWeight = SelectedWeight == null ? weights[0] : 
                    weights.IndexOf(SelectedWeight) == weights.Count-1 ? null 
                        : weights[weights.IndexOf(SelectedWeight) + 1];
            }
            if (mi.Y > 0)
            {
                SelectedWeight = SelectedWeight == null ? weights[^1] : 
                    weights.IndexOf(SelectedWeight) == 0 ? null 
                        : weights[weights.IndexOf(SelectedWeight) - 1];
            }
            if (Input.IsActionJustPressed(Snl.add_new_point) && SelectedWeight == null)
            {
                GD.Print("hm");
                CreateWeight();
            }
            goalPos = SelectedWeight == null ? addWeight.GlobalPosition : weightTemplates[weights.IndexOf(SelectedWeight)].GlobalPosition;
        }
        if (SelectedTemplate != null && SelectedTemplate != lastSelected)
            UpdateWeightTemplates(SelectedTemplate.Project());

        goalPos -= new GV2(30,15);
        Selector.Position += (goalPos - Selector.Position) * (1 - MathF.Exp( -(float)delta * 10));
    }

    public async void CreateProject()
    {
        ProjectFilePicker.Visible = true;
        string filePath = (string)(await ToSignal(ProjectFilePicker, FileDialog.SignalName.FileSelected))[0];
        File.Delete(filePath);
        Directory.CreateDirectory(filePath);
        string name = Path.GetFileNameWithoutExtension(filePath);
        Project project = new(filePath, name);
        string pinfoFilePath = filePath + Path.DirectorySeparatorChar + "pinfo";
        StreamWriter fs = File.CreateText(pinfoFilePath);
        fs.Write(JsonSerializer.Serialize(project, JsonOpts));
        fs.Close();
        RecentProjects.Add(AddProjectTemplate(project));
        ProjectPaths.StoreLine(filePath);
        ProjectPaths.SeekEnd();
    }

    ProjectTemplate LoadProject(string path)
    {
        string pinfoFilePath = path + Path.DirectorySeparatorChar + "pinfo";
        Project? project = JsonSerializer.Deserialize<Project>(File.OpenText(pinfoFilePath).ReadToEnd(), JsonOpts);
        if (project != null)
            return AddProjectTemplate(project);
        else
            throw new Exception($"\nFailed to deserialize project file at {pinfoFilePath}\n");
    }

    ProjectTemplate AddProjectTemplate(Project p)
    {
        ProjectTemplate template = (ProjectTemplate)ProjectTemplateScene.Instantiate();
        ProjectContainer.AddChild(template);
        ProjectContainer.MoveChild(template, 1);
        template.SetProject(p);
        return template;
    }

    void ClearProjectPaths()
    {
        using var fa = Godot.FileAccess.Open("user://projectPaths.dat", Godot.FileAccess.ModeFlags.Write);
        fa.Close();
    }

    void UpdateWeightTemplates(Project p)
    {
        int toUpdate = p.Weights.Count;
        if (toUpdate > weightTemplates.Count)
        {
            int count = weightTemplates.Count;
            for (int i = 0; i < toUpdate - count; i++)
            {
                WeightTemplate template = (WeightTemplate)WeightTemplateScene.Instantiate();
                WeightContainer.AddChild(template);
                WeightContainer.MoveChild(template, 1);
                weightTemplates.Add(template);
            }
        }
        int c = 0;
        foreach (WeightTemplate w in weightTemplates)
        {
            if (c >= toUpdate)
            {
                w.Visible = false;
                continue;
            }            
            w.Visible = true;
            w.SetWeight(p.Weights[c]);
            c += 1;
        }
    }

    void OpenWeight()
    {
    }

    public async void CreateWeight()
    {
        Debug.Assert(Ed.CurrentProject != null);
        Project p = Ed.CurrentProject;
        Ed.weightP.SwitchOn();
        string name = (string)(await ToSignal(Ed.weightP, WeightPicker.SignalName.PickedWeight))[0];
        string filePath = p.Path + Path.DirectorySeparatorChar + name;
        if (Directory.Exists(filePath))
            return;
        Directory.CreateDirectory(filePath);
        (int weight, List<string> axes) w = Ed.weightP.GetWeightAndAxes();
        GD.Print(w.axes);
        ProjectWeight pw = new(w.weight, w.axes);
        string winfoFilePath = filePath + Path.DirectorySeparatorChar + "winfo";
        StreamWriter fs = File.CreateText(winfoFilePath);
        fs.Write(JsonSerializer.Serialize(pw, JsonOpts));
        fs.Close();
        p.Weights.Add(pw);
        p.UpdateInfo();
        UpdateWeightTemplates(p);
    }
}
