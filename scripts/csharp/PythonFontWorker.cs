using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using V2 = System.Numerics.Vector2;

namespace Vectordrawing;

// public struct Request(int upm, int ascent)
// {
//     int upm = 1000;
//     int ascent = 800;
//     int descent = -200;
//     string familyName = "Preview";
//     string styleName = "Regular";

// }

public sealed class PythonFontWorker : IDisposable
{
    private readonly Process _proc;
    private readonly Stream _stdin;
    private readonly Stream _stdout;

    public PythonFontWorker(string pythonExe, string workerPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"-u \"{workerPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,   // important: avoid deadlocks
            CreateNoWindow = true,
        };

        _proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start python worker.");
        _stdin = _proc.StandardInput.BaseStream;
        _stdout = _proc.StandardOutput.BaseStream;

        // Drain stderr so the buffer can't fill and block the process.
        _proc.BeginErrorReadLine();
    }

    public byte[] BuildTtfBytes(object request)
    {
        // Serialize request JSON
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(request);

        WriteFrame(_stdin, json);

        // Read response frame
        byte[] resp = ReadFrame(_stdout);
        if (resp.Length < 1) throw new InvalidDataException("Malformed response.");

        byte status = resp[0];
        byte[] payload = resp[1..];

        if (status == 0)
        {
            return payload; // TTF bytes
        }

        // status == 1: error JSON
        string errText = Encoding.UTF8.GetString(payload);
        throw new Exception("Python worker error: " + errText);
    }

    public void Dispose()
    {
        try
        {
            // Optional shutdown signal: zero-length frame
            Span<byte> hdr = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(hdr, 0);
            _stdin.Write(hdr);
            _stdin.Flush();
        }
        catch { /* ignore */ }

        try { if (!_proc.HasExited) _proc.Kill(); } catch { /* ignore */ }
        _proc.Dispose();
    }

    private static void WriteFrame(Stream s, ReadOnlySpan<byte> payload)
    {
        Span<byte> hdr = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(hdr, payload.Length);
        s.Write(hdr);
        s.Write(payload);
        s.Flush();
    }

    private static byte[] ReadFrame(Stream s)
    {
        Span<byte> hdr = stackalloc byte[4];
        ReadExactly(s, hdr);

        int length = BinaryPrimitives.ReadInt32BigEndian(hdr);
        if (length < 0) throw new InvalidDataException("Negative frame length.");

        byte[] buf = new byte[length];
        ReadExactly(s, buf);
        return buf;
    }

    private static void ReadExactly(Stream s, Span<byte> dest)
    {
        int readTotal = 0;
        while (readTotal < dest.Length)
        {
            int n = s.Read(dest.Slice(readTotal));
            if (n <= 0) throw new EndOfStreamException();
            readTotal += n;
        }
    }

    public async Task<byte[]> SendRequest(char name, Segment[][] segLists, int advanceWidth = 100,
                              int upm = 1000, int ascent = 750, int descent = -250, 
                              string familyName = "Preview", string styleName = "Regular")
    {
        List<object> commands = [];

        float minX = 10000;
        float maxX = -10000;
        foreach (Segment[] segs in segLists)
        {
            foreach (Segment seg in segs)
            {
                float x = seg.InPoint.X;
                minX = x < minX ? x : minX;
                maxX = x > maxX ? x : maxX;
            }
        }
        int leftsidebearing = 100;
        V2 origin = new(minX - leftsidebearing, 256 / UfoWriterReader.HeightFraction);

        foreach (Segment[] segs in segLists)
        {
            V2 start = SegToGlyph(segs[0].InPoint, origin);
            commands.Add(new { cmd = "M", to = new[]{start.X, start.Y} });
            foreach (Segment seg in segs)
            {
                V2 c1 = SegToGlyph(seg.InHandle, origin);
                V2 c2 = SegToGlyph(seg.OutHandle, origin);
                V2 to = SegToGlyph(seg.OutPoint, origin);
                commands.Add(new { cmd = "C", 
                                    c1 = new[]{c1.X, c1.Y},
                                    c2 = new[]{c2.X, c2.Y},
                                    to = new[]{to.X, to.Y}
                });
            }
            commands.Add(new { cmd = "Z" });
        }
        var request = new
        {
            upm,
            ascent,
            descent,
            familyName,
            styleName,
            glyphs = new[]
            {
                new {
                    name,
                    advanceWidth,
                    contours = new object[]
                    {
                        commands.ToArray()
                    }
                }
            },
            cmap = new System.Collections.Generic.Dictionary<string, string>
            {
                [((int)name).ToString()] = name.ToString()
            }
        };
        return BuildTtfBytes(request);
    }

    V2 SegToGlyph(V2 segcoord, V2 origin)
    {
        return new(segcoord.X - origin.X, (1000 - segcoord.Y) - origin.Y);
    }
}