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
using K3CSharp;
using K3CSharp.Parsing;

namespace K3CSharp.Tests
{
    /// <summary>
    /// Analyzes LRS parser failures (legacy parser comparison removed)
    /// NOTE: This tool no longer compares with legacy parser since it's being removed
    /// </summary>
    public class LRSBugAnalyzer
    {
        public static void AnalyzeLRSBugs()
        {
            Console.WriteLine("=== LRS Parser Failure Analysis ===");
            Console.WriteLine("NOTE: Legacy parser comparison removed - Parser class being deleted\n");
            
            var testDir = Path.Combine(Directory.GetCurrentDirectory(), "TestScripts");
            var testFiles = Directory.GetFiles(testDir, "*.k").OrderBy(f => f).ToList();
            
            var failureCategories = new Dictionary<string, List<string>>
            {
                ["ParsingErrors"] = new List<string>(),
                ["EvaluationErrors"] = new List<string>(),
                ["Other"] = new List<string>()
            };
            
            int analyzed = 0;
            int failures = 0;
            int maxToAnalyze = 50; // Analyze first 50 tests
            
            foreach (var testFile in testFiles)
            {
                if (analyzed >= maxToAnalyze) break;
                
                var fileName = Path.GetFileName(testFile);
                var script = File.ReadAllText(testFile);
                
                try
                {
                    analyzed++;
                    
                    ParserConfig.EnableDebugging = false;
                    
                    var lexerLRS = new Lexer(script);
                    var tokensLRS = lexerLRS.Tokenize();
                    var astLRS = ParserConfig.ParseWithConfig(tokensLRS, script);
                    
                    if (astLRS == null)
                    {
                        failures++;
                        failureCategories["ParsingErrors"].Add(fileName);
                        Console.WriteLine($"\n[{failures}] {fileName} - Parsing failed (AST is null)");
                        Console.WriteLine($"Script: {script.Substring(0, Math.Min(60, script.Length))}...");
                        continue;
                    }
                    
                    var evaluatorLRS = new Evaluator();
                    K3Value? resultLRS = null;
                    string? errorLRS = null;
                    
                    try
                    {
                        resultLRS = evaluatorLRS.Evaluate(astLRS);
                    }
                    catch (Exception ex)
                    {
                        errorLRS = ex.Message;
                        failures++;
                        failureCategories["EvaluationErrors"].Add(fileName);
                        Console.WriteLine($"\n[{failures}] {fileName} - Evaluation failed");
                        Console.WriteLine($"Script: {script.Substring(0, Math.Min(60, script.Length))}...");
                        Console.WriteLine($"Error: {errorLRS}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    failureCategories["Other"].Add(fileName);
                    Console.WriteLine($"\n[{failures}] {fileName} - Unexpected error");
                    Console.WriteLine($"Script: {script.Substring(0, Math.Min(60, script.Length))}...");
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            
            // Print summary
            Console.WriteLine("\n=== Failure Summary ===");
            foreach (var category in failureCategories)
            {
                if (category.Value.Count > 0)
                {
                    Console.WriteLine($"\n{category.Key}: {category.Value.Count}");
                    foreach (var file in category.Value)
                    {
                        Console.WriteLine($"  - {file}");
                    }
                }
            }
            
            Console.WriteLine($"\nTotal analyzed: {analyzed}");
            Console.WriteLine($"Total failures: {failures}");
            Console.WriteLine($"Success rate: {((analyzed - failures) * 100.0 / analyzed):F1}%");
        }
    }
}