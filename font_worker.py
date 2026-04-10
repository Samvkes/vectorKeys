#!/usr/bin/env python3
import io
import json
import struct
import sys
import traceback

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.cu2quPen import Cu2QuPen


def build_ttf(req: dict) -> bytes:
    """
    Build a minimal TTF (glyf) font in memory using quadratic outlines.

    Expected request shape (example):
    {
      "upm": 1000,
      "ascent": 800,
      "descent": -200,
      "familyName": "Preview",
      "styleName": "Regular",
      "glyphs": [
        {
          "name": "A",
          "advanceWidth": 600,
          "lsb": 0,
          "contours": [
            [
              {"cmd":"M","to":[100,0]},
              {"cmd":"L","to":[300,700]},
              {"cmd":"L","to":[500,0]},
              {"cmd":"Z"}
            ]
          ]
        }
      ],
      "cmap": { "65": "A" }   # codepoint -> glyph name
    }
    """
    upm = int(req.get("upm", 1000))
    ascent = int(req.get("ascent", 800))
    descent = int(req.get("descent", -200))

    family = req.get("familyName", "PreviewFont")
    style = req.get("styleName", "Regular")

    glyphs = req.get("glyphs", [])
    cmap_in = req.get("cmap", {})

    glyph_order = [".notdef"] + [g["name"] for g in glyphs]

    fb = FontBuilder(upm, isTTF=True)
    fb.setupGlyphOrder(glyph_order)

    # glyf + hmtx
    glyf = {}
    hmtx = {}

    # .notdef (empty)
    tt_pen = TTGlyphPen(glyph_order)
    pen = Cu2QuPen(tt_pen, max_err=upm / 1000)  # or tune tighter/looser

    glyf[".notdef"] = tt_pen.glyph()
    hmtx[".notdef"] = (upm // 2, 0)

    for g in glyphs:
        name = g["name"]
        width = int(g.get("advanceWidth", upm // 2))
        lsb = int(g.get("lsb", 0))

        tt_pen = TTGlyphPen(glyph_order)
        pen = Cu2QuPen(tt_pen, max_err=upm / 1000)  # or tune tighter/looser

        for contour in g.get("contours", []):
            started = False
            last = ()
            for seg in contour:
                cmd = seg["cmd"]
                if cmd == "M":
                    x, y = seg["to"]
                    pen.moveTo((x, y))
                    started = True
                elif cmd == "C":
                    cx1, cy1 = seg["c1"]
                    cx2, cy2 = seg["c2"]
                    x, y = seg["to"]
                    pen.curveTo((cx1,cy1),(cx2,cy2),(x,y))
                elif cmd == "L":
                    x, y = seg["to"]
                    pen.lineTo((x, y))
                elif cmd == "Q":
                    cx, cy = seg["ctrl"]
                    x, y = seg["to"]
                    pen.qCurveTo((cx, cy), (x, y))
                elif cmd == "Z":
                    pen.closePath()
                    started = False
                else:
                    raise ValueError(f"Unsupported cmd: {cmd}")

            if started:
                pen.closePath()

        glyf[name] = tt_pen.glyph()
        hmtx[name] = (width, lsb)

    fb.setupGlyf(glyf)
    fb.setupHorizontalMetrics(hmtx)
    fb.setupHorizontalHeader(ascent=ascent, descent=descent)
    fb.setupMaxp()
    fb.setupPost()

    # cmap: keys may come as strings
    cmap = {int(k): v for k, v in cmap_in.items()}
    fb.setupCharacterMap(cmap)

    fb.setupNameTable({
        "familyName": family,
        "styleName": style,
        "uniqueFontIdentifier": f"{family}-{style}",
        "fullName": f"{family} {style}",
        "psName": f"{family.replace(' ', '')}-{style.replace(' ', '')}",
    })

    fb.setupOS2(
        sTypoAscender=ascent,
        sTypoDescender=descent,
        usWinAscent=ascent,
        usWinDescent=-descent,
    )

    out = io.BytesIO()
    fb.save(out)
    return out.getvalue()


def read_exact(n: int) -> bytes:
    buf = sys.stdin.buffer.read(n)
    if len(buf) != n:
        raise EOFError
    return buf


def write_frame(status: int, payload: bytes) -> None:
    # status: 0 ok, 1 error
    body = bytes([status]) + payload
    sys.stdout.buffer.write(struct.pack(">I", len(body)))
    sys.stdout.buffer.write(body)
    sys.stdout.buffer.flush()


def main():
    while True:
        try:
            hdr = sys.stdin.buffer.read(4)
            if not hdr:
                return  # clean EOF
            if len(hdr) != 4:
                return
            (length,) = struct.unpack(">I", hdr)
            if length == 0:
                return  # optional "shutdown" frame

            req_bytes = read_exact(length)
            req = json.loads(req_bytes.decode("utf-8"))

            ttf_bytes = build_ttf(req)
            write_frame(0, ttf_bytes)

        except EOFError:
            return
        except Exception as e:
            err = {
                "error": str(e),
                "trace": traceback.format_exc(limit=6),
            }
            write_frame(1, json.dumps(err).encode("utf-8"))

if __name__ == "__main__":
    main()
