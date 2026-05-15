// Copyright (c) 2026 Michal Wallace
//
// Licensed under the MIT License. See the LICENSE.txt file for details.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace K3CSharp.MCP;

/// <summary>
/// Manages a K3CSharp child process running in --mcp mode.
/// Handles shadow copying, launch, command send/receive, and reload.
/// </summary>
public sealed class K3Session : IDisposable
{
    // The sentinel K3CSharp emits after each response in --mcp mode.
    private const string Sentinel = "\x01\x02";

    private readonly string _buildDir;
    private readonly string _shadowDir;
    private readonly object _lock = new();
    private Process? _process;
    private int _commandCount;
    private DateTime _startedAt;

    /// <summary>MD5 hash of K3CSharp.dll that the running session was launched from.</summary>
    public string? RunningHash { get; private set; }

    public bool IsRunning => _process is { HasExited: false };
    public int CommandCount => _commandCount;
    public DateTime StartedAt => _startedAt;

    public K3Session(string buildDir)
    {
        _buildDir = Path.GetFullPath(buildDir);
        _shadowDir = Path.Combine(Path.GetTempPath(), "k3mcp");
    }

    /// <summary>
    /// Copies build output to shadow directory and launches K3CSharp --mcp.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            Stop();
            ShadowCopy();

            var exePath = Path.Combine(_shadowDir, "K3CSharp.exe");
            // On non-Windows or if no .exe, try the dll via dotnet
            if (!File.Exists(exePath))
                exePath = Path.Combine(_shadowDir, "K3CSharp");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--mcp",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _shadowDir,
                StandardOutputEncoding = Encoding.UTF8,
            };

            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start K3CSharp process.");

            // Wait for the initial sentinel that signals readiness.
            ReadUntilSentinel();

            _commandCount = 0;
            _startedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Stops the current K3CSharp process if running.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    _process.StandardInput.WriteLine("\\\\");
                    _process.WaitForExit(2000);
                }
                catch { /* ignore */ }

                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(2000);
                }
            }
            _process?.Dispose();
            _process = null;
        }
    }

    /// <summary>
    /// Sends a K expression and returns the interpreter's output.
    /// Auto-restarts the process if it has crashed.
    /// </summary>
    public string Eval(string code)
    {
        lock (_lock)
        {
            EnsureRunning();
            _process!.StandardInput.WriteLine(code);
            _process.StandardInput.Flush();
            var result = ReadUntilSentinel();
            _commandCount++;
            return result;
        }
    }

    /// <summary>
    /// Reloads by stopping, re-shadow-copying, and restarting.
    /// </summary>
    public void Reload()
    {
        Start(); // Start() already calls Stop() + ShadowCopy()
    }

    /// <summary>
    /// Computes MD5 hash of K3CSharp.dll at the given path.
    /// Returns null if file doesn't exist.
    /// </summary>
    public static string? ComputeHash(string dllPath)
    {
        if (!File.Exists(dllPath)) return null;
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(dllPath);
        var hash = md5.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>MD5 of the DLL in the source build directory (current build).</summary>
    public string? GetBuildHash() =>
        ComputeHash(Path.Combine(_buildDir, "K3CSharp.dll"));

    /// <summary>MD5 of the DLL in the shadow copy directory.</summary>
    public string? GetShadowHash() =>
        ComputeHash(Path.Combine(_shadowDir, "K3CSharp.dll"));

    /// <summary>True if the running session is out of date vs the source build.</summary>
    public bool IsStale
    {
        get
        {
            var buildHash = GetBuildHash();
            return buildHash != null && buildHash != RunningHash;
        }
    }

    public string BuildDir => _buildDir;
    public string ShadowDir => _shadowDir;

    public void Dispose() => Stop();

    // ── Private helpers ──────────────────────────────────────────────

    private void ShadowCopy()
    {
        if (!Directory.Exists(_buildDir))
            throw new InvalidOperationException(
                $"Build directory not found: {_buildDir}. Run 'dotnet build K3CSharp/K3CSharp.csproj' first.");

        // Clean and recreate shadow dir
        if (Directory.Exists(_shadowDir))
            Directory.Delete(_shadowDir, recursive: true);
        Directory.CreateDirectory(_shadowDir);

        // Copy all files (flat — the net8.0 output is flat)
        foreach (var file in Directory.GetFiles(_buildDir))
        {
            File.Copy(file, Path.Combine(_shadowDir, Path.GetFileName(file)));
        }

        // Also copy subdirectories (runtimes, etc.) if any
        foreach (var dir in Directory.GetDirectories(_buildDir))
        {
            CopyDirectory(dir, Path.Combine(_shadowDir, Path.GetFileName(dir)));
        }

        // Record the hash of what we just copied
        RunningHash = ComputeHash(Path.Combine(_shadowDir, "K3CSharp.dll"));
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private void EnsureRunning()
    {
        if (!IsRunning)
            Start();
    }

    /// <summary>
    /// Reads stdout character by character until the sentinel is found.
    /// Returns everything before the sentinel.
    /// </summary>
    private string ReadUntilSentinel()
    {
        var sb = new StringBuilder();
        var reader = _process!.StandardOutput;
        int sentinelIndex = 0;

        while (true)
        {
            int c = reader.Read();
            if (c == -1)
                throw new InvalidOperationException("K3CSharp process ended unexpectedly.");

            char ch = (char)c;

            if (ch == Sentinel[sentinelIndex])
            {
                sentinelIndex++;
                if (sentinelIndex == Sentinel.Length)
                {
                    // Full sentinel matched — done.
                    break;
                }
            }
            else
            {
                // Flush any partial sentinel match as normal output
                if (sentinelIndex > 0)
                {
                    sb.Append(Sentinel, 0, sentinelIndex);
                    sentinelIndex = 0;
                }
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}