using OpenTK.Mathematics;

namespace ValorEngine.Core.Graphics;

/// <summary>World-space vertex: position, normal, RGBA albedo color.</summary>
public struct WorldVertex
{
    public Vector3 Pos;
    public Vector3 Normal;
    public Vector4 Color;
}

/// <summary>
/// Per-pass shading context, applied by the renderer when converting world
/// vertices to GPU data. This is the old GLSL world shader (sun diffuse +
/// triplanar noise grain + exp-squared fog) ported 1:1 to C# - the values are
/// identical, only the evaluation moved to the CPU (one sample per vertex).
/// </summary>
public struct ShadeCtx
{
    public Vector3 CamPos;
    public Vector3 SunDir;
    public Vector3 SunColor;
    public Vector3 Ambient;
    public Vector3 FogColor;
    public float FogDensity;
    public bool Emissive;
}

/// <summary>
/// Procedural value-noise field (seeded, seamless-by-tile) used to break up
/// flat surfaces. Same generator as the old GL noise texture (seed 7), so the
/// grain pattern is visually identical to the OpenTK build.
/// </summary>
public static class NoiseField
{
    private const int Size = 64;
    private const int Coarse = 8;
    private static readonly float[] Grid = BuildGrid();

    private static float[] BuildGrid()
    {
        var rnd = new Random(7);
        var grid = new float[(Coarse + 1) * (Coarse + 1)];
        for (int i = 0; i < grid.Length; i++) grid[i] = rnd.NextSingle();
        return grid;
    }

    private static float SampleGrid(float u, float v)
    {
        float fx = u * Coarse, fy = v * Coarse;
        // wrap like the old REPEAT-wrapped GL texture did for negative UVs
        int x0 = ((int)fx % Coarse + Coarse) % Coarse;
        int y0 = ((int)fy % Coarse + Coarse) % Coarse;
        int x1 = (x0 + 1) % Coarse, y1 = (y0 + 1) % Coarse;
        float tx = fx - MathF.Floor(fx), ty = fy - MathF.Floor(fy);
        tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
        float a = Grid[y0 * (Coarse + 1) + x0], b = Grid[y0 * (Coarse + 1) + x1];
        float c = Grid[y1 * (Coarse + 1) + x0], d = Grid[y1 * (Coarse + 1) + x1];
        return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
    }

    public static float Sample(float u, float v)
    {
        float n = 0.55f * SampleGrid(u, v)
                + 0.25f * SampleGrid(u * 3.1f + 0.37f, v * 3.1f + 0.11f)
                + 0.20f * SampleGrid(u * 13.7f + 0.71f, v * 13.7f + 0.23f);
        return Math.Clamp(n, 0f, 1f);
    }
}

/// <summary>
/// A growing/reusable vertex list. Everything the game renders (map, bots, guns,
/// particles, tracers) is batched through instances of this class - the
/// "geometry soup" approach keeps the engine small and fast. The renderer
/// converts + shades the list into GPU vertex data on each DrawWorld call.
/// </summary>
public sealed class MeshBatch
{
    private readonly List<WorldVertex> _verts = new(4096);

    public int QuadCount => _verts.Count / 6;
    public int VertexCount => _verts.Count;

    public void Clear() => _verts.Clear();

    // ------------------------------------------------------------------ quads

    /// <summary>Add a quad from 4 corners (must be in CCW order when viewed from the front) + normal.</summary>
    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 color, Vector3? normal = null)
    {
        var n = normal ?? Vector3.Normalize(Vector3.Cross(b - a, d - a));
        _verts.Add(new WorldVertex { Pos = a, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = b, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = c, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = a, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = c, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = d, Normal = n, Color = color });
    }

    /// <summary>
    /// Add an oriented box. <paramref name="halfExtents"/> are local half sizes rotated by
    /// <paramref name="rot"/> around the box center. This is the workhorse for map geometry,
    /// characters, weapons and particles alike.
    /// </summary>
    public void AddBox(Vector3 center, Vector3 halfExtents, Quaternion rot, Vector4 color)
    {
        // 8 corners in local space
        Span<Vector3> c = stackalloc Vector3[8];
        int i = 0;
        for (int z = -1; z <= 1; z += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int x = -1; x <= 1; x += 2)
                    c[i++] = center + Vector3.Transform(new Vector3(x * halfExtents.X, y * halfExtents.Y, z * halfExtents.Z), rot);

        // faces: corners + normal + color
        Face(c[0], c[1], c[3], c[2], rot, new Vector3(0, 0, -1), color); // -Z
        Face(c[4], c[6], c[7], c[5], rot, new Vector3(0, 0, 1), color);  // +Z
        Face(c[0], c[4], c[5], c[1], rot, new Vector3(0, -1, 0), color); // -Y
        Face(c[2], c[3], c[7], c[6], rot, new Vector3(0, 1, 0), color);  // +Y
        Face(c[0], c[2], c[6], c[4], rot, new Vector3(-1, 0, 0), color); // -X
        Face(c[1], c[5], c[7], c[3], rot, new Vector3(1, 0, 0), color);  // +X
    }

    private void Face(Vector3 a, Vector3 b, Vector3 c2, Vector3 d, Quaternion rot, Vector3 localNormal, Vector4 color)
    {
        var n = Vector3.Transform(localNormal, rot);
        _verts.Add(new WorldVertex { Pos = a, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = b, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = c2, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = a, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = c2, Normal = n, Color = color });
        _verts.Add(new WorldVertex { Pos = d, Normal = n, Color = color });
    }

    /// <summary>Axis-aligned box helper (map blocks).</summary>
    public void AddAabb(Vector3 min, Vector3 max, Vector4 color)
        => AddBox((min + max) * 0.5f, (max - min) * 0.5f, Quaternion.Identity, color);

    /// <summary>Stretched box beam between two points (tracers, laser sights).</summary>
    public void AddBeam(Vector3 from, Vector3 to, float width, Vector4 color)
    {
        var delta = to - from;
        float len = delta.Length;
        if (len < 1e-4f) return;
        var dir = delta / len;
        var rot = MathUtil.QuaternionBetween(new Vector3(0, 0, 1), dir);
        AddBox((from + to) * 0.5f, new Vector3(width * 0.5f, width * 0.5f, len * 0.5f), rot, color);
    }

    // ------------------------------------------------------------------ GPU conversion

    /// <summary>
    /// Convert + shade this batch into interleaved GPU vertices (pos3 + uv2 + col4).
    /// Returns the number of floats written into <paramref name="dst"/> (grown as needed).
    /// The shading math mirrors the old world fragment shader exactly.
    /// </summary>
    public int FillGpuData(ref float[] dst, in ShadeCtx ctx)
    {
        const int stride = 9; // pos(3) + uv(2) + color(4)
        int need = _verts.Count * stride;
        if (dst is null || dst.Length < need) dst = new float[need * 2];

        int k = 0;
        foreach (var v in _verts)
        {
            dst[k++] = v.Pos.X; dst[k++] = v.Pos.Y; dst[k++] = v.Pos.Z;
            dst[k++] = 0f; dst[k++] = 0f; // uv unused for world prims (texture path is sun-only)

            float r, g, b;
            if (ctx.Emissive)
            {
                r = v.Color.X; g = v.Color.Y; b = v.Color.Z;
            }
            else
            {
                var n = v.Normal.Normalized();
                float diff = MathF.Max(Vector3.Dot(n, ctx.SunDir), 0f);

                // triplanar noise uv (world space, 0.35 scale like the old shader)
                float anx = MathF.Abs(n.X), any = MathF.Abs(n.Y), anz = MathF.Abs(n.Z);
                float nu, nv;
                if (any >= anx && any >= anz) { nu = v.Pos.X * 0.35f; nv = v.Pos.Z * 0.35f; }
                else if (anx >= anz)          { nu = v.Pos.Z * 0.35f; nv = v.Pos.Y * 0.35f; }
                else                          { nu = v.Pos.X * 0.35f; nv = v.Pos.Y * 0.35f; }
                float grain = NoiseField.Sample(nu, nv);

                float lr = ctx.Ambient.X + ctx.SunColor.X * diff;
                float lg = ctx.Ambient.Y + ctx.SunColor.Y * diff;
                float lb = ctx.Ambient.Z + ctx.SunColor.Z * diff;
                float shade = 0.70f + 0.36f * grain;
                r = v.Color.X * lr * shade;
                g = v.Color.Y * lg * shade;
                b = v.Color.Z * lb * shade;

                if (ctx.FogDensity > 0f)
                {
                    var d = v.Pos - ctx.CamPos;
                    float dist = d.Length;
                    float f = 1f - MathF.Exp(-ctx.FogDensity * ctx.FogDensity * dist * dist);
                    f = Math.Clamp(f, 0f, 1f);
                    r += (ctx.FogColor.X - r) * f;
                    g += (ctx.FogColor.Y - g) * f;
                    b += (ctx.FogColor.Z - b) * f;
                }
            }

            dst[k++] = r; dst[k++] = g; dst[k++] = b; dst[k++] = v.Color.W;
        }
        return k;
    }
}

/// <summary>Quaternion/rotation helpers.</summary>
public static class MathUtil
{
    /// <summary>Quaternion rotating vector <paramref name="from"/> onto <paramref name="to"/>.</summary>
    public static Quaternion QuaternionBetween(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        float d = Vector3.Dot(from, to);
        if (d > 0.9999f) return Quaternion.Identity;
        if (d < -0.9999f)
        {
            // 180 degrees around any perpendicular axis
            var axis = Vector3.Cross(from, Math.Abs(from.Y) < 0.99f ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0));
            return Quaternion.FromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        var cross = Vector3.Cross(from, to);
        return new Quaternion(cross, 1f + d).Normalized();
    }

    /// <summary>Yaw/pitch (radians) -> direction. Yaw 0 faces +Z, pitch positive looks up.</summary>
    public static Vector3 DirFromAngles(float yaw, float pitch)
    {
        float cp = MathF.Cos(pitch);
        return new Vector3(MathF.Sin(yaw) * cp, MathF.Sin(pitch), MathF.Cos(yaw) * cp);
    }
}
