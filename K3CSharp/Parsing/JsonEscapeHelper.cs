// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Helper class for processing JSON escape sequences in MCP server communication
    /// </summary>
    public static class JsonEscapeHelper
    {
        /// <summary>
        /// Escapes a C# string for safe JSON serialization
        /// </summary>
        /// <param name="input">The input string to escape</param>
        /// <returns>JSON-safe escaped string</returns>
        public static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder();
            foreach (char c in input)
            {
                switch (c)
                {
                    case '"':
                        result.Append("\\\"");
                        break;
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '\b':
                        result.Append("\\b");
                        break;
                    case '\f':
                        result.Append("\\f");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    default:
                        if (c < 0x20 || (c >= 0x7F && c <= 0x9F))
                        {
                            // Control characters must be escaped
                            result.Append($"\\u{(int)c:x4}");
                        }
                        else if (c > 0xFFFF)
                        {
                            // Handle surrogate pairs for characters > U+FFFF
                            result.Append($"\\U{(int)c:x8}");
                        }
                        else
                        {
                            result.Append(c);
                        }
                        break;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Unescapes a JSON string to C# string
        /// </summary>
        /// <param name="input">The JSON-escaped string</param>
        /// <returns>Unescaped C# string</returns>
        public static string UnescapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new StringBuilder();
            var i = 0;
            
            while (i < input.Length)
            {
                if (input[i] == '\\' && i + 1 < input.Length)
                {
                    switch (input[i + 1])
                    {
                        case '"':
                            result.Append('"');
                            i += 2;
                            break;
                        case '\\':
                            result.Append('\\');
                            i += 2;
                            break;
                        case '/':
                            result.Append('/');
                            i += 2;
                            break;
                        case 'b':
                            result.Append('\b');
                            i += 2;
                            break;
                        case 'f':
                            result.Append('\f');
                            i += 2;
                            break;
                        case 'n':
                            result.Append('\n');
                            i += 2;
                            break;
                        case 'r':
                            result.Append('\r');
                            i += 2;
                            break;
                        case 't':
                            result.Append('\t');
                            i += 2;
                            break;
                        case 'u':
                            if (i + 5 < input.Length)
                            {
                                var hex = input.Substring(i + 2, 4);
                                try
                                {
                                    var codePoint = Convert.ToInt32(hex, 16);
                                    result.Append((char)codePoint);
                                    i += 6;
                                }
                                catch
                                {
                                    // Invalid Unicode escape, keep as-is
                                    result.Append(input.Substring(i, 6));
                                    i += 6;
                                }
                            }
                            else
                            {
                                // Incomplete Unicode escape, keep as-is
                                result.Append(input.Substring(i));
                                break;
                            }
                            break;
                        default:
                            // Unknown escape sequence, keep as-is
                            result.Append(input[i]);
                            i++;
                            break;
                    }
                }
                else
                {
                    result.Append(input[i]);
                    i++;
                }
            }
            
            return result.ToString();
        }

        /// <summary>
        /// Processes escape sequences in a string, handling both JSON escapes and custom escapes
        /// </summary>
        /// <param name="input">The input string with escape sequences</param>
        /// <returns>Processed string with escape sequences resolved</returns>
        public static string ProcessJsonEscapes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // First handle custom escape character (´ -> ")
            var processed = input.Replace('´', '"');
            
            // Then process JSON escape sequences
            return UnescapeJsonString(processed);
        }

        /// <summary>
        /// Validates if a string contains valid JSON escape sequences
        /// </summary>
        /// <param name="input">The string to validate</param>
        /// <returns>True if all escape sequences are valid</returns>
        public static bool IsValidJsonEscapes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            var i = 0;
            while (i < input.Length)
            {
                if (input[i] == '\\' && i + 1 < input.Length)
                {
                    var nextChar = input[i + 1];
                    if (!IsValidEscapeCharacter(nextChar))
                    {
                        return false;
                    }
                    
                    if (nextChar == 'u')
                    {
                        // Validate Unicode escape sequence
                        if (i + 5 >= input.Length)
                            return false;
                        
                        var hex = input.Substring(i + 2, 4);
                        if (!Regex.IsMatch(hex, @"^[0-9a-fA-F]{4}$"))
                            return false;
                        
                        i += 6;
                    }
                    else
                    {
                        i += 2;
                    }
                }
                else
                {
                    i++;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Checks if a character is a valid JSON escape character
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <returns>True if valid escape character</returns>
        private static bool IsValidEscapeCharacter(char c)
        {
            return c == '"' || c == '\\' || c == '/' || c == 'b' || 
                   c == 'f' || c == 'n' || c == 'r' || c == 't' || c == 'u';
        }
    }
}