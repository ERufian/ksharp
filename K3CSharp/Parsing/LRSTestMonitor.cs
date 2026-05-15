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
    /// LRS Test Monitoring System
    /// Integrates with test runner to capture LRS vs Legacy parser usage
    /// Tracks which specific test cases trigger LRS failures
    /// Monitors fallback patterns and success rates
    /// </summary>
    public class LRSTestMonitor
    {
        private readonly List<LRSTestRecord> testRecords = new List<LRSTestRecord>();
        private readonly Dictionary<string, TestParsingStats> testStats = new Dictionary<string, TestParsingStats>();
        private readonly Dictionary<ParsingResult, int> resultCounts = new Dictionary<ParsingResult, int>();
        
        /// <summary>
        /// Record a test parsing attempt
        /// </summary>
        public void RecordTest(string testName, string sourceText, ParsingResult result, 
                              ParsingMode mode, int consumedTokens = 0, int totalTokens = 0)
        {
            var record = new LRSTestRecord
            {
                Timestamp = DateTime.UtcNow,
                TestName = testName,
                SourceText = sourceText,
                Result = result,
                Mode = mode,
                ConsumedTokens = consumedTokens,
                TotalTokens = totalTokens
            };
            
            testRecords.Add(record);
            
            // Update result counts
            resultCounts[result] = resultCounts.GetValueOrDefault(result, 0) + 1;
            
            // Update test-specific stats
            if (!testStats.ContainsKey(testName))
            {
                testStats[testName] = new TestParsingStats
                {
                    TestName = testName,
                    TotalAttempts = 0,
                    LRSSuccesses = 0,
                    LegacyFallbacks = 0,
                    Failures = 0
                };
            }
            
            var stats = testStats[testName];
            stats.TotalAttempts++;
            stats.LastAttempt = DateTime.UtcNow;
            
            switch (result)
            {
                case ParsingResult.LRSSuccess:
                    stats.LRSSuccesses++;
                    break;
                case ParsingResult.LegacyFallback:
                    stats.LegacyFallbacks++;
                    break;
                case ParsingResult.Failure:
                    stats.Failures++;
                    break;
            }
        }
        
        /// <summary>
        /// Generate comprehensive test monitoring report
        /// </summary>
        public LRSTestReport GenerateReport()
        {
            var report = new LRSTestReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalTests = testStats.Count,
                TotalAttempts = testRecords.Count,
                ResultCounts = resultCounts,
                TestStats = testStats,
                RecentTests = testRecords.OrderByDescending(r => r.Timestamp)
                                      .Take(100)
                                      .ToList()
            };
            
            // Calculate success rates
            report.LRSSuccessRate = testRecords.Count > 0 
                ? (double)resultCounts.GetValueOrDefault(ParsingResult.LRSSuccess, 0) / testRecords.Count
                : 0.0;
                
            report.LegacyFallbackRate = testRecords.Count > 0
                ? (double)resultCounts.GetValueOrDefault(ParsingResult.LegacyFallback, 0) / testRecords.Count
                : 0.0;
                
            report.FailureRate = testRecords.Count > 0
                ? (double)resultCounts.GetValueOrDefault(ParsingResult.Failure, 0) / testRecords.Count
                : 0.0;
            
            // Find tests with highest failure rates
            report.HighFailureTests = testStats
                .Where(kvp => kvp.Value.TotalAttempts >= 3)
                .Select(kvp => new
                {
                    TestName = kvp.Key,
                    FailureRate = (double)kvp.Value.Failures / kvp.Value.TotalAttempts,
                    Stats = kvp.Value
                })
                .OrderByDescending(x => x.FailureRate)
                .Take(20)
                .ToDictionary(x => x.TestName, x => x.Stats);
            
            // Find tests with highest fallback rates
            report.HighFallbackTests = testStats
                .Where(kvp => kvp.Value.TotalAttempts >= 3)
                .Select(kvp => new
                {
                    TestName = kvp.Key,
                    FallbackRate = (double)kvp.Value.LegacyFallbacks / kvp.Value.TotalAttempts,
                    Stats = kvp.Value
                })
                .OrderByDescending(x => x.FallbackRate)
                .Take(20)
                .ToDictionary(x => x.TestName, x => x.Stats);
            
            return report;
        }
        
        /// <summary>
        /// Get tests that consistently fail with LRS parser
        /// </summary>
        public List<string> GetProblemTests(int minAttempts = 3, double failureRateThreshold = 0.8)
        {
            return testStats
                .Where(kvp => kvp.Value.TotalAttempts >= minAttempts)
                .Where(kvp => (double)kvp.Value.Failures / kvp.Value.TotalAttempts >= failureRateThreshold)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        
        /// <summary>
        /// Get tests that frequently fall back to legacy parser
        /// </summary>
        public List<string> GetHighFallbackTests(int minAttempts = 3, double fallbackRateThreshold = 0.5)
        {
            return testStats
                .Where(kvp => kvp.Value.TotalAttempts >= minAttempts)
                .Where(kvp => (double)kvp.Value.LegacyFallbacks / kvp.Value.TotalAttempts >= fallbackRateThreshold)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        
        /// <summary>
        /// Clear all test records
        /// </summary>
        public void ClearRecords()
        {
            testRecords.Clear();
            testStats.Clear();
            resultCounts.Clear();
        }
        
        /// <summary>
        /// Get statistics for a specific test
        /// </summary>
        public TestParsingStats? GetTestStats(string testName)
        {
            return testStats.GetValueOrDefault(testName);
        }
    }
    
    /// <summary>
    /// Record of a single test parsing attempt
    /// </summary>
    public class LRSTestRecord
    {
        public DateTime Timestamp { get; set; }
        public string TestName { get; set; } = "";
        public string SourceText { get; set; } = "";
        public ParsingResult Result { get; set; }
        public ParsingMode Mode { get; set; }
        public int ConsumedTokens { get; set; }
        public int TotalTokens { get; set; }
        
        public double ConsumptionRatio => TotalTokens > 0 ? (double)ConsumedTokens / TotalTokens : 0.0;
    }
    
    /// <summary>
    /// Statistics for a specific test
    /// </summary>
    public class TestParsingStats
    {
        public string TestName { get; set; } = "";
        public int TotalAttempts { get; set; }
        public int LRSSuccesses { get; set; }
        public int LegacyFallbacks { get; set; }
        public int Failures { get; set; }
        public DateTime LastAttempt { get; set; }
        
        public double LRSSuccessRate => TotalAttempts > 0 ? (double)LRSSuccesses / TotalAttempts : 0.0;
        public double LegacyFallbackRate => TotalAttempts > 0 ? (double)LegacyFallbacks / TotalAttempts : 0.0;
        public double FailureRate => TotalAttempts > 0 ? (double)Failures / TotalAttempts : 0.0;
    }
    
    /// <summary>
    /// Comprehensive test monitoring report
    /// </summary>
    public class LRSTestReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalTests { get; set; }
        public int TotalAttempts { get; set; }
        public Dictionary<ParsingResult, int> ResultCounts { get; set; } = new Dictionary<ParsingResult, int>();
        public Dictionary<string, TestParsingStats> TestStats { get; set; } = new Dictionary<string, TestParsingStats>();
        public List<LRSTestRecord> RecentTests { get; set; } = new List<LRSTestRecord>();
        
        public double LRSSuccessRate { get; set; }
        public double LegacyFallbackRate { get; set; }
        public double FailureRate { get; set; }
        
        public Dictionary<string, TestParsingStats> HighFailureTests { get; set; } = new Dictionary<string, TestParsingStats>();
        public Dictionary<string, TestParsingStats> HighFallbackTests { get; set; } = new Dictionary<string, TestParsingStats>();
        
        /// <summary>
        /// Get summary statistics
        /// </summary>
        public string GetSummary()
        {
            return $"Tests: {TotalTests}, Attempts: {TotalAttempts}, " +
                   $"LRS Success: {LRSSuccessRate:P1}, " +
                   $"Legacy Fallback: {LegacyFallbackRate:P1}, " +
                   $"Failure: {FailureRate:P1}";
        }
        
        /// <summary>
        /// Get top problem tests as formatted string
        /// </summary>
        public string GetProblemTestsSummary(int count = 10)
        {
            var problemTests = HighFailureTests.Take(count);
            return string.Join("\n", problemTests.Select(p => $"{p.Key}: {p.Value.FailureRate:P1} failure rate"));
        }
    }
    
    /// <summary>
    /// Parsing result enumeration
    /// </summary>
    public enum ParsingResult
    {
        LRSSuccess,
        LegacyFallback,
        Failure
    }
}