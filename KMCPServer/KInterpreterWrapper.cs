// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace K3CSharp
{
    public class KInterpreterWrapper : IDisposable
    {
        private readonly string kExePath;
        private readonly int timeoutMs;
        private readonly string runId;
        private readonly string tempDirectory;

        public KInterpreterWrapper(string kExePath = @"c:\k\e.exe", int timeoutMs = 2000)
        {
            this.kExePath = File.Exists(kExePath) ? kExePath : @"c:\k\k.exe";
            this.timeoutMs = timeoutMs;
            // Enhanced concurrency support: use high-precision timestamp with ticks and GUID
            var highPrecisionTimestamp = DateTime.UtcNow.Ticks;
            this.runId = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}_{highPrecisionTimestamp}_{Guid.NewGuid():N}";
            this.tempDirectory = Path.Combine(Path.GetTempPath(), "ksharp_wrapper", runId);
            
            // Ensure temp directory exists
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }
        }

        public string ExecuteScript(string scriptContent)
        {
            // Check for unsupported long integers (32-bit k.exe limitation)
            if (this.kExePath.Contains("k.exe") && ContainsLongInteger(scriptContent))
            {
                return "UNSUPPORTED: Script contains long integers (64-bit) - k.exe 32-bit does not support them";
            }
            
            return ExecuteScriptWithTimeout(scriptContent, timeoutMs);
        }

        private string ExecuteScriptWithTimeout(string scriptContent, int executionTimeoutMs)
        {
            string? tempScriptPath = null;

            try
            {
                tempScriptPath = CreateTempScriptWithExit(scriptContent);
                return ExecuteScriptWithRetryAndErrorCheck(tempScriptPath ?? "", null!, executionTimeoutMs);
            }
            finally
            {
                CleanupTempFilesWithRetry(tempScriptPath ?? "");
            }
        }

        private string ExecuteScriptWithRetryAndErrorCheck(string scriptPath, string outputPath, int executionTimeoutMs)
        {
            const int maxAttempts = 3;
            const int retryDelayMs = 1000;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                Process? process = null;
                try
                {
                    // Start k.exe directly with the script file
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = kExePath,
                        Arguments = $"\"{scriptPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    process = Process.Start(startInfo);

                    // Check if process started successfully
                    if (process == null)
                    {
                        throw new InvalidOperationException("Failed to start K interpreter process");
                    }

                    // Set up to capture output
                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();
                    
                    process.OutputDataReceived += (sender, e) => {
                        if (e?.Data is { } data) outputBuilder.AppendLine(data);
                    };
                    process.ErrorDataReceived += (sender, e) => {
                        if (e?.Data is { } data) errorBuilder.AppendLine(data);
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for process to complete with timeout
                    if (!process.WaitForExit(executionTimeoutMs))
                    {
                        // Kill the process
                        KillProcessTree(process);
                        process.WaitForExit(2000); // Give it 2 seconds to die gracefully

                        if (attempt == maxAttempts)
                        {
                            // On final attempt, check stderr for messages and return filtered stderr if available
                            var timeoutStderr = errorBuilder.ToString();
                            if (!string.IsNullOrWhiteSpace(timeoutStderr))
                            {
                                return FilterOutput(timeoutStderr);
                            }
                            return "timeout error";
                        }

                        // Wait before retry
                        System.Threading.Thread.Sleep(retryDelayMs);
                        continue;
                    }

                    var stdout = outputBuilder.ToString();
                    var stderr = errorBuilder.ToString();

                    // Check if k.exe exited with code 0
                    if (process.ExitCode == 0)
                    {
                        // Add delay after exit to ensure output is complete
                        System.Threading.Thread.Sleep(100);
                        return FilterOutput(stdout);
                    }
                    else
                    {
                        // k.exe exited with error code, return stderr messages
                        if (!string.IsNullOrWhiteSpace(stderr))
                        {
                            return FilterOutput(stderr); // Return stderr messages as per specification
                        }
                        // If no stderr, return stdout (might contain error info)
                        if (!string.IsNullOrWhiteSpace(stdout))
                        {
                            return FilterOutput(stdout);
                        }
                        // If no output at all, return indication of error
                        return $"k.exe exited with code {process.ExitCode}";
                    }
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    // Kill any remaining processes
                    if (process != null && !process.HasExited)
                    {
                        KillProcessTree(process);
                        process.WaitForExit(5000);
                    }

                    // Wait before retry
                    System.Threading.Thread.Sleep(retryDelayMs);
                }
                finally
                {
                    // Ensure process is disposed
                    process?.Dispose();
                }
            }

            throw new Exception("k.exe execution failed after maximum retry attempts");
        }

        private void KillProcessTree(Process process)
        {
            try
            {
                // Kill the process itself
                if (!process.HasExited)
                {
                    process.Kill();
                    // Wait a bit for the process to actually terminate
                    if (!process.WaitForExit(2000)) // Wait up to 2 seconds
                    {
                        // Force kill if it didn't terminate gracefully
                        try { process.Kill(true); } catch { }
                    }
                }

                // Give extra time for child processes to terminate
                System.Threading.Thread.Sleep(500);
            }
            catch
            {
                // Ignore errors when killing processes
            }
        }

        public static bool ContainsLongInteger(string scriptContent)
        {
            // Pattern 1: Regular long integers: digits followed by 'j' (case insensitive)
            // This matches K long integer notation like 123j, 456j, etc.
            var regularLongPattern = @"\b\d+[Jj]\b";
            
            // Pattern 2: Special K long integers: 0Ij (integer long) and 0Nj (null long)
            var specialLongPattern = @"\b0[ILN][Jj]\b";
            
            // Combine both patterns
            var combinedPattern = $"({regularLongPattern}|{specialLongPattern})";
            return Regex.IsMatch(scriptContent, combinedPattern);
        }

        private string CreateTempScriptWithExit(string scriptContent)
        {
            // Enhanced concurrency: use high-precision timestamp with process ID and GUID
            var highPrecisionTimestamp = DateTime.UtcNow.Ticks;
            var tempScriptPath = Path.Combine(tempDirectory, $"k_script_{Environment.ProcessId}_{highPrecisionTimestamp}_{Guid.NewGuid():N}.k");
            
            // Add exit command to ensure k.exe terminates after executing the script
            var modifiedContent = scriptContent.TrimEnd() + "\n\\\\\n";
            
            // Use atomic write with file sharing to prevent locking issues
            using var fileStream = new FileStream(tempScriptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fileStream, Encoding.ASCII);
            {
                writer.Write(modifiedContent);
            }
            
            return tempScriptPath;
        }

        private static string FilterOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return "";
            }

            // Filter out licensing and debug information
            var lines = output.Split('\n');
            var filteredLines = lines.Where(line => 
                !string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith("WIN32") && !line.Contains("EVAL") &&
                !line.StartsWith("w64") && !line.Contains("PROD") &&
                !line.StartsWith("K 3.") && !line.Contains("Copyright") &&
                !Regex.IsMatch(line, @"^>\s*$")).ToArray();
            
            return string.Join("\n", filteredLines).TrimEnd();
        }

        private void CleanupTempFilesWithRetry(params string[] filePaths)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 50;
            
            foreach (var filePath in filePaths)
            {
                if (string.IsNullOrEmpty(filePath)) continue;
                
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        break;
                    }
                    catch (IOException) when (attempt < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(retryDelayMs);
                        continue;
                    }
                    catch
                    {
                        break;
                    }
                }
            }
        }

        public bool IsUsingKExe()
        {
            return kExePath.Contains("k.exe");
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // No managed resources to dispose
                }
                
                // Clean up temp directory
                CleanupTempFilesWithRetry(tempDirectory);
                
                disposed = true;
            }
        }

        ~KInterpreterWrapper()
        {
            Dispose(false);
        }
    }
}