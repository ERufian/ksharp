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

namespace K3CSharp
{
    public class KInterpreterWrapper
    {
        private readonly string kExePath;
        private readonly string tempDirectory;
        private readonly int timeoutMs;
        private readonly string runId;

        public KInterpreterWrapper(string kExePath = @"c:\k\e.exe", int timeoutMs = 10000)
        {
            if (!File.Exists(kExePath))
            {
                kExePath = @"c:\k\k.exe";
            }
            this.kExePath = kExePath;
            this.timeoutMs = timeoutMs;
            this.runId = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}_{Guid.NewGuid():N}";
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
            if (this.kExePath.Contains("k.exe") && KInterpreterWrapper.ContainsLongInteger(scriptContent))
            {
                return "UNSUPPORTED: Script contains long integers (64-bit) - k.exe 32-bit does not support them";
            }
            
            string? tempScriptPath = null;
            string? outputPath = null;
            
            try
            {
                tempScriptPath = CreateTempScriptWithExit(scriptContent);
                outputPath = Path.Combine(tempDirectory, $"k_output_{Guid.NewGuid():N}_{DateTime.Now.Ticks}.txt");
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{kExePath}\" \"{tempScriptPath}\" > \"{outputPath}\"\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                
                if (process != null)
                {
                    bool exited = process.WaitForExit(timeoutMs);
                    
                    if (!exited)
                    {
                        process.Kill();
                        throw new TimeoutException($"k.exe execution timed out after {timeoutMs}ms");
                    }
                    
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"k.exe exited with code {process.ExitCode}");
                    }
                }

                if (File.Exists(outputPath))
                {
                    var output = File.ReadAllText(outputPath);
                    return CleanOutput(output);
                }
                
                return string.Empty;
            }
            finally
            {
                // Clean up temporary files with retry logic
                CleanupTempFile(tempScriptPath);
                CleanupTempFile(outputPath);
            }
        }

        private void CleanupTempFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            // Retry logic for file deletion (handles file locking issues)
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    File.Delete(filePath);
                    break;
                }
                catch (IOException)
                {
                    // File might be locked, wait and retry
                    if (i < 2)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // File might be locked, wait and retry
                    if (i < 2)
                    {
                        System.Threading.Thread.Sleep(50);
                    }
                }
            }
        }

        private string CreateTempScriptWithExit(string scriptContent)
        {
            // Add double backslash to force k.exe to exit after execution
            var scriptWithExit = scriptContent.TrimEnd() + "\n\\\\";
            
            var tempScriptPath = Path.Combine(tempDirectory, $"k_script_{Guid.NewGuid():N}_{DateTime.Now.Ticks}.k");
            File.WriteAllText(tempScriptPath, scriptWithExit);
            
            return tempScriptPath;
        }

        private string CleanOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
                return string.Empty;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var cleanedLines = lines.Where(line => 
                !line.Contains("WIN32") && 
                !line.Contains("CPU") && 
                !line.Contains("MB") &&
                !line.Contains("EVAL")).ToArray();

            return string.Join("\n", cleanedLines).Trim();
        }

        public void CleanupTempDirectory()
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        public static bool ContainsLongInteger(string script)
        {
            // Look for 'j' suffix indicating long integers
            return script.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.Trim().EndsWith("j") && 
                           char.IsDigit(line.Trim().SkipLast(1).LastOrDefault()));
        }
    }
}