// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Text;

namespace K3CSharp
{
    public class CharacterValue : K3Value
    {
        public string Value { get; }

        public CharacterValue(string value, SymbolValue? hint = null)
        {
            // Validate that CharacterValue can only be created with single characters
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            // Un-escape character sequences before length validation
            string unescapedValue = UnescapeCharacterString(value);
            
            // After un-escaping, check if we have exactly one character OR serialization data
            // Serialization data (from _bd) contains non-printable characters and should be allowed
            if (unescapedValue.Length != 1)
            {
                throw new ArgumentException($"CharacterValue can only be created with single characters, but got string of length {unescapedValue.Length}: '{value}'. Use VectorValue for multi-character strings.");
            }
            
            Value = unescapedValue;
            Type = ValueType.Character;
            Hint = hint;
        }
        
        private static string UnescapeCharacterString(string input)
        {
            if (input.Length == 1)
                return input; // Single character, no escaping needed
                
            var result = new StringBuilder();
            int i = 0;
            
            while (i < input.Length)
            {
                if (input[i] == '\\' && i + 1 < input.Length)
                {
                    switch (input[i + 1])
                    {
                        case '\\':
                            result.Append('\\');
                            i += 2;
                            break;
                        case 'b':
                            result.Append('\b');
                            i += 2;
                            break;
                        case 't':
                            result.Append('\t');
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
                        case '"':
                            result.Append('"');
                            i += 2;
                            break;
                        case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7':
                            // Octal sequence \OOO (up to 3 digits)
                            if (i + 3 < input.Length && char.IsDigit(input[i + 2]) && char.IsDigit(input[i + 3]))
                            {
                                string octalStr = input.Substring(i + 1, 3);
                                int octalValue = 0;
                                foreach (char c in octalStr)
                                {
                                    octalValue = octalValue * 8 + (c - '0');
                                }
                                result.Append((char)octalValue);
                                i += 4;
                                break;
                            }
                            else if (i + 2 < input.Length && char.IsDigit(input[i + 2]))
                            {
                                string octalStr = input.Substring(i + 1, 2);
                                int octalValue = 0;
                                foreach (char c in octalStr)
                                {
                                    octalValue = octalValue * 8 + (c - '0');
                                }
                                result.Append((char)octalValue);
                                i += 3;
                                break;
                            }
                            // If not a valid octal sequence, treat as literal backslash
                            result.Append('\\');
                            i += 1;
                            break;
                        default:
                            // Unknown escape sequence, treat as literal backslash
                            result.Append('\\');
                            i += 1;
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

        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append('"');
            
            foreach (char c in Value)
            {
                switch (c)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '\b':
                        result.Append("\\b");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '"':
                        result.Append("\\\"");
                        break;
                    default:
                        if (c >= ' ' && c <= '~')
                        {
                            // Printable characters (space to tilde)
                            result.Append(c);
                        }
                        else
                        {
                            // Non-printable or extended characters - use 3-digit octal
                            string octalValue = Convert.ToString(Convert.ToInt32(c), 8);
                            result.Append($"\\{octalValue.PadLeft(3, '0')}");
                        }
                        break;
                }
            }
            
            result.Append('"');
            return result.ToString();
        }
    }

    public class SymbolValue : K3Value
    {
        public string Value { get; }

        public SymbolValue(string value, SymbolValue? hint = null)
        {
            Value = value;
            Type = ValueType.Symbol;
            Hint = hint;
        }

        public override string ToString()
        {
            // Check if symbol is empty - in K, empty symbols display as `
            if (string.IsNullOrEmpty(Value))
                return "`";
            
            // Check if symbol is a valid variable name according to K spec
            if (IsValidVariableName(Value))
            {
                // Symbol is a valid variable name, display with backtick only
                return "`" + Value;
            }
            else
            {
                // Symbol is not a valid variable name, display with quotes and backtick
                // Escape special characters to match k.exe display behavior
                var escaped = Value
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
                return $"`\"{escaped}\"";
            }
        }
        
        private static bool IsValidVariableName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            
            // Must contain at least one upper or lower case alphabetic character
            bool hasAlphabetic = false;
            
            foreach (char c in value)
            {
                if (char.IsLetter(c))
                {
                    hasAlphabetic = true;
                }
                
                // If character is not alphanumeric, underscore, period, or hyphen, it's invalid
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.' && c != '-')
                {
                    return false;
                }
            }
            
            return hasAlphabetic;
        }
        
        public string ToStringForFormat()
        {
            // For monadic $ formatting, return symbol name in quotes without backtick
            return $"\"{Value}\"";
        }

        public override bool Equals(object? obj)
        {
            if (obj is SymbolValue other)
            {
                return Value == other.Value;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }
    }
}
