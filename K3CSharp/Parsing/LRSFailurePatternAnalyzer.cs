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
    /// LRS Failure Pattern Analyzer
    /// Analyzes top failure patterns and creates prioritized backlog
    /// Identifies quick wins vs complex fixes based on impact and complexity
    /// </summary>
    public class LRSFailurePatternAnalyzer
    {
        private readonly LRSFailureAnalyzer failureAnalyzer;
        private readonly LRSTestMonitor testMonitor;
        
        public LRSFailurePatternAnalyzer(LRSFailureAnalyzer failureAnalyzer, LRSTestMonitor testMonitor)
        {
            this.failureAnalyzer = failureAnalyzer;
            this.testMonitor = testMonitor;
        }
        
        /// <summary>
        /// Generate comprehensive pattern analysis with prioritized fix recommendations
        /// </summary>
        public LRSPatternAnalysis GeneratePatternAnalysis()
        {
            var failureReport = failureAnalyzer.GenerateReport();
            var testReport = testMonitor.GenerateReport();
            
            var analysis = new LRSPatternAnalysis
            {
                GeneratedAt = DateTime.UtcNow,
                TotalFailures = failureReport.TotalFailures,
                TestSuccessRate = 1.0 - testReport.FailureRate,
                
                // Analyze failure patterns
                TopFailurePatterns = AnalyzeFailurePatterns(failureReport),
                TokenSpecificFailures = AnalyzeTokenSpecificFailures(failureReport),
                
                // Categorize by complexity and impact
                QuickWins = IdentifyQuickWins(failureReport, testReport),
                ComplexFixes = IdentifyComplexFixes(failureReport, testReport),
                MediumComplexity = IdentifyMediumComplexity(failureReport, testReport),
                
                // Generate prioritized backlog
                PrioritizedBacklog = GeneratePrioritizedBacklog(failureReport, testReport)
            };
            
            return analysis;
        }
        
        /// <summary>
        /// Analyze failure patterns and categorize them
        /// </summary>
        private List<FailurePatternAnalysis> AnalyzeFailurePatterns(LRSFailureReport report)
        {
            return report.TopFailurePatterns.Take(20).Select(kvp => new FailurePatternAnalysis
            {
                Pattern = kvp.Key,
                Count = kvp.Value,
                Percentage = report.TotalFailures > 0 ? (double)kvp.Value / report.TotalFailures : 0.0,
                Category = CategorizePattern(kvp.Key),
                EstimatedComplexity = EstimateComplexity(kvp.Key),
                EstimatedImpact = EstimateImpact(kvp.Key, kvp.Value, report.TotalFailures)
            }).ToList();
        }
        
        /// <summary>
        /// Analyze token-specific failures
        /// </summary>
        private List<TokenFailureAnalysis> AnalyzeTokenSpecificFailures(LRSFailureReport report)
        {
            return report.TokenTypeFailures.Select(kvp => new TokenFailureAnalysis
            {
                TokenType = kvp.Key,
                Count = kvp.Value,
                Percentage = report.TotalFailures > 0 ? (double)kvp.Value / report.TotalFailures : 0.0,
                CommonPatterns = GetCommonPatternsForToken(kvp.Key, report.RecentFailures),
                RecommendedFixes = GetRecommendedFixesForToken(kvp.Key)
            }).ToList();
        }
        
        /// <summary>
        /// Identify quick wins (high impact, low complexity)
        /// </summary>
        private List<FixRecommendation> IdentifyQuickWins(LRSFailureReport failureReport, LRSTestReport testReport)
        {
            var quickWins = new List<FixRecommendation>();
            
            // Character string parsing issues - usually easy to fix
            var charStringFailures = failureReport.GetFailureCount("CHARACTER_STRING_FAILURE");
            if (charStringFailures > 10)
            {
                quickWins.Add(new FixRecommendation
                {
                    Title = "Fix Character String Parsing",
                    Description = "Improve parsing of character vectors and string literals",
                    EstimatedImpact = (double)charStringFailures / failureReport.TotalFailures,
                    EstimatedComplexity = FixComplexity.Low,
                    AffectedTests = testReport.HighFailureTests.Where(t => t.Value.FailureRate > 0.5).Take(5).Select(t => t.Key).ToList(),
                    Recommendation = "Enhance LRSAtomicParser character string handling"
                });
            }
            
            // Immediate failures - often syntax issues
            var immediateFailures = failureReport.ImmediateFailures;
            if (immediateFailures > 20)
            {
                quickWins.Add(new FixRecommendation
                {
                    Title = "Fix Immediate Failure Cases",
                    Description = "Address cases where parser fails to consume any tokens",
                    EstimatedImpact = (double)immediateFailures / failureReport.TotalFailures,
                    EstimatedComplexity = FixComplexity.Low,
                    AffectedTests = testReport.HighFailureTests.Take(3).Select(t => t.Key).ToList(),
                    Recommendation = "Add better error detection and recovery in expression parsing"
                });
            }
            
            return quickWins.OrderByDescending(r => r.EstimatedImpact).ToList();
        }
        
        /// <summary>
        /// Identify complex fixes (high impact, high complexity)
        /// </summary>
        private List<FixRecommendation> IdentifyComplexFixes(LRSFailureReport failureReport, LRSTestReport testReport)
        {
            var complexFixes = new List<FixRecommendation>();
            
            // Adverb expression failures - complex due to verb interaction
            var adverbFailures = failureReport.GetFailureCount("ADVERB_EXPRESSION_FAILURE");
            if (adverbFailures > 15)
            {
                complexFixes.Add(new FixRecommendation
                {
                    Title = "Enhance Adverb Expression Parsing",
                    Description = "Improve parsing of complex adverb-verb combinations",
                    EstimatedImpact = (double)adverbFailures / failureReport.TotalFailures,
                    EstimatedComplexity = FixComplexity.High,
                    AffectedTests = testReport.HighFailureTests.Where(t => t.Key.Contains("adverb")).Take(10).Select(t => t.Key).ToList(),
                    Recommendation = "Implement verb-agnostic adverb parsing using VerbRegistry properties"
                });
            }
            
            // Bracket expression failures - complex due to nesting
            var bracketFailures = failureReport.GetFailureCount("BRACKET_EXPRESSION_FAILURE");
            if (bracketFailures > 20)
            {
                complexFixes.Add(new FixRecommendation
                {
                    Title = "Improve Bracket Expression Handling",
                    Description = "Enhance parsing of nested brackets and complex expressions",
                    EstimatedImpact = (double)bracketFailures / failureReport.TotalFailures,
                    EstimatedComplexity = FixComplexity.High,
                    AffectedTests = testReport.HighFailureTests.Where(t => t.Key.Contains("bracket") || t.Key.Contains("vector")).Take(10).Select(t => t.Key).ToList(),
                    Recommendation = "Implement better delimiter tracking and expression boundary detection"
                });
            }
            
            return complexFixes.OrderByDescending(r => r.EstimatedImpact).ToList();
        }
        
        /// <summary>
        /// Identify medium complexity fixes
        /// </summary>
        private List<FixRecommendation> IdentifyMediumComplexity(LRSFailureReport failureReport, LRSTestReport testReport)
        {
            var mediumFixes = new List<FixRecommendation>();
            
            // Symbol parsing failures
            var symbolFailures = failureReport.TokenTypeFailures.GetValueOrDefault(TokenType.SYMBOL, 0);
            if (symbolFailures > 10)
            {
                mediumFixes.Add(new FixRecommendation
                {
                    Title = "Improve Symbol Parsing",
                    Description = "Enhance symbol token handling and validation",
                    EstimatedImpact = (double)symbolFailures / failureReport.TotalFailures,
                    EstimatedComplexity = FixComplexity.Medium,
                    AffectedTests = testReport.HighFailureTests.Take(5).Select(t => t.Key).ToList(),
                    Recommendation = "Update LRSAtomicParser symbol handling with better validation"
                });
            }
            
            return mediumFixes.OrderByDescending(r => r.EstimatedImpact).ToList();
        }
        
        /// <summary>
        /// Generate prioritized backlog
        /// </summary>
        private List<FixRecommendation> GeneratePrioritizedBacklog(LRSFailureReport failureReport, LRSTestReport testReport)
        {
            var allRecommendations = new List<FixRecommendation>();
            allRecommendations.AddRange(IdentifyQuickWins(failureReport, testReport));
            allRecommendations.AddRange(IdentifyMediumComplexity(failureReport, testReport));
            allRecommendations.AddRange(IdentifyComplexFixes(failureReport, testReport));
            
            // Sort by impact-to-complexity ratio
            return allRecommendations
                .OrderByDescending(r => r.EstimatedImpact / (int)r.EstimatedComplexity)
                .ToList();
        }
        
        /// <summary>
        /// Categorize failure pattern
        /// </summary>
        private FailurePatternCategory CategorizePattern(string pattern)
        {
            return pattern switch
            {
                "CHARACTER_STRING_FAILURE" => FailurePatternCategory.AtomicParsing,
                "SYMBOL_PARSING_FAILURE" => FailurePatternCategory.AtomicParsing,
                "IDENTIFIER_PARSING_FAILURE" => FailurePatternCategory.AtomicParsing,
                "BRACKET_EXPRESSION_FAILURE" => FailurePatternCategory.ExpressionStructure,
                "ADVERB_EXPRESSION_FAILURE" => FailurePatternCategory.AdverbHandling,
                "NEAR_COMPLETE_FAILURE" => FailurePatternCategory.PartialConsumption,
                "PARTIAL_CONSUMPTION" => FailurePatternCategory.PartialConsumption,
                "IMMEDIATE_FAILURE" => FailurePatternCategory.SyntaxError,
                _ => FailurePatternCategory.Unknown
            };
        }
        
        /// <summary>
        /// Estimate fix complexity for a pattern
        /// </summary>
        private FixComplexity EstimateComplexity(string pattern)
        {
            return pattern switch
            {
                "CHARACTER_STRING_FAILURE" or "IMMEDIATE_FAILURE" => FixComplexity.Low,
                "SYMBOL_PARSING_FAILURE" or "IDENTIFIER_PARSING_FAILURE" => FixComplexity.Medium,
                "BRACKET_EXPRESSION_FAILURE" or "ADVERB_EXPRESSION_FAILURE" => FixComplexity.High,
                "PARTIAL_CONSUMPTION" or "NEAR_COMPLETE_FAILURE" => FixComplexity.Medium,
                _ => FixComplexity.Medium
            };
        }
        
        /// <summary>
        /// Estimate impact of fixing a pattern
        /// </summary>
        private double EstimateImpact(string pattern, int count, int totalFailures)
        {
            var baseImpact = totalFailures > 0 ? (double)count / totalFailures : 0.0;
            
            // Boost impact for high-frequency patterns
            if (count > 50) baseImpact *= 1.5;
            if (count > 100) baseImpact *= 2.0;
            
            return Math.Min(baseImpact, 1.0);
        }
        
        /// <summary>
        /// Get common patterns for specific token type
        /// </summary>
        private List<string> GetCommonPatternsForToken(TokenType tokenType, List<LRSFailureRecord> recentFailures)
        {
            return recentFailures
                .Where(r => r.LastTokenType == tokenType)
                .GroupBy(r => r.FailurePattern)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();
        }
        
        /// <summary>
        /// Get recommended fixes for specific token type
        /// </summary>
        private List<string> GetRecommendedFixesForToken(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.CHARACTER_VECTOR => new List<string> 
                { 
                    "Improve escape sequence handling", 
                    "Fix quote matching", 
                    "Enhanced validation" 
                },
                TokenType.SYMBOL => new List<string> 
                { 
                    "Better symbol validation", 
                    "Improved tokenization", 
                    "Error recovery" 
                },
                TokenType.ADVERB_SLASH or TokenType.ADVERB_BACKSLASH or TokenType.ADVERB_TICK => new List<string> 
                { 
                    "Verb-agnostic adverb parsing", 
                    "Arity validation", 
                    "Better error messages" 
                },
                _ => new List<string> { "General parser improvements" }
            };
        }
    }
    
    /// <summary>
    /// Comprehensive pattern analysis results
    /// </summary>
    public class LRSPatternAnalysis
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalFailures { get; set; }
        public double TestSuccessRate { get; set; }
        
        public List<FailurePatternAnalysis> TopFailurePatterns { get; set; } = new List<FailurePatternAnalysis>();
        public List<TokenFailureAnalysis> TokenSpecificFailures { get; set; } = new List<TokenFailureAnalysis>();
        
        public List<FixRecommendation> QuickWins { get; set; } = new List<FixRecommendation>();
        public List<FixRecommendation> MediumComplexity { get; set; } = new List<FixRecommendation>();
        public List<FixRecommendation> ComplexFixes { get; set; } = new List<FixRecommendation>();
        
        public List<FixRecommendation> PrioritizedBacklog { get; set; } = new List<FixRecommendation>();
        
        /// <summary>
        /// Get summary of analysis
        /// </summary>
        public string GetSummary()
        {
            return $"Total Failures: {TotalFailures}, Success Rate: {TestSuccessRate:P1}, " +
                   $"Quick Wins: {QuickWins.Count}, Complex Fixes: {ComplexFixes.Count}";
        }
    }
    
    /// <summary>
    /// Analysis of a specific failure pattern
    /// </summary>
    public class FailurePatternAnalysis
    {
        public string Pattern { get; set; } = "";
        public int Count { get; set; }
        public double Percentage { get; set; }
        public FailurePatternCategory Category { get; set; }
        public FixComplexity EstimatedComplexity { get; set; }
        public double EstimatedImpact { get; set; }
    }
    
    /// <summary>
    /// Analysis of token-specific failures
    /// </summary>
    public class TokenFailureAnalysis
    {
        public TokenType TokenType { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public List<string> CommonPatterns { get; set; } = new List<string>();
        public List<string> RecommendedFixes { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Fix recommendation with impact and complexity assessment
    /// </summary>
    public class FixRecommendation
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public double EstimatedImpact { get; set; }
        public FixComplexity EstimatedComplexity { get; set; }
        public List<string> AffectedTests { get; set; } = new List<string>();
        public string Recommendation { get; set; } = "";
    }
    
    /// <summary>
    /// Failure pattern categories
    /// </summary>
    public enum FailurePatternCategory
    {
        Unknown,
        AtomicParsing,
        ExpressionStructure,
        AdverbHandling,
        PartialConsumption,
        SyntaxError
    }
    
    /// <summary>
    /// Fix complexity levels
    /// </summary>
    public enum FixComplexity
    {
        Low = 1,
        Medium = 2,
        High = 3
    }
}