using OpenTK.Mathematics;

namespace ValorEngine.Core.Physics;

/// <summary>Axis-aligned bounding box in world space.</summary>
public struct Aabb
{
    public Vector3 Min, Max;

    public Aabb(Vector3 min, Vector3 max) { Min = min; Max = max; }

    public static Aabb FromCenterHalf(Vector3 center, Vector3 half) => new(center - half, center + half);

    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Half => (Max - Min) * 0.5f;

    public bool Overlaps(in Aabb o)
        => Min.X < o.Max.X && Max.X > o.Min.X
        && Min.Y < o.Max.Y && Max.Y > o.Min.Y
        && Min.Z < o.Max.Z && Max.Z > o.Min.Z;

    public bool Contains(Vector3 p)
        => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;
}

/// <summary>Result of a raycast.</summary>
public struct RayHit
{
    public bool Hit;
    public float Distance;
    public Vector3 Point, Normal;
}

/// <summary>
/// Static-collider world: a list of AABBs plus raycasts and the axis-separated
/// character motor with automatic step-up (so stairs made of boxes just work).
/// </summary>
public sealed class PhysicsWorld
{
    public readonly List<Aabb> Colliders = new();

    public void AddCollider(Aabb box) => Colliders.Add(box);

    // ------------------------------------------------------------------ queries

    /// <summary>Ray vs all static colliders. Returns nearest hit.</summary>
    public RayHit Raycast(Vector3 origin, Vector3 dir, float maxDist)
    {
        var best = new RayHit { Hit = false, Distance = maxDist };
        foreach (var c in Colliders)
        {
            if (RayAabb(origin, dir, c, best.Distance, out float t, out Vector3 n) && t < best.Distance)
                best = new RayHit { Hit = true, Distance = t, Point = origin + dir * t, Normal = n };
        }
        return best;
    }

    /// <summary>Segment vs AABB (slab method). d must be normalized (or maxDist scaled accordingly).</summary>
    public static bool RayAabb(Vector3 o, Vector3 d, in Aabb b, float maxT, out float tHit, out Vector3 normal)
    {
        tHit = 0f;
        normal = Vector3.Zero;
        float tmin = 0f, tmax = maxT;
        int axis = -1;
        float sign = 0f;

        for (int i = 0; i < 3; i++)
        {
            float di = i == 0 ? d.X : i == 1 ? d.Y : d.Z;
            float oi = i == 0 ? o.X : i == 1 ? o.Y : o.Z;
            float bmin = i == 0 ? b.Min.X : i == 1 ? b.Min.Y : b.Min.Z;
            float bmax = i == 0 ? b.Max.X : i == 1 ? b.Max.Y : b.Max.Z;

            if (Math.Abs(di) < 1e-8f)
            {
                if (oi < bmin || oi > bmax) return false;
                continue;
            }

            float inv = 1f / di;
            float t1 = (bmin - oi) * inv;
            float t2 = (bmax - oi) * inv;
            float s = -MathF.Sign(di); // normal faces the incoming ray
            if (t1 > t2) { (t1, t2) = (t2, t1); s = -s; }

            if (t1 > tmin) { tmin = t1; axis = i; sign = s; }
            if (t2 < tmax) tmax = t2;
            if (tmin > tmax) return false;
        }

        if (axis < 0) return false; // origin inside box: ignore (no valid face normal)
        tHit = tmin;
        normal = axis switch { 0 => new Vector3(sign, 0, 0), 1 => new Vector3(0, sign, 0), _ => new Vector3(0, 0, sign) };
        return true;
    }

    // ------------------------------------------------------------------ character motor

    public struct MoveResult
    {
        public bool Grounded;
        public bool HitCeiling;
        public bool HitWall;
        public bool SteppedUp;
    }

    /// <summary>
    /// Moves an AABB body by <paramref name="delta"/>, resolving collisions per axis.
    /// Supports stepping up ledges up to <paramref name="stepHeight"/> (stairs!).
    /// <paramref name="pos"/> is the CENTER of the body.
    /// </summary>
    public MoveResult MoveBody(ref Vector3 pos, Vector3 half, Vector3 delta, float stepHeight = 0.55f)
    {
        var res = new MoveResult();

        // --- Y axis
        if (Math.Abs(delta.Y) > 1e-6f)
        {
            pos.Y += delta.Y;
            var body = Aabb.FromCenterHalf(pos, half);
            foreach (var c in Colliders)
            {
                if (!body.Overlaps(c)) continue;
                if (delta.Y < 0) { pos.Y = c.Max.Y + half.Y + 0.001f; res.Grounded = true; }
                else { pos.Y = c.Min.Y - half.Y - 0.001f; res.HitCeiling = true; }
                body = Aabb.FromCenterHalf(pos, half);
            }
        }

        // --- horizontal axes
        res.HitWall |= SlideAxis(ref pos, half, delta.X, 0, stepHeight, ref res);
        res.HitWall |= SlideAxis(ref pos, half, delta.Z, 2, stepHeight, ref res);

        // --- ground probe (needed when standing still / moving off a ledge)
        if (!res.Grounded)
        {
            Vector3 test = pos with { Y = pos.Y - 0.06f };
            var body = Aabb.FromCenterHalf(test, half);
            foreach (var c in Colliders)
                if (body.Overlaps(c)) { res.Grounded = true; break; }
        }
        return res;
    }

    private bool SlideAxis(ref Vector3 pos, Vector3 half, float amount, int axis, float stepHeight, ref MoveResult res)
    {
        if (Math.Abs(amount) < 1e-6f) return false;

        Vector3 Try(Vector3 p) => axis == 0 ? p with { X = p.X + amount } : p with { Z = p.Z + amount };

        Vector3 moved = Try(pos);
        var body = Aabb.FromCenterHalf(moved, half);
        bool blocked = false;
        foreach (var c in Colliders)
            if (body.Overlaps(c)) { blocked = true; break; }

        if (!blocked) { pos = moved; return false; }

        // try step-up: is there headroom at (moved.Y + step)?
        Vector3 stepped = moved with { Y = moved.Y + stepHeight };
        body = Aabb.FromCenterHalf(stepped, half);
        bool headBlocked = false;
        foreach (var c in Colliders)
            if (body.Overlaps(c)) { headBlocked = true; break; }

        if (!headBlocked && res.Grounded)
        {
            // find the tallest surface we just stepped onto (must be within stepHeight)
            float bestTop = float.NegativeInfinity;
            var footBox = new Aabb(
                new Vector3(stepped.X - half.X, pos.Y - 0.02f, stepped.Z - half.Z),
                new Vector3(stepped.X + half.X, pos.Y + stepHeight + half.Y, stepped.Z + half.Z));
            foreach (var c in Colliders)
            {
                if (!footBox.Overlaps(c)) continue;
                if (c.Max.Y > bestTop && c.Max.Y <= pos.Y + stepHeight + half.Y + 0.02f)
                    bestTop = c.Max.Y;
            }
            if (bestTop > float.NegativeInfinity)
            {
                pos = new Vector3(moved.X, bestTop + half.Y + 0.001f, moved.Z);
                res.SteppedUp = true;
                return false;
            }
        }

        // fully blocked: try half-steps twice for a cheap "slide around corner"
        for (int i = 0; i < 2; i++)
        {
            amount *= 0.5f;
            Vector3 retry = axis == 0 ? pos with { X = pos.X + amount } : pos with { Z = pos.Z + amount };
            body = Aabb.FromCenterHalf(retry, half);
            bool hit = false;
            foreach (var c in Colliders)
                if (body.Overlaps(c)) { hit = true; break; }
            if (!hit) { pos = retry; return true; }
        }
        return true;
    }

    /// <summary>Would an AABB at pos intersect any static collider?</summary>
    public bool OverlapsAny(Vector3 pos, Vector3 half)
    {
        var body = Aabb.FromCenterHalf(pos, half);
        foreach (var c in Colliders)
            if (body.Overlaps(c)) return true;
        return false;
    }
}
