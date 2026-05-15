// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using K3CSharp.Parsing;

namespace ApplyTweaks;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            bool mcpMode = false;
            bool showHelp = false;
            string? filePath = null;
            string? id = null;
            string? inputString = null;
            
            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--mcp":
                        mcpMode = true;
                        break;
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    case "--file":
                        if (i + 1 >= args.Length) throw new ArgumentException("--file option requires a path argument");
                        filePath = args[++i];
                        break;
                    case "--id":
                        if (i + 1 >= args.Length) throw new ArgumentException("--id option requires an identifier argument");
                        id = args[++i];
                        break;
                    case "--string":
                        if (i + 1 >= args.Length) throw new ArgumentException("--string option requires a text argument");
                        inputString = args[++i];
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {args[i]}");
                }
            }
            
            if (showHelp)
            {
                ShowHelp();
                return;
            }
            
            if (mcpMode)
            {
                // Run as MCP server
                var server = new ApplyTweaksServer();
                server.Start();
                return;
            }
            
            // Standalone mode
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = @"T:\_src\github.com\ERufian\ksharp\known_differences.txt";
            }
            
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(inputString))
            {
                throw new ArgumentException("Both --id and --string options are required for standalone mode");
            }
            
            // Handle escape character for double quotes
            inputString = JsonEscapeHelper.ProcessJsonEscapes(inputString);
            
            var knownDifferences = new KnownDifferences();
            knownDifferences.LoadFromFile(filePath);
            
            var result = knownDifferences.ApplyTweaks(inputString, id);
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    static void ShowHelp()
    {
        Console.WriteLine("ApplyTweaks - Apply known differences to strings");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ApplyTweaks.exe --id <identifier> --string <text>     Apply tweaks to string");
        Console.WriteLine("  ApplyTweaks.exe --file <path> --id <identifier> --string <text>  Apply tweaks using custom file");
        Console.WriteLine("  ApplyTweaks.exe --mcp                              Start MCP server mode");
        Console.WriteLine("  ApplyTweaks.exe --help                              Show this help");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --id <identifier>     Tweak ID to apply");
        Console.WriteLine("  --string <text>       String to apply tweaks to");
        Console.WriteLine("  --file <path>        Custom tweaks file path");
        Console.WriteLine("  --mcp                Run as MCP server");
        Console.WriteLine("  --help               Show help");
        Console.WriteLine();
        Console.WriteLine("Escape character: Use ´ for double quotes in strings");
    }
}

public class KnownDifference
{
    public string TestName { get; set; } = "";
    public List<string> Tweaks { get; set; } = new();
    public string Notes { get; set; } = "";
}

public class KnownDifferences
{
    private readonly Dictionary<string, KnownDifference> _differences = new();
    public int Count => _differences.Count;
    private string? _lastLoadedFile = null;
    
    public KnownDifferences()
    {
        // Don't load any file by default - wait for explicit load request
    }
    
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) 
        {
            throw new FileNotFoundException($"Tweaks file not found: {filePath}");
        }
        
        _differences.Clear();
        
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
        
        _lastLoadedFile = filePath;
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
    
    public string? GetLastLoadedFile() => _lastLoadedFile;
    
    public void Reload()
    {
        if (string.IsNullOrEmpty(_lastLoadedFile))
        {
            throw new InvalidOperationException("No file has been loaded to reload");
        }
        LoadFromFile(_lastLoadedFile!);
    }
}

public class ApplyTweaksServer
{
    private readonly KnownDifferences _knownDifferences;
    
    public ApplyTweaksServer()
    {
        _knownDifferences = new KnownDifferences();
        // Load default file for MCP server mode
        try
        {
            var defaultPath = @"T:\_src\github.com\ERufian\ksharp\known_differences.txt";
            _knownDifferences.LoadFromFile(defaultPath);
        }
        catch
        {
            // If default file not found, continue without loading - tools will handle loading
        }
    }
    
    public void Start()
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrEmpty(line)) continue;
            
            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, JsonSerializerSettings.Input);
                if (request == null) continue;
                
                var response = ProcessRequest(request);
                if (response != null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(response, JsonSerializerSettings.Output));
                    Console.Out.Flush();
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new JsonRpcResponse 
                { 
                    id = "", 
                    error = new JsonRpcError { code = -32700, message = ex.Message }
                };
                Console.WriteLine(JsonSerializer.Serialize(errorResponse, JsonSerializerSettings.Output));
                Console.Out.Flush();
            }
        }
    }
    
    public JsonRpcResponse? ProcessRequest(JsonRpcRequest request)
    {
        try
        {
            return request.method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => HandleToolCall(request),
                "notifications/initialized" => null, // No response needed for notifications
                _ => new JsonRpcResponse 
                { 
                    id = request.id, 
                    error = new JsonRpcError { code = -32601, message = "Method not found" }
                }
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse 
            { 
                id = request.id, 
                error = new JsonRpcError { code = -32603, message = ex.Message }
            };
        }
    }
    
    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        return new JsonRpcResponse 
        { 
            id = request.id, 
            result = new 
            { 
                protocolVersion = "2024-11-05", 
                capabilities = new 
                { 
                    tools = new { } 
                }, 
                serverInfo = new 
                { 
                    name = "ApplyTweaks", 
                    version = "1.0.0" 
                } 
            } 
        };
    }
    
    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new object[]
        {
            new
            {
                name = "tweakstring",
                description = "Apply tweaks to a string using an ID",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new
                        {
                            type = "string",
                            description = "The tweak ID to apply"
                        },
                        @string = new
                        {
                            type = "string", 
                            description = "The string to apply tweaks to"
                        }
                    },
                    required = new[] { "id", "string" }
                }
            },
            new
            {
                name = "load",
                description = "Load tweaks from a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        file = new
                        {
                            type = "string",
                            description = "Path to the tweaks file (absolute or relative to current directory)"
                        }
                    },
                    required = new[] { "file" }
                }
            },
            new
            {
                name = "reload",
                description = "Reload the last loaded tweaks file",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            }
        };

        return new JsonRpcResponse 
        { 
            id = request.id, 
            result = new Dictionary<string, object> 
            { 
                ["tools"] = tools 
            } 
        };
    }
    
    private JsonRpcResponse HandleToolCall(JsonRpcRequest request)
    {
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(request.@params?.ToString() ?? "", JsonSerializerSettings.Input);
        var toolName = arguments?["name"]?.ToString() ?? "";
        
        // Extract the actual tool arguments from the nested structure
        var toolArgs = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments?["arguments"]?.ToString() ?? "", JsonSerializerSettings.Input);
        
        try
        {
            var result = toolName switch
            {
                "tweakstring" => ExecuteTweakString(toolArgs),
                "load" => ExecuteLoad(toolArgs),
                "reload" => ExecuteReload(),
                _ => throw new Exception($"Unknown tool: {toolName}")
            };

            return new JsonRpcResponse 
            { 
                id = request.id, 
                result = new Dictionary<string, object> 
                { 
                    ["content"] = new object[]
                    {
                        new Dictionary<string, object> 
                        { 
                            ["type"] = "text", 
                            ["text"] = result 
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse 
            { 
                id = request.id, 
                result = new Dictionary<string, object> 
                { 
                    ["content"] = new object[]
                    {
                        new Dictionary<string, object> 
                        { 
                            ["type"] = "text", 
                            ["text"] = $"Error: {ex.Message}",
                            ["isError"] = true
                        }
                    }
                }
            };
        }
    }
    
    private string ExecuteTweakString(Dictionary<string, object>? arguments)
    {
        if (arguments == null) throw new Exception("Arguments required");
        
        var id = arguments?["id"]?.ToString();
        var input = arguments?["string"]?.ToString();
        
        if (string.IsNullOrEmpty(id)) throw new Exception("ID parameter required");
        if (input == null) throw new Exception("String parameter required");
        
        // Handle escape character for double quotes
        input = JsonEscapeHelper.ProcessJsonEscapes(input);
        
        var result = _knownDifferences.ApplyTweaks(input, id);
        
        // If no match found, return unmodified string
        if (!_knownDifferences.HasDifference(id))
        {
            return input;
        }
        
        return result;
    }
    
    private string ExecuteLoad(Dictionary<string, object>? arguments)
    {
        if (arguments == null) throw new Exception("Arguments required");
        
        var filePath = arguments?["file"]?.ToString();
        
        if (string.IsNullOrEmpty(filePath)) throw new Exception("File parameter required");
        
        _knownDifferences.LoadFromFile(filePath);
        return $"Loaded {_knownDifferences.Count} tweaks from {filePath}";
    }
    
    private string ExecuteReload()
    {
        var lastFile = _knownDifferences.GetLastLoadedFile();
        if (string.IsNullOrEmpty(lastFile))
        {
            throw new Exception("No file has been loaded to reload");
        }
        
        _knownDifferences.Reload();
        return $"Reloaded {_knownDifferences.Count} tweaks from {lastFile}";
    }
}

public sealed class JsonRpcRequest
{
    public string jsonrpc { get; set; } = "2.0";
    public string? method { get; set; }
    public object? @params { get; set; }
    public object? id { get; set; }
}

public sealed class JsonRpcResponse
{
    public string jsonrpc { get; set; } = "2.0";
    public object? result { get; set; }
    public JsonRpcError? error { get; set; }
    public object? id { get; set; }
}

public sealed class JsonRpcError
{
    public int code { get; set; }
    public string? message { get; set; }
}