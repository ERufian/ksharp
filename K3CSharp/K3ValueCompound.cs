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
    public class VectorValue : K3Value
    {
        public List<K3Value> Elements { get; }
        public int? VectorType { get; private set; } // Track type for empty vectors

        public VectorValue(List<K3Value> elements, SymbolValue? hint = null)
        {
            Elements = elements;
            Type = ValueType.Vector;
            Hint = hint;
            VectorType = DetermineVectorTypeFromElements(elements);
        }

        public VectorValue(List<K3Value> elements, int vectorType, SymbolValue? hint = null)
        {
            Elements = elements;
            Type = ValueType.Vector;
            Hint = hint;
            VectorType = vectorType;
        }

        private static int DetermineVectorTypeFromElements(List<K3Value> elements)
        {
            if (elements.Count == 0)
                return 0; // Default to mixed list for empty vectors
                
            // Check if all elements are floats
            bool allFloats = true;
            foreach (var element in elements)
            {
                if (!(element is FloatValue))
                {
                    allFloats = false;
                    break;
                }
            }
            if (allFloats)
                return -2; // Float vector
                
            // Check if any element is a float (mixed integers and floats)
            bool hasFloats = false;
            foreach (var element in elements)
            {
                if (element is FloatValue)
                {
                    hasFloats = true;
                    break;
                }
            }
            
            // Check if all elements are integers/longs (ignoring floats for this check)
            bool allIntegersOrLongs = true;
            foreach (var element in elements)
            {
                if (!(element is IntegerValue || element is LongValue))
                {
                    allIntegersOrLongs = false;
                    break;
                }
            }
            
            // Check if all elements are symbols
            bool allSymbols = true;
            foreach (var element in elements)
            {
                if (!(element is SymbolValue))
                {
                    allSymbols = false;
                    break;
                }
            }
            
            // Determine type based on element composition
            if (allFloats)
                return -2; // Float vector
            else if (allIntegersOrLongs && !hasFloats)
                return -1; // Integer vector
            else if (allSymbols)
                return -4; // Symbol vector
            else if (elements[0] is CharacterValue)
                return -3; // Character vector
            else
                return 0; // Default to mixed list (for mixed types)
        }

        public K3Value Minimum(VectorValue other)
        {
            if (Elements.Count != other.Elements.Count)
                throw new InvalidOperationException("Vector size mismatch for minimum");
            
            var result = new List<K3Value>();
            for (int i = 0; i < Elements.Count; i++)
            {
                // Handle nested vectors (matrices) recursively
                if (Elements[i] is VectorValue vecA && other.Elements[i] is VectorValue vecB)
                {
                    result.Add(vecA.Minimum(vecB));
                }
                else if (Elements[i] is IntegerValue intA && other.Elements[i] is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Min(intA.Value, intB.Value)));
                else if (Elements[i] is LongValue longA && other.Elements[i] is LongValue longB)
                    result.Add(new LongValue(Math.Min(longA.Value, longB.Value)));
                else if (Elements[i] is FloatValue floatA && other.Elements[i] is FloatValue floatB)
                    result.Add(new FloatValue(Math.Min(floatA.Value, floatB.Value)));
                // Mixed numeric types: promote to wider type
                else if (Elements[i] is IntegerValue intA2 && other.Elements[i] is LongValue longB2)
                    result.Add(new LongValue(Math.Min(intA2.Value, longB2.Value)));
                else if (Elements[i] is LongValue longA2 && other.Elements[i] is IntegerValue intB2)
                    result.Add(new LongValue(Math.Min(longA2.Value, intB2.Value)));
                else if (Elements[i] is IntegerValue intA3 && other.Elements[i] is FloatValue floatB3)
                    result.Add(new FloatValue(Math.Min(intA3.Value, floatB3.Value)));
                else if (Elements[i] is FloatValue floatA3 && other.Elements[i] is IntegerValue intB3)
                    result.Add(new FloatValue(Math.Min(floatA3.Value, intB3.Value)));
                else if (Elements[i] is LongValue longA4 && other.Elements[i] is FloatValue floatB4)
                    result.Add(new FloatValue(Math.Min(longA4.Value, floatB4.Value)));
                else if (Elements[i] is FloatValue floatA4 && other.Elements[i] is LongValue longB4)
                    result.Add(new FloatValue(Math.Min(floatA4.Value, longB4.Value)));
                else
                    throw new InvalidOperationException("Cannot find minimum of mixed types");
            }
            return new VectorValue(result);
        }

        public K3Value Minimum(K3Value scalar)
        {
            var result = new List<K3Value>();
            foreach (var element in Elements)
            {
                // Handle nested vectors (matrices) recursively
                if (element is VectorValue vecA)
                {
                    result.Add(vecA.Minimum(scalar));
                }
                else if (element is IntegerValue intA && scalar is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Min(intA.Value, intB.Value)));
                else if (element is LongValue longA && scalar is LongValue longB)
                    result.Add(new LongValue(Math.Min(longA.Value, longB.Value)));
                else if (element is FloatValue floatA && scalar is FloatValue floatB)
                    result.Add(new FloatValue(Math.Min(floatA.Value, floatB.Value)));
                // Mixed numeric types: promote to wider type
                else if (element is IntegerValue intA2 && scalar is LongValue longB2)
                    result.Add(new LongValue(Math.Min(intA2.Value, longB2.Value)));
                else if (element is LongValue longA2 && scalar is IntegerValue intB2)
                    result.Add(new LongValue(Math.Min(longA2.Value, intB2.Value)));
                else if (element is IntegerValue intA3 && scalar is FloatValue floatB3)
                    result.Add(new FloatValue(Math.Min(intA3.Value, floatB3.Value)));
                else if (element is FloatValue floatA3 && scalar is IntegerValue intB3)
                    result.Add(new FloatValue(Math.Min(floatA3.Value, intB3.Value)));
                else if (element is LongValue longA4 && scalar is FloatValue floatB4)
                    result.Add(new FloatValue(Math.Min(longA4.Value, floatB4.Value)));
                else if (element is FloatValue floatA4 && scalar is LongValue longB4)
                    result.Add(new FloatValue(Math.Min(floatA4.Value, longB4.Value)));
                else
                    throw new InvalidOperationException("Cannot find minimum of mixed types");
            }
            return new VectorValue(result);
        }

        public K3Value Maximum(VectorValue other)
        {
            if (Elements.Count != other.Elements.Count)
                throw new InvalidOperationException("Vector size mismatch for maximum");
            
            var result = new List<K3Value>();
            for (int i = 0; i < Elements.Count; i++)
            {
                // Handle nested vectors (matrices) recursively
                if (Elements[i] is VectorValue vecA && other.Elements[i] is VectorValue vecB)
                {
                    result.Add(vecA.Maximum(vecB));
                }
                else if (Elements[i] is IntegerValue intA && other.Elements[i] is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Max(intA.Value, intB.Value)));
                else if (Elements[i] is LongValue longA && other.Elements[i] is LongValue longB)
                    result.Add(new LongValue(Math.Max(longA.Value, longB.Value)));
                else if (Elements[i] is FloatValue floatA && other.Elements[i] is FloatValue floatB)
                    result.Add(new FloatValue(Math.Max(floatA.Value, floatB.Value)));
                // Mixed numeric types: promote to wider type
                else if (Elements[i] is IntegerValue intA2 && other.Elements[i] is LongValue longB2)
                    result.Add(new LongValue(Math.Max(intA2.Value, longB2.Value)));
                else if (Elements[i] is LongValue longA2 && other.Elements[i] is IntegerValue intB2)
                    result.Add(new LongValue(Math.Max(longA2.Value, intB2.Value)));
                else if (Elements[i] is IntegerValue intA3 && other.Elements[i] is FloatValue floatB3)
                    result.Add(new FloatValue(Math.Max(intA3.Value, floatB3.Value)));
                else if (Elements[i] is FloatValue floatA3 && other.Elements[i] is IntegerValue intB3)
                    result.Add(new FloatValue(Math.Max(floatA3.Value, intB3.Value)));
                else if (Elements[i] is LongValue longA4 && other.Elements[i] is FloatValue floatB4)
                    result.Add(new FloatValue(Math.Max(longA4.Value, floatB4.Value)));
                else if (Elements[i] is FloatValue floatA4 && other.Elements[i] is LongValue longB4)
                    result.Add(new FloatValue(Math.Max(floatA4.Value, longB4.Value)));
                else
                    throw new InvalidOperationException("Cannot find maximum of mixed types");
            }
            return new VectorValue(result);
        }

        public K3Value Maximum(K3Value scalar)
        {
            var result = new List<K3Value>();
            foreach (var element in Elements)
            {
                // Handle nested vectors (matrices) recursively
                if (element is VectorValue vecA)
                {
                    result.Add(vecA.Maximum(scalar));
                }
                else if (element is IntegerValue intA && scalar is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Max(intA.Value, intB.Value)));
                else if (element is LongValue longA && scalar is LongValue longB)
                    result.Add(new LongValue(Math.Max(longA.Value, longB.Value)));
                else if (element is FloatValue floatA && scalar is FloatValue floatB)
                    result.Add(new FloatValue(Math.Max(floatA.Value, floatB.Value)));
                // Mixed numeric types: promote to wider type
                else if (element is IntegerValue intA2 && scalar is LongValue longB2)
                    result.Add(new LongValue(Math.Max(intA2.Value, longB2.Value)));
                else if (element is LongValue longA2 && scalar is IntegerValue intB2)
                    result.Add(new LongValue(Math.Max(longA2.Value, intB2.Value)));
                else if (element is IntegerValue intA3 && scalar is FloatValue floatB3)
                    result.Add(new FloatValue(Math.Max(intA3.Value, floatB3.Value)));
                else if (element is FloatValue floatA3 && scalar is IntegerValue intB3)
                    result.Add(new FloatValue(Math.Max(floatA3.Value, intB3.Value)));
                else if (element is LongValue longA4 && scalar is FloatValue floatB4)
                    result.Add(new FloatValue(Math.Max(longA4.Value, floatB4.Value)));
                else if (element is FloatValue floatA4 && scalar is LongValue longB4)
                    result.Add(new FloatValue(Math.Max(floatA4.Value, longB4.Value)));
                else
                    throw new InvalidOperationException("Cannot find maximum of mixed types");
            }
            return new VectorValue(result);
        }

        public override string ToString()
        {
            // 1) Handle empty vectors
            if (Elements.Count == 0)
            {
                if (VectorType.HasValue)
                {
                    return VectorType.Value switch
                    {
                        -4 => "0#`",    // Empty symbol vector
                        -3 => "\"\"",    // Empty character vector
                        -2 => "0#0.0",   // Empty float vector
                        -1 => "!0",      // Empty integer vector
                        -64 => "!0j",     // Empty long vector (same as integer)
                        0 => "()",       // Empty list
                        _ => "()"        // Default to empty list
                    };
                }
                return "()"; // Default to empty list if no type specified
            }
                        
            // 2) Handle single-element generic lists (enlist) - only for type 0
            if (Elements.Count == 1)
            {
                return "," + Elements[0].ToString();
            }

            // 3) Check if this is a projection (contains :: symbols) - always use generic list format
            bool hasProjectionMarker = Elements.Any(e => e is SymbolValue sv && sv.Value == "::");
            if (hasProjectionMarker)
            {
                return FormatGenericList();
            }

            // 4) Identify vector type and apply appropriate rules
            var vectorType = VectorType ?? 0; // Default to generic list if no type specified

            // For typed vectors (-1, -2, -3, -4, -64), use type-specific rules
            return vectorType switch
            {
                -1 => FormatNumericVector(),    // Integer vector
                -2 => FormatNumericVector(),    // Float vector
                -64 => FormatNumericVector(),   // Long vector
                -3 => FormatCharacterVector(),  // Character vector
                -4 => FormatSymbolVector(),     // Symbol vector
                _ => FormatGenericList()        // Default to generic list
            };
            
            string FormatNumericVector()
            {
                // Numeric types: elements separated by spaces
                return string.Join(" ", Elements.Select(e => e.ToString()));
            }
            
            string FormatCharacterVector()
            {
                // Character vector: concatenate the string representations of individual characters
                // and remove the surrounding quotes from each character
                var result = "\"";
                foreach (var element in Elements)
                {
                    if (element is CharacterValue cv)
                    {
                        // Get the string representation and remove surrounding quotes
                        var charStr = cv.ToString();
                        if (charStr.StartsWith("\"") && charStr.EndsWith("\"") && charStr.Length > 2)
                        {
                            result += charStr.Substring(1, charStr.Length - 2);
                        }
                        else
                        {
                            result += charStr;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Character vector contains non-character elements");
                    }
                }
                result += "\"";
                return result;
            }
            
            string FormatSymbolVector()
            {
                // Symbol vector: elements with no separation
                return string.Concat(Elements.Select(e => e.ToString()));
            }
            
            string FormatGenericList()
            {
                // Generic list: enclosing parentheses and elements separated by semicolons
                // Special handling for :: projection markers - display without quotes
                var elementsStr = string.Join(";", Elements.Select(e => 
                {
                    if (e is SymbolValue sv && sv.Value == "::")
                        return "::";
                    return e.ToString();
                }));
                return "(" + elementsStr + ")";
            }
        }
    }

    public class DictionaryValue : K3Value
    {
        public Dictionary<SymbolValue, (K3Value Value, DictionaryValue? Attribute)> Entries { get; }

        public DictionaryValue()
        {
            Type = ValueType.Dictionary;
            Entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
        }

        public DictionaryValue(Dictionary<SymbolValue, (K3Value, DictionaryValue?)> entries)
        {
            Type = ValueType.Dictionary;
            Entries = entries;
        }

        public override string ToString()
        {
            if (Entries.Count == 0)
                return ".()";
            
            var entries = new List<string>();
            foreach (var kvp in Entries)
            {
                var key = kvp.Key.ToString();
                var value = kvp.Value.Value;
                var attr = kvp.Value.Attribute;
                
                var valueStr = value is NullValue ? "" : value.ToString();
                
                if (attr != null)
                {
                    entries.Add($"({key};{valueStr};{attr})");
                }
                else
                {
                    // For null attributes, show semicolon
                    entries.Add($"({key};{valueStr};)");
                }
            }
            
            // Handle single-element case with comma prefix (per specification)
            if (entries.Count == 1)
            {
                // Remove the outer parentheses from the single entry to avoid double parentheses
                var singleEntry = entries[0];
                if (singleEntry.StartsWith("(") && singleEntry.EndsWith(")"))
                {
                    singleEntry = singleEntry.Substring(1, singleEntry.Length - 2);
                }
                return ".,(" + singleEntry + ")";
            }
            
            return ".(" + string.Join(";", entries) + ")";
        }
    }
}
