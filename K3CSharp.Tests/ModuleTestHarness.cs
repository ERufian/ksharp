// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;

namespace K3CSharp.Tests
{
    /// <summary>
    /// Simple test harness for validating parser modules
    /// </summary>
    public class ModuleTestHarness
    {
        /// <summary>
        /// Test the MonadicOperatorParser with simple expressions
        /// </summary>
        public static void TestMonadicOperatorParser()
        {
            Console.WriteLine("Testing MonadicOperatorParser...");
            
            // Test cases: (token, expectedSymbol)
            var testCases = new[]
            {
                (TokenType.PLUS, "+"),
                (TokenType.MINUS, "-"),
                (TokenType.MULTIPLY, "*"),
                (TokenType.DIVIDE, "%"),
                (TokenType.MODULUS, "%"),
                (TokenType.POWER, "^"),
                (TokenType.MIN, "&"),
                (TokenType.MAX, "|"),
                (TokenType.MATCH, "~"),
                (TokenType.NOT, "~"),
                (TokenType.HASH, "#"),
                (TokenType.UNDERSCORE, "_"),
                (TokenType.QUESTION, "?"),
                (TokenType.DOLLAR, "$"),
                (TokenType.APPLY, "@")
            };
            
            foreach (var (tokenType, expectedSymbol) in testCases)
            {
                try
                {
                    var tokens = new List<Token>
                    {
                        new Token(tokenType, expectedSymbol, 0),
                        new Token(TokenType.INTEGER, "42", 1)
                    };
                    
                    var context = new ParseContext(tokens, "test");
                    
                    var parser = new MonadicOperatorParser();
                    if (parser.CanHandle(tokenType))
                    {
                        var result = parser.Parse(context);
                        Console.WriteLine($"✓ {tokenType}: Successfully parsed monadic operator");
                    }
                    else
                    {
                        Console.WriteLine($"✗ {tokenType}: Parser cannot handle this token type");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ {tokenType}: Error - {ex.Message}");
                }
            }
            
            Console.WriteLine("MonadicOperatorParser test completed.");
        }
    }
}