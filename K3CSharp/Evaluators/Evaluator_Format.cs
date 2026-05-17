// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    /// <summary>
    /// Format verb implementations ($) extracted from Evaluator.
    /// Standalone class — no external dependencies.
    /// </summary>
    public class FormatHandler
    {
        private readonly IEvaluatorContext ctx;

        public FormatHandler(IEvaluatorContext context)
        {
            ctx = context;
        }
        internal K3Value Format(K3Value operand)
        {
            // Monadic $ operator - convert to string representation
            // For vectors, preserve structure and convert each element to string
            return FormatRecursive(operand);
        }
        
        private K3Value FormatRecursive(K3Value value)
        {
            // Handle vectors with consistent recursion
            if (value is VectorValue vec)
            {
                // For string-atomic behavior: character vectors are atomic units
                if (vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
                {
                    // Character vector is atomic - return as-is
                    return vec;
                }
                
                // Regular vector - recursively format each element and create list of character vectors
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    var formattedElement = FormatRecursive(element);
                    // According to K3 spec: result should be a list where each element is a character vector
                    // If formatted element is already a character vector, add it directly (don't double-enlist)
                    if (formattedElement is VectorValue formattedVec && formattedVec.Elements.Count > 0 && formattedVec.Elements.All(e => e is CharacterValue))
                    {
                        result.Add(formattedElement);
                    }
                    else if (formattedElement is CharacterValue)
                    {
                        // Single character - enlist it to make character vector
                        result.Add(new VectorValue(new List<K3Value> { formattedElement }));
                    }
                    else
                    {
                        result.Add(formattedElement);
                    }
                }
                return new VectorValue(result);
            }
            else
            {
                // For non-vector values, convert to string and create character vector
                string str;
                if (value is SymbolValue sym)
                    str = sym.Value;
                else if (value is CharacterValue charVal)
                    str = charVal.Value; // Use raw value, not ToString() which adds quotes
                else
                    str = value.ToString();
                
                // Handle empty string case - return empty character vector
                if (string.IsNullOrEmpty(str))
                {
                    return new VectorValue(new List<K3Value>(), -3);
                }
                
                var charElements = str.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
                return new VectorValue(charElements);
            }
        }
        
        internal K3Value Format(K3Value left, K3Value right)
        {
            // Binary $ operator - form/format according to updated K3 specification
            
            // Handle {} form specifier for evaluating string expressions
            // Check if left is a SymbolValue with "{}"
            if (left is SymbolValue leftSym && leftSym.Value == "{}")
            {
                return ctx.EvaluateStringExpression(right);
            }
            
            // Check if left is a FunctionValue representing empty braces {}
            // This happens when LRS parser parses {} as an empty function
            if (left is FunctionValue funcVal)
            {
                string origText = funcVal.OriginalSourceText?.Trim() ?? "";
                string bodyText = funcVal.BodyText?.Trim() ?? "";
                
                // Check if it represents empty braces {} (with or without whitespace)
                if (origText == "{}" || string.IsNullOrEmpty(bodyText))
                {
                    return ctx.EvaluateStringExpression(right);
                }
            }
            
            // Check if left is a VectorValue containing a FunctionValue (for {} in lists)
            if (left is VectorValue vec && vec.Elements.Count == 1 && 
                vec.Elements[0] is FunctionValue funcVal2)
            {
                string origText2 = funcVal2.OriginalSourceText?.Trim() ?? "";
                string bodyText2 = funcVal2.BodyText?.Trim() ?? "";
                
                if (origText2 == "{}" || string.IsNullOrEmpty(bodyText2))
                {
                    return ctx.EvaluateStringExpression(right);
                }
            }
            
            // Check if this is a type conversion case (0, 0L, 0.0, `, " ", {})
            // These only work on character vectors according to spec
            // Type conversion happens ONLY when:
            // 1. First argument is a type conversion specifier AND
            // 2. Second argument is a character vector
            if (ctx.IsTypeConversionSpecifier(left) && ctx.IsCharacterVectorOrList(right))
            {
                return ctx.PerformTypeConversion(left, right);
            }
            
            // Atomic iteration: dyadic format is a string-atomic function
            // Apply element-wise when both arguments are conformable vectors
            // But NOT for type conversion specifiers (already handled above)
            // Character vectors are treated as atomic units (not broken into characters)
            if (left is VectorValue leftVec && right is VectorValue rightVec)
            {
                // Check if right is a character vector - treat as atomic
                if (rightVec.Elements.Count > 0 && rightVec.Elements.All(e => e is CharacterValue))
                {
                    // Character vector is atomic, don't iterate
                    // Fall through to normal format handling
                }
                // Check conformability at top level
                else if (leftVec.Elements.Count == rightVec.Elements.Count)
                {
                    // Apply format element-wise
                    var result = new List<K3Value>();
                    for (int i = 0; i < leftVec.Elements.Count; i++)
                    {
                        result.Add(Format(leftVec.Elements[i], rightVec.Elements[i]));
                    }
                    return new VectorValue(result);
                }
            }
            // Atomic iteration: left vector, right atom
            // But NOT if right is a character vector (already handled above)
            if (left is VectorValue leftVec2 && !(right is VectorValue))
            {
                var result = new List<K3Value>();
                foreach (var leftElem in leftVec2.Elements)
                {
                    result.Add(Format(leftElem, right));
                }
                return new VectorValue(result);
            }
            // Atomic iteration: left atom, right vector
            // But NOT if right is a character vector (treat as atomic)
            if (!(left is VectorValue) && right is VectorValue rightVec2)
            {
                // Check if right is a character vector - treat as atomic
                if (rightVec2.Elements.Count > 0 && rightVec2.Elements.All(e => e is CharacterValue))
                {
                    // Character vector is atomic, don't iterate
                    // Fall through to normal format handling
                }
                else
                {
                    var result = new List<K3Value>();
                    foreach (var rightElem in rightVec2.Elements)
                    {
                        result.Add(Format(left, rightElem));
                    }
                    return new VectorValue(result);
                }
            }
            
            // Otherwise, this is a format operation with numeric specifier
            if (left is IntegerValue intFormat)
            {
                return FormatWithSpecifier(intFormat.Value, right);
            }
            else if (left is LongValue longFormat)
            {
                return FormatWithSpecifier((int)longFormat.Value, right);
            }
            else if (left is FloatValue floatFormat)
            {
                return FormatWithFloatSpecifier(floatFormat.Value, right);
            }
            else
            {
                throw new Exception($"Invalid format specifier: {left}");
            }
        }
        
        private K3Value FormatWithSpecifier(int formatSpec, K3Value value)
        {
            // Check if this is a character vector (string) - treat as leaf element per spec
            if (value is VectorValue vec && vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
            {
                // Character vector should be treated as a leaf element, not descended into
                return FormatElement(formatSpec, value);
            }
            else if (value is VectorValue regularVec)
            {
                // Create a list of character vectors, one for each element in the input vector
                var result = new List<K3Value>();
                foreach (var element in regularVec.Elements)
                {
                    var formattedElement = FormatElement(formatSpec, element);
                    result.Add(formattedElement);
                }
                
                return new VectorValue(result);
            }
            else
            {
                return FormatElement(formatSpec, value);
            }
        }
        
        private K3Value FormatElement(int formatSpec, K3Value value)
        {
            string str;

            // Handle character vectors (strings) properly
            if (value is VectorValue charVec && charVec.Elements.Count > 0 && charVec.Elements.All(e => e is CharacterValue))
            {
                // Extract the raw string content from character vector
                var chars = charVec.Elements.Select(e => ((CharacterValue)e).Value);
                str = string.Concat(chars);
            }
            else if (value is SymbolValue symValue)
            {
                // For symbols, format just the name without the backtick
                str = symValue.Value;
            }
            else if (value is FloatValue floatVal)
            {
                // Integer format spec: format float as integer (truncate decimals)
                if (floatVal.Value == Math.Floor(floatVal.Value) && !double.IsInfinity(floatVal.Value) && !double.IsNaN(floatVal.Value))
                {
                    str = ((long)floatVal.Value).ToString();
                }
                else
                {
                    str = value.ToString();
                }
            }
            else
            {
                str = value.ToString();
            }
            
            if (formatSpec > 0)
            {
                // Positive: pad with spaces on the left
                if (str.Length < formatSpec)
                {
                    str = str.PadLeft(formatSpec);
                }
                // If str.Length >= formatSpec, return as-is (no truncation)
            }
            else if (formatSpec < 0)
            {
                // Negative: pad with spaces on the right
                int targetLength = Math.Abs(formatSpec);
                if (str.Length < targetLength)
                {
                    str = str.PadRight(targetLength);
                }
                // If str.Length >= targetLength, return as-is (no truncation)
            }
            else // formatSpec == 0
            {
                // Format specifier 0: return empty character vector
                return new VectorValue(new List<K3Value>(), -3);
            }
            
            // According to K3 spec: format operations should return character vectors
            // Convert string to character vector for proper comma prefix handling
            var charElements = str.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
            return new VectorValue(charElements);
        }
        
        private K3Value FormatWithFloatSpecifier(double formatSpec, K3Value value)
        {
            // Check if this is a character vector (string) - treat as leaf element per spec
            if (value is VectorValue vec && vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
            {
                // Character vector should be treated as a leaf element, not descended into
                return FormatFloatElement(formatSpec, value);
            }
            else if (value is VectorValue regularVec)
            {
                // Create a list of character vectors, one for each element in the input vector
                var result = new List<K3Value>();
                foreach (var element in regularVec.Elements)
                {
                    var formattedElement = FormatFloatElement(formatSpec, element);
                    result.Add(formattedElement);
                }
                
                return new VectorValue(result);
            }
            else
            {
                return FormatFloatElement(formatSpec, value);
            }
        }
        
        private K3Value FormatFloatElement(double formatSpec, K3Value value)
        {
            // Extract width and decimal places from format specifier
            // For example: 8.2 means width 8 with 2 decimal places
            string formatSpecStr = formatSpec.ToString("F10").TrimEnd('0').TrimEnd('.');
            string[] parts = formatSpecStr.Split('.');
            int totalWidth = (int)Math.Truncate(formatSpec);
            int decimalPlaces = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            
            // Get the numeric value
            double numericValue;
            if (value is FloatValue fv)
            {
                numericValue = fv.Value;
            }
            else if (value is IntegerValue iv)
            {
                numericValue = (double)iv.Value;
            }
            else if (value is LongValue lv)
            {
                numericValue = (double)lv.Value;
            }
            else
            {
                return new CharacterValue(value.ToString());
            }
            
            // Use string.Format for clean formatting with width and precision
            string formatString = totalWidth > 0 
                ? $"{{0,{totalWidth}:F{decimalPlaces}}}"  // e.g., "{0,8:F2}"
                : $"{{0:F{decimalPlaces}}}";             // e.g., "{0:F2}"
            
            string str = string.Format(formatString, numericValue);
            
            // Handle negative width (right padding) - string.Format only handles left padding
            if (totalWidth < 0 && str.Length < Math.Abs(totalWidth))
            {
                str = str.PadRight(Math.Abs(totalWidth));
            }
            
            // According to K3 spec: format operations should return character vectors
            // Convert string to character vector for proper comma prefix handling
            var charElements = str.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
            return new VectorValue(charElements);
        }
    }
}