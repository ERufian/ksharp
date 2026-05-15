// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using K3CSharp;
using K3CSharp.Parsing;

namespace KMCPServer
{
    public sealed class KMCPServer : IDisposable
    {
        private readonly KInterpreterWrapper wrapper;

        public KMCPServer()
        {
            this.wrapper = new KInterpreterWrapper();
        }

        public KMCPServer(string? interpreterPath, int timeout)
        {
            this.wrapper = new KInterpreterWrapper(interpreterPath ?? @"c:\k\e.exe", timeout);
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
                        error = new JsonRpcError { code = -32603, message = string.Format("Internal error: {0}", ex.Message) }
                    };
                    Console.WriteLine(JsonSerializer.Serialize(errorResponse, JsonSerializerSettings.Output));
                    Console.Out.Flush();
                }
            }
        }

        private JsonRpcResponse ProcessRequest(JsonRpcRequest request)
        {
            if (request.method == "initialize")
            {
                var capabilities = new
                {
                    tools = new object()  // Empty object, not array
                };
                var serverInfo = new
                {
                    name = "k.exe MCP Server",
                    version = "1.0.0"
                };
                
                return new JsonRpcResponse
                {
                    id = request.id,
                    result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = capabilities,
                        serverInfo = serverInfo
                    }
                };
            }

            if (request.method == "notifications/initialized")
            {
                // No response needed for notifications
                return null!;
            }

            if (request.method == "tools/list")
            {
                var tools = new object[]
                {
                    new
                    {
                        name = "execute_k_command",
                        description = "Execute a K command and return the result",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                command = new
                                {
                                    type = "string",
                                    description = "The K command to execute"
                                }
                            },
                            required = new[] { "command" }
                        }
                    },
                    new
                    {
                        name = "execute_k_script",
                        description = "Execute a K script from a file and return the result",
                        inputSchema = new
                        {
                            type = "object",
                            properties = new
                            {
                                script_path = new
                                {
                                    type = "string",
                                    description = "Path to the K script file to execute"
                                }
                            },
                            required = new[] { "script_path" }
                        }
                    }
                };
                
                return new JsonRpcResponse
                {
                    id = request.id,
                    result = new Dictionary<string, object> { ["tools"] = tools }  // Wrap in object
                };
            }

            if (request.method == "tools/call")
            {
                return ExecuteTool(request);
            }

            return new JsonRpcResponse
            {
                id = request.id,
                error = new JsonRpcError { code = -32601, message = "Method not found" }
            };
        }

        private JsonRpcResponse ExecuteTool(JsonRpcRequest request)
        {
            try
            {
                var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(request.@params?.ToString() ?? "", JsonSerializerSettings.Input);
                var toolName = arguments?["name"]?.ToString() ?? "";
                
                // Extract the actual tool arguments from the nested structure
                var toolArgs = JsonSerializer.Deserialize<Dictionary<string, object>>(arguments?["arguments"]?.ToString() ?? "", JsonSerializerSettings.Input);

                string result = toolName switch
                {
                    "execute_k_command" => ExecuteKCommand(toolArgs?["command"]?.ToString()),
                    "execute_k_script" => ExecuteKScript(toolArgs?["script_path"]?.ToString()),
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

        private string ExecuteKCommand(string? command)
        {
            if (string.IsNullOrEmpty(command))
                throw new Exception("Command argument required");
            
            // Process escape sequences in the command
            var processedCommand = JsonEscapeHelper.ProcessJsonEscapes(command);
            
            return wrapper.ExecuteScript(processedCommand);
        }

        private string ExecuteKScript(string? scriptPath)
        {
            if (string.IsNullOrEmpty(scriptPath))
                throw new Exception("Script path required");
            
            // Process escape sequences in the script path
            var processedPath = JsonEscapeHelper.ProcessJsonEscapes(scriptPath);
                
            var scriptContent = File.ReadAllText(processedPath);
            return wrapper.ExecuteScript(scriptContent);
        }

        public void Dispose()
        {
            wrapper?.Dispose();
        }
    }

    public sealed class JsonRpcRequest
    {
        public string jsonrpc { get; set; } = "2.0";
        public string? method { get; set; }
        public object? @params { get; set; }
        public object? id { get; set; }  // Changed from string to object to handle both string and numeric
    }

    public sealed class JsonRpcResponse
    {
        public string jsonrpc { get; set; } = "2.0";
        public object? result { get; set; }
        public JsonRpcError? error { get; set; }
        public object? id { get; set; }  // Changed from string to object
    }

    public sealed class JsonRpcError
    {
        public int code { get; set; }
        public string? message { get; set; }
    }
}