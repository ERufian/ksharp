using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace K3CSharp.IPC.Tests
{
    /// <summary>
    /// Process-level tests for the integrated ksharp shell IPC surface.
    /// </summary>
    public static class K3ShellIpcDemoTests
    {
        public static bool RunAll()
        {
            Console.WriteLine();
            Console.WriteLine("ksharp shell IPC demo tests:");

            string? error = null;
            try
            {
                error = TwoProcessHelloAndExit();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (error == null)
            {
                Console.WriteLine("  [PASS] two-process-hello-exit");
                Console.WriteLine("  1/1 shell IPC tests passed");
                return true;
            }

            Console.WriteLine($"  [FAIL] two-process-hello-exit  -- {error}");
            Console.WriteLine("  0/1 shell IPC tests passed");
            return false;
        }

        private static string? TwoProcessHelloAndExit()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            string shellDll = Path.Combine(repoRoot, "K3CSharp", "bin", "Debug", "net8.0", "K3CSharp.dll");
            if (!File.Exists(shellDll))
            {
                return $"Shell DLL not found: {shellDll}";
            }

            int port = GetFreePort();
            string tempDir = Path.Combine(Path.GetTempPath(), "k3sharp-ipc-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string serverScript = Path.Combine(tempDir, "server_demo.k");
            string clientScript = Path.Combine(tempDir, "client_demo.k");
            string exitScript = Path.Combine(tempDir, "client_exit.k");
            File.WriteAllText(serverScript, "0");
            File.WriteAllText(clientScript, $"(`127.0.0.1;{port}) 4: \"|\\\"olleh\\\"\"");
            File.WriteAllText(exitScript, $"(`127.0.0.1;{port}) 3: \"_exit[]\"");

            var serverInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            serverInfo.ArgumentList.Add(shellDll);
            serverInfo.ArgumentList.Add("-i");
            serverInfo.ArgumentList.Add(port.ToString());
            serverInfo.ArgumentList.Add(serverScript);

            using var server = Process.Start(serverInfo)
                ?? throw new InvalidOperationException("Failed to start ksharp server process.");

            try
            {
                if (!WaitForPort(port, TimeSpan.FromSeconds(5)))
                {
                    return "Timed out waiting for the server to begin listening.";
                }

                string clientOutput = RunShellOnce(shellDll, repoRoot, clientScript).Trim();
                if (clientOutput != "(0;\"hello\")")
                {
                    return $"Unexpected client output: {clientOutput}";
                }

                _ = RunShellOnce(shellDll, repoRoot, exitScript);
                if (!server.WaitForExit(5000))
                {
                    return "Server did not exit after async _exit[] request.";
                }

                return null;
            }
            finally
            {
                try
                {
                    if (!server.HasExited)
                    {
                        server.Kill(entireProcessTree: true);
                        server.WaitForExit(2000);
                    }
                }
                catch
                {
                    // Best effort cleanup.
                }

                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        private static string RunShellOnce(string shellDll, string workingDir, string scriptPath)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            info.ArgumentList.Add(shellDll);
            info.ArgumentList.Add(scriptPath);

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Failed to start ksharp client process.");

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(stderr))
            {
                throw new Exception($"Client process failed. stdout={stdout.Trim()} stderr={stderr.Trim()}");
            }

            return stdout;
        }

        private static bool WaitForPort(int port, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var client = new TcpClient();
                    client.Connect(IPAddress.Loopback, port);
                    return true;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }

            return false;
        }

        private static int GetFreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
