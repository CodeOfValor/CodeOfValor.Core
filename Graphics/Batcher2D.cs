using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenTK.Mathematics;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector4 = OpenTK.Mathematics.Vector4;

namespace ValorEngine.Core.Graphics;

public enum TextAlign { Left, Center, Right }

/// <summary>
/// Immediate-mode 2D batcher for the HUD: text, rects, rotated rects and lines.
/// Works in top-left-origin screen pixels, exactly like the old GL build.
/// Backed by MonoGame's SpriteBatch: solid prims draw a 1x1 white pixel,
/// text draws cells from the FontAtlas texture.
/// </summary>
public sealed class Batcher2D : IDisposable
{
    private readonly FontAtlas _font;
    private readonly SpriteBatch _sb;
    private readonly Texture2D _solid;
    private bool _begun;

    public Batcher2D(GraphicsDevice gd, FontAtlas font)
    {
        _font = font;
        _sb = new SpriteBatch(gd);
        _solid = new Texture2D(gd, 1, 1, false, SurfaceFormat.Color);
        _solid.SetData(new[] { Microsoft.Xna.Framework.Color.White });
    }

    public void Clear()
    {
        // The HUD calls Clear() before pushing each frame's prims.
        if (_begun) { _sb.End(); _begun = false; }
        _sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                  SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _begun = true;
    }

    private static Color ToColor(Vector4 c) => new(c.X, c.Y, c.Z, c.W);

    // ------------------------------------------------------------------ prims

    private static Microsoft.Xna.Framework.Vector2 X2(float x, float y) => new(x, y);

    public void Rect(float x, float y, float w, float h, Vector4 col)
    {
        _sb.Draw(_solid, X2(x, y), null, ToColor(col), 0f, Microsoft.Xna.Framework.Vector2.Zero, X2(w, h), SpriteEffects.None, 0f);
    }

    public void RectRot(float cx, float cy, float w, float h, float rotRad, Vector4 col)
    {
        _sb.Draw(_solid, X2(cx, cy), null, ToColor(col), rotRad, new Microsoft.Xna.Framework.Vector2(0.5f, 0.5f), X2(w, h), SpriteEffects.None, 0f);
    }

    public void Line(float x1, float y1, float x2, float y2, float thickness, Vector4 col)
    {
        float dx = x2 - x1, dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) return;
        RectRot((x1 + x2) / 2, (y1 + y2) / 2, len, thickness, MathF.Atan2(dy, dx), col);
    }

    public void HollowRect(float x, float y, float w, float h, float t, Vector4 col)
    {
        Rect(x, y, w, t, col);
        Rect(x, y + h - t, w, t, col);
        Rect(x, y, t, h, col);
        Rect(x + w - t, y, t, h, col);
    }

    // ------------------------------------------------------------------ text

    public float TextWidth(string s, float scale) => s.Length * FontAtlas.CellW * scale;

    public void Text(string s, float x, float y, float scale, Vector4 col, TextAlign align = TextAlign.Left)
    {
        if (align == TextAlign.Center) x -= TextWidth(s, scale) / 2;
        else if (align == TextAlign.Right) x -= TextWidth(s, scale);

        for (int i = 0; i < s.Length; i++)
        {
            int cell = _font.IndexOf(s[i]);
            if (cell >= 0)
            {
                var (px, py) = _font.CellOrigin(cell);
                var src = new Rectangle(px, py, FontAtlas.GlyphW, FontAtlas.GlyphH);
                float gx = x + i * FontAtlas.CellW * scale;
                _sb.Draw(_font.Texture, X2(gx, y), src, ToColor(col),
                         0f, Microsoft.Xna.Framework.Vector2.Zero, X2(scale, scale), SpriteEffects.None, 0f);
            }
        }
    }

    // ------------------------------------------------------------------ flush

    /// <summary>Submit everything drawn since Clear() (called by Renderer.DrawHud).</summary>
    public void Flush()
    {
        if (_begun)
        {
            _sb.End();
            _begun = false;
        }
    }

    public void Dispose()
    {
        if (_begun) { try { _sb.End(); } catch { } _begun = false; }
        _solid?.Dispose();
        _sb?.Dispose();
        GC.SuppressFinalize(this);
    }
}
