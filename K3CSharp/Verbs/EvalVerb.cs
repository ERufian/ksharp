using System;
using System.Collections.Generic;
using System.Linq;
using K3CSharp;
using K3CSharp.Parsing;

namespace K3CSharp.Verbs
{
    /// <summary>
    /// Implementation of _eval verb for K3CSharp
    /// Evaluates parse tree representations using existing evaluator
    /// </summary>
    public static class EvalVerbHandler
    {
        // Static variable storage for basic variable evaluation
        private static readonly Dictionary<string, K3Value> variables = new Dictionary<string, K3Value>();
        
        // Reference to the current evaluator instance for accessing global variables
        private static Evaluator? currentEvaluator = null;
        
        /// <summary>
        /// Set the current evaluator instance for accessing global variables
        /// </summary>
        public static void SetEvaluator(Evaluator evaluator)
        {
            currentEvaluator = evaluator;
        }
        
        /// <summary>
        /// Main _eval entry point
        /// </summary>
        public static K3Value Evaluate(K3Value parseTree)
        {
            return Evaluate(new[] { parseTree });
        }
        
        /// <summary>
        /// Set a variable value (used by assignment operations)
        /// </summary>
        public static void SetVariable(string name, K3Value value)
        {
            variables[name] = value;
        }
        
        /// <summary>
        /// Get a variable value
        /// </summary>
        public static K3Value? GetVariable(string name)
        {
            // First check local storage
            if (variables.TryGetValue(name, out var value))
                return value;
                
            // Then check the current evaluator's global variables
            if (currentEvaluator != null)
            {
                try
                {
                    return currentEvaluator.GetVariableValuePublic(name);
                }
                catch
                {
                    // Variable not found in global context
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Clear all variables (for test isolation)
        /// </summary>
        public static void ClearVariables()
        {
            variables.Clear();
        }
        
        /// <summary>
        /// Eval verb implementation matching delegate signature
        /// </summary>
        public static K3Value Evaluate(K3Value[] arguments)
        {
            if (arguments.Length != 1)
                throw new Exception("_eval requires exactly 1 argument");
                
            var parseTree = arguments[0];
            var astNode = ParseTreeConverter.FromKList(parseTree);
            var result = EvaluateAST(astNode);
            return UnenlistIfSingleElement(result);
        }
        
        private static string ASTNodeToString(ASTNode node, int indent = 0)
        {
            var indentStr = new string(' ', indent * 2);
            var result = $"{indentStr}{node.Type}";
            if (node.Value != null)
                result += $"({node.Value})";
            if (node.Children.Count > 0)
            {
                result += ":\n";
                for (int i = 0; i < node.Children.Count; i++)
                {
                    result += ASTNodeToString(node.Children[i], indent + 1);
                    if (i < node.Children.Count - 1)
                        result += ",\n";
                }
            }
            return result;
        }
        
        /// <summary>
        /// Un-enlist 1-item vectors as required by the spec
        /// </summary>
        private static K3Value UnenlistIfSingleElement(K3Value value)
        {
            if (value is VectorValue vec && vec.Elements.Count == 1)
            {
                return vec.Elements[0];
            }
            return value;
        }
        
        /// <summary>
        /// Check if a symbol represents any callable verb (operator, function, projected function, or system variable)
        /// </summary>
        private static bool IsCallableVerb(string symbol)
        {
            string cleanSymbol = CleanVerbSymbol(symbol);
            
            // Use VerbRegistry to check if this is a registered verb
            return VerbRegistry.IsVerb(cleanSymbol);
        }
        
        /// <summary>
        /// Clean verb symbol by removing various quote and backtick formats
        /// </summary>
        private static string CleanVerbSymbol(string symbol)
        {
            if (symbol == null) return "";
            
            // Handle backtick-quote format: `"+"
            if (symbol.StartsWith("`\"") && symbol.EndsWith("\""))
            {
                return symbol.Substring(2, symbol.Length - 3);
            }
            // Handle standard quote format: "+"
            else if (symbol.StartsWith("\"") && symbol.EndsWith("\""))
            {
                return symbol.Substring(1, symbol.Length - 2);
            }
            // Handle backtick format: `+
            else if (symbol.StartsWith("`") && symbol.EndsWith("`"))
            {
                return symbol.Substring(1, symbol.Length - 2);
            }
            // Handle other quote/backtick combinations
            else
            {
                return symbol?.Trim('"').Trim('`').Trim('\'') ?? "";
            }
        }
        
        /// <summary>
        /// Evaluate any callable verb using Verb Registry (operators, functions, projected functions, system variables)
        /// </summary>
        private static K3Value EvaluateCallableVerb(string verbSymbol, K3Value[] arguments)
        {
                        
            var cleanSymbol = CleanVerbSymbol(verbSymbol);
            
            // Special handling for _ci verb
            if (cleanSymbol == "_ci")
            {
                // Apply _ci to each argument and return as vector
                var result = new List<K3Value>();
                foreach (var arg in arguments)
                {
                    if (arg is IntegerValue intArg)
                    {
                        char charValue = (char)intArg.Value;
                        result.Add(new CharacterValue(charValue.ToString()));
                    }
                    else if (arg is VectorValue vec)
                    {
                        var vecResult = new List<K3Value>();
                        foreach (var element in vec.Elements)
                        {
                            if (element is IntegerValue intElement)
                            {
                                char charValue = (char)intElement.Value;
                                vecResult.Add(new CharacterValue(charValue.ToString()));
                            }
                        }
                        result.Add(new VectorValue(vecResult));
                    }
                }
                return new VectorValue(result);
            }
            
            // Special handling for _ci' pattern (when parser doesn't recognize it as adverb)
            if (cleanSymbol == "_ci'")
            {
                // This is _ci' pattern - apply _ci to each argument
                var result = new List<K3Value>();
                foreach (var arg in arguments)
                {
                    if (arg is IntegerValue intArg)
                    {
                        char charValue = (char)intArg.Value;
                        result.Add(new CharacterValue(charValue.ToString()));
                    }
                    else if (arg is VectorValue vec)
                    {
                        var vecResult = new List<K3Value>();
                        foreach (var element in vec.Elements)
                        {
                            if (element is IntegerValue intElement)
                            {
                                char charValue = (char)intElement.Value;
                                vecResult.Add(new CharacterValue(charValue.ToString()));
                            }
                        }
                        result.Add(new VectorValue(vecResult));
                    }
                }
                return new VectorValue(result);
            }
            
            var verbInfo = VerbRegistry.GetVerb(cleanSymbol);
            
            if (verbInfo == null)
            {
                throw new Exception($"Unknown verb: {cleanSymbol}");
            }
            
            // Get the appropriate implementation based on arity
            var arity = arguments.Length;
            if (!verbInfo.SupportedArities.Contains(arity))
            {
                var aritiesStr = string.Join(", ", verbInfo.SupportedArities);
                throw new Exception($"Verb '{cleanSymbol}' does not support {arity} argument{(arity == 1 ? "" : "s")}. Supported arities: [{aritiesStr}]");
            }
            
            // Get the implementation for this arity
            var implementation = verbInfo.Implementations?[arity - 1];
            if (implementation == null)
            {
                // For operators without implementations, use K3Value methods
                return EvaluateOperatorUsingK3ValueMethods(cleanSymbol, arguments);
            }
            
            try
            {
                return implementation(arguments);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error evaluating verb '{cleanSymbol}' with {arity} arguments: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Fallback evaluation for operators without explicit Verb Registry implementations.
        /// Delegates to the Evaluator's EvaluateVerb method to perform the operation.
        /// </summary>
        private static K3Value EvaluateOperatorUsingK3ValueMethods(string operatorSymbol, K3Value[] arguments)
        {
            // Use the Evaluator's EvaluateVerb method to handle the operation
            var evaluator = new Evaluator();
            return evaluator.EvaluateVerb(operatorSymbol, arguments);
        }
        
        /// <summary>
        /// Evaluate AST using existing evaluator infrastructure
        /// </summary>
        private static K3Value EvaluateAST(ASTNode astNode)
        {
            // This would use the existing Evaluator class
            // For now, provide a simplified implementation that handles basic operations
            if (astNode.Type == ASTNodeType.Literal)
            {
                var value = astNode.Value ?? new NullValue();
                return UnenlistIfSingleElement(value);
            }
                
            if (astNode.Type == ASTNodeType.Variable)
            {
                // Variable lookup using our static variable storage
                var variableName = astNode.Value?.ToString()?.Trim('`').Trim('"') ?? "";
                var variableValue = GetVariable(variableName);
                if (variableValue != null)
                {
                    return UnenlistIfSingleElement(variableValue);
                }
                throw new Exception($"Variable not found: {variableName}");
            }
            
            if (astNode.Type == ASTNodeType.FunctionCall)
            {
                return new SymbolValue($"Function: {astNode.Value?.ToString() ?? ""}");
            }
            
            if (astNode.Type == ASTNodeType.DyadicOp)
            {
                // DyadicOp: use generic Verb Registry approach
                if (astNode.Children.Count >= 1)
                {
                    // Get the operator symbol from the DyadicOp node
                    var operatorSymbol = astNode.Value?.ToString() ?? "";
                    
                    if (IsCallableVerb(operatorSymbol))
                    {
                        // Evaluate all operands
                        var arguments = new List<K3Value>();
                        for (int i = 0; i < astNode.Children.Count; i++)
                        {
                            arguments.Add(EvaluateAST(astNode.Children[i]));
                        }
                        
                        // Use the Verb Registry-based evaluation
                        return EvaluateCallableVerb(operatorSymbol, arguments.ToArray());
                    }
                    else
                    {
                        throw new Exception($"Unknown operator: {operatorSymbol}");
                    }
                }
                else
                {
                    throw new Exception($"DyadicOp requires at least 1 child, got {astNode.Children.Count}");
                }
            }
            
            if (astNode.Type == ASTNodeType.Vector)
            {
                // Vector: evaluate as a single unit to respect operator precedence
                // This ensures that parenthesized expressions are evaluated correctly
                
                // Generic case: if this vector contains a callable verb as the first element, evaluate it as a parse tree
                // Only check if first element is a SymbolValue (verb) that could be any callable verb
                if (astNode.Children.Count >= 2 && 
                    astNode.Children[0].Type == ASTNodeType.Literal &&
                    astNode.Children[0].Value is SymbolValue &&
                    IsCallableVerb(astNode.Children[0].Value?.ToString() ?? ""))
                {
                    // This looks like a verb expression: verb operand1 [operand2 ...]
                    var verbSymbol = astNode.Children[0].Value?.ToString() ?? "";
                    
                    // Evaluate all operands
                    var arguments = new List<K3Value>();
                    for (int i = 1; i < astNode.Children.Count; i++)
                    {
                        arguments.Add(EvaluateAST(astNode.Children[i]));
                    }
                    
                    // Use the Verb Registry-based evaluation
                    return EvaluateCallableVerb(verbSymbol, arguments.ToArray());
                }
                
                var elements = new List<K3Value>();
                foreach (var child in astNode.Children)
                {
                    elements.Add(EvaluateAST(child));
                }
                
                // If this is a single-element vector from a parenthesized expression, 
                // return it as-is to preserve precedence
                if (elements.Count == 1)
                    return elements[0];
                    
                return new VectorValue(elements);
            }
            
            // Base case: return null for unimplemented node types instead of throwing
            return new NullValue();
        }
    }
}
