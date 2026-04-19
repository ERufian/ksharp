using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using K3CSharp.Parsing;

namespace K3CSharp
{
    class Program
    {
        private static readonly List<string> commandHistory = new List<string>();

        // Sentinel used in --mcp mode for reliable output framing.
        public const string McpSentinel = "\x01\x02";

        private sealed class RuntimeOptions
        {
            public bool McpMode { get; init; }
            public int? IpcPort { get; init; }
            public string? ScriptPath { get; init; }
        }

        static int Main(string[] args)
        {
            RuntimeOptions options = ParseArgs(args);
            var evaluator = new Evaluator();

            ConsoleCancelEventHandler cancelHandler = (_, e) =>
            {
                evaluator.RequestExit(0);
                e.Cancel = true;
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                if (options.IpcPort.HasValue)
                {
                    evaluator.StartIpcServer(options.IpcPort.Value);
                }

                if (options.ScriptPath != null)
                {
                    RunScriptMode(options.ScriptPath, evaluator, keepServing: options.IpcPort.HasValue);
                    return evaluator.ExitCode;
                }

                if (options.McpMode)
                {
                    RunMcpRepl(evaluator);
                    return evaluator.ExitCode;
                }

                RunRepl(evaluator);
                return evaluator.ExitCode;
            }
            catch (K3ExitException ex)
            {
                return ex.ExitCode;
            }
            finally
            {
                evaluator.StopIpcServer();
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static RuntimeOptions ParseArgs(string[] args)
        {
            bool mcpMode = false;
            int? ipcPort = null;
            string? scriptPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--mcp":
                        mcpMode = true;
                        break;

                    case "-i":
                        if (i + 1 >= args.Length || !int.TryParse(args[++i], out int port))
                        {
                            throw new ArgumentException("Usage: ksharp [-i PORT] [--mcp] [script.k]");
                        }
                        ipcPort = port;
                        break;

                    default:
                        if (scriptPath != null)
                        {
                            throw new ArgumentException("Only one script path may be provided.");
                        }
                        scriptPath = args[i];
                        break;
                }
            }

            if (mcpMode && scriptPath != null)
            {
                throw new ArgumentException("Script execution and --mcp mode cannot be combined.");
            }

            return new RuntimeOptions
            {
                McpMode = mcpMode,
                IpcPort = ipcPort,
                ScriptPath = scriptPath,
            };
        }

        private static void RunScriptMode(string scriptPath, Evaluator evaluator, bool keepServing)
        {
            try
            {
                string normalizedPath = scriptPath;
                if (!Path.HasExtension(normalizedPath))
                {
                    normalizedPath += ".k";
                }

                var content = File.ReadAllText(normalizedPath);
                var result = ExecuteLine(content, evaluator);
                if (result is not NullValue)
                {
                    Console.WriteLine(result);
                }
            }
            catch (K3ExitException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            if (keepServing && !evaluator.IsExitRequested)
            {
                evaluator.WaitForShutdown();
            }
        }

        private static void RunRepl(Evaluator evaluator)
        {
            Console.WriteLine("K3 Interpreter - Version 1.0");
            Console.WriteLine("Type \\\\ to quit, \\ to cancel input, or \\help for help");
            Console.WriteLine("Use arrow keys for history, Ctrl+C to clear line");
            Console.WriteLine();

            while (!evaluator.IsExitRequested)
            {
                Console.Write("  "); // Default prompt: two spaces

                var input = ReadLineOrShutdown(evaluator);
                if (input == null) break;
                if (input == "\\\\")
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }
                if (string.IsNullOrWhiteSpace(input)) continue;

                if (commandHistory.Count == 0 || commandHistory[^1] != input)
                {
                    commandHistory.Add(input);
                    if (commandHistory.Count > 100)
                    {
                        commandHistory.RemoveAt(0);
                    }
                }

                try
                {
                    var result = ExecuteLine(input, evaluator);
                    Console.WriteLine(result);
                }
                catch (K3ExitException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// MCP-friendly REPL: no banner, sentinel-delimited output, stdout flushed after each response.
        /// </summary>
        static void RunMcpRepl(Evaluator evaluator)
        {
            // Signal ready
            Console.Out.Write(McpSentinel);
            Console.Out.Flush();

            string? line;
            while (!evaluator.IsExitRequested && (line = ReadLineOrShutdown(evaluator)) != null)
            {
                if (line == "\\\\") break;
                if (string.IsNullOrWhiteSpace(line))
                {
                    Console.Out.Write(McpSentinel);
                    Console.Out.Flush();
                    continue;
                }

                try
                {
                    var result = ExecuteLine(line, evaluator);
                    var text = result.ToString() ?? "";
                    if (text.Length > 0)
                        Console.Out.Write(text);
                }
                catch (K3ExitException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.Out.Write($"Error: {ex.Message}");
                }

                // Sentinel marks end of this response
                Console.Out.Write(McpSentinel);
                Console.Out.Flush();
            }
        }

        static string? ReadLineOrShutdown(Evaluator evaluator)
        {
            var readTask = Task.Run(Console.ReadLine);
            int signaled = WaitHandle.WaitAny(new[] { ((IAsyncResult)readTask).AsyncWaitHandle, evaluator.GetShutdownWaitHandle() });
            return signaled == 0 ? readTask.Result : null;
        }

        public static K3Value ExecuteLine(string input, Evaluator evaluator)
        {
            lock (evaluator.GetEvaluationLock())
            {
                // Handle REPL commands (prefixed with backslash)
                var trimmedInput = input.Trim().Trim('\uFEFF', '\u200B'); // Remove BOM and zero-width spaces
                if (trimmedInput.StartsWith("\\"))
                {
                    return HandleReplCommand(trimmedInput, evaluator);
                }
                
                var lexer = new Lexer(input);
                var tokens = lexer.Tokenize();
                
                // Use ParserConfig for consistent parser selection
                var ast = ParserConfig.ParseWithConfig(tokens, input);
                return evaluator.Evaluate(ast) ?? new NullValue();
            }
        }

        public static K3Value HandleReplCommand(string command, Evaluator evaluator)
        {
            // Split only on the first space to preserve the rest as an argument
            var parts = command.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0];
            var arg = parts.Length > 1 ? parts[1].Trim() : null;

            switch (cmd)
            {
                case "\\?":
                    // Concise help listing of all system commands
                    Console.WriteLine("\\?         help (this list)");
                    Console.WriteLine("\\l path    load script");
                    Console.WriteLine("\\d [path]  show/set k-tree directory");
                    Console.WriteLine("\\v [path]  list variables");
                    Console.WriteLine("\\t expr    time expression (ms)");
                    Console.WriteLine("\\r [seed]  show/set random seed");
                    Console.WriteLine("\\p [n]     show/set print precision");
                    Console.WriteLine("\\cd [path] show/set OS directory");
                    Console.WriteLine("\\9         reset k-tree & seed");
                    Console.WriteLine("\\^         go to parent branch");
                    Console.WriteLine("\\\\         exit");
                    Console.WriteLine("\\cmd       execute OS command");
                    break;

                case "\\d^":
                    // Set current branch to parent
                    evaluator.SetParentBranch();
                    break;

                case "\\d":
                    if (arg == null)
                    {
                        // Display current branch
                        var currentBranch = evaluator.DirFunction(new NullValue());
                        if (currentBranch is SymbolValue sym && !string.IsNullOrEmpty(sym.Value))
                        {
                            Console.WriteLine(sym.Value);
                        }
                        // Root branch - display nothing per spec
                    }
                    else if (0 == string.CompareOrdinal("^", arg))
                    {
                        // Set current branch to parent
                        evaluator.SetParentBranch();
                    }
                    else
                    {
                        // Set current branch
                        evaluator.SetCurrentBranch(arg);
                    }
                    break;

                case "\\v":
                    // List variable names in a k-tree branch (current branch if no arg)
                    var names = arg != null
                        ? evaluator.GetBranchVariableNames(arg)
                        : evaluator.GetCurrentBranchVariableNames();
                    if (names.Count > 0)
                    {
                        names.Sort(StringComparer.Ordinal);
                        Console.WriteLine(string.Join(" ", names));
                    }
                    break;

                case "\\l":
                    // Load and execute a K script file
                    if (arg == null)
                    {
                        Console.WriteLine("Usage: \\l path");
                    }
                    else
                    {
                        LoadScript(arg, evaluator);
                    }
                    break;

                case "\\t":
                    // Time expression execution
                    if (arg == null)
                    {
                        Console.WriteLine("Usage: \\t expression");
                    }
                    else
                    {
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            // Execute the expression (result is discarded)
                            var lexer = new Lexer(arg);
                            var tokens = lexer.Tokenize();
                            var lrsParser = new LRSParser(tokens);
                            var position = 0;
                            var ast = lrsParser.ParseExpression(ref position);
                            evaluator.Evaluate(ast);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                        sw.Stop();
                        Console.WriteLine(sw.ElapsedMilliseconds);
                    }
                    break;

                case "\\cd":
                    if (arg == null)
                    {
                        // Display current OS working directory
                        Console.WriteLine(Directory.GetCurrentDirectory());
                    }
                    else
                    {
                        // Change OS working directory
                        try
                        {
                            Directory.SetCurrentDirectory(arg);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    break;

                case "\\9":
                    // Reset K tree to default state (for testing purposes)
                    evaluator.ResetKTree();
                    Evaluator.RandomSeed = -314159;
                    break;

                case "\\p":
                    if (arg == null)
                    {
                        Console.WriteLine(Evaluator.floatPrecision);
                    }
                    else
                    {
                        if (int.TryParse(arg, out int newPrecision))
                        {
                            Evaluator.floatPrecision = newPrecision;
                        }
                        else
                        {
                            Console.WriteLine("Usage: \\p [number]");
                        }
                    }
                    break;

                case "\\r":
                    if (arg == null)
                    {
                        Console.WriteLine(Evaluator.RandomSeed);
                    }
                    else
                    {
                        if (int.TryParse(arg, out int newSeed))
                        {
                            Evaluator.RandomSeed = newSeed;
                        }
                        else
                        {
                            Console.WriteLine("Usage: \\r [number]");
                        }
                    }
                    break;

                case "\\":
                    // Display top level help message
                    Console.WriteLine("\\  help (this page)");
                    Console.WriteLine("\\? system commands");
                    Console.WriteLine("\\0 data types");
                    Console.WriteLine("\\+ arithmetic and basic verbs");
                    Console.WriteLine("\\' adverbs");
                    Console.WriteLine("\\. control flow, assignment and debug");
                    Console.WriteLine("\\_ underscore verbs (math, linear, etc.)");
                    Console.WriteLine("\\\\ exit");
                    break;

                case "\\0":
                    Console.WriteLine("int long float char symbol vector dictionary null");
                    Console.WriteLine("4: type");
                    Console.WriteLine("0i 0n -0i inf NaN -inf");
                    break;

                case "\\+":
                    Console.WriteLine("arithmetic: + - * % < > = & | ^");
                    Console.WriteLine("Monadic: - + * % & | < > ^ ! , # _ ? ~ @ . = $");
                    Console.WriteLine("Dyadic: ! _ @ . :: $");
                    Console.WriteLine("indexing: @_n []");
                    Console.WriteLine("dict: !dict .dict");
                    Console.WriteLine("format: $value format${}expr");
                    Console.WriteLine("assignment: :");
                    Console.WriteLine("see \\_ for math, linear algebra, and system information verbs");
                    break;

                case "\\'":
                    Console.WriteLine("/ over reduce");
                    Console.WriteLine("\\ scan");
                    Console.WriteLine("' each");
                    Console.WriteLine("/: each-right");
                    Console.WriteLine("\\: each-left");
                    Console.WriteLine("': each-prior");
                    break;

                case "\\.":
                    Console.WriteLine("assignment:");
                    Console.WriteLine("  name: value        / local assignment");
                    Console.WriteLine("  name:: value       / global assignment");
                    Console.WriteLine("  {[x;y] x+y}       / function definition");
                    Console.WriteLine();
                    Console.WriteLine("control flow:");
                    Console.WriteLine("  if[cond; expr]     / conditional");
                    Console.WriteLine("  while[cond; expr]  / loop");
                    Console.WriteLine("  do[n; expr]        / repeat");
                    Console.WriteLine("  :[c; t; f]         / cond expression");
                    break;

                case "\\_":
                    Console.WriteLine("Math:");
                    Console.WriteLine("  _log   _exp   _abs   _sqrt");
                    Console.WriteLine("  _sin   _cos   _tan");
                    Console.WriteLine("  _dot   _mul   _inv");
                    Console.WriteLine();
                    Console.WriteLine("Rand:");
                    Console.WriteLine("  _draw");
                    Console.WriteLine();
                    Console.WriteLine("Time:");
                    Console.WriteLine("  _t     _gtime  _ltime  _lt  _jd  _dj");
                    Console.WriteLine();
                    Console.WriteLine("List:");
                    Console.WriteLine("  _sv    _vs    _ss     _dv    _di");
                    Console.WriteLine("  _getenv _setenv _size   _exit");
                    Console.WriteLine();
                    Console.WriteLine("Vars:");
                    Console.WriteLine("  _d    _v    _i    _f    _n    _s    _h    _p    _P");
                    Console.WriteLine("  _w    _u    _a    _k    _o    _c    _r    _m    _y");
                    Console.WriteLine();
                    Console.WriteLine("see \\+ for arithmetic and binary verbs");
                    break;

                default:
                    // Any unrecognized backslash command: execute as OS shell command
                    var shellCmd = command.Substring(1); // Strip leading backslash
                    if (string.IsNullOrWhiteSpace(shellCmd))
                        break;
                    ExecuteShellCommand(shellCmd);
                    break;
            }
            return new NullValue();
        }

        private static void LoadScript(string path, Evaluator evaluator)
        {
            // Append .k extension if not present
            if (!Path.HasExtension(path))
                path = path + ".k";

            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: file not found: {path}");
                return;
            }

            try
            {
                var content = File.ReadAllText(path);
                // Execute the entire file content as a single unit (same as file execution mode)
                var lexer = new Lexer(content);
                var tokens = lexer.Tokenize();
                var lrsParser = new LRSParser(tokens);
                var position = 0;
                var ast = lrsParser.ParseExpression(ref position);
                var result = evaluator.Evaluate(ast);
                // Print result only if non-null
                if (result != null && !(result is NullValue))
                {
                    Console.WriteLine(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading {path}: {ex.Message}");
            }
        }

        private static void ExecuteShellCommand(string command)
        {
            try
            {
                string shell, shellArgs;
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                        System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    shell = "cmd.exe";
                    shellArgs = "/c " + command;
                }
                else
                {
                    shell = "/bin/sh";
                    shellArgs = "-c " + command;
                }

                var psi = new ProcessStartInfo(shell, shellArgs)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var stdout = proc.StandardOutput.ReadToEnd();
                    var stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (!string.IsNullOrEmpty(stdout))
                        Console.Write(stdout);
                    if (!string.IsNullOrEmpty(stderr))
                        Console.Write(stderr);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
