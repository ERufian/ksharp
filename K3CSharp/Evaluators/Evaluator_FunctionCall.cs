// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using K3CSharp.Parsing;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private K3Value EvaluateFunctionCall(ASTNode node)
        {
            if (callDepth >= MaxCallDepth)
            {
                throw new Exception("stack");
            }

            if (node.Children.Count < 1)
            {
                throw new Exception("Function call requires a function");
            }
            
            var functionNode = node.Children[0];
            var fnName = functionNode.Value is SymbolValue fs ? fs.Value : functionNode.Value?.ToString() ?? "";
            
            var arguments = new List<K3Value>();
            
            for (int i = 1; i < node.Children.Count; i++)
            {
                arguments.Add(Evaluate(node.Children[i]));
            }

            // Handle variable function calls first (to avoid evaluating built-in functions as variables)
            if (functionNode.Type == ASTNodeType.Variable)
            {
                // Variable function call: functionName[args]
                var functionName = functionNode.Value is SymbolValue symbol ? symbol.Value : functionNode.Value?.ToString() ?? "";
                return CallVariableFunction(functionName, arguments);
            }
            
            // First evaluate the left side to see if it's a vector or dictionary
            var leftValue = Evaluate(functionNode);
            
            // Handle Make function specially
            if (leftValue is FunctionValue func && func.BodyText == "Make")
            {
                return MakeFunction(arguments[0]);
            }

            // Handle DeferredTakeProjection: (n#f) x => n # (f x)
            if (leftValue is DeferredTakeProjection dtp)
            {
                K3Value innerArg = arguments.Count == 1 ? arguments[0] : (K3Value)new VectorValue(arguments);
                K3Value funcResult;
                if (dtp.Func is FunctionValue dtpFv)
                {
                    var tmpNode = new ASTNode(ASTNodeType.Function);
                    tmpNode.Value = dtpFv;
                    funcResult = CallDirectFunction(tmpNode, new List<K3Value> { innerArg });
                }
                else if (dtp.Func is ProjectedFunctionValue dtpPfv)
                    funcResult = CallProjectedFunction(dtpPfv, new List<K3Value> { innerArg });
                else if (dtp.Func is AdverbProjectedFunctionValue dtpApfv)
                    funcResult = CallAdverbProjectedFunction(dtpApfv, new List<K3Value> { innerArg });
                else
                    funcResult = innerArg;
                return Take(dtp.Count, funcResult);
            }
            
            // Check if this should be treated as indexing instead of function call
            if (leftValue is VectorValue || leftValue is DictionaryValue)
            {
                // This is indexing: vector[index] or dictionary[index]
                if (arguments.Count != 1)
                {
                    throw new Exception("Indexing requires exactly one argument");
                }
                return VectorIndex(leftValue!, arguments[0]);
            }
            
            // Handle SymbolValue: may be a KTree path or a variable name that resolves to a dict/vector
            if (leftValue is SymbolValue symVal)
            {
                // Delegate to AtIndexOperation which handles symbol resolution
                if (arguments.Count == 0)
                    return AtIndexOperation(symVal, new NullValue());
                if (arguments.Count == 1)
                    return AtIndexOperation(symVal, arguments[0]);
                // Multiple args: wrap in vector (semicolon list)
                return AtIndexOperation(symVal, new VectorValue(arguments));
            }

            // Handle function calls differently based on the function node type
            if (functionNode.Type == ASTNodeType.Function)
            {
                // Direct function call: {[params] body}[args]
                return CallDirectFunction(functionNode, arguments);
            }
            else
            {
                // Evaluate the function expression and call it
                var function = leftValue;
                
                if (function is FunctionValue functionValue)
                {
                    // Create a temporary AST node for the function to reuse CallDirectFunction
                    var tempFunctionNode = new ASTNode(ASTNodeType.Function);
                    tempFunctionNode.Value = functionValue;
                    return CallDirectFunction(tempFunctionNode, arguments);
                }
                else if (function is AdverbProjectedFunctionValue apfv)
                {
                    return CallAdverbProjectedFunction(apfv, arguments);
                }
                else if (function is ProjectedFunctionValue pfv)
                {
                    return CallProjectedFunction(pfv, arguments);
                }
                else if (function.Type == ValueType.Symbol)
                {
                    var functionName = (function as SymbolValue)?.Value ?? throw new Exception("Invalid function name");
                    return CallVariableFunction(functionName, arguments);
                }
                
                throw new Exception($"Cannot call non-function: {function.Type}");
            }
        }

        private K3Value CreateSlotProjectedFunction(FunctionValue originalFunction, List<string> parameters, List<K3Value> arguments)
        {
            // Slot-based projection: f[1;;3] — some args are NullValue (blank), others are fixed.
            // Build a new function whose parameters are only the blank slots, with the fixed values
            // substituted into the body as literals.
            var remainingParameters = new List<string>();
            var bodyText = originalFunction.BodyText;
            
            for (int i = 0; i < parameters.Count; i++)
            {
                if (arguments[i] is NullValue)
                {
                    remainingParameters.Add(parameters[i]);
                }
                else
                {
                    // Substitute the fixed argument into the body text (whole-word replacement)
                    var paramName = parameters[i];
                    var argLiteral = arguments[i].ToString();
                    bodyText = Regex.Replace(
                        bodyText,
                        $@"\b{Regex.Escape(paramName)}\b",
                        argLiteral);
                }
            }
            
            var projected = new FunctionValue(bodyText, remainingParameters);
            return projected;
        }

        private K3Value CreateProjectedFunction(FunctionValue originalFunction, List<K3Value> providedArguments)
        {
            // Create a new function with reduced valence
            var remainingParameters = originalFunction.Parameters.Skip(providedArguments.Count).ToList();
            var projectedBody = GenerateProjectedBody(originalFunction, providedArguments);
            
            return new FunctionValue(projectedBody, remainingParameters);
        }

        private string GenerateProjectedBody(FunctionValue originalFunction, List<K3Value> providedArguments)
        {
            // For a simpler implementation, we'll create a closure-like approach
            // Store the provided arguments and create a function that takes the remaining ones
            
            if (originalFunction.Parameters.Count <= providedArguments.Count)
            {
                // No remaining parameters, just evaluate the original function
                return originalFunction.BodyText;
            }
            
            // Create a new function body with argument substitution
            var bodyText = originalFunction.BodyText;
            
            // Substitute provided arguments in the body
            for (int i = 0; i < providedArguments.Count && i < originalFunction.Parameters.Count; i++)
            {
                var paramName = originalFunction.Parameters[i];
                var argValue = providedArguments[i].ToString();
                
                // Replace parameter name with its value in the body
                bodyText = bodyText.Replace(paramName, argValue);
            }
            
            return bodyText;
        }

        
        
        
        
        private K3Value CallDirectFunction(ASTNode functionNode, List<K3Value> arguments)
        {
            if (functionNode.Value is not FunctionValue functionValue)
            {
                throw new Exception("Function node must contain a FunctionValue");
            }
            
            var parameters = functionValue.Parameters;
            var bodyText = functionValue.BodyText;
            // Vector argument unpacking: if we have 1 vector argument but need multiple parameters, unpack it
            // Also unpack for implicit-param functions when the vector contains NullValue slots (projection)
            // Skip unpacking for encoded adverb FVs (OVER:, SCAN:, EACH:, etc.) — they handle args via adverb dispatch
            bool isEncodedAdverb = bodyText.StartsWith("OVER:") || bodyText.StartsWith("SCAN:") ||
                                   bodyText.StartsWith("EACH:") || bodyText.StartsWith("EACH_RIGHT:") ||
                                   bodyText.StartsWith("EACH_LEFT:") || bodyText.StartsWith("EACH_PRIOR:");
            bool skipUnpack = bodyText.StartsWith("EACH:");
            if (!skipUnpack && arguments.Count == 1 && arguments[0] is VectorValue vectorArg && vectorArg.Elements.Count > 1)
            {
                if (parameters.Count > 1 ||
                    (parameters.Count == 0 && vectorArg.Elements.Any(e => e is NullValue)))
                {
                    arguments = new List<K3Value>(vectorArg.Elements);
                }
            }
            
            // Handle implicit parameters (x, y, z) for functions with no explicit params
            if (parameters.Count == 0 && arguments.Count > 0)
            {
                // K convention: {x*2} has implicit param x, {x+y} has x and y, etc.
                var implicitParams = new List<string>();
                if (arguments.Count >= 1) implicitParams.Add("x");
                if (arguments.Count >= 2) implicitParams.Add("y");
                if (arguments.Count >= 3) implicitParams.Add("z");
                parameters = implicitParams;
            }

            // Check for slot-based projection: any argument is NullValue (blank slot, e.g. f[1;;3])
            if (parameters.Count > 0 && arguments.Count == parameters.Count &&
                arguments.Any(a => a is NullValue))
            {
                return CreateSlotProjectedFunction(functionValue, parameters, arguments);
            }

            // Check for projection: fewer arguments than expected valence
            // Skip for encoded adverb FVs — they handle partial application via adverb dispatch
            if (!isEncodedAdverb && arguments.Count < parameters.Count)
            {
                return CreateProjectedFunction(functionValue, arguments);
            }

            if (!isEncodedAdverb && arguments.Count != parameters.Count)
            {
                throw new Exception($"Function expects {parameters.Count} arguments, got {arguments.Count}");
            }
            
            // Create a new evaluator scope for this function call
            var functionEvaluator = new Evaluator(this); // Pass parent to inherit currentFunctionValue
            functionEvaluator.isInFunctionCall = true;
            
            // Copy local variables to function scope (for nested functions)
            foreach (var kvp in localVariables)
            {
                functionEvaluator.localVariables[kvp.Key] = kvp.Value;
            }
            
            // Bind parameters to arguments (in local scope)
            for (int i = 0; i < Math.Min(parameters.Count, arguments.Count); i++)
            {
                functionEvaluator.SetVariable(parameters[i], arguments[i]);
            }
            
            // Set the associated K tree for anonymous functions
            functionEvaluator.kTree = functionValue.AssociatedKTree; // Pass the associated K tree
            
            // Bind parameters to arguments (in local scope)
            var bindCount = Math.Min(parameters.Count, arguments.Count);
            for (int i = 0; i < bindCount; i++)
            {
                functionEvaluator.SetVariable(parameters[i], arguments[i]);
            }
            // For encoded adverbs called with fewer args than params, set unbound params to NullValue
            // to prevent GetVariable from climbing to parent and finding stale values
            if (isEncodedAdverb)
            {
                for (int i = bindCount; i < parameters.Count; i++)
                    functionEvaluator.SetVariable(parameters[i], new NullValue());
            }
            
            // Set the current function value for AST caching optimization
            functionEvaluator.currentFunctionValue = functionValue;
            
            // DEBUG: Verify functionValue is set correctly
            if (functionEvaluator.currentFunctionValue == null)
                throw new InvalidOperationException("CallDirectFunction: currentFunctionValue is null after setting");
            
            // Execute the function body using recursive text evaluation
            return ExecuteFunctionBody(bodyText, functionEvaluator, functionValue.PreParsedTokens);
        }

        private K3Value ExecuteFunctionBody(string bodyText, Evaluator functionEvaluator, List<Token>? preParsedTokens = null)
        {
            // DEBUG: Check if currentFunctionValue is set
            if (functionEvaluator.currentFunctionValue == null)
                throw new InvalidOperationException("ExecuteFunctionBody: currentFunctionValue is null");
                        
            if (string.IsNullOrWhiteSpace(bodyText))
            {
                return new IntegerValue(0); // Empty function result
            }
            
            // Check if this is an FFI function with method hint
            if (functionEvaluator.currentFunctionValue?.Hint is SymbolValue hint && 
                HintSystem.IsMemberHint(hint.Value))
            {
                return ExecuteFFIFunction(functionEvaluator.currentFunctionValue, functionEvaluator);
            }
            
            // Handle depth-based each projections: EACH_DEPTH:n (created by 0', 1', 2', etc.)
            // These are created when an integer each is used as a projected function
            if (bodyText.StartsWith("EACH_DEPTH:"))
            {
                string depthStr = bodyText.Substring("EACH_DEPTH:".Length);
                if (int.TryParse(depthStr, out int depth))
                {
                    // Get the data argument from the function scope
                    K3Value? data = null;
                    try { data = functionEvaluator.GetVariable("y"); } catch { }
                    if (data == null || data is NullValue)
                        try { data = functionEvaluator.GetVariable("x"); } catch { }
                    
                    if (data != null && !(data is NullValue))
                    {
                        if (depth == 0)
                        {
                            // 0' is identity - return data unchanged
                            return data;
                        }
                        // Apply each at the specified depth
                        return ApplyEachAtDepth(data, depth);
                    }
                    // No data provided - return the function itself (projected)
                    return new FunctionValue(bodyText, new List<string> { "x", "y" });
                }
            }
            
            // Handle point-free adverb projections: OVER:verbStr, SCAN:verbStr, EACH:verbStr, etc.
            // These are created when a verb+adverb expression with no argument is assigned (e.g. d:{x'}/)
            // When called with 2 args (x=left, y=right), apply: left adverb(innerVerb) right
            // When called with 1 arg  (x=right only), apply monadically: adverb(innerVerb) right
            {
                string? adverbKind = null;
                string? innerVerbText = null;
                foreach (var prefix in new[] { "OVER:", "SCAN:", "EACH:", "EACH_RIGHT:", "EACH_LEFT:", "EACH_PRIOR:" })
                {
                    if (bodyText.StartsWith(prefix))
                    {
                        adverbKind = prefix.TrimEnd(':');
                        innerVerbText = bodyText.Substring(prefix.Length);
                        break;
                    }
                }
                if (adverbKind != null && innerVerbText != null)
                {
                    // Evaluate the inner verb string in the function scope
                    K3Value innerVerb;
                    // If the inner verb text is itself an encoded adverb body, wrap it as a FunctionValue directly
                    // (parsing it as K would misinterpret "EACH:..." as an assignment expression)
                    bool innerIsEncodedAdverb = innerVerbText.StartsWith("OVER:") || innerVerbText.StartsWith("SCAN:") ||
                                               innerVerbText.StartsWith("EACH:") || innerVerbText.StartsWith("EACH_RIGHT:") ||
                                               innerVerbText.StartsWith("EACH_LEFT:") || innerVerbText.StartsWith("EACH_PRIOR:");
                    if (innerIsEncodedAdverb)
                    {
                        innerVerb = new FunctionValue(innerVerbText, new List<string> { "x", "y" });
                    }
                    else
                    {
                        var lexer2 = new Lexer(innerVerbText);
                        var tokens2 = lexer2.Tokenize();
                        var ast2 = ParserConfig.ParseWithConfig(tokens2, innerVerbText);
                        innerVerb = ast2 != null ? (functionEvaluator.Evaluate(ast2) ?? new NullValue()) : new SymbolValue(innerVerbText);
                    }

                    // Get left (x) and right (y) from the function scope
                    K3Value? xVal = null, yVal = null;
                    try { xVal = functionEvaluator.GetVariable("x"); } catch { }
                    try { yVal = functionEvaluator.GetVariable("y"); } catch { }
                    bool hasLeft  = xVal != null && !(xVal is NullValue);
                    bool hasRight = yVal != null && !(yVal is NullValue);

                    var sentinelLeft  = new NullValue();
                    var sentinelRight = new NullValue();
                    var left2  = hasLeft  ? xVal! : sentinelLeft;
                    var right2 = hasRight ? yVal! : sentinelRight;

                    return adverbKind switch
                    {
                        "OVER"       => ApplyAdverbSlash(innerVerb, left2, right2),
                        "SCAN"       => ApplyAdverbBackslash(innerVerb, left2, right2),
                        "EACH"       => HandleAdverbTick(innerVerb, left2, right2),
                        "EACH_RIGHT" => ApplyAdverbSlashColon(innerVerb, left2, right2),
                        "EACH_LEFT"  => ApplyAdverbBackslashColon(innerVerb, left2, right2),
                        "EACH_PRIOR" => ApplyAdverbTickColon(innerVerb, left2, right2),
                        _ => throw new Exception($"Unknown encoded adverb projection: {adverbKind}")
                    };
                }
            }
            
            try
            {
                ASTNode? ast;
                
                // Try to get cached AST from function value if available
                if (functionEvaluator.currentFunctionValue != null)
                {
                    ast = functionEvaluator.currentFunctionValue.GetCachedAst();
                    if (ast != null)
                    {
                        return functionEvaluator.Evaluate(ast) ?? new NullValue();
                    }
                }
                
                // Fallback to parsing from text (deferred validation per spec)
                if (preParsedTokens != null && preParsedTokens.Count > 0)
                {
                    ast = ParserConfig.ParseWithConfig(preParsedTokens, bodyText);
                }
                else
                {
                    var lexer = new Lexer(bodyText);
                    var tokens = lexer.Tokenize();
                    ast = ParserConfig.ParseWithConfig(tokens, bodyText);
                }
                
                if (ast != null)
                {
                    // Cache parsed AST for future use
                    functionEvaluator.currentFunctionValue?.CacheAst(ast);
                    var result = functionEvaluator.Evaluate(ast) ?? new NullValue();
                                        return result;
                }
                else
                {
                                        return new NullValue();
                }
            }
            catch (Exception ex)
            {
                // Don't double-wrap already-wrapped function errors or stack errors
                if (ex.Message == "stack" || ex.Message.StartsWith("Function execution error:"))
                    throw;
                throw new Exception($"Function execution error: {ex.Message}");
            }
        }
    }
}
