using System.Text;

namespace ValorEngine.Core.Diagnostics;

/// <summary>
/// Zero-dependency boot logger. Mirrors every line to the console AND to
/// "valor.log" next to the executable, installs a process-wide
/// unhandled-exception hook, and writes a full crash report with targeted
/// troubleshooting hints. Startup failures are never silent.
/// </summary>
public static class BootLog
{
    private static readonly object Gate = new();
    private static StreamWriter _file;
    private static bool _crashWritten;

    public static string LogPath { get; private set; } = "(unavailable)";

    // ------------------------------------------------------------------ init

    public static void Init()
    {
        try
        {
            LogPath = Path.Combine(AppContext.BaseDirectory, "valor.log");
            _file = new StreamWriter(LogPath, append: false, Encoding.UTF8) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            _file = null;
            try { Console.WriteLine($"[valor] could not open log file: {ex.Message}"); } catch { }
        }

        Line($"VALOR boot log  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Line($"os      : {Environment.OSVersion.VersionString}  ({(Environment.Is64BitProcess ? "x64" : "x86")} process)");
        Line($"runtime : .NET {Environment.Version}");
        Line($"exe     : {Environment.ProcessPath}");
        Line($"cwd     : {Environment.CurrentDirectory}");
        try
        {
            Line($"build   : {typeof(BootLog).Assembly.ManifestModule.ModuleVersionId}  ({typeof(BootLog).Assembly.GetName().Version})");
        }
        catch { }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Crash(e.ExceptionObject as Exception, "unhandled exception (process terminating)");
    }

    // ------------------------------------------------------------------ native probes

    /// <summary>Locates a native library where .NET would probe it: next to the exe,
    /// or under runtimes/&lt;rid&gt;/native (the normal `dotnet build` layout).</summary>
    public static string FindNative(string dll)
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, dll),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", dll),
            Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", "lib" + dll),
        };
        foreach (string c in candidates)
            if (File.Exists(c)) return c;
        return null;
    }

    private static void ProbeNative(string dll, string consequence)
    {
        string where = FindNative(dll);
        Line($"native  : {dll} -> {(where is null ? $"NOT FOUND ({consequence})" : where)}");
    }

    /// <summary>Called by the host once it knows which natives matter, so the log
    /// explains audio/window problems without guesswork.</summary>
    public static void ProbeGameNatives()
    {
        ProbeNative("openal32.dll", "audio will be silent (game still runs)");
        ProbeNative("SDL2.dll", "the window will not open (MonoGame SDL2 backend)");
    }

    // ------------------------------------------------------------------ logging

    public static void Line(string msg)
    {
        string stamp = DateTime.Now.ToString("HH:mm:ss.fff");
        lock (Gate)
        {
            try { Console.WriteLine($"[{stamp}] {msg}"); } catch { }
            try { _file?.WriteLine($"[{stamp}] {msg}"); } catch { }
        }
    }

    // ------------------------------------------------------------------ crashes

    public static void Crash(Exception ex, string context = null)
    {
        lock (Gate)
        {
            if (_crashWritten) return;   // exactly one report per process
            _crashWritten = true;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("============================================================");
            sb.AppendLine(" VALOR CRASH REPORT");
            if (!string.IsNullOrEmpty(context)) sb.AppendLine($" where  : {context}");
            AppendException(sb, ex, 0);
            sb.AppendLine(" how to fix:");
            foreach (string h in HintsFor(ex))
                sb.AppendLine("   * " + h);
            sb.AppendLine($" full log: {LogPath}");
            sb.AppendLine("============================================================");

            try { Console.Error.Write(sb.ToString()); } catch { }
            try { Console.Out.Write(sb.ToString()); } catch { }
            try { _file?.Write(sb.ToString()); } catch { }
        }
    }

    private static void AppendException(StringBuilder sb, Exception ex, int depth)
    {
        if (ex is null)
        {
            sb.AppendLine(" (no exception object)");
            return;
        }
        string pad = depth > 0 ? new string(' ', depth * 2) : "";
        sb.AppendLine($"{pad} type   : {ex.GetType().FullName}");
        sb.AppendLine($"{pad} message: {ex.Message}");
        if (ex.StackTrace is not null) sb.AppendLine(ex.StackTrace);
        if (ex.InnerException is not null)
        {
            sb.AppendLine($"{pad} caused by:");
            AppendException(sb, ex.InnerException, depth + 1);
        }
    }

    private static IEnumerable<string> HintsFor(Exception ex)
    {
        string msg = ex?.Message ?? "";
        var hints = new List<string>();

        if (ex is DllNotFoundException || ex is EntryPointNotFoundException)
        {
            hints.Add($"A native library failed to load: {msg}");
            hints.Add("Run the game from its full build folder - SDL2.dll / openal32.dll (or the");
            hints.Add("runtimes\\win-x64\\native\\ folder) must sit next to CodeOfValor.exe.");
            hints.Add("Never copy the .exe alone.");
            hints.Add("If the files are there, (re)install the Microsoft Visual C++ 2015-2022 Redistributable (x64).");
            return hints;
        }

        if (msg.Contains("window", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("context", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("opengl", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("display", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("sdl", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Window / OpenGL context creation failed. CODE OF VALOR needs OpenGL 3.3+ (MonoGame DesktopGL).");
            hints.Add("1) Update your GPU driver from nvidia.com / amd.com / intel.com (NOT via Windows Update).");
            hints.Add("2) Remote Desktop and most virtual machines expose no real OpenGL - run on the physical machine.");
            hints.Add("3) Laptops with dual GPUs: make sure the discrete GPU is the Windows 'high performance' default.");
            hints.Add("4) If the window opens but stays white, try setting VALOR_VSYNC=1 before launching (or unset it -");
            hints.Add("   the game defaults to vsync OFF + a soft frame cap, which fixes ghost-white windows on VMs).");
            return hints;
        }

        if (msg.Contains("audio", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("openal", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("Audio failed to initialize. The game normally falls back to silent mode automatically.");
            hints.Add("The sound itself will be missing, but the game should still run - see valor.log above.");
            return hints;
        }

        hints.Add("No specific hint for this error - the details above are the cause.");
        hints.Add("Ask for help and include the full valor.log (next to CodeOfValor.exe) plus your GPU model.");
        return hints;
    }
}
