// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using K3CSharp.Parsing;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private K3Value ConvertToString(K3Value value)
        {
            // Convert value to string representation without monadic format enlisting
            if (value is VectorValue vec && vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
            {
                // Character vector - return as-is
                return vec;
            }
            else
            {
                // Convert to string and create proper character vector with individual characters
                string str = value is SymbolValue sym ? sym.Value : value.ToString();
                var charElements = str.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
                return new VectorValue(charElements);
            }
        }
    private K3Value ConvertType(K3Value typeSpec, K3Value value)
        {
            // Check for special type specifiers: 0, 0L, 0.0, `, " ", {}
            if (typeSpec is IntegerValue intSpec && intSpec.Value == 0)
            {
                // Convert to integer
                return ConvertToInteger(value);
            }
            else if (typeSpec is LongValue longSpec && longSpec.Value == 0)
            {
                // Convert to long integer
                return ConvertToLong(value);
            }
            else if (typeSpec is FloatValue floatSpec && floatSpec.Value == 0.0)
            {
                // Convert to float
                return ConvertToFloat(value);
            }
            else if (typeSpec is SymbolValue symSpec && symSpec.Value == "")
            {
                // Convert to symbol (empty backtick)
                return ConvertToSymbol(value);
            }
            else if (typeSpec is CharacterValue charSpec && charSpec.Value == " ")
            {
                // Convert to string/character representation
                // If already a character vector, return as-is
                if (value is VectorValue vecVal && vecVal.Elements.Count > 0 && vecVal.Elements.All(e => e is CharacterValue))
                {
                    return vecVal;
                }
                else if (value is CharacterValue)
                {
                    return value;
                }
                return ConvertToString(value);
            }
            else if (typeSpec is FunctionValue funcSpec && funcSpec.BodyText == "")
            {
                // Evaluate expressions ({} case) - evaluate each leaf expression
                return EvaluateExpressions(value);
            }
            else if (typeSpec is VectorValue vecSpec && vecSpec.Elements.Count == 0)
            {
                // Evaluate expressions ({} case) - evaluate each leaf expression
                return EvaluateExpressions(value);
            }
            else
            {
                throw new Exception($"Invalid type specifier: {typeSpec}");
            }
        }
        
        private K3Value EvaluateExpressions(K3Value value)
        {
            // {} format specifier - evaluate each leaf expression
            if (value is VectorValue vec)
            {
                // Check if this is a character vector (string) that should be evaluated
                if (vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
                {
                    // Reconstruct string from individual characters
                    var chars = vec.Elements.Select(e => ((CharacterValue)e).Value);
                    var code = string.Concat(chars);
                    
                    // Remove outer quotes if present
                    if (code.StartsWith("\"") && code.EndsWith("\"") && code.Length > 1)
                    {
                        code = code.Substring(1, code.Length - 1);
                    }
                    
                    // Evaluate the reconstructed string using full parser for consistency
                    var lexer = new Lexer(code);
                    var tokens = lexer.Tokenize();
                    var ast = ParserConfig.ParseWithConfig(tokens, code);
                    if (ast != null)
                    {
                        return Evaluate(ast) ?? new NullValue();
                    }
                    return new NullValue();
                }
                
                // Regular vector - recurse on elements
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(EvaluateExpressions(element));
                }
                return new VectorValue(result);
            }
            else if (value is CharacterValue charVal)
            {
                // For character values (strings), evaluate as K code using full parser
                var code = charVal.Value;
                if (code.StartsWith("\"") && code.EndsWith("\""))
                {
                    code = code.Substring(1, code.Length - 2);
                }
                
                // Use full parser for consistency
                var lexer = new Lexer(code);
                var tokens = lexer.Tokenize();
                var ast = ParserConfig.ParseWithConfig(tokens, code);
                if (ast != null)
                {
                    return Evaluate(ast) ?? new NullValue();
                }
                return new NullValue();
            }
            else
            {
                // For non-string scalars, return as-is
                return value;
            }
        }
        
        private K3Value ConvertToInteger(K3Value value)
        {
            if (value is VectorValue vec)
            {
                // Check if this is a character vector (string) - treat as leaf element
                if (vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
                {
                    // Extract string content from character vector
                    var str = string.Join("", vec.Elements.Cast<CharacterValue>().Select(c => c.Value));
                    if (int.TryParse(str, out int parseResult))
                    {
                        return new IntegerValue(parseResult);
                    }
                    else
                    {
                        throw new Exception($"Cannot convert '{str}' to integer");
                    }
                }
                
                // Regular vector - recursively convert each element
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(ConvertToInteger(element));
                }
                return new VectorValue(result);
            }
            else if (value is CharacterValue charVal)
            {
                // Single character - try to parse as integer first, fallback to ASCII
                if (int.TryParse(charVal.Value, out int parseResult))
                {
                    return new IntegerValue(parseResult);
                }
                else
                {
                    return new IntegerValue((int)charVal.Value[0]);
                }
            }
            else if (value is IntegerValue)
            {
                return value;
            }
            else if (value is LongValue lv)
            {
                return new IntegerValue((int)lv.Value);
            }
            else if (value is FloatValue fv)
            {
                return new IntegerValue((int)fv.Value);
            }
            else if (value is SymbolValue sv)
            {
                // Try to parse symbol as integer
                if (int.TryParse(sv.Value, out int result))
                {
                    return new IntegerValue(result);
                }
                throw new Exception($"Cannot convert symbol '{sv.Value}' to integer");
            }
            else
            {
                throw new Exception($"Cannot convert {value.Type} to integer");
            }
        }
        
        private K3Value ConvertToLong(K3Value value)
        {
            if (value is VectorValue vec)
            {
                // Check if this is a character vector (string) - treat as leaf element
                if (vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
                {
                    // Extract string content from character vector
                    var str = string.Join("", vec.Elements.Cast<CharacterValue>().Select(c => c.Value));
                    if (long.TryParse(str, out long parseResult))
                    {
                        return new LongValue(parseResult);
                    }
                    else
                    {
                        throw new Exception($"Cannot convert '{str}' to long");
                    }
                }
                
                // Regular vector - recursively convert each element
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(ConvertToLong(element));
                }
                return new VectorValue(result);
            }
            else if (value is CharacterValue charVal)
            {
                // Single character - convert to long
                if (long.TryParse(charVal.Value, out long parseResult))
                {
                    return new LongValue(parseResult);
                }
                else
                {
                    throw new Exception($"Cannot convert '{charVal.Value}' to long");
                }
            }
            else if (value is LongValue)
            {
                return value;
            }
            else if (value is IntegerValue iv)
            {
                return new LongValue(iv.Value);
            }
            else if (value is FloatValue fv)
            {
                return new LongValue((long)fv.Value);
            }
            else
            {
                throw new Exception($"Cannot convert {value.Type} to long");
            }
        }
        
        private K3Value ConvertToFloat(K3Value value)
        {
            // Check for character vectors first (highest priority)
            if (value is VectorValue charVec && charVec.Elements.Count > 0 && charVec.Elements.All(e => e is CharacterValue))
            {
                // Convert character vector to float by concatenating characters and parsing
                var chars = charVec.Elements.Select(e => ((CharacterValue)e).Value);
                var str = string.Concat(chars);
                if (double.TryParse(str, out double result))
                {
                    return new FloatValue(result);
                }
                throw new Exception($"Cannot convert character vector '{str}' to float");
            }
            else if (value is VectorValue vec)
            {
                // Handle general vectors (non-character vectors)
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(ConvertToFloat(element));
                }
                return new VectorValue(result);
            }
            else if (value is FloatValue)
            {
                return value;
            }
            else if (value is IntegerValue iv)
            {
                return new FloatValue(iv.Value);
            }
            else if (value is LongValue lv)
            {
                return new FloatValue(lv.Value);
            }
            else if (value is CharacterValue cv)
            {
                // Convert character to float by parsing its string representation
                if (double.TryParse(cv.Value, out double result))
                {
                    return new FloatValue(result);
                }
                throw new Exception($"Cannot convert character '{cv.Value}' to float");
            }
            else
            {
                throw new Exception($"Cannot convert {value.Type} to float");
            }
        }
        
        private K3Value ConvertToSymbol(K3Value value)
        {
            // Check if this is a character vector (string) - treat as leaf element per spec
            if (value is VectorValue vec && vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
            {
                // Character vector should be treated as a leaf element, not descended into
                // Extract the string content and convert to a single symbol
                var chars = vec.Elements.Select(e => ((CharacterValue)e).Value);
                var symbolName = string.Concat(chars);
                return new SymbolValue(symbolName);
            }
            else if (value is VectorValue regularVec)
            {
                var result = new List<K3Value>();
                foreach (var element in regularVec.Elements)
                {
                    result.Add(ConvertToSymbol(element));
                }
                return new VectorValue(result);
            }
            else if (value is SymbolValue)
            {
                return value;
            }
            else if (value is CharacterValue charVal)
            {
                return new SymbolValue(charVal.Value);
            }
            else
            {
                return new SymbolValue(value.ToString());
            }
        }
    }
}