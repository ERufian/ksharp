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
                
                ExecuteKProcess(tempScriptPath, outputPath);
                return KInterpreterWrapper.ReadAndCleanOutput(outputPath);
            }
            finally
            {
                // Cleanup with retry mechanism
                CleanupTempFilesWithRetry(tempScriptPath ?? "", outputPath ?? "");
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
            return System.Text.RegularExpressions.Regex.IsMatch(scriptContent, combinedPattern);
        }
        
        public bool IsUsingKExe()
        {
            return this.kExePath.Contains("k.exe");
        }

        private string CreateTempScriptWithExit(string scriptContent)
        {
            var tempScriptPath = Path.Combine(tempDirectory, $"k_script_{Guid.NewGuid():N}_{DateTime.Now.Ticks}.k");
            
            // Add _exit 0 to force k.exe to exit with status code 0
            // This is more explicit than double backslash and provides a clean exit
            var modifiedContent = scriptContent.TrimEnd() + "\n_exit 0\n";
            
            // Use atomic write with file sharing to prevent locking issues
            using (var fileStream = new FileStream(tempScriptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream, Encoding.ASCII))
            {
                writer.Write(modifiedContent);
            }
            
            return tempScriptPath;
        }

        private string ExecuteKProcess(string scriptPath, string outputPath)
        {
            // Use shell execution to properly redirect stdout
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{kExePath}\" \"{scriptPath}\" > \"{outputPath}\" 2>&1\"",
                UseShellExecute = false,
                RedirectStandardOutput = false, // Don't redirect - using shell redirection
                RedirectStandardError = false,  // Don't redirect - using shell redirection
                CreateNoWindow = true
            };

            // Set PROMPT environment variable to "$P$G" for consistency with test expectations
            startInfo.EnvironmentVariables["PROMPT"] = "$P$G";

            using var process = new Process { StartInfo = startInfo };
            
            try
            {
                process.Start();
                
                // Simple wait with timeout
                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    throw new TimeoutException($"k.exe execution timed out after {timeoutMs}ms");
                }

                // Add delay after exit to allow file operations to complete
                System.Threading.Thread.Sleep(100); // 100ms delay

                if (process.ExitCode != 0)
                {
                    // Read error from output file if it exists
                    if (File.Exists(outputPath))
                    {
                        var errorOutput = File.ReadAllText(outputPath, Encoding.UTF8);
                        throw new Exception($"k.exe exited with code {process.ExitCode}. Output: {errorOutput}");
                    }
                    throw new Exception($"k.exe exited with code {process.ExitCode}");
                }
                
                return outputPath;
            }
            catch (Exception ex)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                throw new Exception($"Failed to execute k.exe: {ex.Message}", ex);
            }
        }

        public static string ReadAndCleanOutput(string outputPath)
        {
            if (!File.Exists(outputPath))
            {
                return "";
            }

            // Use retry mechanism for file access
            const int maxRetries = 3;
            const int retryDelayMs = 100;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var fileStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.SequentialScan);
                    using var reader = new StreamReader(fileStream, Encoding.UTF8);
                    
                    var lines = new List<string>();
                    string? line;
                    
                    while ((line = reader.ReadLine()) != null)
                    {
                        var trimmedLine = line.Trim();
                        
                        // Skip licensing information lines that start with WIN32 and end with EVAL
                        if (trimmedLine.StartsWith("WIN32") && trimmedLine.Contains("EVAL"))
                        {
                            continue;
                        }
                            if ((trimmedLine.StartsWith("w64") || trimmedLine.StartsWith("WIN32")) && trimmedLine.Contains("PROD"))
                        {
                            continue;
                        }
                        
                        // Skip copyright header
                        if (trimmedLine.StartsWith("K 3.") && trimmedLine.Contains("Copyright"))
                        {
                            continue;
                        }
                        
                        // Skip stderr markers
                        if (trimmedLine.StartsWith("STDERR:"))
                        {
                            continue;
                        }
                        
                        // Add the cleaned line
                        lines.Add(line);
                    }
                    
                    // Filter out empty lines at the start
                    var filteredLines = lines.SkipWhile(string.IsNullOrWhiteSpace).ToList();
                    
                    return string.Join("\n", filteredLines).TrimEnd();
                }
                catch (IOException) when (attempt < maxRetries - 1)
                {
                    // File might be locked, wait and retry
                    System.Threading.Thread.Sleep(retryDelayMs);
                    continue;
                }
            }
            
            // If all retries failed, try one more time with basic read
            try
            {
                return File.ReadAllText(outputPath, Encoding.UTF8).TrimEnd();
            }
            catch
            {
                return "";
            }
        }

        private void CleanupTempFilesWithRetry(params string[] filePaths)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 50;
            
            foreach (var filePath in filePaths)
            {
                if (filePath == null) continue;
                
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        break; // Success
                    }
                    catch (IOException) when (attempt < maxRetries - 1)
                    {
                        // File might be locked, wait and retry
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                    catch
                    {
                        // Other exceptions, don't retry
                        break;
                    }
                }
            }
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

        ~KInterpreterWrapper()
        {
            CleanupTempDirectory();
        }
    }
}