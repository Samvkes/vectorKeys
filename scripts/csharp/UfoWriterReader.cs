using System;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using V2 = System.Numerics.Vector2;

namespace Vectordrawing;

enum UfoFiles
{
    glif,
    plist,
}

struct UfoGlyph
{
        public string Name;
        public int Unicode;
        public string Note;
        public int Lsb;
        public int Rsb;
        public float MinX;
        public float MaxX;
        public Segment[][] Contours;

    public UfoGlyph(string name, int unicode, Segment[][] contours, int lsb = 100, int rsb = 100, string note = "")
    {
        Name = name;
        Unicode = unicode;
        Note = note;
        Lsb = lsb;
        Rsb = rsb;
        Contours = [];
        foreach (Segment[] contour in contours)
        {
            Segment[] Contour = [];
            float f = UfoWriterReader.HeightFraction;
            foreach (Segment seg in contour)
            {
                Contour = [.. Contour, new(seg.InPoint/f, seg.InHandle/f, seg.OutHandle/f, seg.OutPoint/f)];
            }
            Contours = [.. Contours, Contour];
        }
        (MinX, MaxX) = CalculateExtrema();
    }

    (float minX, float maxX) CalculateExtrema()
    {
        float minX = 10000;
        float maxX = -10000;
        foreach (Segment[] segs in Contours)
        {
            foreach (Segment seg in segs)
            {
                float x = seg.InPoint.X;
                minX = x < minX ? x : minX;
                maxX = x > maxX ? x : maxX;
            }
        }
        return (minX, maxX);
    }
}

static class UfoWriterReader
{
    static string? CurrentPath;
    static XDocument? CurrentDocument;
    public static float HeightFraction = 1.29f; 

    public static void ExportUfo(string path, FamilyConfig family)
    {
        GD.Print($"Exporting to {path}.ufo. Isn't that nice");
        UfoGlyph[] glyphs = GetGlyphs();
        SetUpFiles(path, glyphs, family);
        foreach (UfoGlyph glyph in glyphs)
        {
            CreateGlyph(glyph);
        }
        GD.Print($"Finished succesfully!");
    }
    
    static void NewDoc(UfoFiles ftype, string? name = null, int format = 2)
    {
        string version = "1.0";
        CurrentDocument = new XDocument(new XDeclaration("1.0", "UTF-8", null));
        if (ftype == UfoFiles.glif)
            CurrentDocument.Add(new XElement("glyph"
                , new XAttribute("name", name!)
                , new XAttribute("format", format)
            ));
        if (ftype == UfoFiles.plist)
            CurrentDocument.Add(new XElement("plist", new XAttribute("version", version)));
    }

    static UfoGlyph[] GetGlyphs()
    {
        UfoGlyph[] glyphs = [];
        foreach ((char name, Glyph g) in LetterMenu.ShapeDict)
        {
            GD.Print(name);
            if (g.Shapes.S.Count > 0)
            {
                GD.Print("yeah");
                UfoGlyph glyph = new(name.ToString(), name, g.Shapes.GetMergedShapes());
                glyphs = [.. glyphs, glyph];
            }
        }
        return glyphs;
    }

    static void SetUpFiles(string path, UfoGlyph[] glyphs, FamilyConfig family)
    {
        path += ".ufo";
        if (Directory.Exists(path)) return;
        Directory.CreateDirectory(path);
        CurrentPath = path + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(CurrentPath + "glyphs");
        CreateMetainfo("sam", 3, 0); 
        CreateLayerContents();
        CreateFontInfo(family);
        CreateGlyphsContents(glyphs);
    }

    static string GlyphsPath()
    {
        return CurrentPath + "glyphs" + Path.DirectorySeparatorChar;
    }

    static void CreateMetainfo(string creator = "seraf", int formatVersion = 3, int formatVersionMinor = 0)
    {
        NewDoc(UfoFiles.plist);
        CurrentDocument!.Element("plist")!.Add(
            new XElement("dict"
                , new XElement("key", "creator")
                , new XElement("string", creator)
                , new XElement("key", "formatVersion")
                , new XElement("integer", formatVersion)
                , new XElement("key", "formatVersionMinor")
                , new XElement("integer", formatVersionMinor)
            )
        );
        SaveDoc("metainfo", UfoFiles.plist);
    }

    static void CreateLayerContents()
    {
        NewDoc(UfoFiles.plist);
        CurrentDocument!.Element("plist")!.Add(
            new XElement("array",
                new XElement("array"
                , new XElement("string", "public.default")
                , new XElement("string", "glyphs")
                )
            )
        );
        SaveDoc("layercontents", UfoFiles.plist);
    }

    static void CreateGlyphsContents(UfoGlyph[] glyphs)
    {
        NewDoc(UfoFiles.plist);
        CurrentDocument!.Element("plist")!.Add(new XElement("dict"));
        foreach (UfoGlyph glyph in glyphs)
        {
            CurrentDocument!.Element("plist")!.Element("dict")!.Add(
                new XElement("key", glyph.Name),
                new XElement("string", glyph.Name + ".glif")
            );
        }
        SaveDoc("contents", UfoFiles.plist, true);
    }

    static void CreateFontInfo(FamilyConfig family)
    {
        NewDoc(UfoFiles.plist);
        CurrentDocument!.Element("plist")!.Add(
            new XElement("dict"
                , new XElement("key", "familyName"), new XElement("string", family.Name)
                , new XElement("key", "styleName"), new XElement("string", "regular")
                , new XElement("key", "unitsPerEm"), new XElement("integer", 1000)
                , new XElement("key", "ascender"), new XElement("integer", 750)
                , new XElement("key", "descender"), new XElement("integer", -250)
                , new XElement("key", "xHeight"), new XElement("integer", 510)
                , new XElement("key", "capHeight"), new XElement("integer", 710)
                , new XElement("key", "italicAngle"), new XElement("real", 0)
                , new XElement("key", "versionMajor"), new XElement("integer", 1)
                , new XElement("key", "versionMinor"), new XElement("integer", 0)
                , new XElement("key", "styleMapFamilyName"), new XElement("string", family.Name)
                , new XElement("key", "styleMapStyleName"), new XElement("string", "regular")
            )
        );
        SaveDoc("fontinfo", UfoFiles.plist);
    }

    static void SaveDoc(string name, UfoFiles ftype, bool inGlyphs = false)
    {
        string p = ftype == UfoFiles.glif || inGlyphs ? GlyphsPath() : CurrentPath!;
        using var writer = new System.Xml.XmlTextWriter($"{p}{name}.{ftype}", new System.Text.UTF8Encoding(false)) { Formatting = System.Xml.Formatting.Indented };
        CurrentDocument!.Save(writer);
    }

    static void CreateGlyph(UfoGlyph glyph)
    {
        NewDoc(UfoFiles.glif, glyph.Name);
        CurrentDocument!.Element("glyph")!.Add(
            new XElement("advance",
                new XAttribute("width", glyph.Lsb + (glyph.MaxX - glyph.MinX) + glyph.Rsb)
                , new XAttribute("height", 0)
            )
            , new XElement("unicode", new XAttribute("hex", glyph.Unicode.ToString("X4")))
            , new XElement("note", glyph.Note)
            , new XElement("outline")
        );
        foreach (Segment[] contour in glyph.Contours)
        {
            XElement c = new("contour");
            V2 start = contour[0].InPoint;
            V2 origin = new(glyph.MinX - glyph.Lsb, 256 / HeightFraction);

            c.Add(Point(start, PointType.line, origin));
            foreach (Segment seg in contour)
            {
                c.Add(Point(seg.InHandle, PointType.offcurve, origin));
                c.Add(Point(seg.OutHandle, PointType.offcurve, origin));
                c.Add(Point(seg.OutPoint, PointType.curve, origin));
            }
            CurrentDocument!.Element("glyph")!.Element("outline")!.Add(c);
        }
        SaveDoc(glyph.Name, UfoFiles.glif);
    }

    enum PointType
    {
        move,
        line,
        offcurve,
        curve,
        qcurve,
    }

    static XElement Point(V2 coord, PointType type, V2 origin, bool smooth = true)
    {
        return new XElement("point"
            , new XAttribute("x", coord.X - origin.X)
            , new XAttribute("y", (1000 - coord.Y) - origin.Y)
            , new XAttribute("type", type)
        );
    }
}