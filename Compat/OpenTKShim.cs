// VALOR engine - MonoGame port.
//
// This file implements the small subset of the OpenTK.Mathematics API that the
// VALOR engine and game code were written against (Vector2/3/4, Vector2i,
// Quaternion, Matrix4). Semantics match OpenTK 4.x exactly:
//   - matrices transform row vectors: Vector3.Transform(v, m) == v * M
//   - translation lives in M41/M42/M43
//   - Matrix4.LookAt / CreatePerspectiveFieldOfView produce right-handed,
//     OpenGL-style (-1..1 depth) matrices
//   - Quaternion rotates vectors with v' = q * v * q^-1
//
// The renderer converts these to Microsoft.Xna.Framework.Matrix at the draw
// boundary (direct element copy - the layouts are identical - plus a z-remap
// for perspective projections, since MonoGame expects 0..1 depth).
//
// No code in the game layer needed to change: `using OpenTK.Mathematics;`
// now resolves to this compatibility namespace.

using System.Runtime.InteropServices;

namespace OpenTK.Mathematics;

// --------------------------------------------------------------------------------- Vector2

[StructLayout(LayoutKind.Sequential)]
public struct Vector2 : IEquatable<Vector2>
{
    public float X, Y;

    public Vector2(float x, float y) { X = x; Y = y; }
    public Vector2(float v) { X = v; Y = v; }

    public static readonly Vector2 Zero = new(0f, 0f);
    public static readonly Vector2 One = new(1f, 1f);
    public static readonly Vector2 UnitX = new(1f, 0f);
    public static readonly Vector2 UnitY = new(0f, 1f);

    public float Length => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared => X * X + Y * Y;
    public Vector2 Normalized() { float l = Length; return l > 1e-12f ? this / l : Zero; }

    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;
    public static float Distance(Vector2 a, Vector2 b) => (a - b).Length;
    public static float DistanceSquared(Vector2 a, Vector2 b) => (a - b).LengthSquared;
    public static Vector2 Normalize(Vector2 v) => v.Normalized();
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    public static Vector2 Min(Vector2 a, Vector2 b) => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
    public static Vector2 Max(Vector2 a, Vector2 b) => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
    public static Vector2 Clamp(Vector2 v, Vector2 min, Vector2 max) => Min(Max(v, min), max);
    public static Vector2 Transform(Vector2 v, Matrix4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + m.M41,
        v.X * m.M12 + v.Y * m.M22 + m.M42);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);
    public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2 operator *(Vector2 a, float s) => new(a.X * s, a.Y * s);
    public static Vector2 operator *(float s, Vector2 a) => new(a.X * s, a.Y * s);
    public static Vector2 operator /(Vector2 a, float s) => new(a.X / s, a.Y / s);
    public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.X / b.X, a.Y / b.Y);
    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    public bool Equals(Vector2 o) => this == o;
    public override bool Equals(object o) => o is Vector2 v && this == v;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X:0.###}, {Y:0.###})";
}

// --------------------------------------------------------------------------------- Vector3

[StructLayout(LayoutKind.Sequential)]
public struct Vector3 : IEquatable<Vector3>
{
    public float X, Y, Z;

    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
    public Vector3(float v) { X = v; Y = v; Z = v; }
    public Vector3(Vector2 v, float z) { X = v.X; Y = v.Y; Z = z; }

    public static readonly Vector3 Zero = new(0f, 0f, 0f);
    public static readonly Vector3 One = new(1f, 1f, 1f);
    public static readonly Vector3 UnitX = new(1f, 0f, 0f);
    public static readonly Vector3 UnitY = new(0f, 1f, 0f);
    public static readonly Vector3 UnitZ = new(0f, 0f, 1f);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public float LengthSquared => X * X + Y * Y + Z * Z;
    public Vector2 Xy => new(X, Y);
    public Vector3 Normalized() { float l = Length; return l > 1e-12f ? this / l : Zero; }

    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3 Cross(Vector3 a, Vector3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);
    public static float Distance(Vector3 a, Vector3 b) => (a - b).Length;
    public static float DistanceSquared(Vector3 a, Vector3 b) => (a - b).LengthSquared;
    public static Vector3 Normalize(Vector3 v) => v.Normalized();
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
    public static Vector3 Min(Vector3 a, Vector3 b) => new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Min(a.Z, b.Z));
    public static Vector3 Max(Vector3 a, Vector3 b) => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Max(a.Z, b.Z));
    public static Vector3 Clamp(Vector3 v, Vector3 min, Vector3 max) => Min(Max(v, min), max);
    public static Vector3 ComponentMax(Vector3 a, Vector3 b) => Max(a, b);
    public static Vector3 ComponentMin(Vector3 a, Vector3 b) => Min(a, b);

    /// <summary>Rotate a vector by a unit quaternion (v' = q * v * q^-1).</summary>
    public static Vector3 Transform(Vector3 v, Quaternion q)
    {
        var n = new Vector3(q.X, q.Y, q.Z);
        var t = 2f * Cross(n, v);
        return v + q.W * t + Cross(n, t);
    }

    /// <summary>Row-vector transform: v' = v * M (matches OpenTK 4 semantics).</summary>
    public static Vector3 Transform(Vector3 v, Matrix4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + m.M41,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + m.M42,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + m.M43);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    public static Vector3 operator *(Vector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator *(float s, Vector3 a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator /(Vector3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);
    public static Vector3 operator /(Vector3 a, Vector3 b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

    public bool Equals(Vector3 o) => this == o;
    public override bool Equals(object o) => o is Vector3 v && this == v;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
}

// --------------------------------------------------------------------------------- Vector4

[StructLayout(LayoutKind.Sequential)]
public struct Vector4 : IEquatable<Vector4>
{
    public float X, Y, Z, W;

    public Vector4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
    public Vector4(float v) { X = v; Y = v; Z = v; W = v; }
    public Vector4(Vector3 v, float w) { X = v.X; Y = v.Y; Z = v.Z; W = w; }

    public static readonly Vector4 Zero = new(0f, 0f, 0f, 0f);
    public static readonly Vector4 One = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 UnitW = new(0f, 0f, 0f, 1f);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);
    public float LengthSquared => X * X + Y * Y + Z * Z + W * W;
    public Vector3 Xyz => new(X, Y, Z);

    public static float Dot(Vector4 a, Vector4 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
    public static Vector4 Normalize(Vector4 v) { float l = v.Length; return l > 1e-12f ? v / l : Zero; }
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    /// <summary>Row-vector transform: v' = v * M (matches OpenTK 4 semantics).</summary>
    public static Vector4 Transform(Vector4 v, Matrix4 m) => new(
        v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + v.W * m.M41,
        v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + v.W * m.M42,
        v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + v.W * m.M43,
        v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + v.W * m.M44);

    public static Vector4 operator +(Vector4 a, Vector4 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    public static Vector4 operator -(Vector4 a, Vector4 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
    public static Vector4 operator -(Vector4 a) => new(-a.X, -a.Y, -a.Z, -a.W);
    public static Vector4 operator *(Vector4 a, float s) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);
    public static Vector4 operator *(float s, Vector4 a) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);
    public static Vector4 operator *(Vector4 a, Vector4 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);
    public static Vector4 operator /(Vector4 a, float s) => new(a.X / s, a.Y / s, a.Z / s, a.W / s);
    public static bool operator ==(Vector4 a, Vector4 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;
    public static bool operator !=(Vector4 a, Vector4 b) => !(a == b);

    public bool Equals(Vector4 o) => this == o;
    public override bool Equals(object o) => o is Vector4 v && this == v;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###}, {W:0.###})";
}

// --------------------------------------------------------------------------------- Vector2i

[StructLayout(LayoutKind.Sequential)]
public struct Vector2i : IEquatable<Vector2i>
{
    public int X, Y;

    public Vector2i(int x, int y) { X = x; Y = y; }

    public static readonly Vector2i Zero = new(0, 0);
    public static readonly Vector2i One = new(1, 1);

    public static Vector2i operator +(Vector2i a, Vector2i b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2i operator -(Vector2i a, Vector2i b) => new(a.X - b.X, a.Y - b.Y);
    public static implicit operator Vector2(Vector2i a) => new(a.X, a.Y);
    public static bool operator ==(Vector2i a, Vector2i b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2i a, Vector2i b) => !(a == b);

    public bool Equals(Vector2i o) => this == o;
    public override bool Equals(object o) => o is Vector2i v && this == v;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
}

// --------------------------------------------------------------------------------- Quaternion

[StructLayout(LayoutKind.Sequential)]
public struct Quaternion : IEquatable<Quaternion>
{
    public float X, Y, Z, W;

    public Quaternion(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
    public Quaternion(Vector3 xyz, float w) { X = xyz.X; Y = xyz.Y; Z = xyz.Z; W = w; }

    public static readonly Quaternion Identity = new(0f, 0f, 0f, 1f);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);
    public float LengthSquared => X * X + Y * Y + Z * Z + W * W;
    public Vector3 Xyz => new(X, Y, Z);

    public Quaternion Normalized()
    {
        float l = Length;
        return l > 1e-12f ? new Quaternion(X / l, Y / l, Z / l, W / l) : Identity;
    }

    public Quaternion Conjugated() => new(-X, -Y, -Z, W);

    public static Quaternion FromAxisAngle(Vector3 axis, float angle)
    {
        float l = axis.Length;
        if (l < 1e-12f) return Identity;
        float half = angle * 0.5f;
        float s = MathF.Sin(half) / l;
        return new Quaternion(axis.X * s, axis.Y * s, axis.Z * s, MathF.Cos(half));
    }

    /// <summary>OpenTK 4 semantics: pitch = rotation around X, yaw around Y, roll around Z.</summary>
    public static Quaternion FromEulerAngles(float pitch, float yaw, float roll)
    {
        float hp = pitch * 0.5f, hy = yaw * 0.5f, hr = roll * 0.5f;
        float sp = MathF.Sin(hp), cp = MathF.Cos(hp);
        float sy = MathF.Sin(hy), cy = MathF.Cos(hy);
        float sr = MathF.Sin(hr), cr = MathF.Cos(hr);
        return new Quaternion(
            cy * sp * cr + sy * cp * sr,
            cy * sp * sr - sy * cp * cr,
            cy * cp * sr - sy * sp * cr,
            cy * cp * cr + sy * sp * sr);
    }

    /// <summary>Build a rotation matrix from a unit-ish quaternion (same layout as OpenTK).</summary>
    public static Matrix4 ToMatrix(Quaternion q)
    {
        var n = q.Normalized();
        float x = n.X, y = n.Y, z = n.Z, w = n.W;
        float x2 = x + x, y2 = y + y, z2 = z + z;
        float xx = x * x2, xy = x * y2, xz = x * z2;
        float yy = y * y2, yz = y * z2, zz = z * z2;
        float wx = w * x2, wy = w * y2, wz = w * z2;
        var m = default(Matrix4);
        m.M11 = 1f - (yy + zz); m.M12 = xy - wz;         m.M13 = xz + wy;         m.M14 = 0f;
        m.M21 = xy + wz;         m.M22 = 1f - (xx + zz); m.M23 = yz - wx;         m.M24 = 0f;
        m.M31 = xz - wy;         m.M32 = yz + wx;         m.M33 = 1f - (xx + yy); m.M34 = 0f;
        m.M41 = 0f;              m.M42 = 0f;              m.M43 = 0f;              m.M44 = 1f;
        return m;
    }

    public static Quaternion operator *(Quaternion a, Quaternion b)
    {
        return new Quaternion(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
    }

    public static bool operator ==(Quaternion a, Quaternion b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;
    public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

    public bool Equals(Quaternion o) => this == o;
    public override bool Equals(object o) => o is Quaternion q && this == q;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###}, {W:0.###})";
}

// --------------------------------------------------------------------------------- Matrix4

[StructLayout(LayoutKind.Sequential)]
public struct Matrix4 : IEquatable<Matrix4>
{
    // Row-major storage, row-vector semantics (OpenTK 4 / XNA layout):
    //   M11 M12 M13 M14
    //   M21 M22 M23 M24
    //   M31 M32 M33 M34
    //   M41 M42 M43 M44   (translation in M41..M43)
    public float M11, M12, M13, M14;
    public float M21, M22, M23, M24;
    public float M31, M32, M33, M34;
    public float M41, M42, M43, M44;

    public static readonly Matrix4 Identity = new()
    {
        M11 = 1f, M12 = 0f, M13 = 0f, M14 = 0f,
        M21 = 0f, M22 = 1f, M23 = 0f, M24 = 0f,
        M31 = 0f, M32 = 0f, M33 = 1f, M34 = 0f,
        M41 = 0f, M42 = 0f, M43 = 0f, M44 = 1f,
    };

    public Vector3 ExtractTranslation() => new(M41, M42, M43);

    // Row accessors (OpenTK 4 layout: Row0 = M11..M14)
    public Vector4 Row0 => new(M11, M12, M13, M14);
    public Vector4 Row1 => new(M21, M22, M23, M24);
    public Vector4 Row2 => new(M31, M32, M33, M34);
    public Vector4 Row3 => new(M41, M42, M43, M44);

    /// <summary>Right-handed look-at (OpenTK 4 / MonoGame compatible output layout).</summary>
    public static Matrix4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        var z = Vector3.Normalize(eye - target);           // camera backward
        var x = Vector3.Normalize(Vector3.Cross(up, z));   // camera right
        var y = Vector3.Cross(z, x);                       // camera up
        return new Matrix4
        {
            M11 = x.X, M12 = y.X, M13 = z.X, M14 = 0f,
            M21 = x.Y, M22 = y.Y, M23 = z.Y, M24 = 0f,
            M31 = x.Z, M32 = y.Z, M33 = z.Z, M34 = 0f,
            M41 = -Vector3.Dot(x, eye), M42 = -Vector3.Dot(y, eye), M43 = -Vector3.Dot(z, eye), M44 = 1f,
        };
    }

    /// <summary>OpenGL-style perspective (NDC depth -1..1). The renderer applies the
    /// standard z-remap when converting to a MonoGame projection (0..1 depth).</summary>
    public static Matrix4 CreatePerspectiveFieldOfView(float fovY, float aspect, float zNear, float zFar)
    {
        if (fovY <= 0f || fovY >= MathF.PI) throw new ArgumentOutOfRangeException(nameof(fovY));
        if (zNear <= 0f) throw new ArgumentOutOfRangeException(nameof(zNear));
        float f = 1f / MathF.Tan(fovY * 0.5f);
        return new Matrix4
        {
            M11 = f / aspect, M12 = 0f, M13 = 0f, M14 = 0f,
            M21 = 0f, M22 = f, M23 = 0f, M24 = 0f,
            M31 = 0f, M32 = 0f, M33 = (zFar + zNear) / (zNear - zFar), M34 = -1f,
            M41 = 0f, M42 = 0f, M43 = 2f * zFar * zNear / (zNear - zFar), M44 = 0f,
        };
    }

    public static Matrix4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        float invRL = 1f / (right - left);
        float invTB = 1f / (top - bottom);
        float invNF = 1f / (zFar - zNear);
        return new Matrix4
        {
            M11 = 2f * invRL, M12 = 0f, M13 = 0f, M14 = 0f,
            M21 = 0f, M22 = 2f * invTB, M23 = 0f, M24 = 0f,
            M31 = 0f, M32 = 0f, M33 = 2f * invNF, M34 = 0f,
            M41 = -(left + right) * invRL, M42 = -(bottom + top) * invTB, M43 = -(zNear + zFar) * invNF, M44 = 1f,
        };
    }

    public static Matrix4 CreateTranslation(Vector3 t) => new()
    {
        M11 = 1f, M12 = 0f, M13 = 0f, M14 = 0f,
        M21 = 0f, M22 = 1f, M23 = 0f, M24 = 0f,
        M31 = 0f, M32 = 0f, M33 = 1f, M34 = 0f,
        M41 = t.X, M42 = t.Y, M43 = t.Z, M44 = 1f,
    };

    public static Matrix4 CreateScale(Vector3 s) => new()
    {
        M11 = s.X, M12 = 0f, M13 = 0f, M14 = 0f,
        M21 = 0f, M22 = s.Y, M23 = 0f, M24 = 0f,
        M31 = 0f, M32 = 0f, M33 = s.Z, M34 = 0f,
        M41 = 0f, M42 = 0f, M43 = 0f, M44 = 1f,
    };

    public static Matrix4 CreateRotationX(float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Matrix4
        {
            M11 = 1f, M12 = 0f, M13 = 0f, M14 = 0f,
            M21 = 0f, M22 = c, M23 = s, M24 = 0f,
            M31 = 0f, M32 = -s, M33 = c, M34 = 0f,
            M41 = 0f, M42 = 0f, M43 = 0f, M44 = 1f,
        };
    }

    public static Matrix4 CreateRotationY(float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Matrix4
        {
            M11 = c, M12 = 0f, M13 = -s, M14 = 0f,
            M21 = 0f, M22 = 1f, M23 = 0f, M24 = 0f,
            M31 = s, M32 = 0f, M33 = c, M34 = 0f,
            M41 = 0f, M42 = 0f, M43 = 0f, M44 = 1f,
        };
    }

    public static Matrix4 CreateRotationZ(float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Matrix4
        {
            M11 = c, M12 = s, M13 = 0f, M14 = 0f,
            M21 = -s, M22 = c, M23 = 0f, M24 = 0f,
            M31 = 0f, M32 = 0f, M33 = 1f, M34 = 0f,
            M41 = 0f, M42 = 0f, M43 = 0f, M44 = 1f,
        };
    }

    public static Matrix4 CreateFromQuaternion(Quaternion q) => Quaternion.ToMatrix(q);

    public static Matrix4 operator *(Matrix4 a, Matrix4 b) => new()
    {
        M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
        M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
        M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
        M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
        M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
        M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
        M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
        M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
        M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
        M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
        M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
        M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
        M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
        M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
        M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
        M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44,
    };

    public Matrix4 Inverted() => Invert(this);

    /// <summary>Standard adjugate/cofactor 4x4 inverse (same formula set as OpenTK/XNA).</summary>
    public static Matrix4 Invert(Matrix4 m)
    {
        float a11 = m.M11, a12 = m.M12, a13 = m.M13, a14 = m.M14;
        float a21 = m.M21, a22 = m.M22, a23 = m.M23, a24 = m.M24;
        float a31 = m.M31, a32 = m.M32, a33 = m.M33, a34 = m.M34;
        float a41 = m.M41, a42 = m.M42, a43 = m.M43, a44 = m.M44;

        float s0 = a11 * a22 - a21 * a12;
        float s1 = a11 * a23 - a21 * a13;
        float s2 = a11 * a24 - a21 * a14;
        float s3 = a12 * a23 - a22 * a13;
        float s4 = a12 * a24 - a22 * a14;
        float s5 = a13 * a24 - a23 * a14;

        float c5 = a33 * a44 - a43 * a34;
        float c4 = a32 * a44 - a42 * a34;
        float c3 = a32 * a43 - a42 * a33;
        float c2 = a31 * a44 - a41 * a34;
        float c1 = a31 * a43 - a41 * a33;
        float c0 = a31 * a42 - a41 * a32;

        float det = s0 * c5 - s1 * c4 + s2 * c3 + s3 * c2 - s4 * c1 + s5 * c0;
        if (MathF.Abs(det) < 1e-20f) return Identity;
        float inv = 1f / det;

        return new Matrix4
        {
            M11 = (a22 * c5 - a23 * c4 + a24 * c3) * inv,
            M12 = (-a12 * c5 + a13 * c4 - a14 * c3) * inv,
            M13 = (a42 * s5 - a43 * s4 + a44 * s3) * inv,
            M14 = (-a32 * s5 + a33 * s4 - a34 * s3) * inv,
            M21 = (-a21 * c5 + a23 * c2 - a24 * c1) * inv,
            M22 = (a11 * c5 - a13 * c2 + a14 * c1) * inv,
            M23 = (-a41 * s5 + a43 * s2 - a44 * s1) * inv,
            M24 = (a31 * s5 - a33 * s2 + a34 * s1) * inv,
            M31 = (a21 * c4 - a22 * c2 + a24 * c0) * inv,
            M32 = (-a11 * c4 + a12 * c2 - a14 * c0) * inv,
            M33 = (a41 * s4 - a42 * s2 + a44 * s0) * inv,
            M34 = (-a31 * s4 + a32 * s2 - a34 * s0) * inv,
            M41 = (-a21 * c3 + a22 * c1 - a23 * c0) * inv,
            M42 = (a11 * c3 - a12 * c1 + a13 * c0) * inv,
            M43 = (-a41 * s3 + a42 * s1 - a43 * s0) * inv,
            M44 = (a31 * s3 - a32 * s1 + a33 * s0) * inv,
        };
    }

    public Matrix4 Transposed() => new()
    {
        M11 = M11, M12 = M21, M13 = M31, M14 = M41,
        M21 = M12, M22 = M22, M23 = M32, M24 = M42,
        M31 = M13, M32 = M23, M33 = M33, M34 = M43,
        M41 = M14, M42 = M24, M43 = M34, M44 = M44,
    };

    public bool Equals(Matrix4 o) =>
        M11 == o.M11 && M12 == o.M12 && M13 == o.M13 && M14 == o.M14 &&
        M21 == o.M21 && M22 == o.M22 && M23 == o.M23 && M24 == o.M24 &&
        M31 == o.M31 && M32 == o.M32 && M33 == o.M33 && M34 == o.M34 &&
        M41 == o.M41 && M42 == o.M42 && M43 == o.M43 && M44 == o.M44;
    public override bool Equals(object o) => o is Matrix4 m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(M11, M22, M33, M44);
    public override string ToString() =>
        $"[{M11:0.##} {M12:0.##} {M13:0.##} {M14:0.##}; {M21:0.##} {M22:0.##} {M23:0.##} {M24:0.##}; {M31:0.##} {M32:0.##} {M33:0.##} {M34:0.##}; {M41:0.##} {M42:0.##} {M43:0.##} {M44:0.##}]";
}
