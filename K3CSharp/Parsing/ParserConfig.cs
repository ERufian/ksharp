using System;
using System.Collections.Generic;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Parser configuration for gradual LRS migration
    /// Controls feature flags and parsing modes
    /// </summary>
    public static class ParserConfig
    {
        /// <summary>
        /// Enable LRS parser as primary parsing mechanism
        /// </summary>
        public static bool UseLRSParser { get; set; } = true;
        
        /// <summary>
        /// Enable fallback to legacy parser when LRS fails
        /// </summary>
        public static bool EnableFallback { get; set; } = false;
        
        /// <summary>
        /// Enable debugging output for parsing operations
        /// </summary>
        public static bool EnableDebugging { get; set; } = false;
        
        /// <summary>
        /// Enable parse tree construction mode
        /// </summary>
        public static bool BuildParseTree { get; set; } = false;
        
        /// <summary>
        /// Get current parsing mode
        /// </summary>
        public static ParsingMode CurrentMode => UseLRSParser ? 
            (EnableFallback ? ParsingMode.LRSWithFallback : ParsingMode.LRSOnly) : 
            ParsingMode.LegacyOnly;
        
        /// <summary>
        /// Parse with configuration-based mode selection
        /// </summary>
        public static ASTNode? ParseWithConfig(List<Token> tokens, string source)
        {
            var wrapper = new LRSParserWrapper(tokens, source);
            return wrapper.Parse();
        }

        /// <summary>
        /// Check if expression is incomplete using configuration
        /// </summary>
        public static bool IsIncompleteExpressionWithConfig(List<Token> tokens, string source)
        {
            var wrapper = new LRSParserWrapper(tokens, source);
            return wrapper.IsIncompleteExpression();
        }

        /// <summary>
        /// Check if source text is incomplete by scanning raw characters.
        /// Handles unbalanced quotes, parentheses, brackets, and braces without tokenizing.
        /// This avoids Lexer exceptions for unterminated string literals.
        /// Per speclet: a string opened with " but not yet closed means the expression is incomplete.
        /// </summary>
        public static bool IsSourceIncomplete(string source)
        {
            int parens = 0;
            int brackets = 0;
            int braces = 0;
            bool inString = false;
            bool inLineComment = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];

                if (inLineComment)
                {
                    if (c == '\n') inLineComment = false;
                    continue;
                }

                if (inString)
                {
                    if (c == '\\' && i + 1 < source.Length)
                    {
                        i++;
                        continue;
                    }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }

                // Line comment: space followed by /
                if (c == ' ' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (c == '(') parens++;
                else if (c == ')') parens--;
                else if (c == '[') brackets++;
                else if (c == ']') brackets--;
                else if (c == '{') braces++;
                else if (c == '}') braces--;
            }

            return inString || parens != 0 || brackets != 0 || braces != 0;
        }

        /// <summary>
        /// Get parsing statistics for monitoring
        /// </summary>
        public static ParsingStats? GetParsingStats(List<Token> tokens, string source)
        {
            var wrapper = new LRSParserWrapper(tokens, source);
            return wrapper.GetParsingStats();
        }
        
        /// <summary>
        /// Enable LRS parser with safe configuration
        /// </summary>
        public static void EnableLRSSafely()
        {
            UseLRSParser = true;
            EnableFallback = true;
            EnableDebugging = false;
        }
        
        /// <summary>
        /// Disable LRS parser (revert to legacy)
        /// </summary>
        public static void DisableLRS()
        {
            UseLRSParser = false;
            EnableFallback = true;
        }
        
        /// <summary>
        /// Enable pure LRS mode (no fallback)
        /// </summary>
        public static void EnablePureLRS()
        {
            UseLRSParser = true;
            EnableFallback = false;
            EnableDebugging = true;
        }
        
        /// <summary>
        /// Get configuration summary
        /// </summary>
        public static string GetConfigSummary()
        {
            return $"LRS: {(UseLRSParser ? "Enabled" : "Disabled")}, " +
                   $"Fallback: {(EnableFallback ? "Enabled" : "Disabled")}, " +
                   $"Debug: {(EnableDebugging ? "Enabled" : "Disabled")}, " +
                   $"ParseTree: {(BuildParseTree ? "Enabled" : "Disabled")}";
        }
        
        /// <summary>
        /// Log configuration change
        /// </summary>
        public static void LogConfigChange(string operation)
        {
            if (EnableDebugging)
            {
                // Config operation logged
            }
        }
    }
}
