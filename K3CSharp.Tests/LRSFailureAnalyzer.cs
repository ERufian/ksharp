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
using K3CSharp.Parsing;
using K3CSharp;

namespace K3CSharp.Tests
{
    /// <summary>
    /// Analyzes LRS parser failures and generates detailed reports
    /// </summary>
    public class LRSFailureAnalyzer
    {
        private readonly List<ParserFailureRecord> failureRecords = new();
        private readonly List<ParserIncorrectResultRecord> incorrectResultRecords = new();
        private readonly object lockObject = new object();
        
        /// <summary>
        /// Record a parsing failure where LRS parser returned null
        /// </summary>
        public void RecordFailure(string sourceText, int consumedTokens, int totalTokens, TokenType? lastTokenType, ASTNode? legacyAST = null)
        {
            lock (lockObject)
            {
                var record = new ParserFailureRecord
                {
                    Timestamp = DateTime.Now,
                    SourceText = sourceText,
                    ConsumedTokens = consumedTokens,
                    TotalTokens = totalTokens,
                    LastTokenType = lastTokenType,
                    FailurePoint = DetermineFailurePoint(consumedTokens, totalTokens, lastTokenType),
                    LegacyParserAST = legacyAST
                };
                
                failureRecords.Add(record);
                
                // Limit records to prevent memory issues
                if (failureRecords.Count > 10000)
                {
                    failureRecords.RemoveAt(0);
                }
            }
        }
        
        /// <summary>
        /// Record a case where LRS parser produced a non-null AST but incorrect results
        /// </summary>
        public void RecordIncorrectResult(string sourceText, ASTNode lrsAST, string expectedResult, string actualResult, string legacyResult = "")
        {
            lock (lockObject)
            {
                var record = new ParserIncorrectResultRecord
                {
                    Timestamp = DateTime.Now,
                    SourceText = sourceText,
                    LRSAST = lrsAST,
                    ExpectedResult = expectedResult,
                    ActualResult = actualResult,
                    LegacyResult = legacyResult,
                    ParseTree = GenerateParseTree(lrsAST)
                };
                
                incorrectResultRecords.Add(record);
                
                // Limit records to prevent memory issues
                if (incorrectResultRecords.Count > 10000)
                {
                    incorrectResultRecords.RemoveAt(0);
                }
            }
        }
        
        /// <summary>
        /// Generate a comprehensive failure analysis report
        /// </summary>
        public LRSFailureReport GenerateReport()
        {
            lock (lockObject)
            {
                var report = new LRSFailureReport
                {
                    GeneratedAt = DateTime.Now,
                    TotalFailures = failureRecords.Count,
                    TotalIncorrectResults = incorrectResultRecords.Count,
                    FailureRecords = new List<ParserFailureRecord>(failureRecords),
                    IncorrectResultRecords = new List<ParserIncorrectResultRecord>(incorrectResultRecords)
                };
                
                // Analyze failure patterns
                report.FailurePatterns = AnalyzeFailurePatterns(failureRecords);
                report.IncorrectResultPatterns = AnalyzeIncorrectResultPatterns(incorrectResultRecords);
                
                return report;
            }
        }
        
        /// <summary>
        /// Clear all failure records
        /// </summary>
        public void ClearRecords()
        {
            lock (lockObject)
            {
                failureRecords.Clear();
                incorrectResultRecords.Clear();
            }
        }
        
        /// <summary>
        /// Get failure statistics
        /// </summary>
        public FailureStatistics GetStatistics()
        {
            lock (lockObject)
            {
                return new FailureStatistics
                {
                    TotalFailures = failureRecords.Count,
                    TotalIncorrectResults = incorrectResultRecords.Count,
                    MostCommonFailurePoints = failureRecords
                        .GroupBy(r => r.FailurePoint)
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    MostCommonLastTokenTypes = failureRecords
                        .Where(r => r.LastTokenType.HasValue)
                        .GroupBy(r => r.LastTokenType!.Value)
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .ToDictionary(g => g.Key.ToString(), g => g.Count())
                };
            }
        }
        
        private string DetermineFailurePoint(int consumedTokens, int totalTokens, TokenType? lastTokenType)
        {
            if (consumedTokens == 0)
                return "Start of expression";
            if (consumedTokens >= totalTokens)
                return "End of expression";
            if (lastTokenType.HasValue)
                return $"After {lastTokenType.Value} (position {consumedTokens}/{totalTokens})";
            return $"Position {consumedTokens}/{totalTokens}";
        }
        
        private Dictionary<string, int> AnalyzeFailurePatterns(List<ParserFailureRecord> records)
        {
            var patterns = new Dictionary<string, int>();
            
            // Group by failure point
            foreach (var group in records.GroupBy(r => r.FailurePoint))
            {
                patterns[$"Failure at {group.Key}"] = group.Count();
            }
            
            // Group by token count ranges
            var tokenRanges = records
                .GroupBy(r => GetTokenRange(r.TotalTokens))
                .OrderBy(g => g.Key);
            
            foreach (var group in tokenRanges)
            {
                patterns[$"Token count {group.Key}"] = group.Count();
            }
            
            return patterns;
        }
        
        private Dictionary<string, int> AnalyzeIncorrectResultPatterns(List<ParserIncorrectResultRecord> records)
        {
            var patterns = new Dictionary<string, int>();
            
            // Group by AST node types
            var nodeTypes = records
                .Where(r => r.LRSAST != null)
                .GroupBy(r => r.LRSAST!.GetType().Name)
                .OrderByDescending(g => g.Count());
            
            foreach (var group in nodeTypes.Take(10))
            {
                patterns[$"AST Type: {group.Key}"] = group.Count();
            }
            
            // Group by result patterns (empty, wrong value, etc.)
            var resultPatterns = records
                .GroupBy(r => GetResultPattern(r.ExpectedResult, r.ActualResult))
                .OrderByDescending(g => g.Count());
            
            foreach (var group in resultPatterns)
            {
                patterns[$"Result Pattern: {group.Key}"] = group.Count();
            }
            
            return patterns;
        }
        
        private string GetTokenRange(int tokenCount)
        {
            return tokenCount switch
            {
                <= 5 => "1-5",
                <= 10 => "6-10",
                <= 20 => "11-20",
                <= 50 => "21-50",
                _ => "50+"
            };
        }
        
        private string GetResultPattern(string expected, string actual)
        {
            if (string.IsNullOrEmpty(actual) || actual == "()")
                return "Empty result";
            if (expected.Contains("Error") && !actual.Contains("Error"))
                return "Expected error but got result";
            if (!expected.Contains("Error") && actual.Contains("Error"))
                return "Expected result but got error";
            if (expected.Length > 0 && actual.Length == 0)
                return "Non-empty expected, empty actual";
            if (expected.Length == 0 && actual.Length > 0)
                return "Empty expected, non-empty actual";
            return "Value mismatch";
        }
        
        private string GenerateParseTree(ASTNode node)
        {
            if (node == null) return "null";
            
            var result = new System.Text.StringBuilder();
            GenerateParseTreeRecursive(node, result, 0);
            return result.ToString();
        }
        
        private void GenerateParseTreeRecursive(ASTNode node, System.Text.StringBuilder result, int depth)
        {
            var indent = new string(' ', depth * 2);
            result.AppendLine($"{indent}{node.Type}: {node}");
            
            if (node.Type == ASTNodeType.DyadicOp)
            {
                if (node.Children.Count >= 2)
                {
                    GenerateParseTreeRecursive(node.Children[0], result, depth + 1);
                    result.AppendLine($"{indent}  Operator: {node.Value}");
                    GenerateParseTreeRecursive(node.Children[1], result, depth + 1);
                }
            }
            else if (node.Type == ASTNodeType.FunctionCall)
            {
                result.AppendLine($"{indent}  Function: {node.Value}");
                foreach (var child in node.Children)
                {
                    GenerateParseTreeRecursive(child, result, depth + 1);
                }
            }
            else
            {
                // Handle all other node types generically
                foreach (var child in node.Children)
                {
                    GenerateParseTreeRecursive(child, result, depth + 1);
                }
            }
        }
    }
    
    /// <summary>
    /// Record of a parser failure
    /// </summary>
    public class ParserFailureRecord
    {
        public DateTime Timestamp { get; set; }
        public string TestName { get; set; } = "";
        public string SourceText { get; set; } = "";
        public int ConsumedTokens { get; set; }
        public int TotalTokens { get; set; }
        public TokenType? LastTokenType { get; set; }
        public string FailurePoint { get; set; } = "";
        public ASTNode? LegacyParserAST { get; set; }
    }
    
    /// <summary>
    /// Record of an incorrect parsing result
    /// </summary>
    public class ParserIncorrectResultRecord
    {
        public DateTime Timestamp { get; set; }
        public string SourceText { get; set; } = "";
        public ASTNode? LRSAST { get; set; }
        public string ExpectedResult { get; set; } = "";
        public string ActualResult { get; set; } = "";
        public string LegacyResult { get; set; } = "";
        public string ParseTree { get; set; } = "";
    }
    
    /// <summary>
    /// Comprehensive failure analysis report
    /// </summary>
    public class LRSFailureReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalFailures { get; set; }
        public int TotalIncorrectResults { get; set; }
        public int NullResults { get; set; }
        public int IncompleteResults { get; set; }
        public List<ParserFailureRecord> FailureRecords { get; set; } = new();
        public List<ParserIncorrectResultRecord> IncorrectResultRecords { get; set; } = new();
        public Dictionary<string, int> FailurePatterns { get; set; } = new();
        public Dictionary<string, int> IncorrectResultPatterns { get; set; } = new();
    }
    
    /// <summary>
    /// Failure statistics summary
    /// </summary>
    public class FailureStatistics
    {
        public int TotalFailures { get; set; }
        public int TotalIncorrectResults { get; set; }
        public Dictionary<string, int> MostCommonFailurePoints { get; set; } = new();
        public Dictionary<string, int> MostCommonLastTokenTypes { get; set; } = new();
    }
}