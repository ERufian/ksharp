// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using K3CSharp;

namespace K3CSharp.Tests
{
    /// <summary>
    /// Comprehensive test suite for VerbRegistry functionality
    /// </summary>
    public class VerbRegistryTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("=== VerbRegistry Test Suite ===");
            
            var testResults = new List<string>();
            
            // Basic functionality tests
            testResults.Add(TestBasicVerbLookup());
            testResults.Add(TestArityValidation());
            testResults.Add(TestMultiArities());
            testResults.Add(SystemVariableTests());
            testResults.Add(ProjectedFunctionTests());
            
            // Performance tests
            testResults.Add(PerformanceTests.TestVerbLookupPerformance());
            testResults.Add(PerformanceTests.TestArityValidationPerformance());
            testResults.Add(PerformanceTests.TestRegistryValidationPerformance());
            
            // Integration tests
            testResults.Add(IntegrationTests.TestEvaluatorIntegration());
            testResults.Add(IntegrationTests.TestParserIntegration());
            
            // Report results
            var passed = testResults.Count(r => r.StartsWith("✓"));
            var total = testResults.Count;
            
            Console.WriteLine($"\n=== Test Results ===");
            Console.WriteLine($"Passed: {passed}/{total}");
            
            foreach (var result in testResults)
            {
                Console.WriteLine(result);
            }
            
            // Export registry information
            Console.WriteLine("\n=== Registry Information ===");
            Console.WriteLine(VerbRegistry.ExportVerbInfo());
            
            // Performance statistics
            Console.WriteLine("\n=== Performance Statistics ===");
            var stats = VerbRegistry.GetPerformanceStats();
            foreach (var stat in stats)
            {
                Console.WriteLine($"{stat.Key}: {stat.Value}");
            }
            
            // Validation results
            Console.WriteLine("\n=== Registry Validation ===");
            var issues = VerbRegistry.ValidateRegistry();
            if (issues.Count == 0)
            {
                Console.WriteLine("✓ No validation issues found");
            }
            else
            {
                Console.WriteLine($"✗ Found {issues.Count} validation issues:");
                foreach (var issue in issues)
                {
                    Console.WriteLine($"  - {issue}");
                }
            }
        }
        
        private static string TestBasicVerbLookup()
        {
            try
            {
                var verb = VerbRegistry.GetVerb("+");
                if (verb == null) return "✗ Basic lookup failed - '+' not found";
                if (verb.Type != VerbType.Operator) return "✗ Type mismatch - '+' should be Operator";
                if (!verb.SupportedArities.Contains(1) || !verb.SupportedArities.Contains(2)) 
                    return "✗ Arity mismatch - '+' should support 1 and 2";
                
                return "✓ Basic verb lookup tests passed";
            }
            catch (Exception ex)
            {
                return $"✗ Basic lookup failed: {ex.Message}";
            }
        }
        
        private static string TestArityValidation()
        {
            try
            {
                var error = VerbRegistry.ValidateVerbArity("+", 2);
                if (!string.IsNullOrEmpty(error)) return $"✗ Valid arity validation failed: {error}";
                
                error = VerbRegistry.ValidateVerbArity("+", 3);
                if (string.IsNullOrEmpty(error)) return "✗ Invalid arity validation should have failed";
                
                error = VerbRegistry.ValidateVerbArity("nonexistent", 1);
                if (string.IsNullOrEmpty(error)) return "✗ Non-existent verb validation should have failed";
                
                return "✓ Arity validation tests passed";
            }
            catch (Exception ex)
            {
                return $"✗ Arity validation failed: {ex.Message}";
            }
        }
        
        private static string TestMultiArities()
        {
            try
            {
                // Test context-sensitive arity determination
                var arity1 = VerbRegistry.GetPreferredArity("#", false);
                var arity2 = VerbRegistry.GetPreferredArity("#", true);
                
                if (arity1 != 1) return "✗ Hash without left operand should prefer arity 1";
                if (arity2 != 2) return "✗ Hash with left operand should prefer arity 2";
                
                // Test multi-arity support
                if (!VerbRegistry.SupportsArity("#", 1)) return "✗ Hash should support arity 1";
                if (!VerbRegistry.SupportsArity("#", 2)) return "✗ Hash should support arity 2";
                if (VerbRegistry.SupportsArity("#", 3)) return "✗ Hash should not support arity 3";
                
                return "✓ Multi-arity tests passed";
            }
            catch (Exception ex)
            {
                return $"✗ Multi-arity tests failed: {ex.Message}";
            }
        }
        
        private static string SystemVariableTests()
        {
            try
            {
                if (!VerbRegistry.IsSystemVariable("_d")) return "✗ _d should be recognized as system variable";
                if (VerbRegistry.IsSystemVariable("+")) return "✗ + should not be system variable";
                
                var type = VerbRegistry.GetVerbType("_d");
                if (type != VerbType.SystemVariable) return "✗ _d type should be SystemVariable";
                
                return "✓ System variable tests passed";
            }
            catch (Exception ex)
            {
                return $"✗ System variable tests failed: {ex.Message}";
            }
        }
        
        private static string ProjectedFunctionTests()
        {
            try
            {
                // Test projected function registration
                VerbRegistry.RegisterProjectedFunction("test_proj", new[] { 1 }, "Test projected function");
                
                if (!VerbRegistry.IsProjectedFunction("test_proj")) return "✗ Registered projected function not recognized";
                
                var remainingArity = VerbRegistry.GetRemainingArity("test_proj");
                if (remainingArity != 0) return $"✗ Remaining arity should be 0, got {remainingArity}";
                
                // Test higher-order verbs
                if (!VerbRegistry.SupportsAdverbs("+")) return "✗ + should support adverbs";
                
                return "✓ Projected function tests passed";
            }
            catch (Exception ex)
            {
                return $"✗ Projected function tests failed: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Performance testing utilities
        /// </summary>
        public static class PerformanceTests
        {
            public static string TestVerbLookupPerformance()
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    
                    // Test 10,000 verb lookups
                    for (int i = 0; i < 10000; i++)
                    {
                        var verb = VerbRegistry.GetVerb("+");
                        if (verb == null) return "✗ Performance test failed - verb lookup returned null";
                    }
                    
                    sw.Stop();
                    
                    if (sw.ElapsedMilliseconds > 100) // Should be under 100ms
                        return $"✗ Verb lookup performance too slow: {sw.ElapsedMilliseconds}ms";
                    
                    return $"✓ Verb lookup performance: {sw.ElapsedMilliseconds}ms for 10,000 lookups";
                }
                catch (Exception ex)
                {
                    return $"✗ Verb lookup performance test failed: {ex.Message}";
                }
            }
            
            public static string TestArityValidationPerformance()
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    
                    // Test 10,000 arity validations
                    for (int i = 0; i < 10000; i++)
                    {
                        var error = VerbRegistry.ValidateVerbArity("+", 2);
                        if (!string.IsNullOrEmpty(error)) return "✗ Performance test failed - validation returned error";
                    }
                    
                    sw.Stop();
                    
                    if (sw.ElapsedMilliseconds > 50) // Should be under 50ms
                        return $"✗ Arity validation performance too slow: {sw.ElapsedMilliseconds}ms";
                    
                    return $"✓ Arity validation performance: {sw.ElapsedMilliseconds}ms for 10,000 validations";
                }
                catch (Exception ex)
                {
                    return $"✗ Arity validation performance test failed: {ex.Message}";
                }
            }
            
            public static string TestRegistryValidationPerformance()
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    
                    // Test 100 registry validations
                    for (int i = 0; i < 100; i++)
                    {
                        var issues = VerbRegistry.ValidateRegistry();
                        // Registry should be valid, so issues should be empty
                    }
                    
                    sw.Stop();
                    
                    if (sw.ElapsedMilliseconds > 1000) // Should be under 1 second
                        return $"✗ Registry validation performance too slow: {sw.ElapsedMilliseconds}ms";
                    
                    return $"✓ Registry validation performance: {sw.ElapsedMilliseconds}ms for 100 validations";
                }
                catch (Exception ex)
                {
                    return $"✗ Registry validation performance test failed: {ex.Message}";
                }
            }
        }
        
        /// <summary>
        /// Integration testing utilities
        /// </summary>
        public static class IntegrationTests
        {
            public static string TestEvaluatorIntegration()
            {
                try
                {
                    var evaluator = new Evaluator();
                    
                    // Test _val function
                    var result = evaluator.EvaluateVerb("_val", new[] { new SymbolValue("+") });
                    if (!(result is IntegerValue intVal) || intVal.Value != 2)
                        return "✗ _val function integration failed";
                    
                    // Test system variable access
                    var systemVar = evaluator.GetSystemVariable("_d");
                    if (!(systemVar is IntegerValue dayVal) || dayVal.Value < 1 || dayVal.Value > 31)
                        return "✗ System variable integration failed";
                    
                    return "✓ Evaluator integration tests passed";
                }
                catch (Exception ex)
                {
                    return $"✗ Evaluator integration tests failed: {ex.Message}";
                }
            }
            
            public static string TestParserIntegration()
            {
                try
                {
                    // Test that parser can access VerbRegistry for context-aware parsing
                    // This is a simplified test - in a full implementation, we'd test actual parsing
                    
                    var hasPlus = VerbRegistry.HasVerb("+");
                    if (!hasPlus) return "✗ Parser integration failed - VerbRegistry not accessible";
                    
                    var plusType = VerbRegistry.GetVerbType("+");
                    if (plusType != VerbType.Operator) return "✗ Parser integration failed - wrong verb type";
                    
                    return "✓ Parser integration tests passed";
                }
                catch (Exception ex)
                {
                    return $"✗ Parser integration tests failed: {ex.Message}";
                }
            }
        }
    }
}