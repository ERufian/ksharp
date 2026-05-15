// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using K3CSharp;
using K3CSharp.Parsing;

namespace K3CSharp.Tests
{
    /// <summary>
    /// Test class to validate LRS parser functionality without requiring build
    /// </summary>
    public static class LRSParserValidator
    {
        /// <summary>
        /// Validate that the LRS parser correctly handles monadic operators
        /// </summary>
        public static void ValidateMonadicOperators()
        {
            Console.WriteLine("=== LRS Parser Validation ===");
            
            // Test 1: Parse "^,`a" (monadic ^ with monadic ,)
            TestParseExpression("^,`a", "Should parse as ^(,`a)");
            
            // Test 2: Parse "-5" (monadic minus)
            TestParseExpression("-5", "Should parse as monadic minus");
            
            // Test 3: Parse ",`a" (monadic enlist)
            TestParseExpression(",`a", "Should parse as monadic enlist");
            
            // Test 4: Parse "1+2" (dyadic plus)
            TestParseExpression("1+2", "Should parse as dyadic plus");
            
            Console.WriteLine("=== Operator Detection Validation ===");
            
            // Test verb-agnostic operator detection
            ValidateOperatorDetection();
            
            Console.WriteLine("=== LRS Parser Validation Complete ===");
        }
        
        /// <summary>
        /// Test parsing a specific expression
        /// </summary>
        private static void TestParseExpression(string expression, string description)
        {
            Console.WriteLine($"\nTesting: {expression}");
            Console.WriteLine($"Description: {description}");
            
            try
            {
                // Test the lexer
                var lexer = new Lexer(expression);
                var tokens = lexer.Tokenize();
                
                // Test the LRS parser
                var lrsParser = new LRSParser(tokens);
                var position = 0;
                var astNode = lrsParser.ParseExpression(ref position);
                
                if (astNode == null)
                {
                    Console.WriteLine("❌ FAILED: LRS Parser returned null");
                    throw new Exception($"LRS Parser failed to parse: {expression}");
                }
                else
                {
                    Console.WriteLine("✅ SUCCESS: LRS Parser parsed successfully");
                    Console.WriteLine($"AST Type: {astNode.Type}");
                    Console.WriteLine($"AST Value: {astNode.Value}");
                    Console.WriteLine($"AST Children: {astNode.Children.Count}");
                    
                    // Print AST structure
                    PrintAST(astNode, 0);
                    
                    // Convert to K list
                    var kList = ParseTreeConverter.ToKList(astNode);
                    Console.WriteLine($"K List Result: {kList}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FAILED: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Validate verb-agnostic operator detection
        /// </summary>
        private static void ValidateOperatorDetection()
        {
            var testTokens = new[]
            {
                TokenType.POWER,    // ^ (monadic/dyadic)
                TokenType.JOIN,      // , (monadic/dyadic)
                TokenType.PLUS,      // + (dyadic only)
                TokenType.MINUS,     // - (monadic/dyadic)
                TokenType.MULTIPLY,  // * (dyadic only)
                TokenType.DIVIDE,    // % (dyadic only)
                TokenType.PARSE,     // _parse (function)
                TokenType.EVAL       // _eval (function)
            };
            
            foreach (var tokenType in testTokens)
            {
                Console.WriteLine($"\n{tokenType}:");
                Console.WriteLine($"  IsDyadicOperator: {OperatorDetector.IsDyadicOperator(tokenType)}");
                Console.WriteLine($"  SupportsMonadic: {OperatorDetector.SupportsMonadic(tokenType)}");
                Console.WriteLine($"  SupportsDyadic: {OperatorDetector.SupportsDyadic(tokenType)}");
                Console.WriteLine($"  IsFunction: {OperatorDetector.IsFunction(tokenType)}");
            }
        }
        
        /// <summary>
        /// Print AST structure for debugging
        /// </summary>
        private static void PrintAST(ASTNode node, int depth)
        {
            var indent = new string(' ', depth * 2);
            Console.WriteLine($"{indent}{node.Type}: {node.Value} (children: {node.Children.Count})");
            for (int i = 0; i < node.Children.Count; i++)
            {
                Console.WriteLine($"{indent}  Child {i}:");
                PrintAST(node.Children[i], depth + 1);
            }
        }
        
        /// <summary>
        /// Validate that the AST structure for "^,`a" is correct
        /// </summary>
        public static void ValidateMonadicShapeAtomicAST(ASTNode node)
        {
            Console.WriteLine("Validating AST structure for ^,`a");
            
            // Expected structure: ^ (, `a)
            // Should be DyadicOp("^") with child DyadicOp(",") with child Literal(`a)
            
            if (node.Type != ASTNodeType.DyadicOp)
                throw new Exception($"Expected DyadicOp, got {node.Type}");
                
            if (node.Value?.ToString() != "^")
                throw new Exception($"Expected ^ operator, got {node.Value}");
                
            if (node.Children.Count != 1)
                throw new Exception($"Expected 1 child for monadic ^, got {node.Children.Count}");
            
            var child = node.Children[0];
            if (child.Type != ASTNodeType.DyadicOp)
                throw new Exception($"Expected DyadicOp child, got {child.Type}");
                
            if (child.Value?.ToString() != ",")
                throw new Exception($"Expected , operator child, got {child.Value}");
                
            if (child.Children.Count != 1)
                throw new Exception($"Expected 1 child for monadic ,, got {child.Children.Count}");
            
            var grandchild = child.Children[0];
            if (grandchild.Type != ASTNodeType.Literal)
                throw new Exception($"Expected Literal grandchild, got {grandchild.Type}");
            
            Console.WriteLine("✅ AST structure validation passed!");
        }
    }
}