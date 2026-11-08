using OpenTK.Mathematics;

namespace ValorEngine.Core.Audio;

/// <summary>
/// Procedural sound design. Every effect in the game is synthesized from noise,
/// sines and envelopes at startup - the repo contains no audio assets at all.
/// </summary>
public static class Synth
{
    public const int SampleRate = 22050;

    // ------------------------------------------------------------------ DSP primitives

    private static float[] NewBuf(float seconds) => new float[(int)(seconds * SampleRate)];

    private static void FillNoise(float[] x, Random rng, float gain = 1f)
    {
        for (int i = 0; i < x.Length; i++) x[i] += (rng.NextSingle() * 2f - 1f) * gain;
    }

    private static void OnePoleLP(float[] x, float cutoffHz)
    {
        float dt = 1f / SampleRate;
        float alpha = dt / (1f / (2f * MathF.PI * cutoffHz) + dt);
        float y = 0f;
        for (int i = 0; i < x.Length; i++) { y += alpha * (x[i] - y); x[i] = y; }
    }

    private static void HPFromLP(float[] x, float cutoffHz)
    {
        var low = (float[])x.Clone();
        OnePoleLP(low, cutoffHz);
        for (int i = 0; i < x.Length; i++) x[i] -= low[i];
    }

    private static void Envelope(float[] x, float attackSec, float decayPow, float releaseSec = 0f)
    {
        int aN = Math.Max(1, (int)(attackSec * SampleRate));
        int rN = Math.Max(0, (int)(releaseSec * SampleRate));
        for (int i = 0; i < x.Length; i++)
        {
            float g;
            if (i < aN) g = i / (float)aN;
            else if (i >= x.Length - rN && rN > 0) g = (x.Length - i) / (float)rN;
            else
            {
                float t = (i - aN) / (float)Math.Max(1, x.Length - aN - rN);
                g = MathF.Pow(1f - Math.Clamp(t, 0f, 1f), decayPow);
            }
            x[i] *= g;
        }
    }

    private static void AddSine(float[] x, float f0, float f1, float gain, float decayPow = 4f)
    {
        float phase = 0f;
        for (int i = 0; i < x.Length; i++)
        {
            float t = i / (float)x.Length;
            float f = f0 + (f1 - f0) * t;
            phase += 2f * MathF.PI * f / SampleRate;
            float env = MathF.Pow(1f - t, decayPow);
            x[i] += MathF.Sin(phase) * gain * env;
        }
    }

    private static void AddDelayedCopy(float[] x, float delaySec, float gain, float cutoffHz)
    {
        int d = (int)(delaySec * SampleRate);
        var tail = new float[x.Length];
        for (int i = d; i < x.Length; i++) tail[i] = x[i - d] * gain;
        OnePoleLP(tail, cutoffHz);
        for (int i = 0; i < x.Length; i++) x[i] += tail[i];
    }

    private static void MixInto(float[] dst, float[] src, float gain)
    {
        for (int i = 0; i < Math.Min(dst.Length, src.Length); i++) dst[i] += src[i] * gain;
    }

    private static void NormalizePeak(float[] x, float peak)
    {
        float max = 0f;
        for (int i = 0; i < x.Length; i++) max = Math.Max(max, Math.Abs(x[i]));
        if (max < 1e-5f) return;
        float g = peak / max;
        for (int i = 0; i < x.Length; i++) x[i] *= g;
    }

    // ------------------------------------------------------------------ layered builders

    /// <summary>Full gunshot: noise body + bright crack + sub thump + optional slapback tail.</summary>
    private static float[] Gunshot(float dur, float cutoff, float crackGain, float subF, float subGain, float tailGain, Random rng)
    {
        var body = NewBuf(dur);
        FillNoise(body, rng);
        OnePoleLP(body, cutoff);
        Envelope(body, 0.0004f, 7f);

        var crack = NewBuf(0.004f);
        FillNoise(crack, rng);
        HPFromLP(crack, 2500f);
        Envelope(crack, 0.0002f, 2f);

        var sub = NewBuf(dur);
        AddSine(sub, subF * 1.35f, subF * 0.8f, subGain, 5f);

        var mix = NewBuf(dur);
        MixInto(mix, body, 1f);
        MixInto(mix, crack, crackGain);
        MixInto(mix, sub, 1f);

        if (tailGain > 0f)
        {
            AddDelayedCopy(mix, 0.055f, tailGain, 1400f);
            AddDelayedCopy(mix, 0.125f, tailGain * 0.5f, 900f);
        }
        NormalizePeak(mix, 0.95f);
        return mix;
    }

    private static float[] Click(float f, float dur, float gain, Random rng)
    {
        var x = NewBuf(dur);
        AddSine(x, f, f * 0.85f, gain, 2f);
        FillNoise(x, rng, gain * 0.4f);
        OnePoleLP(x, 6000f);
        Envelope(x, 0.0003f, 3f);
        NormalizePeak(x, gain);
        return x;
    }

    private static float[] Thud(float f, float dur, float gain)
    {
        var x = NewBuf(dur);
        AddSine(x, f * 1.2f, f * 0.7f, 1f, 5f);
        Envelope(x, 0.002f, 6f);
        NormalizePeak(x, gain);
        return x;
    }

    // ------------------------------------------------------------------ bank

    /// <summary>Registers the default effect bank into an AudioSystem.</summary>
    public static void RegisterDefaults(AudioSystem audio)
    {
        var rng = new Random(99);

        float[] far(float dur, float cutoff, float subF, float subGain, float tail)
        {
            var x = NewBuf(dur);
            FillNoise(x, rng);
            OnePoleLP(x, cutoff);
            Envelope(x, 0.002f, 3.5f);
            AddSine(x, subF, subF * 0.7f, subGain, 4f);
            if (tail > 0) AddDelayedCopy(x, 0.09f, tail, 500f);
            NormalizePeak(x, 0.85f);
            return x;
        }

        audio.Add("ar_near", Gunshot(0.16f, 6500f, 0.9f, 105f, 0.55f, 0.30f, rng), 0.9f);
        audio.Add("ar_far", far(0.28f, 1300f, 90f, 0.5f, 0.4f), 0.55f);

        audio.Add("smg_near", Gunshot(0.11f, 8200f, 1.1f, 135f, 0.4f, 0.16f, rng), 0.78f);
        audio.Add("smg_far", far(0.22f, 1500f, 120f, 0.35f, 0.25f), 0.5f);

        audio.Add("dmr_near", Gunshot(0.30f, 4300f, 0.8f, 70f, 0.85f, 0.45f, rng), 1.0f);
        audio.Add("dmr_far", far(0.45f, 900f, 60f, 0.7f, 0.55f), 0.65f);

        audio.Add("pst_near", Gunshot(0.12f, 6000f, 1.0f, 150f, 0.45f, 0.18f, rng), 0.75f);
        audio.Add("pst_far", far(0.2f, 1400f, 130f, 0.35f, 0.25f), 0.48f);

        // footsteps / movement
        var step = NewBuf(0.07f);
        FillNoise(step, rng);
        OnePoleLP(step, 550f);
        Envelope(step, 0.001f, 9f);
        audio.Add("footstep", step, 0.42f);

        var land = NewBuf(0.14f);
        AddSine(land, 110f, 70f, 1f, 6f);
        var ln = NewBuf(0.14f); FillNoise(ln, rng); OnePoleLP(ln, 350f); Envelope(ln, 0.001f, 7f);
        MixInto(land, ln, 0.8f);
        NormalizePeak(land, 0.8f);
        audio.Add("land", land, 0.7f);

        var slide = NewBuf(0.30f);
        FillNoise(slide, rng);
        OnePoleLP(slide, 1100f);
        Envelope(slide, 0.03f, 2.5f);
        audio.Add("slide", slide, 0.55f);

        // weapon handling
        var magOut = NewBuf(0.09f);
        MixInto(magOut, Click(700f, 0.03f, 0.9f, rng), 1f);
        MixInto(magOut, Click(480f, 0.03f, 0.9f, rng), 1f);
        audio.Add("mag_out", magOut, 0.8f);

        var magIn = NewBuf(0.09f);
        MixInto(magIn, Click(900f, 0.03f, 1f, rng), 1f);
        MixInto(magIn, Click(600f, 0.04f, 1.1f, rng), 1f);
        audio.Add("mag_in", magIn, 0.9f);

        var bolt = NewBuf(0.13f);
        MixInto(bolt, Click(1700f, 0.025f, 1f, rng), 1f);
        MixInto(bolt, Click(2300f, 0.03f, 1f, rng), 1f);
        var spring = NewBuf(0.06f); FillNoise(spring, rng); OnePoleLP(spring, 5200f); Envelope(spring, 0.001f, 3f);
        MixInto(bolt, spring, 0.5f);
        audio.Add("bolt", bolt, 0.85f);

        var sw = NewBuf(0.10f);
        FillNoise(sw, rng);
        OnePoleLP(sw, 3200f);
        Envelope(sw, 0.004f, 4f);
        MixInto(sw, Click(1500f, 0.02f, 1f, rng), 0.7f);
        audio.Add("switch", sw, 0.6f);

        var empty = NewBuf(0.035f);
        MixInto(empty, Click(2100f, 0.02f, 1f, rng), 1f);
        audio.Add("empty", empty, 0.7f);

        // combat feedback
        var hit = NewBuf(0.045f);
        AddSine(hit, 1250f, 1250f, 1f, 1.5f);
        Envelope(hit, 0.001f, 2f);
        NormalizePeak(hit, 0.6f);
        audio.Add("hit", hit, 0.9f);

        var hs = NewBuf(0.16f);
        AddSine(hs, 1750f, 1650f, 1f, 3f);
        AddSine(hs, 3500f, 3400f, 0.35f, 4f);
        Envelope(hs, 0.001f, 3f);
        NormalizePeak(hs, 0.62f);
        audio.Add("headshot", hs, 1.0f);

        var kill = NewBuf(0.17f);
        AddSine(kill, 880f, 880f, 0.9f, 2f);
        int half = kill.Length / 2;
        for (int i = half; i < kill.Length; i++)
        {
            float t = (i - half) / (float)(kill.Length - half);
            kill[i] += MathF.Sin(2f * MathF.PI * 1318f * (i - half) / SampleRate) * MathF.Pow(1f - t, 2f) * 0.9f;
        }
        Envelope(kill, 0.002f, 2f);
        NormalizePeak(kill, 0.6f);
        audio.Add("kill", kill, 1.0f);

        audio.Add("damage", Thud(120f, 0.13f, 0.85f), 0.9f);

        // UI / streaks
        var ui = NewBuf(0.025f);
        MixInto(ui, Click(1400f, 0.02f, 1f, rng), 1f);
        audio.Add("ui", ui, 0.5f);

        var streak = NewBuf(0.42f);
        foreach (var f in new[] { 440f, 554f, 659f })
        {
            var t1 = NewBuf(0.42f);
            AddSine(t1, f, f, 0.33f, 1.2f);
            MixInto(streak, t1, 1f);
        }
        OnePoleLP(streak, 3200f);
        Envelope(streak, 0.09f, 1.4f, 0.12f);
        NormalizePeak(streak, 0.55f);
        audio.Add("streak", streak, 1.0f);

        var def = NewBuf(0.5f);
        AddSine(def, 220f, 160f, 1f, 2f);
        AddSine(def, 330f, 240f, 0.5f, 2f);
        Envelope(def, 0.02f, 2f, 0.1f);
        NormalizePeak(def, 0.5f);
        audio.Add("match_end", def, 1f);

        var spawn = NewBuf(0.22f);
        AddSine(spawn, 520f, 780f, 0.8f, 2f);
        Envelope(spawn, 0.01f, 2f);
        NormalizePeak(spawn, 0.45f);
        audio.Add("deploy", spawn, 0.8f);
    }
}
