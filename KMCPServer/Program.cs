// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.IO;
using System.Linq;
using K3CSharp;
using K3CSharp.Parsing;

namespace KMCPServer
{
    class Program
    {
        static int Main(string[] args)
        {
            var server = new KMCPServer();
            
            // Check for --mcp flag to enable MCP mode
            bool mcpMode = args.Contains("--mcp");
            
            if (mcpMode)
            {
                Console.WriteLine("Starting K.exe MCP Server...");
                Console.WriteLine("Press Ctrl+C to stop the server");
                
                // Parse default interpreter and timeout for MCP mode
                string? interpreter = null;
                int timeout = 2000; // Default timeout
                
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--interpreter":
                            if (i + 1 < args.Length)
                            {
                                interpreter = args[++i];
                            }
                            break;
                        case "--timeout":
                            if (i + 1 < args.Length && int.TryParse(args[++i], out int t))
                            {
                                timeout = t * 1000; // Convert to milliseconds
                            }
                            break;
                    }
                }
                
                var mcpServer = new KMCPServer(interpreter, timeout);
                mcpServer.Start();
                return 0;
            }
            else
            {
                // Standalone mode - parse and execute command
                var result = ExecuteStandalone(args);
                Console.WriteLine(result);
                return 0;
            }
        }

        static string ExecuteStandalone(string[] args)
        {
            string? command = null;
            string? script = null;
            int timeout = 2000; // Default 2 seconds as per specification
            string? interpreter = null;
            string? passthrough = null;

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--command":
                        if (i + 1 < args.Length)
                        {
                            command = JsonEscapeHelper.ProcessJsonEscapes(args[++i]);
                        }
                        break;
                    case "--script":
                        if (i + 1 < args.Length)
                        {
                            script = File.ReadAllText(JsonEscapeHelper.ProcessJsonEscapes(args[++i]));
                        }
                        break;
                    case "--timeout":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int t))
                        {
                            timeout = t * 1000; // Convert to milliseconds
                        }
                        break;
                    case "--interpreter":
                        if (i + 1 < args.Length)
                        {
                            interpreter = args[++i];
                        }
                        break;
                    case "--passthrough":
                        if (i + 1 < args.Length)
                        {
                            passthrough = args[++i];
                        }
                        break;
                    case "--mcp":
                        // MCP mode - handled by the initial check
                        break;
                    default:
                        // Unknown option - add to passthrough
                        if (passthrough == null)
                        {
                            passthrough = args[i];
                        }
                        else
                        {
                            passthrough += " " + args[i];
                        }
                        break;
                }
            }

            // Create wrapper with custom settings
            var kPath = GetKInterpreterPath(interpreter ?? "");
            var wrapper = new KInterpreterWrapper(kPath, timeout);

            try
            {
                if (command != null)
                {
                    return wrapper.ExecuteScript(command ?? "");
                }
                else
                {
                    return wrapper.ExecuteScript(script ?? "");
                }
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
        }

        static string GetKInterpreterPath(string customPath)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                return customPath;
            }

            // Check K3HOME environment variable
            var k3Home = Environment.GetEnvironmentVariable("K3HOME");
            if (!string.IsNullOrEmpty(k3Home))
            {
                var ePath = Path.Combine(k3Home, "e.exe");
                if (File.Exists(ePath))
                {
                    return ePath;
                }
                var kPath = Path.Combine(k3Home, "k.exe");
                if (File.Exists(kPath))
                {
                    return kPath;
                }
            }

            // Default paths
            if (File.Exists(@"c:\k\e.exe"))
            {
                return @"c:\k\e.exe";
            }
            if (File.Exists(@"c:\k\k.exe"))
            {
                return @"c:\k\k.exe";
            }

            throw new FileNotFoundException("K interpreter not found. Please install k.exe or set K3HOME environment variable.");
        }
    }
}

