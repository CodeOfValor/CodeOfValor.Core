using Microsoft.Xna.Framework.Graphics;
using OpenTK.Mathematics;

namespace ValorEngine.Core.Graphics;

/// <summary>
/// Runtime-generated 5x7 bitmap font atlas (no font files needed).
/// Cell 0 of the atlas is a solid white block used for solid rects/lines;
/// remaining cells hold glyphs for the character set used by the HUD.
/// Built into a MonoGame Texture2D (alpha = coverage, RGB = white) so the
/// SpriteBatch-backed Batcher2D can tint glyphs with any vertex color.
/// </summary>
public sealed class FontAtlas : IDisposable
{
    public const int CellW = 6, CellH = 8;      // cell pitch (5px glyph + 1px gap, 7px glyph + 1px gap)
    public const int GlyphW = 5, GlyphH = 7;
    public const int Cols = 16, Rows = 8;
    public const int AtlasW = Cols * CellW;     // 96
    public const int AtlasH = Rows * CellH;     // 64

    private readonly Dictionary<char, int> _index = new();
    public Texture2D Texture { get; }

    // Character -> glyph-table order. MUST match the Glyphs[] table below:
    // A-Z, then 0-9, then punctuation (index in this string minus the leading space
    // = row index in Glyphs).
    private const string Chars = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!()*+,-./:<>?'_[]=#";

    // 5 bytes per glyph, one byte per column, bit0 = top row.
    private static readonly byte[][] Glyphs =
    {
        new[]{ (byte)0x7C, (byte)0x12, (byte)0x11, (byte)0x12, (byte)0x7C }, // A
        new[]{ (byte)0x7F, (byte)0x49, (byte)0x49, (byte)0x49, (byte)0x36 }, // B
        new[]{ (byte)0x3E, (byte)0x41, (byte)0x41, (byte)0x41, (byte)0x22 }, // C
        new[]{ (byte)0x7F, (byte)0x41, (byte)0x41, (byte)0x22, (byte)0x1C }, // D
        new[]{ (byte)0x7F, (byte)0x49, (byte)0x49, (byte)0x49, (byte)0x41 }, // E
        new[]{ (byte)0x7F, (byte)0x09, (byte)0x09, (byte)0x09, (byte)0x01 }, // F
        new[]{ (byte)0x3E, (byte)0x41, (byte)0x49, (byte)0x49, (byte)0x7A }, // G
        new[]{ (byte)0x7F, (byte)0x08, (byte)0x08, (byte)0x08, (byte)0x7F }, // H
        new[]{ (byte)0x00, (byte)0x41, (byte)0x7F, (byte)0x41, (byte)0x00 }, // I
        new[]{ (byte)0x20, (byte)0x40, (byte)0x41, (byte)0x3F, (byte)0x01 }, // J
        new[]{ (byte)0x7F, (byte)0x08, (byte)0x14, (byte)0x22, (byte)0x41 }, // K
        new[]{ (byte)0x7F, (byte)0x40, (byte)0x40, (byte)0x40, (byte)0x40 }, // L
        new[]{ (byte)0x7F, (byte)0x02, (byte)0x0C, (byte)0x02, (byte)0x7F }, // M
        new[]{ (byte)0x7F, (byte)0x04, (byte)0x08, (byte)0x10, (byte)0x7F }, // N
        new[]{ (byte)0x3E, (byte)0x41, (byte)0x41, (byte)0x41, (byte)0x3E }, // O
        new[]{ (byte)0x7F, (byte)0x09, (byte)0x09, (byte)0x09, (byte)0x06 }, // P
        new[]{ (byte)0x3E, (byte)0x41, (byte)0x51, (byte)0x21, (byte)0x5E }, // Q
        new[]{ (byte)0x7F, (byte)0x09, (byte)0x19, (byte)0x29, (byte)0x46 }, // R
        new[]{ (byte)0x46, (byte)0x49, (byte)0x49, (byte)0x49, (byte)0x31 }, // S
        new[]{ (byte)0x01, (byte)0x01, (byte)0x7F, (byte)0x01, (byte)0x01 }, // T
        new[]{ (byte)0x3F, (byte)0x40, (byte)0x40, (byte)0x40, (byte)0x3F }, // U
        new[]{ (byte)0x1F, (byte)0x20, (byte)0x40, (byte)0x20, (byte)0x1F }, // V
        new[]{ (byte)0x3F, (byte)0x40, (byte)0x38, (byte)0x40, (byte)0x3F }, // W
        new[]{ (byte)0x63, (byte)0x14, (byte)0x08, (byte)0x14, (byte)0x63 }, // X
        new[]{ (byte)0x07, (byte)0x08, (byte)0x70, (byte)0x08, (byte)0x07 }, // Y
        new[]{ (byte)0x61, (byte)0x51, (byte)0x49, (byte)0x45, (byte)0x43 }, // Z
        new[]{ (byte)0x3E, (byte)0x51, (byte)0x49, (byte)0x45, (byte)0x3E }, // 0
        new[]{ (byte)0x00, (byte)0x42, (byte)0x7F, (byte)0x40, (byte)0x00 }, // 1
        new[]{ (byte)0x42, (byte)0x61, (byte)0x51, (byte)0x49, (byte)0x46 }, // 2
        new[]{ (byte)0x21, (byte)0x41, (byte)0x45, (byte)0x4B, (byte)0x31 }, // 3
        new[]{ (byte)0x18, (byte)0x14, (byte)0x12, (byte)0x7F, (byte)0x10 }, // 4
        new[]{ (byte)0x27, (byte)0x45, (byte)0x45, (byte)0x45, (byte)0x39 }, // 5
        new[]{ (byte)0x3C, (byte)0x4A, (byte)0x49, (byte)0x49, (byte)0x30 }, // 6
        new[]{ (byte)0x01, (byte)0x71, (byte)0x09, (byte)0x05, (byte)0x03 }, // 7
        new[]{ (byte)0x36, (byte)0x49, (byte)0x49, (byte)0x49, (byte)0x36 }, // 8
        new[]{ (byte)0x06, (byte)0x49, (byte)0x49, (byte)0x29, (byte)0x1E }, // 9
        new[]{ (byte)0x00, (byte)0x00, (byte)0x5F, (byte)0x00, (byte)0x00 }, // !
        new[]{ (byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00 }, // (  narrow paren-left
        new[]{ (byte)0x00, (byte)0x00, (byte)0x04, (byte)0x0A, (byte)0x11 }, // )  paren-right
        new[]{ (byte)0x08, (byte)0x2A, (byte)0x1C, (byte)0x2A, (byte)0x08 }, // *
        new[]{ (byte)0x08, (byte)0x08, (byte)0x3E, (byte)0x08, (byte)0x08 }, // +
        new[]{ (byte)0x00, (byte)0x50, (byte)0x20, (byte)0x00, (byte)0x00 }, // ,
        new[]{ (byte)0x08, (byte)0x08, (byte)0x08, (byte)0x08, (byte)0x08 }, // -
        new[]{ (byte)0x00, (byte)0x60, (byte)0x60, (byte)0x00, (byte)0x00 }, // .
        new[]{ (byte)0x20, (byte)0x10, (byte)0x08, (byte)0x04, (byte)0x02 }, // /
        new[]{ (byte)0x00, (byte)0x36, (byte)0x36, (byte)0x00, (byte)0x00 }, // :
        new[]{ (byte)0x08, (byte)0x14, (byte)0x22, (byte)0x41, (byte)0x00 }, // <
        new[]{ (byte)0x00, (byte)0x41, (byte)0x22, (byte)0x14, (byte)0x08 }, // >
        new[]{ (byte)0x02, (byte)0x01, (byte)0x51, (byte)0x09, (byte)0x06 }, // ?
        new[]{ (byte)0x00, (byte)0x00, (byte)0x03, (byte)0x02, (byte)0x00 }, // '  apostrophe
        new[]{ (byte)0x40, (byte)0x40, (byte)0x40, (byte)0x40, (byte)0x40 }, // _
        new[]{ (byte)0x7F, (byte)0x41, (byte)0x41, (byte)0x00, (byte)0x00 }, // [
        new[]{ (byte)0x00, (byte)0x00, (byte)0x41, (byte)0x41, (byte)0x7F }, // ]
        new[]{ (byte)0x46, (byte)0x46, (byte)0x46, (byte)0x46, (byte)0x46 }, // =
        new[]{ (byte)0x24, (byte)0x7F, (byte)0x24, (byte)0x7F, (byte)0x24 }, // #
    };

    public FontAtlas(GraphicsDevice gd)
    {
        // register character -> glyph cell index (cells start at 1; cell 0 = solid white)
        int glyphCursor = 0;
        foreach (var ch in Chars)
        {
            if (ch == ' ') continue; // space handled as blank advance
            _index[ch] = 1 + glyphCursor;
            glyphCursor++;
        }

        // RGBA: white everywhere, alpha = coverage (SpriteBatch multiplies vertex color)
        var pixels = new Microsoft.Xna.Framework.Color[AtlasW * AtlasH];

        // cell 0: solid white block (for rects)
        for (int y = 0; y < CellH; y++)
            for (int x = 0; x < CellW; x++)
                pixels[y * AtlasW + x] = Microsoft.Xna.Framework.Color.White;

        // glyph cells
        foreach (var (ch, cell) in _index)
        {
            var g = Glyphs[GetGlyphTableIndex(ch)];
            int cx = (cell % Cols) * CellW;
            int cy = (cell / Cols) * CellH;
            for (int col = 0; col < GlyphW; col++)
            {
                int bits = g[col];
                for (int row = 0; row < GlyphH; row++)
                    if ((bits >> row & 1) != 0)
                        pixels[(cy + row) * AtlasW + cx + col] = Microsoft.Xna.Framework.Color.White;
            }
        }

        Texture = new Texture2D(gd, AtlasW, AtlasH, false, SurfaceFormat.Color);
        Texture.SetData(pixels);
    }

    private static int GetGlyphTableIndex(char ch)
    {
        // position of ch inside Chars (excluding space which is not registered)
        int i = 0;
        foreach (var c in Chars)
        {
            if (c == ' ') continue;
            if (c == ch) return i;
            i++;
        }
        return 0;
    }

    /// <summary>Returns the glyph cell index (or -1 for space; unknown chars map to '?').</summary>
    public int IndexOf(char ch)
    {
        ch = char.ToUpperInvariant(ch);
        if (ch == ' ') return -1;
        return _index.TryGetValue(ch, out int i) ? i : _index['?'];
    }

    /// <summary>Top-left pixel of a cell inside the atlas (for SpriteBatch source rects).</summary>
    public (int px, int py) CellOrigin(int cell)
    {
        return (cell % Cols * CellW, cell / Cols * CellH);
    }

    /// <summary>UV rect of a cell, inset half a texel to avoid bleed with linear filtering.</summary>
    public (Vector2 uvMin, Vector2 uvMax) CellUv(int cell)
    {
        float x0 = cell % Cols * CellW, y0 = cell / Cols * CellH;
        return (
            new Vector2((x0 + 0.5f) / AtlasW, (y0 + 0.5f) / AtlasH),
            new Vector2((x0 + GlyphW - 0.5f) / AtlasW, (y0 + GlyphH - 0.5f) / AtlasH));
    }

    /// <summary>UV rect of the solid-white cell.</summary>
    public (Vector2 uvMin, Vector2 uvMax) SolidUv() => CellUv(0);

    public void Dispose()
    {
        Texture?.Dispose();
        GC.SuppressFinalize(this);
    }
}
