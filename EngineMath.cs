namespace ValorEngine.Core;

/// <summary>Small math helpers shared by every VALOR system.</summary>
public static class EngineMath
{
    public const float Pi = MathF.PI;
    public const float Tau = MathF.PI * 2f;
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

    public static float InvLerp(float a, float b, float v) => Math.Abs(b - a) < 1e-6f ? 0f : Math.Clamp((v - a) / (b - a), 0f, 1f);

    /// <summary>Frame-rate independent exponential smoothing (higher lambda = snappier).</summary>
    public static float Damp(float a, float b, float lambda, float dt) => LerpUnclamped(a, b, 1f - MathF.Exp(-lambda * dt));

    public static OpenTK.Mathematics.Vector3 Damp(OpenTK.Mathematics.Vector3 a, OpenTK.Mathematics.Vector3 b, float lambda, float dt)
    {
        float t = 1f - MathF.Exp(-lambda * dt);
        return new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    /// <summary>Moves value toward target by at most maxDelta (used for recoil recovery).</summary>
    public static float MoveToward(float value, float target, float maxDelta)
    {
        float d = target - value;
        if (Math.Abs(d) <= maxDelta) return target;
        return value + MathF.Sign(d) * maxDelta;
    }

    /// <summary>Shortest-arc angle interpolation in radians.</summary>
    public static float LerpAngleRad(float a, float b, float t)
    {
        float d = WrapAngle(b - a);
        return a + d * Math.Clamp(t, 0f, 1f);
    }

    public static float MoveAngleTowardRad(float a, float b, float maxDelta)
    {
        float d = WrapAngle(b - a);
        if (Math.Abs(d) <= maxDelta) return b;
        return a + MathF.Sign(d) * maxDelta;
    }

    /// <summary>Wraps an angle into (-PI, PI].</summary>
    public static float WrapAngle(float a)
    {
        a %= Tau;
        if (a > MathF.PI) a -= Tau;
        if (a <= -MathF.PI) a += Tau;
        return a;
    }

    public static float ClampAngleRad(float a, float min, float max) => Math.Clamp(a, min, max);
}

/// <summary>Shared deterministic-per-frame RNG helpers.</summary>
public static class Rand
{
    public static Random Shared = new(1337);

    public static float Range(float min, float max) => min + (max - min) * Shared.NextSingle();

    public static int Range(int minInclusive, int maxExclusive) => Shared.Next(minInclusive, maxExclusive);

    public static bool Chance(float p) => Shared.NextSingle() < p;

    /// <summary>Approximately normal distribution in [-1,1] (sum of two uniforms).</summary>
    public static float SignedBias() => (Shared.NextSingle() + Shared.NextSingle()) - 1f;
}
