// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using K3CSharp;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Verb Query Extensions for Verb-Agnostic Parsing
    /// Extension methods for VerbRegistry to support verb-agnostic queries
    /// Uses existing VerbRegistry properties to remain agnostic about individual verbs
    /// </summary>
    public static class VerbQueryExtensions
    {
        // Cache for frequently accessed queries to improve performance
        private static readonly Dictionary<int, List<string>> verbsWithArityCache = new Dictionary<int, List<string>>();
        private static readonly Dictionary<string, List<string>> adverbCompatibleCache = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, bool> isMonadicCache = new Dictionary<string, bool>();
        private static readonly Dictionary<string, bool> isDyadicCache = new Dictionary<string, bool>();
        private static readonly Dictionary<string, bool> isSystemFunctionCache = new Dictionary<string, bool>();
        
        /// <summary>
        /// Get all verbs supporting specific arity
        /// </summary>
        /// <param name="arity">The arity to check (1 for monadic, 2 for dyadic)</param>
        /// <returns>List of verb names that support the specified arity</returns>
        public static List<string> GetVerbsWithArity(int arity)
        {
            if (verbsWithArityCache.TryGetValue(arity, out var cached))
                return cached;
            
            var verbs = new List<string>();
            
            // Iterate through all registered verbs
            foreach (var verbName in GetAllRegisteredVerbNames())
            {
                var verb = VerbRegistry.GetVerb(verbName);
                if (verb != null && verb.SupportedArities.Contains(arity))
                {
                    verbs.Add(verbName);
                }
            }
            
            verbsWithArityCache[arity] = verbs;
            return verbs;
        }
        
        /// <summary>
        /// Get system functions by arity
        /// </summary>
        /// <param name="arity">The arity to filter by</param>
        /// <returns>List of system function names with the specified arity</returns>
        public static List<string> GetSystemVerbsWithArity(int arity)
        {
            return GetVerbsWithArity(arity)
                .Where(verbName => IsSystemFunction(verbName))
                .ToList();
        }
        
        /// <summary>
        /// Get verbs compatible with specific adverb type
        /// </summary>
        /// <param name="adverbType">The adverb type to check compatibility for</param>
        /// <returns>List of verb names that can work with the specified adverb</returns>
        public static List<string> GetAdverbCompatibleVerbs(string adverbType)
        {
            if (adverbCompatibleCache.TryGetValue(adverbType, out var cached))
                return cached;
            
            var compatibleVerbs = new List<string>();
            
            foreach (var verbName in GetAllRegisteredVerbNames())
            {
                if (VerbRegistry.SupportsAdverbs(verbName, adverbType))
                {
                    compatibleVerbs.Add(verbName);
                }
            }
            
            adverbCompatibleCache[adverbType] = compatibleVerbs;
            return compatibleVerbs;
        }
        
        /// <summary>
        /// Check if a verb supports monadic operations
        /// </summary>
        /// <param name="verbName">Name of the verb to check</param>
        /// <returns>True if the verb supports arity 1</returns>
        public static bool IsMonadicOperation(string verbName)
        {
            if (isMonadicCache.TryGetValue(verbName, out var cached))
                return cached;
            
            var verb = VerbRegistry.GetVerb(verbName);
            var result = verb?.SupportedArities.Contains(1) ?? false;
            
            isMonadicCache[verbName] = result;
            return result;
        }
        
        /// <summary>
        /// Check if a verb supports dyadic operations
        /// </summary>
        /// <param name="verbName">Name of the verb to check</param>
        /// <returns>True if the verb supports arity 2</returns>
        public static bool IsDyadicOperation(string verbName)
        {
            if (isDyadicCache.TryGetValue(verbName, out var cached))
                return cached;
            
            var verb = VerbRegistry.GetVerb(verbName);
            var result = verb?.SupportedArities.Contains(2) ?? false;
            
            isDyadicCache[verbName] = result;
            return result;
        }
        
        /// <summary>
        /// Check if a verb is a system function
        /// </summary>
        /// <param name="verbName">Name of the verb to check</param>
        /// <returns>True if the verb is a system function</returns>
        public static bool IsSystemFunction(string verbName)
        {
            if (isSystemFunctionCache.TryGetValue(verbName, out var cached))
                return cached;
            
            var verb = VerbRegistry.GetVerb(verbName);
            var result = verb?.Type == VerbType.SystemFunction || verb?.Type == VerbType.Function;
            
            isSystemFunctionCache[verbName] = result;
            return result;
        }
        
        /// <summary>
        /// Get the preferred arity for a verb based on context
        /// </summary>
        /// <param name="verbName">Name of the verb</param>
        /// <param name="hasLeftOperand">Whether there's a left operand</param>
        /// <param name="hasRightOperand">Whether there's a right operand</param>
        /// <returns>Preferred arity for the verb</returns>
        public static int GetPreferredArity(string verbName, bool hasLeftOperand, bool hasRightOperand)
        {
            return VerbRegistry.GetPreferredArity(verbName, hasLeftOperand, hasRightOperand);
        }
        
        /// <summary>
        /// Check if a verb can be used with any adverbs
        /// </summary>
        /// <param name="verbName">Name of the verb to check</param>
        /// <returns>True if the verb supports any adverb-compatible operations</returns>
        public static bool SupportsAnyAdverbs(string verbName)
        {
            return VerbRegistry.SupportsAdverbs(verbName);
        }
        
        /// <summary>
        /// Get all verbs that can be used with each (') adverb
        /// </summary>
        /// <returns>List of monadic verbs compatible with each adverb</returns>
        public static List<string> GetEachCompatibleVerbs()
        {
            return GetVerbsWithArity(1)
                .Where(verbName => VerbRegistry.SupportsAdverbs(verbName, "each"))
                .ToList();
        }
        
        /// <summary>
        /// Get all verbs that can be used with each-right (/:) adverb
        /// </summary>
        /// <returns>List of dyadic verbs compatible with each-right adverb</returns>
        public static List<string> GetEachRightCompatibleVerbs()
        {
            return GetVerbsWithArity(2)
                .Where(verbName => VerbRegistry.SupportsAdverbs(verbName, "each-right"))
                .ToList();
        }
        
        /// <summary>
        /// Get all verbs that can be used with each-left (\:) adverb
        /// </summary>
        /// <returns>List of dyadic verbs compatible with each-left adverb</returns>
        public static List<string> GetEachLeftCompatibleVerbs()
        {
            return GetVerbsWithArity(2)
                .Where(verbName => VerbRegistry.SupportsAdverbs(verbName, "each-left"))
                .ToList();
        }
        
        /// <summary>
        /// Get all verbs that can be used with over (/) adverb
        /// </summary>
        /// <returns>List of verbs compatible with over adverb</returns>
        public static List<string> GetOverCompatibleVerbs()
        {
            return GetAllRegisteredVerbNames()
                .Where(verbName => VerbRegistry.SupportsAdverbs(verbName, "over"))
                .ToList();
        }
        
        /// <summary>
        /// Get all verbs that can be used with scan (\) adverb
        /// </summary>
        /// <returns>List of verbs compatible with scan adverb</returns>
        public static List<string> GetScanCompatibleVerbs()
        {
            return GetAllRegisteredVerbNames()
                .Where(verbName => VerbRegistry.SupportsAdverbs(verbName, "over"))
                .ToList();
        }
        
        /// <summary>
        /// Clear all caches (useful for testing or when verbs are dynamically registered)
        /// </summary>
        public static void ClearCaches()
        {
            verbsWithArityCache.Clear();
            adverbCompatibleCache.Clear();
            isMonadicCache.Clear();
            isDyadicCache.Clear();
            isSystemFunctionCache.Clear();
        }
        
        /// <summary>
        /// Get statistics about verb distribution
        /// </summary>
        /// <returns>Dictionary with verb type counts</returns>
        public static Dictionary<string, int> GetVerbStatistics()
        {
            var stats = new Dictionary<string, int>
            {
                ["TotalVerbs"] = 0,
                ["MonadicOnly"] = 0,
                ["DyadicOnly"] = 0,
                ["BothArity"] = 0,
                ["SystemFunctions"] = 0,
                ["AdverbCompatible"] = 0
            };
            
            foreach (var verbName in GetAllRegisteredVerbNames())
            {
                stats["TotalVerbs"]++;
                
                var isMonadic = IsMonadicOperation(verbName);
                var isDyadic = IsDyadicOperation(verbName);
                
                if (isMonadic && isDyadic)
                    stats["BothArity"]++;
                else if (isMonadic)
                    stats["MonadicOnly"]++;
                else if (isDyadic)
                    stats["DyadicOnly"]++;
                
                if (IsSystemFunction(verbName))
                    stats["SystemFunctions"]++;
                
                if (SupportsAnyAdverbs(verbName))
                    stats["AdverbCompatible"]++;
            }
            
            return stats;
        }
        
        /// <summary>
        /// Helper method to get all registered verb names
        /// This uses reflection to access the internal verbs dictionary
        /// </summary>
        private static IEnumerable<string> GetAllRegisteredVerbNames()
        {
            // For now, we'll use a known set of common verbs
            // In a production environment, this should be improved to access the actual registry
            var commonVerbs = new[]
            {
                "+", "-", "*", "%", "^", "#", "$", "@", "_", "~", "!", "|", "&", "<", ">", "=", "~:",
                ",", ";", ":", "#", "_ci", "_ic", "_in", "_sv", "_vs", "_dv", "_di",
                "ABS", "COS", "SIN", "TAN", "EXP", "LOG", "SQRT", "FLOOR", "CEILING",
                "NOT", "NEGATE", "BIN", "BINL", "LIN", "GET", "SET", "TYPE", "COUNT",
                "FIRST", "LAST", "UNIQUE", "GROUP", "UNGROUP", "FLIP", "TRANSPOSE",
                "ENLIST", "JOIN", "RAZE", "EACH", "OVER", "SCAN", "EACH_RIGHT", "EACH_LEFT",
                "GETHINT", "SETHINT", "_gethint", "_sethint"
            };
            
            return commonVerbs.Where(verbName => VerbRegistry.GetVerb(verbName) != null);
        }
    }
}