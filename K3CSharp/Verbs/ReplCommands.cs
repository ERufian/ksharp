// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using K3CSharp;

namespace K3CSharp.Verbs
{
    /// <summary>
    /// Additional REPL command methods for dot execute
    /// </summary>
    public static class ReplCommands
    {
        public static K3Value ExecuteReplCommand(string command)
        {
            // Handle REPL commands like \p, \d, etc.
            return command switch
            {
                "\\p" => GetPrecision(),
                "\\d" => GetWorkingDirectory(),
                "\\t" => GetKTree(),
                "\\v" => GetKTreeVariables(),
                "\\w" => GetWorkspace(),
                _ => throw new Exception($"Unknown REPL command: {command}")
            };
        }
        
        public static K3Value GetPrecision()
        {
            // Return current precision setting
            return new IntegerValue(7); // Default precision
        }
        
        public static K3Value GetWorkingDirectory()
        {
            return new CharacterValue(System.IO.Directory.GetCurrentDirectory());
        }
        
        public static K3Value GetKTree()
        {
            // Return current K tree structure
            return new SymbolValue("k-tree");
        }
        
        public static K3Value GetKTreeVariables()
        {
            // Return variables in current K tree
            return new VectorValue(new List<K3Value>());
        }
        
        public static K3Value GetWorkspace()
        {
            // Return workspace information
            return new SymbolValue("workspace");
        }
    }
}