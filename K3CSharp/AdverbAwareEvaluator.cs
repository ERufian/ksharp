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
    public partial class Evaluator
    {
        /// <summary>
        /// Enhanced evaluator for verbs with adverbs (nested class for access to private methods)
        /// </summary>
        private class AdverbAwareEvaluator
        {
            private readonly Evaluator evaluator;
            private string currentVerb = "+"; // Track current verb context

            public AdverbAwareEvaluator(Evaluator evaluator)
            {
                this.evaluator = evaluator;
            }

            /// <summary>
            /// Evaluate a verb with adverbs applied sequentially from outermost to innermost
            /// </summary>
            /// <param name="verbWithAdverbs">The verb with adverbs to evaluate</param>
            /// <param name="arguments">Arguments to pass to the verb</param>
            /// <returns>Result of applying verb with adverbs</returns>
            public K3Value EvaluateVerbWithAdverbs(VerbWithAdverbs verbWithAdverbs, params K3Value[] arguments)
            {
                // Set current verb context
                currentVerb = verbWithAdverbs.BaseVerb;
                
                // Check if verb is monadic (has disambiguating colon suffix)
                bool isMonadicVerb = verbWithAdverbs.BaseVerb.EndsWith(":");
                
                // Also check verb arity from VerbRegistry
                var verbInfo = VerbRegistry.GetVerb(verbWithAdverbs.BaseVerb);
                bool isMonadicOnly = verbInfo != null && verbInfo.SupportedArities.Length == 1 && verbInfo.SupportedArities[0] == 1;
                
                // Check if verb has a monadic variant (e.g., + has +:)
                string monadicVariant = verbWithAdverbs.BaseVerb + ":";
                bool hasMonadicVariant = VerbRegistry.IsVerb(monadicVariant);
                
                // Special handling for over adverb
                if (verbWithAdverbs.Adverbs.Contains("over"))
                {
                    // If verb is monadic (either by colon suffix or monadic-only), use Over Monad
                    if ((isMonadicVerb || isMonadicOnly) && arguments.Length >= 2)
                    {
                        string verbToUse = isMonadicVerb ? verbWithAdverbs.BaseVerb : monadicVariant;
                        var left = arguments[0];
                        var x = arguments[1];
                        return evaluator.OverMonad(verbToUse, left, x);
                    }
                    
                    // Check if we have initialization (left argument is not dummy 0)
                    if (arguments.Length == 2 && arguments[0] is IntegerValue leftInt && leftInt.Value != 0)
                    {
                        // Use provided initialization
                        return ApplyOverAdverbWithInit(arguments[0], arguments[1], arguments);
                    }
                    else
                    {
                        // Use dummy initialization (0)
                        return ApplyOverAdverb(arguments[1], arguments);
                    }
                }
                
                // Special handling for scan adverb
                if (verbWithAdverbs.Adverbs.Contains("scan"))
                {
                    // Original scan handling without identifier-based verb routing
                    if ((isMonadicVerb || isMonadicOnly) && arguments.Length >= 2)
                    {
                        var left = arguments[0];
                        var x = arguments[1];
                        string verbToUse = isMonadicVerb ? verbWithAdverbs.BaseVerb : monadicVariant;
                        return evaluator.ScanMonad(verbToUse, left, x);
                    }

                    // Check if we have initialization (left argument is not dummy 0)
                    if (arguments.Length == 2 && arguments[0] is IntegerValue leftInt && leftInt.Value != 0)
                    {
                        // Use provided initialization
                        return ApplyScanAdverbWithInit(arguments[0], arguments[1], arguments);
                    }
                    else
                    {
                        // Use dummy initialization (first element)
                        return ApplyScanAdverb(arguments[1], arguments);
                    }
                }
                
                // Start with the base verb and arguments
                K3Value result = ApplyBaseVerb(verbWithAdverbs.BaseVerb, arguments);
                
                // Apply adverbs from innermost to outermost (reverse order of parsing)
                var adverbsReversed = verbWithAdverbs.Adverbs.AsEnumerable().Reverse().ToList();
                
                foreach (var adverb in adverbsReversed)
                {
                    result = ApplyAdverb(adverb, result, arguments);
                }
                
                return result;
            }

            private K3Value ApplyBaseVerb(string verb, K3Value[] arguments)
            {
                if (arguments[0] == null)
                    throw new ArgumentNullException(nameof(arguments), "First argument cannot be null");
                    
                return verb switch
                {
                    "+" => arguments.Length == 1 ? evaluator.Transpose(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("+", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "-" => arguments.Length == 1 ? evaluator.MonadicMinus(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("-", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "*" => arguments.Length == 1 ? evaluator.First(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("*", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "%" => arguments.Length == 1 ? throw new Exception("% operator requires 2 arguments") : evaluator.EvaluateDyadicOperatorWithRegistry("%", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "^" => arguments.Length == 1 ? evaluator.Power(arguments[0], new IntegerValue(1)) : evaluator.EvaluateDyadicOperatorWithRegistry("^", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "<" => arguments.Length == 1 ? evaluator.LessThan(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry("<", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    ">" => arguments.Length == 1 ? evaluator.GreaterThan(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry(">", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "=" => arguments.Length == 1 ? K3Value.Equals(arguments[0], new IntegerValue(0)) ? new IntegerValue(1) : new IntegerValue(0) : evaluator.EvaluateDyadicOperatorWithRegistry("=", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "!" => arguments.Length == 1 ? evaluator.Match(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry("!", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "&" => arguments.Length == 1 ? evaluator.Where(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("&", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "|" => arguments.Length == 1 ? evaluator.Reverse(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("|", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "~" => arguments.Length == 1 ? evaluator.Match(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry("~", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "," => arguments.Length == 1 ? evaluator.Enlist(arguments[0]) : evaluator.Join(arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "." => evaluator.DotApply(arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "@" => evaluator.AtIndex(arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "#" => arguments.Length == 1 ? evaluator.Count(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("#", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "_" => arguments.Length == 1 ? evaluator.Floor(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("_", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "?" => arguments.Length == 1 ? evaluator.hashgroupHandler.Unique(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("?", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "$" => arguments.Length == 1 ? evaluator.formatHandler.Format(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("$", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    _ => VerbRegistry.HasVerb(verb) && VerbRegistry.GetVerb(verb) is { } vInfo
                        ? (arguments.Length == 1
                            ? evaluator.ApplyMonadicVerb(verb, arguments[0])
                            : vInfo.SupportedArities.Contains(2)
                                ? evaluator.EvaluateDyadicOperatorWithRegistry(verb, arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments)))
                                : evaluator.ApplyMonadicVerb(verb, arguments[1] ?? arguments[0]))
                        : throw new Exception($"Unknown verb: {verb}")
                };
            }

            public K3Value HandleTwoArgumentAdverb(VerbWithAdverbs verbWithAdverbs, K3Value argument)
            {
                // For 2-argument adverb structures, handle over/scan/each adverbs correctly
                if (verbWithAdverbs.Adverbs.Contains("over"))
                {
                    // Over adverb (/) - use existing implementation
                    return evaluator.ApplyAdverbSlash(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("scan"))
                {
                    // Scan adverb (\) - use existing implementation
                    return evaluator.ApplyAdverbBackslash(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each"))
                {
                    // Each adverb (') - use existing implementation
                    return evaluator.HandleAdverbTick(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each-right"))
                {
                    // Each-right adverb (/:) - use existing implementation
                    return evaluator.ApplyAdverbSlashColon(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each-left"))
                {
                    // Each-left adverb (\:) - use existing implementation
                    return evaluator.ApplyAdverbBackslashColon(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each-prior"))
                {
                    // Each-prior adverb (':) - use existing implementation
                    return evaluator.ApplyAdverbTickColon(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                
                // Fallback to treating it as a single argument adverb
                return EvaluateVerbWithAdverbs(verbWithAdverbs, argument);
            }
            
            private K3Value CreateVerbValue(string verbSymbol)
            {
                // Create a verb value from the verb symbol
                return verbSymbol switch
                {
                    "+" => new SymbolValue("+"),
                    "-" => new SymbolValue("-"),
                    "*" => new SymbolValue("*"),
                    "%" => new SymbolValue("%"),
                    "^" => new SymbolValue("^"),
                    "<" => new SymbolValue("<"),
                    ">" => new SymbolValue(">"),
                    "=" => new SymbolValue("="),
                    "!" => new SymbolValue("!"),
                    "&" => new SymbolValue("&"),
                    "|" => new SymbolValue("|"),
                    "~" => new SymbolValue("~"),
                    "," => new SymbolValue(","),
                    "." => new SymbolValue("."),
                    "@" => new SymbolValue("@"),
                    "#" => new SymbolValue("#"),
                    "_" => new SymbolValue("_"),
                    "?" => new SymbolValue("?"),
                    "$" => new SymbolValue("$"),
                    _ => throw new Exception($"Unknown verb symbol: {verbSymbol}")
                };
            }

            private K3Value ApplyAdverb(string adverb, K3Value verbResult, K3Value[] originalArguments)
            {
                return adverb switch
                {
                    "over" => ApplyOverAdverb(verbResult, originalArguments),
                    "scan" => ApplyScanAdverb(verbResult, originalArguments),
                    "each" => ApplyEachAdverb(verbResult, originalArguments),
                    "each-right" => ApplyEachRightAdverb(verbResult, originalArguments),
                    "each-left" => ApplyEachLeftAdverb(verbResult, originalArguments),
                    "each-prior" => ApplyEachPriorAdverb(verbResult, originalArguments),
                    _ => throw new Exception($"Unknown adverb: {adverb}")
                };
            }

            private K3Value ApplyOverAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Over adverb (/) - reduce/fold operation
                if (verbResult is VectorValue vector)
                {
                    if (vector.Elements.Count == 0)
                    {
                        // Return identity element based on the base verb
                        return GetIdentityElementForVerb(currentVerb);
                    }
                    else if (vector.Elements.Count == 1)
                    {
                        return vector.Elements[0]; // Single element, return as-is
                    }
                    else
                    {
                        // Reduce operation: apply verb cumulatively
                        K3Value result = vector.Elements[0];
                        for (int i = 1; i < vector.Elements.Count; i++)
                        {
                            result = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { result, vector.Elements[i] });
                        }
                        return result;
                    }
                }
                return verbResult;
            }
            
            private K3Value ApplyOverAdverbWithInit(K3Value initialization, K3Value verbResult, K3Value[] originalArguments)
            {
                // Over adverb (/) with initialization - reduce/fold operation with provided init
                if (verbResult is VectorValue vector)
                {
                    if (vector.Elements.Count == 0)
                    {
                        return initialization; // Empty vector, return initialization
                    }
                    else
                    {
                        // Check if the verb is monadic (has disambiguating colon suffix)
                        string verb = GetVerbFromContext(originalArguments);
                        
                        // Check verb arity from VerbRegistry
                        var verbInfo = VerbRegistry.GetVerb(verb);
                        bool isMonadicOnly = verbInfo != null && verbInfo.SupportedArities.Length == 1 && verbInfo.SupportedArities[0] == 1;
                        
                        // Also check for colon suffix which forces monadic interpretation
                        bool hasDisambiguatingColon = verb.EndsWith(":");
                        
                        if (isMonadicOnly || hasDisambiguatingColon)
                        {
                            // For monadic verbs with initialization, ignore initialization and apply monadic verb to the vector
                            // This handles cases like 1 +:/x where 1 is a conditional selector, not initialization
                            return evaluator.ApplyMonadicVerb(verb, verbResult);
                        }
                        else
                        {
                            // Reduce operation: start with initialization, apply verb cumulatively
                            K3Value result = initialization;
                            foreach (var element in vector.Elements)
                            {
                                result = ApplyBaseVerb(verb, new[] { result, element });
                            }
                            return result;
                        }
                    }
                }
                // Scalar data: apply verb with initialization once
                return ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { initialization, verbResult });
            }

            private K3Value ApplyScanAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Scan adverb (\) - cumulative application
                if (verbResult is VectorValue vector)
                {
                    if (vector.Elements.Count == 0)
                    {
                        return new VectorValue(new List<K3Value>());
                    }

                    var results = new List<K3Value>();
                    K3Value cumulative = vector.Elements[0];
                    results.Add(cumulative);
                    
                    for (int i = 1; i < vector.Elements.Count; i++)
                    {
                        cumulative = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { cumulative, vector.Elements[i] });
                        results.Add(cumulative);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }
            
            private K3Value ApplyScanAdverbWithInit(K3Value initialization, K3Value verbResult, K3Value[] originalArguments)
            {
                // Scan adverb (\) with initialization - cumulative application with provided init
                if (verbResult is VectorValue vector)
                {
                    var results = new List<K3Value>();
                    K3Value cumulative = initialization;
                    results.Add(cumulative);
                    
                    foreach (var element in vector.Elements)
                    {
                        cumulative = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { cumulative, element });
                        results.Add(cumulative);
                    }
                    return new VectorValue(results);
                }
                // Scalar data: apply verb with initialization once
                return ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { initialization, verbResult });
            }

            private K3Value ApplyEachAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each adverb (') - apply verb element-wise between vectors
                if (originalArguments.Length == 2 && originalArguments[0] is VectorValue leftVec && originalArguments[1] is VectorValue rightVec)
                {
                    // Apply verb element-wise between corresponding elements
                    var results = new List<K3Value>();
                    var minLength = Math.Min(leftVec.Elements.Count, rightVec.Elements.Count);
                    
                    for (int i = 0; i < minLength; i++)
                    {
                        var result = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { leftVec.Elements[i], rightVec.Elements[i] });
                        results.Add(result);
                    }
                    return new VectorValue(results);
                }
                else if (verbResult is VectorValue vector)
                {
                    // Fallback for single vector case
                    var results = new List<K3Value>();
                    foreach (var element in vector.Elements)
                    {
                        // For each, apply the verb with the same arity as original
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), 
                            originalArguments.Length == 1 ? new[] { element } : new[] { element, originalArguments[1] });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private K3Value ApplyEachRightAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each-right adverb (/:) - apply verb with right argument to each element of left
                if (originalArguments.Length >= 2 && originalArguments[0] is VectorValue leftVector)
                {
                    var results = new List<K3Value>();
                    foreach (var element in leftVector.Elements)
                    {
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { element, originalArguments[1] });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private K3Value ApplyEachLeftAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each-left adverb (\:) - apply verb with left argument to each element of right
                if (originalArguments.Length >= 2 && originalArguments[1] is VectorValue rightVector)
                {
                    var results = new List<K3Value>();
                    foreach (var element in rightVector.Elements)
                    {
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { originalArguments[0], element });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private K3Value ApplyEachPriorAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each-prior adverb (':) - apply verb with previous element
                if (verbResult is VectorValue vector)
                {
                    var results = new List<K3Value>();
                    results.Add(vector.Elements[0]); // First element stays the same
                    
                    for (int i = 1; i < vector.Elements.Count; i++)
                    {
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { vector.Elements[i], vector.Elements[i-1] });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private string GetVerbFromContext(K3Value[] arguments)
            {
                // Return the current verb context being tracked
                return currentVerb;
            }
            
            private K3Value GetIdentityElementForVerb(string verb)
            {
                // Return the appropriate identity element for the given verb
                return verb switch
                {
                    "*" => new IntegerValue(1),        // Multiplication identity
                    "+" => new IntegerValue(0),        // Addition identity
                    "&" => new IntegerValue(1),        // Min identity (matches k.exe)
                    "|" => new IntegerValue(0),        // Max identity (matches k.exe)
                    "^" => new IntegerValue(1),        // Power identity
                    "%" => new IntegerValue(1),        // Divide identity
                    "-" => new IntegerValue(0),        // Subtract identity (monadic case)
                    _ => new IntegerValue(0)           // Default identity
                };
            }
        }
    }
}
