using Microsoft.Xna.Framework.Audio;
using OpenTK.Mathematics;
using ValorEngine.Core.Diagnostics;

namespace ValorEngine.Core.Audio;

/// <summary>A registered synthesized sound.</summary>
public sealed class Sound
{
    public SoundEffect Sfx;
    public float Gain = 1f;
}

/// <summary>
/// MonoGame SoundEffect wrapper: synthesizes every sound effect at startup
/// (no asset files), keeps a pool of voices, and does simple distance
/// attenuation + stereo panning manually so the game controls loudness math
/// (the old build used AL_DISTANCE_MODEL = None for the same reason).
/// If no audio device exists the game runs silently instead of crashing.
/// </summary>
public sealed class AudioSystem : IDisposable
{
    private readonly Dictionary<string, Sound> _sounds = new();
    private SoundEffectInstance[] _voices;
    private bool _ok;

    private Vector3 _listenerPos, _listenerFwd, _listenerUp;

    public float MasterVolume { get; set; } = 0.8f;
    public bool Available => _ok;

    public AudioSystem(int voiceCount = 32)
    {
        _voices = new SoundEffectInstance[voiceCount];

        // Audio setup runs on the MAIN thread (MonoGame's OpenAL context is
        // thread-affine - opening it on a worker would break every later call).
        // A tiny probe (create + dispose a one-sample SoundEffect) forces the
        // backend device open; any failure (no device, missing openal32.dll,
        // wedged audio endpoint) is caught and the game runs silent.
        // VALOR_NOAUDIO=1 skips audio entirely.
        if (Environment.GetEnvironmentVariable("VALOR_NOAUDIO") == "1")
        {
            BootLog.Line("[audio] VALOR_NOAUDIO=1 - running silent by request");
            return;
        }

        try
        {
            // 16-bit PCM silence, a few samples @ Synth.SampleRate - the cheapest
            // possible probe that forces the backend device open.
            var probe = new SoundEffect(new byte[] { 0, 0, 0, 0 }, Synth.SampleRate, AudioChannels.Mono);
            var inst = probe.CreateInstance();
            inst.Dispose();
            probe.Dispose();
            _ok = true;
        }
        catch (Exception ex)
        {
            BootLog.Line("[audio] init failed - running silent: " + ex.Message);
            _ok = false;
        }
    }

    /// <summary>Register a synthesized buffer (16-bit mono PCM is built here).</summary>
    public void Add(string name, float[] pcm, float gain = 1f)
    {
        if (!_ok) return;
        try
        {
            // MonoGame's SoundEffect ctor takes little-endian 16-bit PCM bytes.
            var bytes = new byte[pcm.Length * 2];
            for (int i = 0; i < pcm.Length; i++)
            {
                short s = (short)(Math.Clamp(pcm[i], -1f, 1f) * 32000);
                bytes[i * 2] = (byte)s;
                bytes[i * 2 + 1] = (byte)(s >> 8);
            }

            _sounds[name] = new Sound
            {
                Sfx = new SoundEffect(bytes, Synth.SampleRate, AudioChannels.Mono),
                Gain = gain,
            };
        }
        catch (Exception ex)
        {
            BootLog.Line($"[audio] could not register '{name}': {ex.Message}");
        }
    }

    public bool Has(string name) => _ok && _sounds.ContainsKey(name);

    /// <summary>Non-positional (UI / player-local) playback.</summary>
    public void Play(string name, float gain = 1f, float pitch = 1f)
    {
        if (!_ok || !_sounds.TryGetValue(name, out var s)) return;
        var v = FindFreeVoice();
        if (v is null) return;
        try
        {
            v.Stop();
            v.Volume = Math.Clamp(s.Gain * gain * MasterVolume, 0f, 1f);
            v.Pitch = Math.Clamp(MathF.Log2(MathF.Max(pitch, 0.25f)), -1f, 1f); // linear rate -> semitones
            v.Pan = 0f;
            v.Play();
        }
        catch { /* a dead voice must never take the game down */ }
    }

    /// <summary>Positional playback with manual distance falloff + stereo pan.
    /// nearName/farName lets guns use a brighter close cut and a duller distant cut.</summary>
    public void PlayAt(string name, Vector3 pos, float gain = 1f, float pitchJitter = 0.06f, string farName = null, float farSwitchDist = 30f)
    {
        if (!_ok) return;
        float dist = Vector3.Distance(pos, _listenerPos);
        string use = farName != null && dist > farSwitchDist ? farName : name;
        if (!_sounds.TryGetValue(use, out var s)) return;

        float att = Math.Clamp(14f / (2f + dist * 0.55f), 0f, 1f);
        att = MathF.Pow(att, 1.35f);
        float finalGain = s.Gain * gain * att * MasterVolume;
        if (finalGain < 0.01f) return;

        // stereo pan from listener orientation (dot of direction with camera right)
        var toSrc = pos - _listenerPos;
        float len = toSrc.Length;
        float pan = 0f;
        if (len > 1e-4f)
        {
            var right = Vector3.Normalize(Vector3.Cross(_listenerFwd, _listenerUp));
            pan = Math.Clamp(Vector3.Dot(toSrc / len, right), -1f, 1f) * 0.7f;
        }

        var v = FindFreeVoice();
        if (v is null) return;
        try
        {
            v.Stop();
            float jitter = 1f + (Random.Shared.NextSingle() * 2f - 1f) * pitchJitter;
            v.Volume = Math.Clamp(finalGain, 0f, 1f);
            v.Pitch = Math.Clamp(MathF.Log2(jitter), -1f, 1f);
            v.Pan = pan;
            v.Play();
        }
        catch { /* ignore */ }
    }

    private SoundEffectInstance FindFreeVoice()
    {
        foreach (var v in _voices)
            if (v.State != SoundState.Playing)
                return v;
        return null;
    }

    public void SetListener(Vector3 pos, Vector3 fwd, Vector3 up)
    {
        _listenerPos = pos; _listenerFwd = fwd; _listenerUp = up;
    }

    public void Dispose()
    {
        if (!_ok) return;
        try
        {
            foreach (var v in _voices) v.Dispose();
            foreach (var snd in _sounds.Values) snd.Sfx.Dispose();
        }
        catch { /* shutting down - ignore */ }
        _ok = false;
    }
}
