// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Text;

namespace K3CSharp
{
    /// <summary>
    /// Hint system for FFI type mapping and validation
    /// </summary>
    public static class HintSystem
    {
        /// <summary>
        /// Mapping from hint symbols to C# types
        /// </summary>
        private static readonly Dictionary<string, Type> HintToTypeMap = new Dictionary<string, Type>
        {
            // Basic types
            ["int"] = typeof(int),
            ["integer"] = typeof(int),
            ["short"] = typeof(short),
            ["ushort"] = typeof(ushort),
            ["long"] = typeof(long),
            ["ulong"] = typeof(ulong),
            ["float"] = typeof(double),
            ["double"] = typeof(double),
            ["char"] = typeof(char),
            ["character"] = typeof(char),
            ["string"] = typeof(string),
            ["symbol"] = typeof(string),
            ["bool"] = typeof(bool),
            ["boolean"] = typeof(bool),
            ["datetime"] = typeof(DateTime),
            ["timespan"] = typeof(TimeSpan),
            ["stringbuilder"] = typeof(StringBuilder),
            
            // Collection types
            ["list"] = typeof(List<object>),
            ["vector"] = typeof(List<object>),
            ["array"] = typeof(object[]),
            ["hashtable"] = typeof(System.Collections.Hashtable),
            ["dictionary"] = typeof(Dictionary<string, object>),
            
            // .NET specific types
            ["object"] = typeof(object),
            ["method"] = typeof(Delegate),
            ["property"] = typeof(object),
            ["field"] = typeof(object),
            ["constructor"] = typeof(object),
            ["event"] = typeof(Delegate),
            
            // Special hints
            ["void"] = typeof(void),
            ["null"] = typeof(object),
            ["disposed"] = typeof(object)
        };

        /// <summary>
        /// Set of valid hint symbols
        /// </summary>
        private static readonly HashSet<string> ValidHints = new HashSet<string>(HintToTypeMap.Keys);

        /// <summary>
        /// Check if a hint symbol is valid
        /// </summary>
        /// <param name="hintSymbol">The hint symbol to validate</param>
        /// <returns>True if the hint is valid, false otherwise</returns>
        public static bool IsValidHint(string hintSymbol)
        {
            return ValidHints.Contains(hintSymbol);
        }

        /// <summary>
        /// Get the C# type associated with a hint
        /// </summary>
        /// <param name="hintSymbol">The hint symbol</param>
        /// <returns>The mapped C# type, or null if not found</returns>
        public static Type? GetMappedType(string hintSymbol)
        {
            return HintToTypeMap.TryGetValue(hintSymbol, out var type) ? type : null;
        }

        /// <summary>
        /// Get the default hint for a K3Value type
        /// </summary>
        /// <param name="value">The K3Value to get the default hint for</param>
        /// <returns>The default hint symbol</returns>
        public static string GetDefaultHint(K3Value value)
        {
            return value.Type switch
            {
                ValueType.Integer => "int",
                ValueType.Long => "long",
                ValueType.Float => "float",
                ValueType.Character => "char",
                ValueType.Symbol => "symbol",
                ValueType.Vector => "vector",
                ValueType.Function => "method",
                ValueType.Dictionary => "hashtable",
                ValueType.List => "list",
                ValueType.Null => "null",
                _ => "object"
            };
        }

        /// <summary>
        /// Validate and normalize a hint symbol
        /// </summary>
        /// <param name="hintSymbol">The hint symbol to validate</param>
        /// <returns>The normalized hint symbol, or throws if invalid</returns>
        /// <exception cref="ArgumentException">Thrown when the hint is invalid</exception>
        public static string ValidateHint(string hintSymbol)
        {
            if (!IsValidHint(hintSymbol))
            {
                throw new ArgumentException($"Invalid hint symbol: '{hintSymbol}'. Valid hints are: {string.Join(", ", ValidHints)}");
            }
            return hintSymbol;
        }

        /// <summary>
        /// Get all valid hint symbols
        /// </summary>
        /// <returns>Array of all valid hint symbols</returns>
        public static string[] GetAllValidHints()
        {
            var hints = new string[ValidHints.Count];
            ValidHints.CopyTo(hints);
            Array.Sort(hints);
            return hints;
        }

        /// <summary>
        /// Check if a hint represents a collection type
        /// </summary>
        /// <param name="hintSymbol">The hint symbol to check</param>
        /// <returns>True if the hint represents a collection type</returns>
        public static bool IsCollectionHint(string hintSymbol)
        {
            return hintSymbol is "list" or "vector" or "array" or "hashtable" or "dictionary";
        }

        /// <summary>
        /// Check if a hint represents a numeric type
        /// </summary>
        /// <param name="hintSymbol">The hint symbol to check</param>
        /// <returns>True if the hint represents a numeric type</returns>
        public static bool IsNumericHint(string hintSymbol)
        {
            return hintSymbol is "int" or "integer" or "long" or "float" or "double";
        }

        /// <summary>
        /// Check if a hint represents a .NET member type
        /// </summary>
        /// <param name="hintSymbol">The hint symbol to check</param>
        /// <returns>True if the hint represents a .NET member type</returns>
        public static bool IsMemberHint(string hintSymbol)
        {
            return hintSymbol is "method" or "property" or "field" or "constructor" or "event"
                or "static_method" or "property_getter" or "property_setter" or "field_getter" or "field_setter";
        }
    }
}