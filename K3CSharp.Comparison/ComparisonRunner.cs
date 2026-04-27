using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using K3CSharp;
using K3CSharp.Parsing;

namespace K3CSharp.Comparison
{
    public class KnownDifference
    {
        public string TestName { get; set; } = "";
        public List<string> Tweaks { get; set; } = new List<string>();
        public string Notes { get; set; } = "";
    }
    
    public class KnownDifferences
    {
        private readonly Dictionary<string, KnownDifference> _differences = new();
        public int Count => _differences.Count;
        
        public KnownDifferences(string filePath)
        {
            LoadFromFile(filePath);
        }
        
        private void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            
            foreach (var line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                
                var parts = line.Split("::", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var testName = parts[0].Trim();
                    var tweaks = parts[1].Split('&', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
                    var notes = parts[2].Trim();
                    
                    _differences[testName] = new KnownDifference
                    {
                        TestName = testName,
                        Tweaks = tweaks,
                        Notes = notes
                    };
                }
            }
        }
        
        public bool HasDifference(string testName) => _differences.ContainsKey(testName);
        
        public KnownDifference? GetDifference(string testName) => _differences.GetValueOrDefault(testName);
        
        public string ApplyTweaks(string input, string testName)
        {
            if (!HasDifference(testName)) return input;
            
            var difference = GetDifference(testName);
            if (difference == null) return input;
            var result = input;
            
            foreach (var tweak in difference.Tweaks)
            {
                result = ApplyTweak(result, tweak);
            }
            
            return result;
        }
        
        private static string ApplyTweak(string input, string tweak)
        {
            var parts = tweak.Split(':');
            if (parts.Length < 2) return input;
            
            var operation = parts[0].ToLower();
            
            // Handle escape sequences in patterns and replacements
            for (int i = 1; i < parts.Length; i++)
            {
                parts[i] = parts[i].Replace("\\n", "\n")
                                 .Replace("\\t", "\t")
                                 .Replace("\\r", "\r")
                                 .Replace("\\;", ";")  // Handle escaped semicolons
                                 .Replace("space", " ") // Handle space keyword
                                 .Replace("\\\\", "\\");
            }
            
            return operation switch
            {
                "regex" => Regex.Replace(input, parts[1], parts.Length > 2 ? parts[2] : ""),
                _ => input
            };
        }
    }
    public class ComparisonRunner
    {
        static ComparisonRunner()
        {
            // Enable Safe LRS mode with fallback - critical for correct parsing
            ParserConfig.EnableLRSSafely();
            ParserConfig.EnableDebugging = false;
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("K3Sharp vs k.exe Comparison Report Generator");
            Console.WriteLine("============================================");
            
            // Parse command line arguments
            bool forceKExe = false;
            bool showHelp = false;
            string? singleTest = null;
            
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--32bit":
                    case "--kexe":
                        forceKExe = true;
                        break;
                    case "--64bit":
                    case "--k":
                        forceKExe = false;
                        break;
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    default:
                        // Treat as single test name
                        singleTest = args[i];
                        break;
                }
            }
            
            if (showHelp)
            {
                ShowHelp();
                return;
            }
            
            // Create wrapper with appropriate executable
            KInterpreterWrapper wrapper;
            if (forceKExe)
            {
                Console.WriteLine("🔧 Running 32-bit k.exe");
                wrapper = new KInterpreterWrapper(@"c:\k\k.exe");
            }
            else
            {
                Console.WriteLine("🔧 Running 64-bit K (e.exe with k.exe fallback)");
                wrapper = new KInterpreterWrapper();
            }
            
            var testScriptsPath = @"T:\_src\github.com\ERufian\ksharp\K3CSharp.Tests\TestScripts";
            var reportPath = "comparison_table.txt";
            var knownDifferencesPath =  @"T:\_src\github.com\ERufian\ksharp\known_differences.txt";
            
            // Check if single test mode
            if (singleTest != null)
            {
                RunSingleTest(singleTest, wrapper, testScriptsPath, knownDifferencesPath);
                return;
            }
            
            // Load known differences
            var knownDifferences = new KnownDifferences(knownDifferencesPath);
            Console.WriteLine($"Loaded {knownDifferences.Count} known differences from {knownDifferencesPath}");
            
            try
            {
                // Get all .k files dynamically from the TestScripts folder
                var testFiles = Directory.GetFiles(testScriptsPath, "*.k")
                    .Select(Path.GetFileName)
                    .OrderBy(f => f)
                    .ToList();
                
                Console.WriteLine($"Found {testFiles.Count} test scripts in {testScriptsPath}");
                
                var results = new List<TestComparison>();
                int batchSize = 20; // Process in smaller batches to avoid timeouts
                int processed = 0;
                
                // Process in batches to avoid timeouts
                for (int i = 0; i < testFiles.Count; i += batchSize)
                {
                    var batch = testFiles.Skip(i).Take(batchSize).ToList();
                    Console.WriteLine($"\nProcessing batch {Math.Min(i + batchSize, testFiles.Count)}/{testFiles.Count}...");
                    
                    foreach (var fileName in batch)
                    {
                        processed++;
                        Console.Write($"{processed:D3}. {fileName,-50} ");
                        
                        var result = CompareTestFile(wrapper, fileName ?? "", testScriptsPath, knownDifferences);
                        results.Add(result);
                        
                        var statusSymbol = result.Status switch
                        {
                            ComparisonStatus.Matched => "✅",
                            ComparisonStatus.Differed => "❌", 
                            ComparisonStatus.Skipped => "⚠️",
                            ComparisonStatus.Error => "💥",
                            _ => "❓"
                        };
                        Console.WriteLine(statusSymbol);
                    }
                    
                    // Save progress after each batch
                    Console.WriteLine("Saving progress...");
                    GenerateComparisonReport(results, reportPath, false, testScriptsPath);
                }
                
                // Generate final complete report
                Console.WriteLine("\nGenerating final report...");
                GenerateComparisonReport(results, reportPath, true, testScriptsPath);
                
                // Print summary
                var summary = results.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
                Console.WriteLine($"\n🎉 COMPARISON COMPLETE!");
                Console.WriteLine($"================================");
                Console.WriteLine($"Total Tests:     {testFiles.Count}");
                Console.WriteLine($"✅ Matched:       {summary.GetValueOrDefault(ComparisonStatus.Matched, 0)}");
                Console.WriteLine($"❌ Differed:      {summary.GetValueOrDefault(ComparisonStatus.Differed, 0)}");
                Console.WriteLine($"⚠️  Skipped:       {summary.GetValueOrDefault(ComparisonStatus.Skipped, 0)}");
                Console.WriteLine($"💥 Errors:        {summary.GetValueOrDefault(ComparisonStatus.Error, 0)}");
                
                var successRate = summary.GetValueOrDefault(ComparisonStatus.Matched, 0) * 100.0 / (testFiles.Count - summary.GetValueOrDefault(ComparisonStatus.Skipped, 0));
                Console.WriteLine($"Success Rate:    {successRate:F1}%");
                Console.WriteLine($"Report saved to:  {reportPath}");
            }
            catch (Exception ex) { 
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            finally { 
                wrapper.CleanupTempDirectory();
            }
        }
        
        private static void RunSingleTest(string testName, KInterpreterWrapper wrapper, string testScriptsPath, string knownDifferencesPath)
        {
            // Load known differences
            var knownDifferences = new KnownDifferences(knownDifferencesPath);
            
            // Construct test file name (add .k extension if not present)
            var fileName = testName.EndsWith(".k") ? testName : $"{testName}.k";
            var testFilePath = Path.Combine(testScriptsPath, fileName);
            
            Console.WriteLine($"🔍 Running comparison for: {fileName}");
            Console.WriteLine("===================================================");
            
            if (!File.Exists(testFilePath))
            {
                Console.WriteLine($"❌ Test file not found: {testFilePath}");
                return;
            }
            
            try
            {
                var result = CompareTestFile(wrapper, fileName, testScriptsPath, knownDifferences);
                
                // Output full results to stdout without truncation
                Console.WriteLine($"K3Sharp: {result.K3SharpOutput}");
                Console.WriteLine($"k.exe:   {result.KOutput}");
                
                if (!string.IsNullOrEmpty(result.Notes))
                {
                    Console.WriteLine($"Notes:    {result.Notes}");
                }
                
                if (result.Status == ComparisonStatus.Error)
                {
                    Console.WriteLine($"Error:    {result.Message}");
                }
                
                var statusSymbol = result.Status switch
                {
                    ComparisonStatus.Matched => "✅",
                    ComparisonStatus.Differed => "❌", 
                    ComparisonStatus.Skipped => "⚠️",
                    ComparisonStatus.Error => "💥",
                    _ => "❓"
                };
                
                Console.WriteLine($"ℹ️  Result: {statusSymbol} {result.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Error running test: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                wrapper.CleanupTempDirectory();
            }
        }
        
        private static TestComparison CompareTestFile(KInterpreterWrapper wrapper, string fileName, string testScriptsPath, KnownDifferences knownDifferences)
        {
            var comparison = new TestComparison { FileName = fileName };
            try
            {
                var scriptContent = File.ReadAllText(Path.Combine(testScriptsPath, fileName));
                
                // Check for unsupported long integers (32-bit k.exe limitation)
                // Only skip if using k.exe, not e.exe
                if (KInterpreterWrapper.ContainsLongInteger(scriptContent) && wrapper.IsUsingKExe())
                {
                    comparison.Status = ComparisonStatus.Skipped;
                    comparison.Message = "Contains long integers";
                    comparison.Notes = "k.exe 32-bit limitation";
                    return comparison;
                }
                
                if (fileName.ToLower().StartsWith("ffi_"))
                {
                    comparison.Status = ComparisonStatus.Skipped;
                    comparison.Message = "FFI Test";
                    comparison.Notes = "k.exe doesn't support FFI";
                    return comparison;
                }
                
                if (scriptContent.Contains("_parse") || scriptContent.Contains("_eval") )
                {
                    comparison.Status = ComparisonStatus.Skipped;
                    comparison.Message = "Parse Tree Test";
                    comparison.Notes = "k.exe doesn't support _parse/_eval";
                    return comparison;
                }

                // Execute K3Sharp first to catch K3Sharp errors
                try
                {
                    comparison.K3SharpOutput = ExecuteK3Sharp(scriptContent);
                }
                catch (Exception k3SharpEx)
                {
                    // K3Sharp had an error
                    comparison.Status = ComparisonStatus.Error;
                    comparison.Message = $"K3Sharp Error: {k3SharpEx.Message}";
                    comparison.Notes = "K3Sharp error only";
                    
                    // Still try to execute k.exe to see if it also fails
                    try
                    {
                        comparison.KOutput = wrapper.ExecuteScript(scriptContent);
                        if (comparison.KOutput.StartsWith("UNSUPPORTED:"))
                        {
                            comparison.Notes = "Both K3Sharp and k.exe errors";
                        }
                        else
                        {
                            comparison.Notes = "K3Sharp error, k.exe succeeded";
                        }
                    }
                    catch (TimeoutException)
                    {
                        comparison.Notes = "K3Sharp error, k.exe timeout";
                    }
                    catch (Exception)
                    {
                        comparison.Notes = "Both K3Sharp and k.exe errors";
                    }
                    
                    return comparison;
                }
                
                // Execute k.exe
                try
                {
                    comparison.KOutput = wrapper.ExecuteScript(scriptContent);
                }
                catch (TimeoutException)
                {
                    // k.exe timed out but K3Sharp succeeded
                    comparison.Status = ComparisonStatus.Error;
                    comparison.Message = "k.exe execution timed out";
                    comparison.Notes = "Interpreter wrapper timeout";
                    return comparison;
                }
                catch (Exception kEx)
                {
                    // k.exe had an error but K3Sharp succeeded
                    comparison.Status = ComparisonStatus.Error;
                    comparison.Message = $"k.exe Error: {kEx.Message}";
                    comparison.Notes = "k.exe error only";
                    return comparison;
                }
                
                if (comparison.KOutput.StartsWith("UNSUPPORTED:"))
                {
                    comparison.Status = ComparisonStatus.Skipped;
                    comparison.Message = comparison.KOutput;
                    comparison.Notes = "k.exe limitation";
                    return comparison;
                }
                
                // Apply known differences to k.exe output if applicable
                var testNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var adjustedKOutput = knownDifferences.ApplyTweaks(comparison.KOutput, testNameWithoutExtension);
                
                // Store notes if we applied known differences
                if (knownDifferences.HasDifference(testNameWithoutExtension))
                {
                    var difference = knownDifferences.GetDifference(testNameWithoutExtension);
                    if (difference != null)
                    {
                        var existingNotes = difference.Notes;
                        comparison.Notes = string.IsNullOrEmpty(existingNotes) ? "Known difference" : existingNotes;
                        // Store the adjusted output for reporting
                        comparison.KOutput = adjustedKOutput;
                    }
                }
                
                // Strict comparison - no format relaxation
                comparison.Status = comparison.K3SharpOutput.Trim() == adjustedKOutput.Trim() 
                    ? ComparisonStatus.Matched : ComparisonStatus.Differed;
            }
            catch (Exception ex) { 
                comparison.Status = ComparisonStatus.Error; 
                comparison.Message = ex.Message;
                comparison.Notes = "Unexpected comparison error";
            }
            return comparison;
        }
        
        private static string ExecuteK3Sharp(string scriptContent)
        {
            // Set PROMPT environment variable to "$P$G" for consistency with test expectations
            Environment.SetEnvironmentVariable("PROMPT", "$P$G");
            
            var evaluator = new Evaluator();
            
            // Reset K tree before each test to ensure isolation
            evaluator.ResetKTree();
            
            var lines = scriptContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            K3Value? lastResult = null;
            string accumulatedLine = "";
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("//"))
                    continue;
                
                // Handle special commands
                if (trimmedLine.StartsWith("\\seed "))
                {
                    var parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int newSeed))
                    {
                        Evaluator.RandomSeed = newSeed;
                    }
                    continue;
                }
                
                accumulatedLine += line;
                
                // Check if the expression is incomplete (unbalanced delimiters) before attempting to parse.
                // Uses source-level scan to handle unterminated strings without Lexer exception.
                if (ParserConfig.IsSourceIncomplete(accumulatedLine))
                {
                    if (!accumulatedLine.EndsWith("\n"))
                        accumulatedLine += "\n";
                    continue;
                }
                
                // Try to parse and execute the accumulated line
                try
                {
                    var lexer = new Lexer(accumulatedLine);
                    var tokens = lexer.Tokenize();
                    // Preprocess for implicit indexing/apply
                    tokens = lexer.PreprocessImplicitIndexing(tokens);
                    var ast = ParserConfig.ParseWithConfig(tokens, accumulatedLine);
                    
                    if (ast != null)
                    {
                        lastResult = evaluator.Evaluate(ast);
                        accumulatedLine = "";
                    }
                }
                catch
                {
                    // If parsing/evaluation fails, continue accumulating lines
                    if (!accumulatedLine.EndsWith("\n"))
                    {
                        accumulatedLine += "\n";
                    }
                }
            }
            
            return lastResult?.ToString() ?? new NullValue().ToString();
        }
        
        private static void GenerateComparisonReport(List<TestComparison> results, string reportPath, bool isFinal, string testScriptsPath)
        {
            using var writer = new StreamWriter(reportPath, append: !isFinal);
            
            if (isFinal)
            {
                writer.WriteLine("K3Sharp vs k.exe Comparison Report");
                writer.WriteLine("=================================");
                writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine();
                
                // Summary table
                var summary = results.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
                writer.WriteLine("SUMMARY:");
                writer.WriteLine("--------");
                writer.WriteLine($"Total Tests:     {results.Count}");
                writer.WriteLine($"✅ Matched:       {summary.GetValueOrDefault(ComparisonStatus.Matched, 0)}");
                writer.WriteLine($"❌ Differed:      {summary.GetValueOrDefault(ComparisonStatus.Differed, 0)}");
                writer.WriteLine($"⚠️  Skipped:       {summary.GetValueOrDefault(ComparisonStatus.Skipped, 0)}");
                writer.WriteLine($"💥 Errors:        {summary.GetValueOrDefault(ComparisonStatus.Error, 0)}");
                
                var successRate = summary.GetValueOrDefault(ComparisonStatus.Matched, 0) * 100.0 / (results.Count - summary.GetValueOrDefault(ComparisonStatus.Skipped, 0));
                writer.WriteLine($"Success Rate:    {successRate:F1}%");
                writer.WriteLine();
                
                // Detailed results table
                writer.WriteLine("DETAILED RESULTS:");
                writer.WriteLine("-----------------");
                writer.WriteLine("Status".PadRight(8) + " " + "Test File".PadRight(50) + " " + "K3Sharp Output".PadRight(30) + " " + "k.exe Output".PadRight(30) + " " + "Notes".PadRight(50));
                writer.WriteLine(new string('-', 170));
                
                foreach (var result in results.OrderBy(r => r.FileName))
                {
                    var status = result.Status switch
                    {
                        ComparisonStatus.Matched => "✅ PASS",
                        ComparisonStatus.Differed => "❌ FAIL", 
                        ComparisonStatus.Skipped => "⚠️  SKIP",
                        ComparisonStatus.Error => "💥 ERROR",
                        _ => "❓ UNKNOWN"
                    };
                    
                    var k3SharpOutput = TruncateString(result.K3SharpOutput, 28);
                    var kOutput = TruncateString(result.KOutput, 28);
                    var notes = TruncateString(result.Notes, 48);
                    
                    writer.WriteLine($"{status.PadRight(8)} {result.FileName.PadRight(50)} {k3SharpOutput.PadRight(30)} {kOutput.PadRight(30)} {notes.PadRight(50)}");
                }
                
                writer.WriteLine();
                writer.WriteLine("FAILED TESTS DETAILS:");
                writer.WriteLine("--------------------");
                
                var failedTests = results.Where(r => r.Status == ComparisonStatus.Differed).ToList();
                if (failedTests.Any())
                {
                    foreach (var test in failedTests.Take(20)) // Limit to first 20 failed tests
                    {
                        writer.WriteLine($"File: {test.FileName}");
                        writer.WriteLine($"K3Sharp: {test.K3SharpOutput}");
                        writer.WriteLine($"k.exe:   {test.KOutput}");
                        writer.WriteLine();
                    }
                    if (failedTests.Count > 20)
                    {
                        writer.WriteLine($"... and {failedTests.Count - 20} more failed tests");
                    }
                }
                else
                {
                    writer.WriteLine("No failed tests found!");
                }
                
                writer.WriteLine();
                writer.WriteLine("ERROR TESTS DETAILS:");
                writer.WriteLine("-------------------");
                
                var errorTests = results.Where(r => r.Status == ComparisonStatus.Error).ToList();
                if (errorTests.Any())
                {
                    foreach (var test in errorTests.Take(10)) // Limit to first 10 error tests
                    {
                        writer.WriteLine($"File: {test.FileName}");
                        writer.WriteLine($"Error: {test.Message}");
                        writer.WriteLine();
                    }
                    if (errorTests.Count > 10)
                    {
                        writer.WriteLine($"... and {errorTests.Count - 10} more error tests");
                    }
                }
                else
                {
                    writer.WriteLine("No error tests found!");
                }
                
                writer.WriteLine();
                writer.WriteLine("K.EXE FAILING TESTS:");
                writer.WriteLine("--------------------");
                
                var kFailingTests = results.Where(r => 
                    r.Status == ComparisonStatus.Error && (
                        r.Notes.Contains("Both K3Sharp and k.exe errors") ||
                        r.Notes.Contains("k.exe error only") ||
                        r.Notes.Contains("k.exe timeout")
                        )
                    ).ToList();
                
                if (kFailingTests.Any())
                {
                    foreach (var test in kFailingTests)
                    {
                        writer.WriteLine($"Test: {test.FileName}");
                        try
                        {
                            var testFilePath = Path.Combine(testScriptsPath, test.FileName);
                            var testInput = File.ReadAllText(testFilePath).Trim();
                            writer.WriteLine($"Input: {testInput}");
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"Input: [Error reading file: {ex.Message}]");
                        }
                        writer.WriteLine($"Error: {test.Message}");
                        writer.WriteLine();
                    }
                }
                else
                {
                    writer.WriteLine("No tests failing in k.exe found!");
                }
            }
            else
            {
                    // Progress update
                writer.WriteLine($"Progress update: {results.Count} tests processed at {DateTime.Now:HH:mm:ss}");
            }
        }
        
        private static string TruncateString(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > maxLength ? s.AsSpan(0, maxLength - 3).ToString() + "..." : s;
        }
        
        private static void ShowHelp()
        {
            Console.WriteLine("K3Sharp vs K Comparison Tool - Usage Guide");
            Console.WriteLine("==========================================");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  --32bit, --kexe    Run with 32-bit k.exe (skips long integer tests)");
            Console.WriteLine("  --64bit, --k       Run with 64-bit K (e.exe) - default behavior");
            Console.WriteLine("  --help, -h         Show this help message");
            Console.WriteLine();
            Console.WriteLine("SINGLE TEST MODE:");
            Console.WriteLine("  [test_name]        Run a single test comparison");
            Console.WriteLine("  --32bit [test]    Run single test with 32-bit k.exe");
            Console.WriteLine("  --64bit [test]     Run single test with 64-bit K (e.exe)");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  dotnet run                                          # Run all tests with default (e.exe)");
            Console.WriteLine("  dotnet run --32bit                                  # Run all tests with 32-bit k.exe");
            Console.WriteLine("  dotnet run integer_types_long.k                    # Run single test with default");
            Console.WriteLine("  dotnet run --32bit integer_types_long.k            # Run single test with k.exe");
            Console.WriteLine("  dotnet run --help                                   # Show help");
            Console.WriteLine();
            Console.WriteLine("NOTES:");
            Console.WriteLine("  - Default behavior: Uses e.exe if available, falls back to k.exe");
            Console.WriteLine("  - Long integer tests are automatically skipped when using k.exe");
            Console.WriteLine("  - When using e.exe, long integer tests run and compare results");
        }
    }
    
    public class TestComparison
    {
        public string FileName { get; set; } = "";
        public string K3SharpOutput { get; set; } = "";
        public string KOutput { get; set; } = "";
        public ComparisonStatus Status { get; set; }
        public string Message { get; set; } = "";
        public string Notes { get; set; } = "";
    }
    
    public enum ComparisonStatus { Matched, Differed, Skipped, Error }
}
