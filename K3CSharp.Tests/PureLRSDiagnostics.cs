// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using K3CSharp;
using K3CSharp.Parsing;

namespace K3CSharp.Tests
{
    public class PureLRSDiagnosticsRunner
    {
        public static void RunDiagnostics()
        {
            Console.WriteLine("=== Pure LRS Diagnostics ===");
            
            // Enable Pure LRS mode WITH debugging for troubleshooting
            ParserConfig.EnablePureLRS();
            ParserConfig.EnableDebugging = true;
            ParserConfig.LogConfigChange("Pure LRS Debug - Bracket Function Call Trace");
            
            // Test case 1: Simple vector (should create implicit vector)
            Console.WriteLine("\n--- Test 1: Simple vector '1 2 3 4 5' ---");
            TestExpression("1 2 3 4 5");
            
            // Test case 2: Simple assignment (should parse assignment)
            Console.WriteLine("\n--- Test 2: Simple assignment 'a:5' ---");
            TestExpression("a:5");
            
            // Test case 3: String assignment (should parse assignment)
            Console.WriteLine("\n--- Test 3: String assignment 's:\"hello\"' ---");
            TestExpression("s:\"hello\"");
            
            // Test case 4: Mixed types (should create list)
            Console.WriteLine("\n--- Test 4: Mixed types '1 2.5 \"hello\"' ---");
            TestExpression("1 2.5 \"hello\"");
            
            // Test case 5: Special values
            Console.WriteLine("\n--- Test 5: Special values '0I 0N -0I' ---");
            TestExpression("0I 0N -0I");
            
            // Test case 6: Symbol vector
            Console.WriteLine("\n--- Test 6: Symbol vector '`a`b`c' ---");
            TestExpression("`a`b`c");
            
            // Test case 7: Empty vector
            Console.WriteLine("\n--- Test 7: Empty vector '()' ---");
            TestExpression("()");
            
            // Test case 8: Simple arithmetic
            Console.WriteLine("\n--- Test 8: Simple arithmetic '1 + 2' ---");
            TestExpression("1 + 2");
            
            // Test case 9: Multi-line script
            Console.WriteLine("\n--- Test 9: Multi-line script ---");
            TestMultiLineScript("a:5\na+3");
            
            // Test case 10: Dictionary creation and indexing
            Console.WriteLine("\n--- Test 10: Dictionary indexing ---");
            TestMultiLineScript("d:.((`keyA;1 2 3;);(`keyB;1 3 5))\nd[`keyB]");
            
            // Test case 11: Simple implicit vector
            Console.WriteLine("\n--- Test 11: Simple implicit vector '1 2 3 4 5' ---");
            TestExpression("1 2 3 4 5");
            
            // Test case 12: Monadic colon
            Console.WriteLine("\n--- Test 12: Monadic colon ': 42' ---");
            TestExpression(": 42");
            
            // Test case 13: System function _gtime
            Console.WriteLine("\n--- Test 13: System function '_gtime 0' ---");
            TestExpression("_gtime 0");
            
            // Test case 14: Comment line followed by code
            Console.WriteLine("\n--- Test 14: Comment + code ---");
            TestExpression("_bd 42");
            
            // Test case 15: Just system operator
            Console.WriteLine("\n--- Test 15: Just _bd 42 ---");
            TestExpression("_bd 42");
            
            // Test case 16: 5-element vector (failing in LRS)
            Console.WriteLine("\n--- Test 16: 5-element vector '1 2 3 4 5' ---");
            TestExpression("1 2 3 4 5");
            
            // Test case 17: _db _bd system operators
            Console.WriteLine("\n--- Test 17: _db _bd 42 ---");
            TestExpression("_db _bd 42");
            
            // Test case 18: Lambda bracket call
            Console.WriteLine("\n--- Test 18: Lambda bracket call ---");
            TestExpression("{a:\"hello\";a}[]");
            
            // Test case 19: Function bracket call div[8;4]
            Console.WriteLine("\n--- Test 19: Function bracket call ---");
            TestExpression("div[8;4]");
            
            Console.WriteLine("\n=== Diagnostics Complete ===");
        }
        
        static void TestExpression(string expression)
        {
            try
            {
                var lexer = new Lexer(expression);
                var tokens = lexer.Tokenize();
                
                Console.WriteLine($"Tokens: {string.Join(", ", tokens.Select(t => $"{t.Type}({t.Lexeme})"))}");
                
                var parser = new LRSParser(tokens);
                parser.PureLRSMode = true;
                
                int position = 0;
                var result = parser.ParseExpression(ref position);
                
                Console.WriteLine($"Result: {(result == null ? "NULL" : result.Type.ToString())}");
                if (result != null)
                {
                    var evaluator = new Evaluator();
                    var value = evaluator.Evaluate(result);
                    Console.WriteLine($"Value: {value}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
        
        static void TestMultiLineScript(string script)
        {
            try
            {
                var lexer = new Lexer(script);
                var tokens = lexer.Tokenize();
                
                Console.WriteLine($"Multi-line tokens: {string.Join(", ", tokens.Select(t => $"{t.Type}({t.Lexeme})"))}");
                
                // Use LRSParserWrapper for multi-line scripts (Pure LRS mode)
                var wrapper = new LRSParserWrapper(tokens, script);
                
                var result = wrapper.Parse();
                
                Console.WriteLine($"Multi-line result: {(result == null ? "NULL" : result.Type.ToString())}");
                if (result != null)
                {
                    var evaluator = new Evaluator();
                    var value = evaluator.Evaluate(result);
                    Console.WriteLine($"Multi-line value: {value}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Multi-line ERROR: {ex.Message}");
            }
        }
    }
}