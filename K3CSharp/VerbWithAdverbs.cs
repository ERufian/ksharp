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
    /// Represents a verb with its attached adverbs for proper evaluation
    /// </summary>
    public class VerbWithAdverbs
    {
        public string BaseVerb { get; }
        public List<string> Adverbs { get; }
        public int Position { get; }

        public VerbWithAdverbs(string baseVerb, List<string> adverbs, int position = -1)
        {
            BaseVerb = baseVerb;
            Adverbs = adverbs ?? new List<string>();
            Position = position;
        }

        /// <summary>
        /// Get the effective arity of the verb with adverbs applied
        /// </summary>
        public int GetEffectiveArity()
        {
            // Base arity depends on the verb
            int baseArity = GetBaseVerbArity(BaseVerb);
            
            // Apply adverb arity modifications
            foreach (var adverb in Adverbs)
            {
                baseArity = ApplyAdverbArityModification(baseArity, adverb);
            }
            
            return baseArity;
        }

        private int GetBaseVerbArity(string verb)
        {
            // Determine base verb arity using VerbRegistry
            var verbInfo = VerbRegistry.GetVerb(verb);
            if (verbInfo != null && verbInfo.SupportedArities.Length > 0)
            {
                // Return the minimum supported arity as the base arity
                return verbInfo.SupportedArities.Min();
            }
            return 1; // Default to monadic
        }

        private int ApplyAdverbArityModification(int currentArity, string adverb)
        {
            return adverb switch
            {
                "/" => currentArity, // Over: same arity
                "\\" => currentArity, // Scan: same arity  
                "'" => currentArity, // Each: same arity
                "/:" => Math.Max(currentArity, 2), // Each-right: at least dyadic
                "\\:" => Math.Max(currentArity, 2), // Each-left: at least dyadic
                "':" => Math.Max(currentArity, 2), // Each-prior: at least dyadic
                _ => currentArity
            };
        }
    }

    /// <summary>
    /// Parser for extracting verbs and their attached adverbs from AST nodes
    /// </summary>
    public class VerbAdverbParser
    {
        /// <summary>
        /// Parse a verb with adverbs from an AST node
        /// </summary>
        /// <param name="node">AST node to parse</param>
        /// <returns>VerbWithAdverbs object or null if not a verb with adverbs</returns>
        public static VerbWithAdverbs? ParseVerbWithAdverbs(ASTNode node)
        {
            // Handle simple literal verbs (like +, -, *, etc.)
            if (node.Type == ASTNodeType.Literal && node.Value is SymbolValue symbolValue)
            {
                var verbSymbol = symbolValue.Value.ToString();
                if (IsVerb(verbSymbol))
                {
                    return new VerbWithAdverbs(verbSymbol, new List<string>(), node.StartPosition);
                }
                return null;
            }
            
            // Don't handle Variable nodes here - let them fall through to legacy evaluation
            // where the verb will be evaluated to a FunctionValue before reaching the adverb handler
            
            if (node.Type != ASTNodeType.DyadicOp || node.Value == null)
                return null;

            var opSymbol = node.Value.ToString();
            
            // Check if this is an adverb
            if (IsAdverb(opSymbol))
            {
                // Parse the left side to find the base verb and any nested adverbs
                var leftResult = ParseVerbWithAdverbs(node.Children[0]);
                if (leftResult != null)
                {
                    // Add this adverb to the list
                    var adverbs = new List<string>(leftResult.Adverbs) { opSymbol };
                    return new VerbWithAdverbs(leftResult.BaseVerb, adverbs, node.StartPosition);
                }
                else if (node.Children[0].Type == ASTNodeType.Literal && node.Children[0].Value is SymbolValue leftSymbolValue)
                {
                    // Base verb found
                    var baseVerb = leftSymbolValue.Value.ToString();
                    return new VerbWithAdverbs(baseVerb, new List<string> { opSymbol }, node.StartPosition);
                }
            }
            
            // Check if this is a base verb
            if (IsVerb(opSymbol))
            {
                return new VerbWithAdverbs(opSymbol, new List<string>(), node.StartPosition);
            }

            return null;
        }

        private static bool IsAdverb(string symbol)
        {
            return symbol == "/" || symbol == "\\" || symbol == "'" || 
                   symbol == "/:" || symbol == "\\:" || symbol == "':";
        }

        private static bool IsVerb(string symbol)
        {
            // Check if this is a known verb symbol
            return VerbRegistry.IsVerb(symbol);
        }
    }
}
