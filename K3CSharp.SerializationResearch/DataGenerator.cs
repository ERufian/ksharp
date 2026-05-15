// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.SerializationResearch
{
    public enum DataType
    {
        Integer, Float, Character, Symbol, Dictionary, Null, AnonymousFunction,
        IntegerVector, FloatVector, CharacterVector, SymbolVector, List
    }

    public class DataGenerator
    {
        private readonly Random random;

        public DataGenerator()
        {
            random = new Random();
        }

        public List<string> GenerateExamples(DataType dataType, int count, bool edgeCasesOnly)
        {
            if (edgeCasesOnly)
            {
                return GenerateEdgeCases(dataType);
            }

            var examples = new List<string>();
            for (int i = 0; i < count; i++)
            {
                examples.Add(GenerateRandomExample(dataType));
            }
            return examples;
        }

        private List<string> GenerateEdgeCases(DataType dataType)
        {
            return dataType switch
            {
                DataType.Integer => GenerateIntegerEdgeCases(),
                DataType.Float => GenerateFloatEdgeCases(),
                DataType.Character => GenerateCharacterEdgeCases(),
                DataType.Symbol => GenerateSymbolEdgeCases(),
                DataType.Dictionary => GenerateDictionaryEdgeCases(),
                DataType.Null => GenerateNullEdgeCases(),
                DataType.AnonymousFunction => GenerateAnonymousFunctionEdgeCases(),
                DataType.IntegerVector => GenerateIntegerVectorEdgeCases(),
                DataType.FloatVector => GenerateFloatVectorEdgeCases(),
                DataType.CharacterVector => GenerateCharacterVectorEdgeCases(),
                DataType.SymbolVector => GenerateSymbolVectorEdgeCases(),
                DataType.List => GenerateListEdgeCases(),
                _ => throw new ArgumentException($"Unsupported data type: {dataType}")
            };
        }

        private string GenerateRandomExample(DataType dataType)
        {
            return dataType switch
            {
                DataType.Integer => GenerateRandomInteger(),
                DataType.Float => GenerateRandomFloat(),
                DataType.Character => GenerateRandomCharacter(),
                DataType.Symbol => GenerateRandomSymbol(),
                DataType.Dictionary => GenerateRandomDictionary(),
                DataType.Null => "_n",
                DataType.AnonymousFunction => GenerateRandomAnonymousFunction(),
                DataType.IntegerVector => GenerateRandomIntegerVector(),
                DataType.FloatVector => GenerateRandomFloatVector(),
                DataType.CharacterVector => GenerateRandomCharacterVector(),
                DataType.SymbolVector => GenerateRandomSymbolVector(),
                DataType.List => GenerateRandomList(),
                _ => throw new ArgumentException($"Unsupported data type: {dataType}")
            };
        }

        #region Integer Generators
        private List<string> GenerateIntegerEdgeCases()
        {
            return new List<string>
            {
                "0", "1", "-1", "2147483647", "-2147483648",
                "0N", "0I", "-0I"
            };
        }

        private string GenerateRandomInteger()
        {
            // Generate random 32-bit integer
            return random.Next(int.MinValue, int.MaxValue).ToString();
        }
        #endregion

        #region Float Generators
        private List<string> GenerateFloatEdgeCases()
        {
            return new List<string>
            {
                "0.0", "1.0", "-1.0", "0.5", "-0.5",
                "0n", "0i", "-0i",
                "1.7976931348623157E+308", // Max double
                "-1.7976931348623157E+308", // Min double
                "4.940656458412465E-324"    // Min positive double
            };
        }

        private string GenerateRandomFloat()
        {
            // Generate random double with various formats
            double value = random.NextDouble() * random.Next(-1000000, 1000000);
            return value.ToString("G17"); // High precision format
        }
        #endregion

        #region Character Generators
        private List<string> GenerateCharacterEdgeCases()
        {
            return new List<string>
            {
                "\"a\"", "\"b\"", "\"z\"", "\"A\"", "\"Z\"", "\"0\"", "\"9\"",
                "\" \"", "\"\\n\"", "\"\\t\"", "\"\\r\"", "\"\\0\"",
                "\"\\001\"", "\"\\377\"" // Octal escape sequences
            };
        }

        private string GenerateRandomCharacter()
        {
            // Generate random ASCII character (0-255)
            int code = random.Next(0, 256);
            if (code >= 32 && code <= 126) // Printable
            {
                return $"\"{(char)code}\"";
            }
            else // Non-printable - use octal escape
            {
                return $"\"\\{code:D3}\"";
            }
        }
        #endregion

        #region Symbol Generators
        private List<string> GenerateSymbolEdgeCases()
        {
            return new List<string>
            {
                "`a", "`symbol", "`test123", "`_underscore",
                "`\"", "`\"hello\"", "`\"\\n\\t\"", "`\"\\001\"",
                "`" // Empty symbol
            };
        }

        private string GenerateRandomSymbol()
        {
            if (random.Next(0, 2) == 0)
            {
                // Unquoted alphanumeric symbol
                var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
                var length = random.Next(1, 8);
                var symbol = new string(Enumerable.Repeat(chars, length)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
                
                // Ensure it doesn't start with a number
                if (char.IsDigit(symbol[0]))
                {
                    symbol = "_" + symbol;
                }
                return $"`{symbol}";
            }
            else
            {
                // Quoted symbol with random ASCII content
                var length = random.Next(1, 5);
                var content = new string(Enumerable.Range(0, length)
                    .Select(_ => (char)random.Next(0, 256)).ToArray());
                return $"`\"{content}\"";
            }
        }
        #endregion

        #region Dictionary Generators
        private List<string> GenerateDictionaryEdgeCases()
        {
            return new List<string>
            {
                ".()", // Empty dictionary
                ".,(`key;value)", // Single key-value
                ".,(`a;1;`b;2)", // Multiple key-value pairs
                ".,(`\"quoted key\";\"value\")", // Quoted key
                ".,(`nested;.(`inner;1))", // Nested dictionary
                ".(`attr;1;`key;2)" // Dictionary with attribute
            };
        }

        private string GenerateRandomDictionary()
        {
            var count = random.Next(1, 4);
            var pairs = new List<string>();
            
            for (int i = 0; i < count; i++)
            {
                var key = GenerateRandomSymbol();
                var value = random.Next(0, 3) switch
                {
                    0 => random.Next(-100, 100).ToString(),
                    1 => GenerateRandomSymbol(),
                    2 => $"\"{(char)random.Next(32, 127)}\"",
                    _ => "0"
                };
                pairs.Add($"({key};{value})");
            }
            
            // Use enlist operator only for 1-item dictionaries
            var pairString = string.Join(";", pairs);
            return count == 1 ? $",({pairString})" : $"({pairString})";
        }
        #endregion

        #region Null Generator
        private List<string> GenerateNullEdgeCases()
        {
            return new List<string> { "_n" };
        }
        #endregion

        #region Anonymous Function Generators
        private List<string> GenerateAnonymousFunctionEdgeCases()
        {
            return new List<string>
            {
                "{[]}", // Empty function
                "{[x]}", // Args only
                "{x+y}", // Body only with arithmetic
                "{[x;y] x+y}", // Multiple args with arithmetic
                "{[x] {[y] x+y}}", // Nested function
                "{[x] x+y;z-x}", // Multiple expressions with semicolon
                "{[x;y;z] x*y+z/x}", // Complex arithmetic
                "{[x] x<10;x>5}", // Comparison operators
                "{[x] x&y;x|z}", // Bitwise operators
                "{[x] ~x;#x}", // Monadic operators
                "{[x] x@y;x!z}", // Dyadic operators
                "{[x] x::y;x.$z}", // Special operators
                "{[x] x=1;x?2}", // Assignment and conditional
                "{[x] x^y;x%z}", // Power and modulo
                "{[x] _x;+x;-x}", // Monadic arithmetic
                "{[x] .x;@x;~x}" // Special monadic operators
            };
        }

        private string GenerateRandomAnonymousFunction()
        {
            var argCount = random.Next(0, 4); // 0-3 args
            var args = argCount > 0 ? 
                $"[{string.Join("", Enumerable.Range(0, argCount).Select(i => $"{(char)('x' + i)}"))}]" : 
                "[]";
            
            // Define diverse operators as per speclet
            var arithmeticOps = new[] { "+", "-", "*", "%", "/", "^" };
            var comparisonOps = new[] { "<", ">", "=", "<=", ">=" };
            var bitwiseOps = new[] { "&", "|", "^" };
            var monadicOps = new[] { "-", "+", "*", "%", "&", "|", "<", ">", "^", "!", "#", "_", "?", "~", "@", ".", "=", "$" };
            var dyadicOps = new[] { "!", "_", "@", ".", "::", "$" };
            
            var expressions = new List<string>();
            var exprCount = random.Next(1, 4); // 1-3 expressions
            
            for (int i = 0; i < exprCount; i++)
            {
                var exprType = random.Next(0, 6);
                var argVars = args == "[]" ? "0" : string.Join("", Enumerable.Range(0, Math.Min(argCount, 2)).Select(j => $"{(char)('x' + j)}"));
                
                var expr = exprType switch
                {
                    0 => $"{argVars}{arithmeticOps[random.Next(arithmeticOps.Length)]}{random.Next(1, 10)}",
                    1 => $"{argVars}{comparisonOps[random.Next(comparisonOps.Length)]}{random.Next(1, 10)}",
                    2 => $"{argVars}{bitwiseOps[random.Next(bitwiseOps.Length)]}{random.Next(1, 10)}",
                    3 => $"{monadicOps[random.Next(monadicOps.Length)]}{argVars}",
                    4 => $"{argVars}{dyadicOps[random.Next(dyadicOps.Length)]}{random.Next(1, 10)}",
                    5 => $"{argVars}+{random.Next(1, 10)}-{random.Next(1, 5)}*{random.Next(1, 3)}", // Complex expression
                    _ => $"{argVars}+{random.Next(1, 10)}"
                };
                
                expressions.Add(expr);
            }
            
            var body = random.NextDouble() < 0.05 
                ? string.Join("\n", expressions) // Use newlines 5% of the time
                : string.Join(";", expressions); // Use semicolons 95% of the time
            
            return $"{{{args} {body}}}";
        }
        #endregion

        #region Vector Generators
        private List<string> GenerateIntegerVectorEdgeCases()
        {
            return new List<string>
            {
                "!0", // Empty vector
                ",1", // Single element (enlisted)
                "1 2 3", // Multiple elements
                "0N 0I -0I" // Special values
            };
        }

        private string GenerateRandomIntegerVector()
        {
            var length = random.Next(0, 11); // 0-10 elements
            if (length == 0) return "!0";
            
            var elements = Enumerable.Range(0, length)
                .Select(_ => random.Next(-1000, 1000).ToString());
            var vector = string.Join(" ", elements);
            
            // Use enlist operator for single element
            return length == 1 ? $",{vector}" : vector;
        }

        private List<string> GenerateFloatVectorEdgeCases()
        {
            return new List<string>
            {
                "0#0.0", // Empty vector
                ",1.0", // Single element (enlisted)
                "1.0 2.5 3.14", // Multiple elements
                "0n 0i -0i" // Special values
            };
        }

        private string GenerateRandomFloatVector()
        {
            var length = random.Next(0, 11);
            if (length == 0) return "0#0.0";
            
            var elements = Enumerable.Range(0, length)
                .Select(_ => GenerateRandomFloat());
            var vector = string.Join(" ", elements);
            
            // Use enlist operator for single element
            return length == 1 ? $",{vector}" : vector;
        }

        private List<string> GenerateCharacterVectorEdgeCases()
        {
            return new List<string>
            {
                "\"\"", // Empty vector
                ",\"a\"", // Single character (enlisted)
                "\"hello\"", // Multiple characters
                "\"\\n\\t\\r\"" // Escape sequences
            };
        }

        private string GenerateRandomCharacterVector()
        {
            var length = random.Next(0, 11);
            if (length == 0) return "\"\"";
            
            var chars = Enumerable.Range(0, length)
                .Select(_ => (char)random.Next(32, 127)); // Printable only
            var vector = $"\"{new string(chars.ToArray())}\"";
            
            // Use enlist operator for single character
            return length == 1 ? $",{vector}" : vector;
        }

        private List<string> GenerateSymbolVectorEdgeCases()
        {
            return new List<string>
            {
                "0#`", // Empty vector
                ",`a", // Single symbol (enlisted)
                "`a `b `c", // Multiple symbols
                "`\"quoted\" `symbol" // Mixed quoted/unquoted
            };
        }

        private string GenerateRandomSymbolVector()
        {
            var length = random.Next(0, 11);
            if (length == 0) return "0#`";
            
            var symbols = Enumerable.Range(0, length)
                .Select(_ => GenerateRandomSymbol().Trim('`'));
            var vector = string.Join(" ", symbols.Select(s => $"`{s}"));
            
            // Use enlist operator for single symbol
            return length == 1 ? $",{vector}" : vector;
        }
        #endregion

        #region List Generators
        private List<string> GenerateListEdgeCases()
        {
            return new List<string>
            {
                "()", // Empty list
                ",_n", // Single element (enlisted null)
                "(1;2.5;\"a\")", // Mixed types
                "(_n;`symbol;{[]})", // With null, symbol, function
                "((1;2);(3;4))", // Nested lists
                "(.,(`a;1);.,(`b;2))" // With dictionaries (proper 1-item dict syntax)
            };
        }

        private string GenerateRandomList()
        {
            var length = random.Next(0, 6); // 0-5 elements
            if (length == 0) return "()";
            
            var elements = new List<string>();
            var usedTypes = new HashSet<DataType>();
            var lastType = (DataType?)null;
            
            for (int i = 0; i < length; i++)
            {
                DataType type;
                do
                {
                    // Avoid creating uniform lists that would collapse to vectors
                    // If we have 2+ elements and all previous elements are the same basic type,
                    // ensure the next element is different
                    if (i >= 2 && usedTypes.Count == 1)
                    {
                        var basicTypes = new[] { DataType.Integer, DataType.Float, DataType.Character, DataType.Symbol };
                        var differentTypes = basicTypes.Where(t => t != lastType).ToList();
                        type = differentTypes[random.Next(differentTypes.Count)];
                    }
                    else
                    {
                        type = (DataType)random.Next(0, 7); // Basic types only
                    }
                } while (usedTypes.Count < 4 && i < 4 && usedTypes.Contains(type)); // Avoid uniform lists
                
                usedTypes.Add(type);
                lastType = type;
                elements.Add(GenerateRandomExample(type));
            }
            
            return length == 1 ? ",_n" : $"({string.Join(";", elements)})";
        }
        #endregion
    }
}