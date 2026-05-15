// Copyright (c) 2026 Michal Wallace
//
// Licensed under the MIT License. See the LICENSE.txt file for details.
using System.Text.Json;
using System.Text.Json.Nodes;
using K3CSharp.MCP;
using K3CSharp.Parsing;

// ── Tool definitions for tools/list ──────────────────────────────

var tools = new JsonArray
{
    MakeTool("k_eval",
        "Execute K code in the running K3 interpreter session. Returns the interpreter output.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["code"] = new JsonObject { ["type"] = "string", ["description"] = "K expression(s) to evaluate" }
            },
            ["required"] = new JsonArray { "code" }
        }),
    MakeTool("k_reset",
        "Reset the K session to a clean state. Clears all variables, resets K-tree and random seed.",
        new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }),
    MakeTool("k_reload",
        "Hot-reload the K3 interpreter from a fresh build without restarting the MCP server.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["preserve_state"] = new JsonObject { ["type"] = "boolean", ["description"] = "Attempt to save/restore state across reload (best-effort)", ["default"] = false }
            }
        }),
    MakeTool("k_inspect",
        "Inspect the K namespace tree. Returns variable and function names at a given path.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["path"] = new JsonObject { ["type"] = "string", ["description"] = "K tree path (e.g. \".\", \".k\")", ["default"] = "." },
                ["depth"] = new JsonObject { ["type"] = "integer", ["description"] = "Levels deep to enumerate", ["default"] = 1 }
            }
        }),
    MakeTool("k_status",
        "Report K3 session health: running state, hashes, staleness, uptime, command count.",
        new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() }),
};

// ── K3 Session (lazy start) ──────────────────────────────────────

var buildDir = Environment.GetEnvironmentVariable("K3_BUILD_DIR")
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "K3CSharp", "bin", "Debug", "net8.0"));

using var session = new K3Session(buildDir);
bool sessionStarted = false;

void EnsureSession()
{
    if (!sessionStarted || !session.IsRunning)
    {
        session.Start();
        sessionStarted = true;
    }
}

// ── Main loop: JSON-RPC 2.0 over stdio ──────────────────────────

string? line;
while ((line = Console.ReadLine()) != null)
{
    line = line.Trim();
    if (line.Length == 0) continue;

    JsonNode? msg;
    try { msg = JsonNode.Parse(line); }
    catch { continue; } // ignore malformed
    if (msg == null) continue;

    var method = msg["method"]?.GetValue<string>();
    var id = msg["id"]; // null for notifications

    switch (method)
    {
        case "initialize":
            Reply(id, new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject
                {
                    ["tools"] = new JsonObject()
                },
                ["serverInfo"] = new JsonObject
                {
                    ["name"] = "k3sharp",
                    ["version"] = "1.0.0"
                }
            });
            break;

        case "notifications/initialized":
            // No response needed for notifications.
            break;

        case "tools/list":
            Reply(id, new JsonObject { ["tools"] = tools.DeepClone() });
            break;

        case "tools/call":
            HandleToolCall(id, msg["params"]);
            break;

        default:
            if (id != null)
                ReplyError(id, -32601, $"Method not found: {method}");
            break;
    }
}

// ── Tool dispatch ────────────────────────────────────────────────

void HandleToolCall(JsonNode? id, JsonNode? @params)
{
    var toolName = @params?["name"]?.GetValue<string>();
    var args = @params?["arguments"];

    try
    {
        EnsureSession();

        string text = toolName switch
        {
            "k_eval" => DoEval(args),
            "k_reset" => DoReset(),
            "k_reload" => DoReload(),
            "k_inspect" => DoInspect(args),
            "k_status" => DoStatus(),
            _ => throw new Exception($"Unknown tool: {toolName}")
        };

        Reply(id, new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = text }
            }
        });
    }
    catch (Exception ex)
    {
        Reply(id, new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = $"Error: {ex.Message}" }
            },
            ["isError"] = true
        });
    }
}

string DoEval(JsonNode? args)
{
    var code = args?["code"]?.GetValue<string>()
        ?? throw new Exception("Missing 'code' argument");
    
    // Process escape sequences in the K code
    var processedCode = JsonEscapeHelper.ProcessJsonEscapes(code);
    
    return session.Eval(processedCode);
}

string DoReset()
{
    session.Eval("\\9");
    return "Session reset.";
}

string DoReload()
{
    session.Reload();
    sessionStarted = true;
    return $"Reloaded. running_hash: {session.RunningHash}";
}

string DoInspect(JsonNode? args)
{
    var path = args?["path"]?.GetValue<string>() ?? ".";
    var currentBranch = session.Eval("\\d").Trim();

    if (path != "." && path != currentBranch)
        session.Eval($"\\d {path}");

    var vars = session.Eval("\\v").Trim();

    if (path != "." && path != currentBranch)
        session.Eval($"\\d {currentBranch}");

    var lines = new List<string> { $"Path: {path}" };
    if (!string.IsNullOrEmpty(vars))
        lines.Add(vars);
    return string.Join('\n', lines);
}

string DoStatus()
{
    return string.Join('\n', new[]
    {
        $"running: {session.IsRunning}",
        $"build_dir: {session.BuildDir}",
        $"shadow_dir: {session.ShadowDir}",
        $"commands: {session.CommandCount}",
        $"uptime: {(DateTime.UtcNow - session.StartedAt):g}",
        $"running_hash: {session.RunningHash ?? "(none)"}",
        $"build_hash: {session.GetBuildHash() ?? "(none)"}",
        $"stale: {session.IsStale}",
    });
}

// ── JSON-RPC helpers ─────────────────────────────────────────────

static void Reply(JsonNode? id, JsonNode result)
{
    var response = new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result
    };
    Console.WriteLine(JsonSerializer.Serialize(response, JsonSerializerSettings.Output));
    Console.Out.Flush();
}

static void ReplyError(JsonNode? id, int code, string message)
{
    var response = new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message
        }
    };
    Console.WriteLine(JsonSerializer.Serialize(response, JsonSerializerSettings.Output));
    Console.Out.Flush();
}

static JsonObject MakeTool(string name, string description, JsonObject inputSchema)
{
    return new JsonObject
    {
        ["name"] = name,
        ["description"] = description,
        ["inputSchema"] = inputSchema
    };
}