// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// LRS Failure Analysis System
    /// Tracks failure patterns by token type, position, and expression structure
    /// Categorizes failures: syntax errors, unsupported constructs, edge cases
    /// Generates detailed reports with failure clustering
    /// </summary>
    public class LRSFailureAnalyzer
    {
        private readonly List<LRSFailureRecord> failureRecords = new List<LRSFailureRecord>();
        private readonly Dictionary<string, int> failurePatterns = new Dictionary<string, int>();
        private readonly Dictionary<TokenType, int> tokenTypeFailures = new Dictionary<TokenType, int>();
        
        /// <summary>
        /// Record a parsing failure for analysis
        /// </summary>
        public void RecordFailure(string sourceText, int consumedTokens, int totalTokens, 
                                 TokenType? lastTokenType, string? context = null)
        {
            var record = new LRSFailureRecord
            {
                Timestamp = DateTime.UtcNow,
                SourceText = sourceText,
                ConsumedTokens = consumedTokens,
                TotalTokens = totalTokens,
                LastTokenType = lastTokenType,
                Context = context,
                FailurePattern = ClassifyFailure(sourceText, consumedTokens, totalTokens, lastTokenType)
            };
            
            failureRecords.Add(record);
            
            // Update pattern counts
            var patternKey = record.FailurePattern;
            failurePatterns[patternKey] = failurePatterns.GetValueOrDefault(patternKey, 0) + 1;
            
            // Update token type counts
            if (lastTokenType.HasValue)
            {
                tokenTypeFailures[lastTokenType.Value] = tokenTypeFailures.GetValueOrDefault(lastTokenType.Value, 0) + 1;
            }
        }
        
        /// <summary>
        /// Classify the type of failure based on parsing context
        /// </summary>
        private string ClassifyFailure(string sourceText, int consumedTokens, int totalTokens, TokenType? lastTokenType)
        {
            // Partial consumption (e.g., consumed: 18/19)
            if (consumedTokens > 0 && consumedTokens < totalTokens)
            {
                var consumptionRatio = (double)consumedTokens / totalTokens;
                
                if (consumptionRatio >= 0.9)
                    return "NEAR_COMPLETE_FAILURE";
                else if (consumptionRatio >= 0.5)
                    return "PARTIAL_CONSUMPTION";
                else
                    return "EARLY_FAILURE";
            }
            
            // No tokens consumed
            if (consumedTokens == 0)
            {
                return "IMMEDIATE_FAILURE";
            }
            
            // Token-specific patterns
            if (lastTokenType.HasValue)
            {
                return lastTokenType.Value switch
                {
                    TokenType.CHARACTER_VECTOR => "CHARACTER_STRING_FAILURE",
                    TokenType.SYMBOL => "SYMBOL_PARSING_FAILURE",
                    TokenType.IDENTIFIER => "IDENTIFIER_PARSING_FAILURE",
                    TokenType.RIGHT_BRACKET or TokenType.RIGHT_PAREN => "UNMATCHED_CLOSING_DELIMITER",
                    TokenType.ADVERB_SLASH or TokenType.ADVERB_BACKSLASH or TokenType.ADVERB_TICK => "ADVERB_FAILURE",
                    _ => "TOKEN_SPECIFIC_FAILURE"
                };
            }
            
            // Content-based patterns
            if (sourceText.Contains("[") || sourceText.Contains("]") || 
                sourceText.Contains("(") || sourceText.Contains(")"))
            {
                return "BRACKET_EXPRESSION_FAILURE";
            }
            
            if (sourceText.Contains("'") || sourceText.Contains("/"))
            {
                return "ADVERB_EXPRESSION_FAILURE";
            }
            
            return "UNKNOWN_FAILURE_PATTERN";
        }
        
        /// <summary>
        /// Generate failure analysis report
        /// </summary>
        public LRSFailureReport GenerateReport()
        {
            var report = new LRSFailureReport
            {
                TotalFailures = failureRecords.Count,
                GeneratedAt = DateTime.UtcNow,
                FailurePatterns = failurePatterns.OrderByDescending(kvp => kvp.Value)
                                                  .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                TokenTypeFailures = tokenTypeFailures.OrderByDescending(kvp => kvp.Value)
                                                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                TopFailurePatterns = failurePatterns.OrderByDescending(kvp => kvp.Value)
                                                   .Take(20)
                                                   .ToList(),
                RecentFailures = failureRecords.OrderByDescending(r => r.Timestamp)
                                              .Take(50)
                                              .ToList()
            };
            
            // Calculate statistics
            report.AverageConsumptionRatio = failureRecords.Count > 0 
                ? failureRecords.Average(r => (double)r.ConsumedTokens / r.TotalTokens)
                : 0.0;
                
            report.PartialConsumptionFailures = failureRecords.Count(r => 
                r.ConsumedTokens > 0 && r.ConsumedTokens < r.TotalTokens);
                
            report.ImmediateFailures = failureRecords.Count(r => r.ConsumedTokens == 0);
            
            return report;
        }
        
        /// <summary>
        /// Get the most common failure patterns
        /// </summary>
        public List<KeyValuePair<string, int>> GetTopFailurePatterns(int count = 10)
        {
            return failurePatterns.OrderByDescending(kvp => kvp.Value)
                                 .Take(count)
                                 .ToList();
        }
        
        /// <summary>
        /// Clear all failure records
        /// </summary>
        public void ClearRecords()
        {
            failureRecords.Clear();
            failurePatterns.Clear();
            tokenTypeFailures.Clear();
        }
        
        /// <summary>
        /// Get failure count for specific pattern
        /// </summary>
        public int GetFailureCount(string pattern)
        {
            return failurePatterns.GetValueOrDefault(pattern, 0);
        }
    }
    
    /// <summary>
    /// Record of a single LRS parsing failure
    /// </summary>
    public class LRSFailureRecord
    {
        public DateTime Timestamp { get; set; }
        public string SourceText { get; set; } = "";
        public int ConsumedTokens { get; set; }
        public int TotalTokens { get; set; }
        public TokenType? LastTokenType { get; set; }
        public string? Context { get; set; }
        public string FailurePattern { get; set; } = "";
        
        public double ConsumptionRatio => TotalTokens > 0 ? (double)ConsumedTokens / TotalTokens : 0.0;
    }
    
    /// <summary>
    /// Comprehensive failure analysis report
    /// </summary>
    public class LRSFailureReport
    {
        public int TotalFailures { get; set; }
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, int> FailurePatterns { get; set; } = new Dictionary<string, int>();
        public Dictionary<TokenType, int> TokenTypeFailures { get; set; } = new Dictionary<TokenType, int>();
        public List<KeyValuePair<string, int>> TopFailurePatterns { get; set; } = new List<KeyValuePair<string, int>>();
        public List<LRSFailureRecord> RecentFailures { get; set; } = new List<LRSFailureRecord>();
        
        public double AverageConsumptionRatio { get; set; }
        public int PartialConsumptionFailures { get; set; }
        public int ImmediateFailures { get; set; }
        
        /// <summary>
        /// Get summary statistics
        /// </summary>
        public string GetSummary()
        {
            return $"Total Failures: {TotalFailures}, " +
                   $"Partial Consumption: {PartialConsumptionFailures}, " +
                   $"Immediate Failures: {ImmediateFailures}, " +
                   $"Avg Consumption: {AverageConsumptionRatio:P1}";
        }
        
        /// <summary>
        /// Get top failure patterns as formatted string
        /// </summary>
        public string GetTopPatternsSummary(int count = 10)
        {
            var topPatterns = TopFailurePatterns.Take(count);
            return string.Join("\n", topPatterns.Select(p => $"{p.Key}: {p.Value}"));
        }
        
        /// <summary>
        /// Get failure count for specific pattern
        /// </summary>
        public int GetFailureCount(string pattern)
        {
            return FailurePatterns.GetValueOrDefault(pattern, 0);
        }
    }
}