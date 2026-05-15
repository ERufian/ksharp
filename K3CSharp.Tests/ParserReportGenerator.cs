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
    /// Comprehensive parser analysis report generator
    /// </summary>
    public class ParserReportGenerator
    {
        private readonly ParserAnalysisConfig config;
        private readonly Tests.LRSFailureAnalyzer failureAnalyzer;
        
        public ParserReportGenerator()
        {
            config = ParserAnalysisConfig.Instance;
            failureAnalyzer = new Tests.LRSFailureAnalyzer();
        }
        
        /// <summary>
        /// Generate comprehensive parser analysis report
        /// </summary>
        public void GenerateReport(List<SimpleTestRunner.TestResult> testResults)
        {
            if (!config.ParserAnalysis.EnableReportGeneration)
            {
                return;
            }
            
            try
            {
                var reportPath = config.ParserAnalysis.ReportOutputPath;
                Directory.CreateDirectory(reportPath);
                
                // Get failure records from LRSParserWrapper instead of creating a new analyzer
                var failureRecords = LRSParserWrapper.GetFailureRecords();
                
                // Generate report from failure records
                var report = GenerateReportFromRecords(failureRecords);
                
                // Generate main analysis report
                GenerateMainReport(report, testResults, reportPath);
                
                Console.WriteLine($"Parser analysis report generated at: {reportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating parser report: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generate report from failure records
        /// </summary>
        private LRSFailureReport GenerateReportFromRecords(List<K3CSharp.Parsing.ParserFailureRecord> records)
        {
            // Categorize failures
            var nullResults = records.Where(r => !r.FailurePoint.StartsWith("Incomplete consumption")).ToList();
            var incompleteResults = records.Where(r => r.FailurePoint.StartsWith("Incomplete consumption")).ToList();
            
            var report = new LRSFailureReport
            {
                GeneratedAt = DateTime.Now,
                TotalFailures = records.Count,
                TotalIncorrectResults = 0, // We'll track this separately if needed
                NullResults = nullResults.Count,
                IncompleteResults = incompleteResults.Count
            };
            
            // Convert records to the expected type
            var convertedRecords = records.Select(r => new ParserFailureRecord
            {
                Timestamp = r.Timestamp,
                TestName = r.TestName,
                SourceText = r.SourceText,
                ConsumedTokens = r.ConsumedTokens,
                TotalTokens = r.TotalTokens,
                LastTokenType = r.LastTokenType,
                FailurePoint = r.FailurePoint
            }).ToList();
            
            report.FailureRecords = convertedRecords.Take(config.ParserAnalysis.MaxReportEntries).ToList();
            
            // Generate failure patterns
            var failurePatterns = records
                .GroupBy(r => r.FailurePoint)
                .ToDictionary(g => g.Key, g => g.Count());
            
            report.FailurePatterns = failurePatterns;
            
            return report;
        }
        
        /// <summary>
        /// Generate main comprehensive report
        /// </summary>
        private void GenerateMainReport(LRSFailureReport report, List<SimpleTestRunner.TestResult> testResults, string reportPath)
        {
            var mainReportPath = Path.Combine(reportPath, "LRS_fail_table.md");
            
            using var writer = new StreamWriter(mainReportPath);
            
            writer.WriteLine("# K3CSharp Parser Failures");
            writer.WriteLine();
            writer.WriteLine($"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"**Test Results:** {testResults.Count(t => t.Passed)}/{testResults.Count} passed ({(testResults.Count(t => t.Passed) * 100.0 / testResults.Count):F1}%)");
            writer.WriteLine();
            
            // Executive Summary
            writer.WriteLine("## Executive Summary");
            writer.WriteLine();
            writer.WriteLine($"**Total Tests:** {testResults.Count}");
            writer.WriteLine($"**Passed Tests:** {testResults.Count(t => t.Passed)}");
            writer.WriteLine($"**Failed Tests:** {testResults.Count - testResults.Count(t => t.Passed)}");
            writer.WriteLine($"**Success Rate:** {(testResults.Count(t => t.Passed) * 100.0 / testResults.Count):F1}%");
            writer.WriteLine();
            writer.WriteLine("**LRS Parser Statistics:**");
            writer.WriteLine($"- NULL Results: {report.NullResults}");
            writer.WriteLine($"- Incomplete Token Consumption: {report.IncompleteResults}");
            writer.WriteLine($"- Total Fallbacks to Legacy: {report.TotalFailures}");
            writer.WriteLine($"- Incorrect Results: {report.TotalIncorrectResults}");
            writer.WriteLine($"- LRS is handling: {((testResults.Count - report.TotalFailures) * 100.0 / testResults.Count):F1}%");
            writer.WriteLine();
            
            // Top Failure Patterns
            if (report.FailurePatterns.Any())
            {
                writer.WriteLine("**Top Failure Patterns:**");
                var topPatterns = report.FailurePatterns.OrderByDescending(p => p.Value).Take(10);
                foreach (var pattern in topPatterns)
                {
                    writer.WriteLine($"- {pattern.Key}: {pattern.Value}");
                }
                writer.WriteLine();
            }
            
            // Failure Analysis
            if (report.TotalFailures > 0)
            {
                writer.WriteLine("## LRS Parser Failures");
                writer.WriteLine();
                
                // Show NULL Results
                var nullRecords = report.FailureRecords.Where(r => !r.FailurePoint.StartsWith("Incomplete consumption")).Take(config.ParserAnalysis.MaxReportEntries).ToList();
                if (nullRecords.Any())
                {
                    writer.WriteLine("### NULL Results (LRS returned NULL)");
                    writer.WriteLine();
                    var count = 0;
                    foreach (var record in nullRecords)
                    {
                        var testName = string.IsNullOrEmpty(record.TestName) ? "Unknown" : record.TestName;
                        writer.WriteLine($"{count + 1}. **{testName}**:");
                        writer.WriteLine("```k");
                        writer.WriteLine(record.SourceText);
                        writer.WriteLine("```");
                        writer.WriteLine($"{record.FailurePoint}");
                        writer.WriteLine("-------------------------------------------------");
                        count++;
                    }
                    
                    var moreNull = report.NullResults - nullRecords.Count;
                    if (moreNull > 0)
                    {
                        writer.WriteLine($"... and {moreNull} more NULL result cases");
                        writer.WriteLine();
                    }
                }
                
                // Show Incomplete Consumption Results
                var incompleteRecords = report.FailureRecords.Where(r => r.FailurePoint.StartsWith("Incomplete consumption")).Take(config.ParserAnalysis.MaxReportEntries).ToList();
                if (incompleteRecords.Any())
                {
                    writer.WriteLine("### Incomplete Token Consumption (LRS returned result but didn't consume all tokens)");
                    writer.WriteLine();
                    var count = 0;
                    foreach (var record in incompleteRecords)
                    {
                        var testName = string.IsNullOrEmpty(record.TestName) ? "Unknown" : record.TestName;
                        writer.WriteLine($"**{testName}**:");
                        writer.WriteLine("```k");
                        writer.WriteLine(record.SourceText);
                        writer.WriteLine("```");
                        writer.WriteLine($"{record.FailurePoint} (consumed {record.ConsumedTokens}/{record.TotalTokens})");
                        writer.WriteLine("-------------------------------------------------");
                        count++;
                    }
                    
                    var moreIncomplete = report.IncompleteResults - incompleteRecords.Count;
                    if (moreIncomplete > 0)
                    {
                        writer.WriteLine($"... and {moreIncomplete} more incomplete consumption cases");
                        writer.WriteLine();
                    }
                }
                writer.WriteLine();
            }
            
            // End of report
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine("*Report generated by K3CSharp Parser Analysis System*");
        }
        
        /// <summary>
        /// Generate detailed failure analysis report
        /// </summary>
        private void GenerateDetailedFailureReport(LRSFailureReport report, string reportPath)
        {
            var detailReportPath = Path.Combine(reportPath, "detailed_failure_analysis.md");
            
            using var writer = new StreamWriter(detailReportPath);
            
            writer.WriteLine("# Detailed Parser Failure Analysis");
            writer.WriteLine();
            writer.WriteLine($"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();
            
            // Token Type Analysis
            writer.WriteLine("## Token Type Analysis");
            writer.WriteLine();
            var tokenStats = report.FailureRecords
                .Where(r => r.LastTokenType.HasValue)
                .GroupBy(r => r.LastTokenType!.Value)
                .OrderByDescending(g => g.Count());
            
            foreach (var group in tokenStats)
            {
                writer.WriteLine($"### {group.Key}");
                writer.WriteLine();
                writer.WriteLine($"**Count:** {group.Count()}");
                writer.WriteLine();
                writer.WriteLine("**Examples:**");
                foreach (var record in group.Take(5))
                {
                    writer.WriteLine($"- `{record.SourceText}` ({record.FailurePoint})");
                }
                writer.WriteLine();
            }
            
            // Failure Point Analysis
            writer.WriteLine("## Failure Point Analysis");
            writer.WriteLine();
            var failurePointStats = report.FailureRecords
                .GroupBy(r => r.FailurePoint)
                .OrderByDescending(g => g.Count());
            
            foreach (var group in failurePointStats)
            {
                writer.WriteLine($"### {group.Key}");
                writer.WriteLine();
                writer.WriteLine($"**Count:** {group.Count()}");
                writer.WriteLine();
                writer.WriteLine("**Examples:**");
                foreach (var record in group.Take(5))
                {
                    writer.WriteLine($"- `{record.SourceText}` ({record.ConsumedTokens}/{record.TotalTokens} tokens)");
                }
                writer.WriteLine();
            }
        }
        
        /// <summary>
        /// Generate summary statistics report
        /// </summary>
        private void GenerateSummaryReport(LRSFailureReport report, List<SimpleTestRunner.TestResult> testResults, string reportPath)
        {
            var summaryPath = Path.Combine(reportPath, "summary_statistics.txt");
            
            using var writer = new StreamWriter(summaryPath);
            
            writer.WriteLine("K3CSharp Parser Analysis Summary");
            writer.WriteLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine(new string('=', 50));
            writer.WriteLine();
            
            writer.WriteLine($"Total Tests: {testResults.Count}");
            writer.WriteLine($"Passed Tests: {testResults.Count(t => t.Passed)}");
            writer.WriteLine($"Failed Tests: {testResults.Count(t => !t.Passed)}");
            writer.WriteLine($"Success Rate: {(testResults.Count(t => t.Passed) * 100.0 / testResults.Count):F1}%");
            writer.WriteLine();
            
            writer.WriteLine("LRS Parser Statistics:");
            writer.WriteLine($"- NULL Results: {report.TotalFailures}");
            writer.WriteLine($"- Incorrect Results: {report.TotalIncorrectResults}");
            writer.WriteLine($"- LRS is handling: {((testResults.Count - report.TotalFailures) * 100.0 / testResults.Count):F1}%");
            writer.WriteLine();
            
            if (report.FailurePatterns.Any())
            {
                writer.WriteLine("Top Failure Patterns:");
                foreach (var pattern in report.FailurePatterns.OrderByDescending(p => p.Value).Take(10))
                {
                    writer.WriteLine($"- {pattern.Key}: {pattern.Value}");
                }
                writer.WriteLine();
            }
        }
        
        /// <summary>
        /// Generate simple parse tree representation
        /// </summary>
        private string GenerateSimpleParseTree(ASTNode node)
        {
            if (node == null) return "null";
            
            var result = new System.Text.StringBuilder();
            GenerateSimpleParseTreeRecursive(node, result, 0);
            return result.ToString();
        }
        
        private void GenerateSimpleParseTreeRecursive(ASTNode node, System.Text.StringBuilder result, int depth)
        {
            var indent = new string(' ', depth * 2);
            result.AppendLine($"{indent}{node.Type}");
            
            foreach (var child in node.Children)
            {
                GenerateSimpleParseTreeRecursive(child, result, depth + 1);
            }
        }
    }
}