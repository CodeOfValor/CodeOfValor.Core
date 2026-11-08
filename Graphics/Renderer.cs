using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenTK.Mathematics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;
using Matrix4 = OpenTK.Mathematics.Matrix4;

namespace ValorEngine.Core.Graphics;

/// <summary>Per-frame view/environment parameters (game-side, VALOR math).</summary>
public struct FrameParams
{
    public Vector3 CamPos;
    public Matrix4 View, Proj;
    public Vector3 SunDir;      // unit vector pointing TOWARD the sun
    public Vector3 SunColor;
    public Vector3 Ambient;
    public Vector3 FogColor;
    public float FogDensity;
}

/// <summary>
/// Owns GPU state on MonoGame: the unlit world effect, the sky (gradient dome +
/// additive sun billboards), the font atlas and the SpriteBatch HUD. The game
/// pushes geometry through MeshBatch / Batcher2D; lighting + fog are evaluated
/// per vertex on the CPU (see MeshBatch.FillGpuData) so no custom shader compile
/// step is needed - the rendered result matches the old GLSL build.
/// </summary>
public sealed class Renderer : IDisposable
{
    public FontAtlas Font;
    public Batcher2D HudBatch { get; private set; }

    private readonly GraphicsDevice _gd;
    private BasicEffect _world;      // unlit, vertex color (everything pre-shaded)
    private BasicEffect _textured;   // unlit, texture * vertex color (sun billboards)
    private Texture2D _sunTex;
    private DynamicVertexBuffer _worldVb;
    private DynamicVertexBuffer _skyVb;
    private DynamicVertexBuffer _sunVb;
    private float[] _gpu;
    private float[] _skyGpu;
    private readonly float[] _sunVerts = new float[6 * 9];
    private int _fbW = 1280, _fbH = 720;

    public Renderer(GraphicsDevice gd)
    {
        _gd = gd;
    }

    // ------------------------------------------------------------------ setup

    public void Init()
    {
        _world = new BasicEffect(_gd)
        {
            World = Matrix.Identity,
            View = Matrix.Identity,
            Projection = Matrix.Identity,
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
            FogEnabled = false,
            PreferPerPixelLighting = false,
            Alpha = 1f,
        };

        _textured = new BasicEffect(_gd)
        {
            World = Matrix.Identity,
            View = Matrix.Identity,
            Projection = Matrix.Identity,
            VertexColorEnabled = true,
            TextureEnabled = true,
            LightingEnabled = false,
            FogEnabled = false,
            Alpha = 1f,
        };
        _sunTex = MakeSunTexture(64);
        _textured.Texture = _sunTex;

        Font = new FontAtlas(_gd);
        HudBatch = new Batcher2D(_gd, Font);
    }

    /// <summary>Radial falloff sprite: sharp bright core + broad soft halo,
    /// replacing the old pow(sd,400) + pow(sd,10) sky terms.</summary>
    private Texture2D MakeSunTexture(int size)
    {
        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = MathF.Sqrt(dx * dx + dy * dy);
                float core = MathF.Max(1f - r * 2.2f, 0f);
                core = core * core * core;                       // tight disc
                float halo = MathF.Max(1f - r, 0f);
                halo = halo * halo * 0.16f;                      // wide faint glow
                float v = Math.Clamp(core + halo, 0f, 1f);
                px[y * size + x] = new Color(v, v, v, v);
            }
        }
        var tex = new Texture2D(_gd, size, size, false, SurfaceFormat.Color);
        tex.SetData(px);
        return tex;
    }

    public void Resize(int w, int h)
    {
        _fbW = Math.Max(1, w);
        _fbH = Math.Max(1, h);
    }

    public Vector2i FramebufferSize => new(_fbW, _fbH);
    public float Aspect => _fbW / (float)_fbH;

    // ------------------------------------------------------------------ matrix conversion

    /// <summary>Direct element copy VALOR -> XNA matrix. The layouts and transform
    /// conventions are identical (row vectors, translation in row 4), and the shim's
    /// LookAt output is bit-identical to MonoGame's CreateLookAt.</summary>
    private static Matrix ToXna(in Matrix4 m) => new(
        m.M11, m.M12, m.M13, m.M14,
        m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34,
        m.M41, m.M42, m.M43, m.M44);

    /// <summary>Rebuild a projection with MonoGame's own CreatePerspectiveFieldOfView
    /// using fov/aspect/near/far recovered from the shim's GL-style matrix. Using
    /// MonoGame's matrix end-to-end guarantees the depth-range convention matches
    /// what the built-in shaders + GL backend expect.
    /// Shim: M22 = 1/tan(fov/2), M11 = M22/aspect, M33 = (f+n)/(n-f), M43 = 2fn/(n-f).</summary>
    private static Matrix ToXnaProj(in Matrix4 p)
    {
        float fovY = 2f * MathF.Atan(1f / p.M22);
        float aspect = p.M22 / p.M11;
        float near = p.M43 / (p.M33 - 1f);
        float far = p.M43 / (p.M33 + 1f);
        near = Math.Clamp(near, 1e-4f, far * 0.999f);
        return Matrix.CreatePerspectiveFieldOfView(fovY, aspect, near, far);
    }

    // ------------------------------------------------------------------ frame

    public void BeginFrame(FrameParams p)
    {
        var fog = new Color(p.FogColor.X, p.FogColor.Y, p.FogColor.Z);
        _gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, fog, 1f, 0);
        DrawSky(p);
    }

    /// <summary>Draw a world-space batch. emissive=true skips lighting and fog (tracers, flashes).</summary>
    public void DrawWorld(MeshBatch batch, in FrameParams p, bool emissive, bool blended)
    {
        if (batch.VertexCount == 0) return;

        var ctx = new ShadeCtx
        {
            CamPos = p.CamPos,
            SunDir = p.SunDir,
            SunColor = p.SunColor,
            Ambient = p.Ambient,
            FogColor = p.FogColor,
            FogDensity = p.FogDensity,
            Emissive = emissive,
        };

        int floats = batch.FillGpuData(ref _gpu, ctx);
        int verts = floats / 9;

        var vb = GetWorldVb(verts);
        vb.SetData(_gpu, 0, floats, SetDataOptions.Discard);

        _gd.BlendState = blended ? BlendState.NonPremultiplied : BlendState.Opaque;
        _gd.DepthStencilState = blended ? DepthStencilState.DepthRead : DepthStencilState.Default;
        _gd.RasterizerState = RasterizerState.CullNone;

        _world.View = ToXna(p.View);
        _world.Projection = ToXnaProj(p.Proj);

        _gd.SetVertexBuffer(vb);
        foreach (var pass in _world.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawPrimitives(PrimitiveType.TriangleList, 0, verts / 3);
        }
    }

    /// <summary>Viewmodel pass: projection only, no fog, depth cleared so guns never clip walls.</summary>
    public void DrawViewmodel(MeshBatch batch, in FrameParams p, Matrix4 projViewmodel)
    {
        if (batch.VertexCount == 0) return;

        var ctx = new ShadeCtx
        {
            CamPos = Vector3.Zero,
            SunDir = p.SunDir,
            SunColor = p.SunColor,
            Ambient = p.Ambient * 1.15f,
            FogColor = p.FogColor,
            FogDensity = 0f,
            Emissive = false,
        };

        int floats = batch.FillGpuData(ref _gpu, ctx);
        int verts = floats / 9;

        var vb = GetWorldVb(verts);
        vb.SetData(_gpu, 0, floats, SetDataOptions.Discard);

        _gd.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 0);
        _gd.BlendState = BlendState.NonPremultiplied;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.RasterizerState = RasterizerState.CullNone; // gun boxes seen from inside at extreme sway angles

        _world.View = Matrix.Identity;
        _world.Projection = ToXnaProj(projViewmodel);

        _gd.SetVertexBuffer(vb);
        foreach (var pass in _world.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawPrimitives(PrimitiveType.TriangleList, 0, verts / 3);
        }
    }

    private DynamicVertexBuffer GetWorldVb(int verts)
    {
        if (_worldVb is null || _worldVb.VertexCount < verts)
        {
            _worldVb?.Dispose();
            _worldVb = new DynamicVertexBuffer(_gd, WorldVertexDecl.Decl, Math.Max(4096, verts), BufferUsage.WriteOnly);
        }
        return _worldVb;
    }

    public void ClearDepth() => _gd.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 0);
    public void DrawHud() => HudBatch.Flush();

    public void EndFrame() { /* MonoGame presents after Game.Draw returns */ }

    // ------------------------------------------------------------------ sky

    /// <summary>
    /// Sky = camera-centered gradient dome (vertex colors carry the exact
    /// zenith/horizon/ground blend from the old fragment shader) + additive sun
    /// billboard (radial core+halo texture replaces pow(sd,400)/pow(sd,10)).
    /// </summary>
    private void DrawSky(in FrameParams p)
    {
        var zenith = p.FogColor * 0.55f + new Vector3(0.10f, 0.14f, 0.26f);
        var horizon = p.FogColor;
        var ground = p.FogColor * 0.55f;

        _gd.BlendState = BlendState.Opaque;
        _gd.DepthStencilState = DepthStencilState.None;   // sky never occludes or writes depth
        _gd.RasterizerState = RasterizerState.CullNone;

        _world.View = ToXna(p.View);
        _world.Projection = ToXnaProj(p.Proj);

        // ---- dome
        int floats = BuildDome(p.CamPos, zenith, horizon, ground, ref _skyGpu);
        int verts = floats / 9;
        if (_skyVb is null || _skyVb.VertexCount < verts)
        {
            _skyVb?.Dispose();
            _skyVb = new DynamicVertexBuffer(_gd, WorldVertexDecl.Decl, Math.Max(1024, verts), BufferUsage.WriteOnly);
        }
        _skyVb.SetData(_skyGpu, 0, floats, SetDataOptions.Discard);

        _gd.SetVertexBuffer(_skyVb);
        foreach (var pass in _world.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawPrimitives(PrimitiveType.TriangleList, 0, verts / 3);
        }

        // ---- sun billboards (additive)
        if (Vector3.Dot(p.SunDir, p.SunDir) > 0.5f)
        {
            var sunPos = p.CamPos + p.SunDir * 250f;

            // camera basis from the view matrix (columns 1/2 = camera right/up)
            var v = p.View;
            var right = Vector3.Normalize(new Vector3(v.M11, v.M21, v.M31));
            var up = Vector3.Normalize(new Vector3(v.M12, v.M22, v.M32));

            _gd.BlendState = BlendState.Additive;
            _gd.DepthStencilState = DepthStencilState.None;
            _gd.RasterizerState = RasterizerState.CullNone;

            _textured.View = _world.View;
            _textured.Projection = _world.Projection;

            DrawSunQuad(sunPos, right, up, 150f, p.SunColor, 0.55f);   // wide halo
            DrawSunQuad(sunPos, right, up, 34f, p.SunColor, 1.0f);     // bright core
        }
    }

    private void DrawSunQuad(Vector3 pos, Vector3 right, Vector3 up, float size, Vector3 tint, float intensity)
    {
        var tl = pos - right * size - up * size;
        var tr = pos + right * size - up * size;
        var br = pos + right * size + up * size;
        var bl = pos - right * size + up * size;
        var col = new Vector4(tint * intensity, 1f);

        _sunVertIdx = 0;
        PushSunVert(tl, 0f, 1f, col); PushSunVert(tr, 1f, 1f, col); PushSunVert(br, 1f, 0f, col);
        PushSunVert(tl, 0f, 1f, col); PushSunVert(br, 1f, 0f, col); PushSunVert(bl, 0f, 0f, col);

        if (_sunVb is null)
            _sunVb = new DynamicVertexBuffer(_gd, WorldVertexDecl.Decl, 6, BufferUsage.WriteOnly);
        _sunVb.SetData(_sunVerts, 0, 54, SetDataOptions.Discard);

        _gd.SetVertexBuffer(_sunVb);
        foreach (var pass in _textured.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);
        }
    }

    private void PushSunVert(Vector3 pos, float u, float v, in Vector4 col)
    {
        _sunVerts[_sunVertIdx + 0] = pos.X;
        _sunVerts[_sunVertIdx + 1] = pos.Y;
        _sunVerts[_sunVertIdx + 2] = pos.Z;
        _sunVerts[_sunVertIdx + 3] = u;
        _sunVerts[_sunVertIdx + 4] = v;
        _sunVerts[_sunVertIdx + 5] = col.X;
        _sunVerts[_sunVertIdx + 6] = col.Y;
        _sunVerts[_sunVertIdx + 7] = col.Z;
        _sunVerts[_sunVertIdx + 8] = col.W;
        _sunVertIdx += 9;
    }
    private int _sunVertIdx;

    private static int BuildDome(Vector3 camPos, Vector3 zenith, Vector3 horizon, Vector3 ground, ref float[] dst)
    {
        const int rows = 14;   // phi slices (-90..90 deg)
        const int cols = 18;   // theta slices (0..360 deg)
        const float radius = 260f;
        const float tau = MathF.PI * 2f;

        Vector3 SkyColor(Vector3 dir)
        {
            float h = dir.Y;
            if (h > 0f)
            {
                float t = MathF.Pow(Math.Clamp(h, 0f, 1f), 0.55f);
                return horizon + (zenith - horizon) * t;
            }
            float g = Math.Clamp(-h * 4f, 0f, 1f);
            return horizon + (ground - horizon) * g;
        }

        int quads = rows * cols;
        int need = quads * 6 * 9;
        var buf = dst is null || dst.Length < need ? new float[need * 2] : dst;

        int k = 0;
        Vector3 Pos(int r, int c)
        {
            float phi = (r / (float)rows - 0.5f) * MathF.PI;   // -90..90
            float theta = c / (float)cols * tau;               // 0..360
            float cp = MathF.Cos(phi);
            var dir = new Vector3(MathF.Sin(theta) * cp, MathF.Sin(phi), MathF.Cos(theta) * cp);
            return camPos + dir * radius;
        }

        void Vert(Vector3 pos, Vector3 col)
        {
            buf[k++] = pos.X; buf[k++] = pos.Y; buf[k++] = pos.Z;
            buf[k++] = 0f; buf[k++] = 0f; // uv
            buf[k++] = col.X; buf[k++] = col.Y; buf[k++] = col.Z; buf[k++] = 1f;
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var a = Pos(r, c);
                var b = Pos(r, c + 1);
                var c2 = Pos(r + 1, c + 1);
                var d = Pos(r + 1, c);

                // color per-vertex from each corner's direction
                var ca = SkyColor(Vector3.Normalize(a - camPos));
                var cb = SkyColor(Vector3.Normalize(b - camPos));
                var cc = SkyColor(Vector3.Normalize(c2 - camPos));
                var cd = SkyColor(Vector3.Normalize(d - camPos));

                Vert(a, ca); Vert(b, cb); Vert(c2, cc);
                Vert(a, ca); Vert(c2, cc); Vert(d, cd);
            }
        }
        dst = buf;
        return k;
    }

    // ------------------------------------------------------------------ dispose

    public void Dispose()
    {
        _world?.Dispose();
        _textured?.Dispose();
        _sunTex?.Dispose();
        _worldVb?.Dispose();
        _skyVb?.Dispose();
        _sunVb?.Dispose();
        Font?.Dispose();
        HudBatch?.Dispose();
    }
}

/// <summary>GPU vertex layout: position(3) + uv(2) + color(4), matching MeshBatch.FillGpuData
/// and the sun/sky builders. UV is unused by the unlit world path but required by the
/// textured (sun billboard) path - one shared declaration keeps things simple.</summary>
internal static class WorldVertexDecl
{
    public static readonly VertexDeclaration Decl = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(20, VertexElementFormat.Vector4, VertexElementUsage.Color, 0));
}
