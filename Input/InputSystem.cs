using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenTK.Mathematics;
using Vector2 = OpenTK.Mathematics.Vector2;

namespace ValorEngine.Core.Input;

/// <summary>Mouse buttons (maps MonoGame's ButtonState flags onto the old OpenTK-style API).</summary>
public enum MouseButton { Left, Right, Middle }

/// <summary>
/// Thin wrapper over MonoGame keyboard + mouse with:
///  - frame-coherent queries (down / pressed / released),
///  - once-per-window-cycle look delta capture (consumed by the first sim tick),
///  - pointer-lock management (cursor warped to window center while playing).
/// </summary>
public sealed class InputSystem
{
    private readonly EngineHost _game;
    private float _pendingX, _pendingY; // mouse delta captured once per window cycle
    private float _sensitivity = 1f;    // user multiplier
    public const float BaseSensRad = 0.0022f;

    private KeyboardState _kb, _prevKb;
    private MouseState _ms;
    private bool _prevL, _prevR, _prevM;
    private bool _pointerLocked;
    private int _prevScroll;

    public InputSystem(EngineHost game) => _game = game;

    // ---- pointer lock -------------------------------------------------

    public bool PointerLocked
    {
        get => _pointerLocked;
        set
        {
            if (_pointerLocked == value) return;
            _pointerLocked = value;
            _game.IsMouseVisible = !value;
            if (value) WarpCursorToCenter();
        }
    }

    /// <summary>User sensitivity multiplier (1.0 default). Adjustable in pause menu.</summary>
    public float Sensitivity { get => _sensitivity; set => _sensitivity = Math.Clamp(value, 0.2f, 4f); }

    private void WarpCursorToCenter()
    {
        var vp = _game.GraphicsDevice.Viewport;
        Mouse.SetPosition(vp.Width / 2, vp.Height / 2);
    }

    // ---- per-frame bookkeeping ----------------------------------------

    /// <summary>Poll keyboard + mouse exactly once per window cycle.</summary>
    public void BeginFrame()
    {
        _prevKb = _kb;
        _kb = Keyboard.GetState();

        bool l = _ms.LeftButton == ButtonState.Pressed;
        bool r = _ms.RightButton == ButtonState.Pressed;
        bool m = _ms.MiddleButton == ButtonState.Pressed;
        int scroll = _ms.ScrollWheelValue;

        _ms = Mouse.GetState();

        if (_pointerLocked)
        {
            var vp = _game.GraphicsDevice.Viewport;
            // The cursor is warped to the center after each frame, so the delta
            // between the recentered position and the raw poll is the look delta.
            _pendingX += _ms.X - vp.Width / 2;
            _pendingY += _ms.Y - vp.Height / 2;
        }

        _prevL = l; _prevR = r; _prevM = m;
        _prevScroll = scroll;
    }

    public void EndFrame()
    {
        // drop unconsumed look delta (menus etc.) so it never leaks into gameplay
        _pendingX = 0;
        _pendingY = 0;
    }

    /// <summary>Called after simulation: while pointer-locked the OS cursor is
    /// pinned to the window center so it can never leave the window.</summary>
    public void AfterFrame()
    {
        if (_pointerLocked) WarpCursorToCenter();
    }

    // ---- keyboard ------------------------------------------------------

    public bool Down(Keys k) => _kb.IsKeyDown(k);
    public bool Pressed(Keys k) => _kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k);
    public bool Released(Keys k) => !_kb.IsKeyDown(k) && _prevKb.IsKeyDown(k);

    /// <summary>Axis from two keys (-1..1).</summary>
    public float Axis(Keys negative, Keys positive) => (Down(positive) ? 1f : 0f) - (Down(negative) ? 1f : 0f);

    // ---- mouse ---------------------------------------------------------

    private bool CurDown(MouseButton b) => b switch
    {
        MouseButton.Left => _ms.LeftButton == ButtonState.Pressed,
        MouseButton.Right => _ms.RightButton == ButtonState.Pressed,
        _ => _ms.MiddleButton == ButtonState.Pressed,
    };

    private bool PrevDown(MouseButton b) => b switch
    {
        MouseButton.Left => _prevL,
        MouseButton.Right => _prevR,
        _ => _prevM,
    };

    public bool MouseDown(MouseButton b) => CurDown(b);
    public bool MousePressed(MouseButton b) => CurDown(b) && !PrevDown(b);
    public bool MouseReleased(MouseButton b) => !CurDown(b) && PrevDown(b);

    /// <summary>Consume the pending look delta (radians, sensitivity applied). The first
    /// sim tick of each window cycle receives it; later ticks get zero.</summary>
    public (float yaw, float pitch) TakeLook(float scale)
    {
        var r = (_pendingX * BaseSensRad * scale, _pendingY * BaseSensRad * scale);
        _pendingX = 0;
        _pendingY = 0;
        return r;
    }

    public float TakeScroll() => _ms.ScrollWheelValue - _prevScroll;

    public Vector2 MousePos => new(_ms.X, _ms.Y);
}
