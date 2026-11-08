using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenTK.Mathematics;
using ValorEngine.Core.Diagnostics;
using ValorEngine.Core.Input;

namespace ValorEngine.Core;

/// <summary>
/// Base class for a VALOR game, hosted on MonoGame (the pure-C# XNA successor).
/// Owns the window + GraphicsDevice, the fixed-timestep loop and the input system.
/// A game implements <see cref="OnStart"/> (create systems), <see cref="Simulate"/>
/// (fixed 120 Hz simulation step) and <see cref="Draw"/> (render current state).
///
/// The loop contract is identical to the OpenTK build: Update accumulates real time
/// and runs 120 Hz Simulate ticks; Draw renders once per presented frame. VSync is
/// OFF by default with a soft ~145 fps frame cap - never blocking on a vblank (the
/// #1 cause of ghost-white windows on VMs / remote sessions / stubborn drivers).
/// Set VALOR_VSYNC=1 to opt into vsync.
/// </summary>
public abstract class EngineHost : Game
{
    /// <summary>Fixed simulation rate in Hz.</summary>
    public const int TickRate = 120;
    public const float FixedDt = 1f / TickRate;

    private readonly GraphicsDeviceManager _gfx;
    private double _accumulator;
    private long _frameCount;
    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();

    /// <summary>Input system bound to this window. Read it inside Simulate/Draw only.</summary>
    protected InputSystem Input;

    /// <summary>Total fixed simulation ticks elapsed since start.</summary>
    protected new long Tick;
    protected float Time; // seconds, advanced per fixed tick

    protected EngineHost(int width, int height, string title)
    {
        _gfx = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = width,
            PreferredBackBufferHeight = height,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = false, // never wait for a vblank
            PreferMultiSampling = false,
        };
        IsFixedTimeStep = false;
        IsMouseVisible = false;
        Window.Title = title;
        Window.AllowUserResizing = true;
        InactiveSleepTime = TimeSpan.Zero;   // keep rendering when unfocused (screenshots, streaming)
    }

    // ------------------------------------------------------------------ lifecycle

    protected sealed override void Initialize()
    {
        base.Initialize();

        bool wantVsync = Environment.GetEnvironmentVariable("VALOR_VSYNC") == "1";
        _gfx.SynchronizeWithVerticalRetrace = wantVsync;

        Input = new InputSystem(this);

        try
        {
            OnStart();
        }
        catch (Exception ex)
        {
            BootLog.Crash(ex, "OnStart (engine systems init)");
            throw;
        }
    }

    protected sealed override void Update(GameTime gameTime)
    {
        if (Input is null) return; // first frames before Initialize finished

        Input.BeginFrame();

        // Fixed-timestep accumulator (clamped so a stall never simulates a whole minute).
        double dt = Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25);
        _accumulator += dt;
        while (_accumulator >= FixedDt)
        {
            _accumulator -= FixedDt;
            Tick++;
            Time = Tick * FixedDt;
            Simulate(FixedDt);
        }

        Input.EndFrame();
        Input.AfterFrame();   // re-center the captured cursor when pointer-locked

        base.Update(gameTime);
    }

    protected sealed override void Draw(GameTime gameTime)
    {
        if (Input is null) return;

        _frameCount++;
        if (_frameCount == 1)
            BootLog.Line($"loop: first frame rendered {_clock.ElapsedMilliseconds} ms after boot");
        else if (_frameCount % 600 == 0)
            BootLog.Line($"loop: heartbeat - {_frameCount} frames drawn, {_clock.ElapsedMilliseconds / 1000.0:F0}s uptime");

        float frameDt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Draw(frameDt);

        base.Draw(gameTime);

        // Soft frame cap when vsync is off (~145 fps): keeps the GPU cool and the
        // frametime sane without ever blocking on a vblank.
        if (!_gfx.SynchronizeWithVerticalRetrace)
        {
            const double target = 1.0 / 145.0;
            double remain = target - gameTime.ElapsedGameTime.TotalSeconds;
            if (remain > 0.0015)
            {
                Thread.Sleep((int)(remain * 700));
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed.TotalSeconds < remain) Thread.SpinWait(32);
            }
        }
        _frameClock.Restart();
    }

    // ------------------------------------------------------------------ services for games

    /// <summary>Backbuffer size in pixels.</summary>
    public Vector2i FramebufferSize => new(
        GraphicsDevice.PresentationParameters.BackBufferWidth,
        GraphicsDevice.PresentationParameters.BackBufferHeight);

    /// <summary>Request game exit (replaces the old window Close()).</summary>
    public void RequestExit() => Exit();

    // ------------------------------------------------------------------ contract

    /// <summary>Called once after the GraphicsDevice exists. Create your systems here.</summary>
    protected abstract void OnStart();

    /// <summary>Fixed-rate simulation step (dt is always 1/TickRate seconds).</summary>
    protected abstract void Simulate(float dt);

    /// <summary>Render one frame. Raw frame delta (seconds) is provided.</summary>
    protected abstract void Draw(float dt);
}
