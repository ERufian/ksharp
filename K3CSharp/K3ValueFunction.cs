// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using K3CSharp.Parsing;

namespace K3CSharp
{
    public class FunctionValue : K3Value
    {
        public string BodyText { get; }
        public string OriginalSourceText { get; }
        public List<string> Parameters { get; }
        public int Valence { get; }
        public List<Token> PreParsedTokens { get; }
        
        // AST cache for performance optimization
        private ASTNode? _cachedAst;
        private readonly object _astCacheLock = new object();

        // Associated K tree for anonymous functions
        public KTree AssociatedKTree { get; private set; }
        
        // For adverb chaining: store the right argument when this function is created by an adverb
        public K3Value? RightArgument { get; set; }
        
        public FunctionValue(string bodyText, List<string> parameters, List<Token> preParsedTokens = null!, string originalSourceText = "", SymbolValue? hint = null)
        {
            BodyText = bodyText;
            OriginalSourceText = originalSourceText;
            Parameters = parameters;
            Type = ValueType.Function;
            Hint = hint;
            Valence = parameters.Count;
            PreParsedTokens = preParsedTokens;
            AssociatedKTree = new KTree(); // Create associated K tree for anonymous functions
        }
        
        // Get or create cached AST (thread-safe)
        public ASTNode? GetCachedAst()
        {
            if (_cachedAst != null)
            {
                return _cachedAst;
            }
            
            lock (_astCacheLock)
            {
                // Double-check pattern for thread safety
                if (_cachedAst != null)
                {
                    return _cachedAst;
                }

                // Parse and cache the AST
                ASTNode? ast;
                if (PreParsedTokens != null && PreParsedTokens.Count > 0)
                {
                    ast = ParserConfig.ParseWithConfig(PreParsedTokens, BodyText);
                }
                else
                {
                    var lexer = new Lexer(BodyText);
                    var tokens = lexer.Tokenize();
                    ast = ParserConfig.ParseWithConfig(tokens, BodyText);
                }
                
                _cachedAst = ast;
                return _cachedAst;
            }
        }

        // Cache an already parsed AST (thread-safe)
        public void CacheAst(ASTNode ast)
        {
            lock (_astCacheLock)
            {
                _cachedAst = ast;
            }
        }

        public override string ToString()
        {
            // Use the original source text if available for exact representation
            if (!string.IsNullOrEmpty(OriginalSourceText))
                return OriginalSourceText;
                
            // Fall back to reconstructed representation for backward compatibility
            var paramsStr = Parameters.Count > 0 ? "[" + string.Join(";", Parameters) + "] " : "";
            return "{" + paramsStr + BodyText + "}";
        }
    }

    public class NullValue : K3Value
    {
        public NullValue()
        {
            Type = ValueType.Null;
        }

        public override string ToString()
        {
            return "";
        }
    }

    public class ProjectedFunctionValue : K3Value
    {
        public string OperatorName { get; }
        public int RequiredArguments { get; }
        /// <summary>
        /// Bound arguments for bracket projections (e.g., +[3 3 5] binds left arg).
        /// null entries represent unbound (missing) argument positions.
        /// null list means no bound arguments (simple projection like (+)).
        /// </summary>
        public List<K3Value?>? BoundArguments { get; }

        public ProjectedFunctionValue(string operatorName, int requiredArguments, List<K3Value?>? boundArguments = null)
        {
            Type = ValueType.Function; // Treat as a function type
            OperatorName = operatorName;
            RequiredArguments = requiredArguments;
            BoundArguments = boundArguments;
        }

        public override string ToString()
        {
            // If there are bound arguments, display in bracket notation like k.exe
            if (BoundArguments != null && BoundArguments.Count > 0)
            {
                var parts = new List<string>();
                foreach (var arg in BoundArguments)
                {
                    parts.Add(arg?.ToString() ?? "");
                }
                return OperatorName + "[" + string.Join(";", parts) + "]";
            }
            // Monadic projections (arity 1) should be displayed with a colon
            // Dyadic projections (arity 2) are displayed without colon
            return RequiredArguments == 1 ? OperatorName + ":" : OperatorName;
        }

        public override bool Equals(object? obj)
        {
            if (obj is ProjectedFunctionValue otherProjected)
                return OperatorName == otherProjected.OperatorName && RequiredArguments == otherProjected.RequiredArguments;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OperatorName, RequiredArguments);
        }
    }

    public class AdverbProjectedFunctionValue : K3Value
    {
        public string AdverbName { get; }
        public string Verb { get; }
        public int RequiredArguments { get; }
        public K3Value? BoundLeftArgument { get; }
        public FunctionValue? FuncValue { get; }

        public AdverbProjectedFunctionValue(string adverbName, string verb, int requiredArguments, K3Value? boundLeftArgument = null, FunctionValue? funcValue = null)
        {
            Type = ValueType.Function; // Treat as a function type
            AdverbName = adverbName;
            Verb = verb;
            RequiredArguments = requiredArguments;
            BoundLeftArgument = boundLeftArgument;
            FuncValue = funcValue;
        }

        public override string ToString()
        {
            return $"{AdverbName}({Verb})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is AdverbProjectedFunctionValue other)
                return AdverbName == other.AdverbName && Verb == other.Verb && RequiredArguments == other.RequiredArguments &&
                       (BoundLeftArgument == null ? other.BoundLeftArgument == null : BoundLeftArgument.Equals(other.BoundLeftArgument));
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AdverbName, Verb, RequiredArguments, BoundLeftArgument);
        }
    }

    /// <summary>
    /// Represents a deferred take-projection: n#f where f is a function/projection.
    /// When applied to x, computes n#(f x) — used for patterns like 3#,: applied to a value.
    /// </summary>
    public class DeferredTakeProjection : K3Value
    {
        public IntegerValue Count { get; }
        public K3Value Func { get; }
        public Evaluator Evaluator { get; }

        public DeferredTakeProjection(IntegerValue count, K3Value func, Evaluator evaluator)
        {
            Count = count;
            Func = func;
            Evaluator = evaluator;
            Type = ValueType.Function;
        }

        public override string ToString() => $"{Count}#{Func}";
    }
}
