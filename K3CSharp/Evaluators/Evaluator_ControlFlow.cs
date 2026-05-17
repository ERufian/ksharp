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
        private K3Value EvaluateControlFlow(string name, ASTNode argsNode)
        {
            // Control flow functions need to re-evaluate their arguments on each iteration
            // The argsNode is a Vector of AST nodes (from bracket parsing)
            var argNodes = new List<ASTNode>();
            if (argsNode.Type == ASTNodeType.Vector)
            {
                argNodes.AddRange(argsNode.Children);
            }
            else
            {
                argNodes.Add(argsNode);
            }

            switch (name)
            {
                case "do":
                {
                    if (argNodes.Count < 2)
                        throw new Exception("Do function requires at least 2 arguments: count and expression(s)");
                    var count = ToInteger(Evaluate(argNodes[0]));
                    if (count < 0)
                        throw new Exception("Do count must be non-negative");
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 1; j < argNodes.Count; j++)
                        {
                            Evaluate(argNodes[j]);
                        }
                    }
                    return new SymbolValue("");
                }
                case "while":
                {
                    if (argNodes.Count < 2)
                        throw new Exception("While function requires at least 2 arguments: condition and expression(s)");
                    int maxIterations = 10000; // Safety limit
                    int iter = 0;
                    while (iter++ < maxIterations)
                    {
                        var condResult = Evaluate(argNodes[0]);
                        if (!IsNonZeroInteger(condResult))
                            break;
                        for (int j = 1; j < argNodes.Count; j++)
                        {
                            Evaluate(argNodes[j]);
                        }
                    }
                    return new SymbolValue(""); // while returns empty string
                }
                case "if":
                {
                    if (argNodes.Count < 2)
                        throw new Exception("If function requires at least 2 arguments: condition and expression(s)");
                    var condResult = Evaluate(argNodes[0]);
                    if (IsNonZeroInteger(condResult))
                    {
                        for (int j = 1; j < argNodes.Count; j++)
                        {
                            Evaluate(argNodes[j]);
                        }
                    }
                    return new SymbolValue(""); // if returns empty string
                }
                default:
                    throw new Exception($"Unknown control flow: {name}");
            }
        }

        private K3Value ConditionalEvaluation(List<K3Value> arguments)
        {
            // Conditional evaluation: [cond; true; false] or [cond1;true1; cond2;true2; ; condN;trueN; false]
            // Arguments alternate between conditions and expressions to execute
            // Returns the result of the first true expression, or the else branch if all false

            if (arguments.Count < 3)
            {
                throw new Exception("Conditional evaluation requires at least 3 arguments");
            }

            // Process arguments in pairs: (condition, expression)
            for (int i = 0; i < arguments.Count - 1; i += 2)
            {
                var condition = arguments[i];
                var expression = arguments[i + 1];

                // Evaluate condition
                var conditionResult = EvaluateExpression(condition);

                // Check if condition is a non-zero integer
                if (IsNonZeroInteger(conditionResult))
                {
                    // Condition is true, execute the expression
                    return EvaluateExpression(expression);
                }
            }

            // If odd number of arguments, the last is the "else" branch
            if (arguments.Count % 2 == 1)
            {
                return EvaluateExpression(arguments[arguments.Count - 1]);
            }

            // All conditions were false and no else branch, return nil
            return new NullValue();
        }
        
                
        private K3Value EvaluateExpression(K3Value expression)
        {
            // If the expression is already evaluated, return it
            if (!(expression is FunctionValue))
            {
                return expression;
            }
            
            // If it's a function value, we need to evaluate it
            // For now, this is a simplified implementation
            // In a full implementation, we'd need to handle function evaluation properly
            return expression;
        }

        private K3Value DoFunction(K3Value operand)
        {
            // Do function: do[count; expression] or do[count; expression1; ; expressionN]
            // Execute expressions count times, return empty string (matching k.exe behavior)
            
            if (operand is VectorValue args && args.Elements.Count >= 2)
            {
                var countValue = args.Elements[0] is FunctionValue countFunc
                    ? Evaluate(ParserConfig.ParseWithConfig(countFunc.PreParsedTokens ?? new List<Token>(), "") ?? new ASTNode(ASTNodeType.Literal, new NullValue()))
                    : EvaluateExpression(args.Elements[0]);
                var count = ToInteger(countValue);
                
                if (count < 0)
                {
                    throw new Exception("Do count must be non-negative");
                }
                
                var expressions = args.Elements.Skip(1).ToList();
                
                for (int i = 0; i < count; i++)
                {
                    foreach (var expr in expressions)
                    {
                        // Handle FunctionValue (contains AST to evaluate) vs regular K3Value
                        if (expr is FunctionValue func)
                        {
                            // Parse and evaluate the function body
                            var ast = ParserConfig.ParseWithConfig(func.PreParsedTokens ?? new List<Token>(), "");
                            if (ast != null) Evaluate(ast); // Execute but don't store result
                        }
                        else
                        {
                            EvaluateExpression(expr); // Execute but don't store result
                        }
                    }
                }
                
                // Return empty string to match k.exe behavior
                return new SymbolValue("");
            }
            else
            {
                throw new Exception("Do function requires at least 2 arguments: count and expression(s)");
            }
        }

        private K3Value WhileFunction(K3Value operand)
        {
            // While function: while[condition; expression] or while[condition; expression1; ; expressionN]
            // Execute expressions while condition is not equal to 0
            
            if (operand is VectorValue args && args.Elements.Count >= 2)
            {
                var condition = args.Elements[0];
                var expressions = args.Elements.Skip(1).ToList();
                K3Value result = new SymbolValue(""); // Empty symbol for when loop doesn't execute
                
                while (true)
                {
                    // Evaluate condition
                    var conditionResult = EvaluateExpression(condition);
                    
                    // Check if condition is zero (false)
                    if (!IsNonZeroInteger(conditionResult))
                    {
                        break;
                    }
                    
                    // Execute all expressions
                    foreach (var expr in expressions)
                    {
                        result = EvaluateExpression(expr);
                    }
                }
                
                return result;
            }
            else
            {
                throw new Exception("While function requires at least 2 arguments: condition and expression(s)");
            }
        }

        private K3Value IfFunction(K3Value operand)
        {
            // If function: if[condition; expression] or if[condition; expression1; ; expressionN]
            // Execute expressions if condition is not equal to 0, return empty string (matching k.exe behavior)
            
            if (operand is VectorValue args && args.Elements.Count >= 2)
            {
                var condition = args.Elements[0];
                var expressions = args.Elements.Skip(1).ToList();
                
                // Evaluate condition
                var conditionResult = EvaluateExpression(condition);
                
                // Check if condition is non-zero (true)
                if (IsNonZeroInteger(conditionResult))
                {
                    // Execute all expressions but don't store result
                    foreach (var expr in expressions)
                    {
                        EvaluateExpression(expr);
                    }
                }
                
                // Return empty string to match k.exe behavior
                return new SymbolValue("");
            }
            else
            {
                throw new Exception("If function requires at least 2 arguments: condition and expression(s)");
            }
        }
    }
}
