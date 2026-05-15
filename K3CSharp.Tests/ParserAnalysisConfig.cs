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

namespace K3CSharp.Tests
{
    /// <summary>
    /// Configuration manager for parser analysis and reporting
    /// </summary>
    public class ParserAnalysisConfig
    {
        private static ParserAnalysisConfig? _instance;
        private static readonly string ConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "parser_config.json");
        
        public static ParserAnalysisConfig Instance => _instance ??= LoadConfig();
        
        public ParserAnalysisSettings ParserAnalysis { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
        
        private static ParserAnalysisConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = System.Text.Json.JsonSerializer.Deserialize<ParserAnalysisConfig>(json, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return config ?? new ParserAnalysisConfig();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load parser config: {ex.Message}");
            }
            
            return new ParserAnalysisConfig();
        }
        
        public static void ReloadConfig()
        {
            _instance = LoadConfig();
        }
    }
    
    public class ParserAnalysisSettings
    {
        public bool EnableReportGeneration { get; set; } = false;
        public string ReportOutputPath { get; set; } = "parser_results";
        public int MaxReportEntries { get; set; } = 1000;
        public bool IncludeParseTrees { get; set; } = true;
        public bool IncludeLegacyParserAST { get; set; } = true;
        public string ReportFormat { get; set; } = "Markdown";
        public bool GenerateDetailedFailureAnalysis { get; set; } = true;
        public bool GroupFailuresByType { get; set; } = true;
    }
    
    public class LoggingSettings
    {
        public bool EnableDebugMessages { get; set; } = false;
        public bool EnableLRSWrapperMessages { get; set; } = false;
        public string LogLevel { get; set; } = "Info";
    }
}