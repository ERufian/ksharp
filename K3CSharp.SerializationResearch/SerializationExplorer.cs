// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using K3CSharp;

namespace K3CSharp.SerializationResearch
{
    public class SerializationExplorer
    {
        private readonly KInterpreterWrapper wrapper;
        private readonly DataGenerator dataGenerator;
        private readonly string outputDirectory;

        public SerializationExplorer(string kExePath = @"c:\k\k.exe", string outputDirectory = "output")
        {
            wrapper = new KInterpreterWrapper(kExePath, 10000); // 10 second timeout
            dataGenerator = new DataGenerator();
            this.outputDirectory = outputDirectory;
            
            // Ensure output directory exists
            if (!Directory.Exists(this.outputDirectory))
            {
                Directory.CreateDirectory(this.outputDirectory);
            }
        }

        public string ExploreSerialization(DataType dataType, int count, bool edgeCasesOnly)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"serialization_{dataType}_{timestamp}.txt";
            var outputPath = Path.Combine(outputDirectory, filename);

            try
            {
                var examples = dataGenerator.GenerateExamples(dataType, count, edgeCasesOnly);
                var results = new List<SerializationResult>();

                Console.WriteLine($"Generating {examples.Count} examples for {dataType}...");

                foreach (var example in examples)
                {
                    var result = ProcessExample(example, dataType);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }

                // Write results to file
                WriteResultsFile(outputPath, dataType, results, edgeCasesOnly);
                
                Console.WriteLine($"Generated {results.Count} successful results out of {examples.Count} examples");
                Console.WriteLine($"Results saved to: {outputPath}");

                return outputPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during serialization exploration: {ex.Message}");
                throw;
            }
            finally
            {
                wrapper.CleanupTempDirectory();
            }
        }

        public string TestHypothesis(string testData, DataType dataType = DataType.Integer)
        {
            if (string.IsNullOrEmpty(testData))
            {
                throw new ArgumentException("Test data cannot be null or empty");
            }

            var results = new List<SerializationResult>();
            
            // Process the test data as a single example
            var result = ProcessExample(testData, dataType);
            
            if (result != null)
            {
                results.Add(result);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(outputDirectory, $"hypothesis_test_{timestamp}.txt");
            
            WriteResultsFile(outputPath, dataType, results, false);
            return outputPath;
        }

        public string RunCustomExperiments()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var listFilename = $"serialization_ListPaddingExperiments_List_{timestamp}.txt";
            var dictFilename = $"serialization_ListPaddingExperiments_Dictionary_{timestamp}.txt";
            var listOutputPath = Path.Combine(outputDirectory, listFilename);
            var dictOutputPath = Path.Combine(outputDirectory, dictFilename);

            try
            {
                var listResults = new List<SerializationResult>();
                var dictResults = new List<SerializationResult>();
                
                Console.WriteLine("Running custom padding experiments...");

                // Experiment 1: Symbol + Vector Size Correlation
                Console.WriteLine("Experiment 1: Symbol + Vector Size Correlation");
                var exp1Tests = new[]
                {
                    "(`test;1 2 3 4)",
                    "(`test;1 2 3 4 5)",
                    "(`test;1 2 3 4 5 6)",
                    "(`test;1 2 3 4 5 6 7)",
                    "(`test;1 2 3 4 5 6 7 8)"
                };
                
                foreach (var test in exp1Tests)
                {
                    var result = ProcessExample(test, DataType.List);
                    if (result != null) listResults.Add(result);
                }

                // Experiment 2: Symbol Type Variations
                Console.WriteLine("Experiment 2: Symbol Type Variations");
                var exp2Tests = new[]
                {
                    "(`sym;1 2 3)",
                    "(`sym;\"hello\")",
                    "(`sym;`a `b `c)",
                    "(`sym;1.5 2.5 3.5)",
                    "(`sym;\"abc\")"
                };
                
                foreach (var test in exp2Tests)
                {
                    var result = ProcessExample(test, DataType.List);
                    if (result != null) listResults.Add(result);
                }

                // Experiment 3: Alignment Boundary Testing
                Console.WriteLine("Experiment 3: Alignment Boundary Testing");
                var exp3Tests = new[]
                {
                    "(`a;1 2)",
                    "(`ab;1 2 3)",
                    "(`abc;1 2 3 4)",
                    "(`abcd;1 2 3 4 5)",
                    "(`abcde;1 2 3 4 5 6)",
                    "(`abcdef;1 2 3 4 5 6 7)",
                    "(`abcdefg;1 2 3 4 5 6 7 8)",
                    "(`abcdefgh;1 2 3 4 5 6 7 8 9)"
                };
                
                foreach (var test in exp3Tests)
                {
                    var result = ProcessExample(test, DataType.List);
                    if (result != null) listResults.Add(result);
                }

                // Experiment 4: Dictionary Triplet Padding (as lists)
                Console.WriteLine("Experiment 4: Dictionary Triplet Padding (as lists)");
                var exp4Tests = new[]
                {
                    "((`col1;1 2 3 4);(`col2;5 6 7 8))",
                    "((`longsymbolname;1 2 3 4 5);(`short;5 6 7 8))",
                    "((`x;1 2);(`y;3 4 5 6 7 8 9))",
                    "((`test;`a `b `c);(`other;1 2 3))"
                };
                
                foreach (var test in exp4Tests)
                {
                    var result = ProcessExample(test, DataType.List);
                    if (result != null) listResults.Add(result);
                }

                // Experiment 5: Mixed List Structure Variations
                Console.WriteLine("Experiment 5: Mixed List Structure Variations");
                var exp5Tests = new[]
                {
                    "(`sym;1)",
                    "(`sym;1 2)",
                    "(`sym;1 2 3)",
                    "(`sym;1 2 3 4)",
                    "(`sym;1 2 3 4 5)",
                    "(1;`sym;2 3 4)",
                    "(1;2;`sym;3 4 5)",
                    "(1;2;3;`sym;4 5 6)"
                };
                
                foreach (var test in exp5Tests)
                {
                    var result = ProcessExample(test, DataType.List);
                    if (result != null) listResults.Add(result);
                }

                // Experiment 6: Function-Containing Mixed Lists
                Console.WriteLine("Experiment 6: Function-Containing Mixed Lists");
                var exp6Tests = new[]
                {
                    ",{[]}", // 1-item list of functions
                    "(`sym;{[]})",
                    "(1;{[]})",
                    "(1;`sym;{[]})",
                    "({[]};1)",
                    "({[]};`sym)",
                    "({[]};1;2)",
                    "({[]};`sym;1)"
                };
                
                foreach (var test in exp6Tests)
                {
                    var result = ProcessExample(test, DataType.List);
                    if (result != null) listResults.Add(result);
                }

                // Experiment 7: Complex Mixed List Alignment
                Console.WriteLine("Experiment 7: Complex Mixed List Alignment");
                var exp7Tests = new[]
                {
                    "(_n;`symbol;{[]})",
                    "(_n;`symbol;{[]};1)",
                    "(_n;`symbol;1;{[]})",
                    "(`symbol;{[]};_n)",
                    "(1;`symbol;{[]};2)",
                    "(`a;{[]};`b;{[]})",
                    "(_n;`a;{[]};`b;{[]})",
                    "({[]};`symbol;_n)"
                };
                
                foreach (var test in exp7Tests)
                {
                    var result = ProcessExample(test, DataType.List);
                    if (result != null) listResults.Add(result);
                }

                // Experiment 8: Dictionary Symbol Vector Alignment
                Console.WriteLine("Experiment 8: Dictionary Symbol Vector Alignment");
                var exp8Tests = new[]
                {
                    ".,(`a;`b `c `d)",
                    ".,(`short;`x `y)",
                    ".,(`verylongsymbolname;`a `b `c `d `e)",
                    ".,(`x;`a `b `c `d `e `f `g `h)",
                    ".,(`test;`a `b `c `d `e `f `g `h `i `j)"
                };
                
                foreach (var test in exp8Tests)
                {
                    var result = ProcessExample(test, DataType.Dictionary);
                    if (result != null) dictResults.Add(result);
                }

                // Experiment 9: Dictionary Mixed Content Alignment
                Console.WriteLine("Experiment 9: Dictionary Mixed Content Alignment");
                var exp9Tests = new[]
                {
                    ".,(`sym;1 2 3)",
                    ".,(`sym;`a `b `c)",
                    ".,(`sym;1.5 2.5 3.5)",
                    ".,(`sym;\"abc\")",
                    ".,(`sym;1 2 3 4 5 6 7 8 9 10)"
                };
                
                foreach (var test in exp9Tests)
                {
                    var result = ProcessExample(test, DataType.Dictionary);
                    if (result != null) dictResults.Add(result);
                }

                // Experiment 10: Complex Dictionary Structure Analysis
                Console.WriteLine("Experiment 10: Complex Dictionary Structure Analysis");
                var exp10Tests = new[]
                {
                    ".((`colA;`a `b `c);(`colB;`dd `eee `ffff))",
                    ".((`x;`a `b);(`y;`c `d `e `f);(`z;`g `h `i `j))",
                    ".((`short;`a);(`verylongsymbolname;`x `y `z))",
                    ".((`num;1 2 3 4 5);(`sym;`a `b `c `d `e);(`char;\"abc\"))"
                };
                
                foreach (var test in exp10Tests)
                {
                    var result = ProcessExample(test, DataType.Dictionary);
                    if (result != null) dictResults.Add(result);
                }

                // Experiment 11: Dictionary vs List Comparison - Same Structure
                Console.WriteLine("Experiment 11: Dictionary vs List Comparison - Same Structure");
                var exp11Tests = new[]
                {
                    // Dictionary versions
                    ".,(`a;1 2 3)",
                    ".,(`ab;1 2 3 4)",
                    ".,(`abc;1 2 3 4 5)",
                    ".,(`abcd;1 2 3 4 5 6)",
                    ".,(`test;`x `y `z)",
                    ".,(`longsymbol;`a `b `c `d `e)",
                    // List equivalents (triplets)
                    ",(`a;1 2 3;)",
                    ",(`ab;1 2 3 4;)",
                    ",(`abc;1 2 3 4 5;)",
                    ",(`abcd;1 2 3 4 5 6;)",
                    ",(`test;`x `y `z;)",
                    ",(`longsymbol;`a `b `c `d `e;)"
                };
                
                foreach (var test in exp11Tests)
                {
                    var dataType = test.StartsWith(".") ? DataType.Dictionary : DataType.List;
                    var result = ProcessExample(test, dataType);
                    if (result != null)
                    {
                        if (dataType == DataType.Dictionary)
                            dictResults.Add(result);
                        else
                            listResults.Add(result);
                    }
                }

                // Experiment 12: Symbol Length Variations in Dictionaries
                Console.WriteLine("Experiment 12: Symbol Length Variations in Dictionaries");
                var exp12Tests = new[]
                {
                    ".,(`a;1)",           // 1-char symbol
                    ".,(`ab;1 2)",        // 2-char symbol  
                    ".,(`abc;1 2 3)",      // 3-char symbol
                    ".,(`abcd;1 2 3 4)",   // 4-char symbol
                    ".,(`abcde;1 2 3 4 5)", // 5-char symbol
                    ".,(`abcdef;1 2 3 4 5 6)", // 6-char symbol
                    ".,(`abcdefgh;1 2 3 4 5 6 7 8)" // 8-char symbol
                };
                
                foreach (var test in exp12Tests)
                {
                    var result = ProcessExample(test, DataType.Dictionary);
                    if (result != null) dictResults.Add(result);
                }

                // Experiment 13: Multiple Entry Dictionary Analysis
                Console.WriteLine("Experiment 13: Multiple Entry Dictionary Analysis");
                var exp13Tests = new[]
                {
                    ".,(`a;1)",
                    ".((`a;1);(`b;2))",
                    ".((`a;1);(`b;2);(`c;3))",
                    ".((`a;1);(`b;2);(`c;3);(`d;4))",
                    ".((`a;1);(`b;2);(`c;3);(`d;4);(`e;5))"
                };
                
                foreach (var test in exp13Tests)
                {
                    var result = ProcessExample(test, DataType.Dictionary);
                    if (result != null) dictResults.Add(result);
                }

                // Write results to separate files
                WriteResultsFile(listOutputPath, DataType.List, listResults, false);
                WriteResultsFile(dictOutputPath, DataType.Dictionary, dictResults, false);
                
                var totalResults = listResults.Count + dictResults.Count;
                Console.WriteLine($"Generated {totalResults} successful experiment results");
                Console.WriteLine($"List results saved to: {listOutputPath}");
                Console.WriteLine($"Dictionary results saved to: {dictOutputPath}");

                return listOutputPath; // Return primary file path
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during custom experiments: {ex.Message}");
                throw;
            }
            finally
            {
                wrapper.CleanupTempDirectory();
            }
        }

        private SerializationResult? ProcessExample(string example, DataType dataType)
        {
            try
            {
                // Create _bd command
                var bdCommand = $"_bd {example}";
                
                // Execute with k.exe
                var result = wrapper.ExecuteScript(bdCommand);
                
                // Clean up result (remove licensing info, etc.)
                var cleanedResult = CleanResult(result);
                
                // Validate that the result type matches expected type
                if (!ValidateResultType(example, dataType))
                {
                    Console.WriteLine($"Generated example '{example}' does not match expected type {dataType}");
                    return null;
                }
                
                return new SerializationResult
                {
                    Input = example,
                    BdCommand = bdCommand,
                    SerializedOutput = cleanedResult,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process example '{example}': {ex.Message}");
                return null; // Skip failed examples as per specification
            }
        }

        private bool ValidateResultType(string example, DataType expectedType)
        {
            try
            {
                // Use 4: operator to check type of generated example
                var typeCommand = $"4: {example}";
                var typeResult = wrapper.ExecuteScript(typeCommand);
                var cleanedType = CleanResult(typeResult).Trim();
                
                // Map K type names to our DataType enum
                var actualType = cleanedType switch
                {
                    "integer" or "1" => DataType.Integer,
                    "float" or "2" => DataType.Float,
                    "character" or "3" => DataType.Character,
                    "symbol" or "4" => DataType.Symbol,
                    "dictionary" or "5" => DataType.Dictionary,
                    "null" or "6" => DataType.Null,
                    "function" or "7" => DataType.AnonymousFunction, // Handle both "function" and numeric type code 7
                    "integer vector" or "-1" => DataType.IntegerVector,
                    "float vector" or "-2" => DataType.FloatVector,
                    "character vector" or "-3" => DataType.CharacterVector,
                    "symbol vector" or "-4" => DataType.SymbolVector,
                    "list" or "0" => DataType.List,
                    _ => (DataType?)null
                };
                
                return actualType == expectedType;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Type validation failed for '{example}': {ex.Message}");
                return false;
            }
        }

        private string CleanResult(string result)
        {
            if (string.IsNullOrEmpty(result))
                return string.Empty;

            // Remove licensing information lines
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var cleanedLines = lines.Where(line => 
                !line.Contains("WIN32") && 
                !line.Contains("CPU") && 
                !line.Contains("MB") &&
                !line.Contains("EVAL")).ToArray();

            return string.Join("\n", cleanedLines).Trim();
        }

        private void WriteResultsFile(string outputPath, DataType dataType, List<SerializationResult> results, bool edgeCasesOnly)
        {
            using var writer = new StreamWriter(outputPath);
            
            // Write header
            writer.WriteLine($"# Serialization Research: {dataType} ({(edgeCasesOnly ? "edge cases" : "random examples")})");
            writer.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"# Total examples: {results.Count}");
            writer.WriteLine();

            // Write results
            foreach (var result in results)
            {
                writer.WriteLine($"{result.BdCommand} → {result.SerializedOutput}");
            }

            // Write summary statistics
            writer.WriteLine();
            writer.WriteLine($"# Summary:");
            writer.WriteLine($"# Successful examples: {results.Count}");
            writer.WriteLine($"# Data type: {dataType}");
            writer.WriteLine($"# Mode: {(edgeCasesOnly ? "Edge cases" : "Random generation")}");
        }

        public void Cleanup()
        {
            wrapper.CleanupTempDirectory();
        }
    }

    public class SerializationResult
    {
        public string Input { get; set; } = string.Empty;
        public string BdCommand { get; set; } = string.Empty;
        public string SerializedOutput { get; set; } = string.Empty;
        public bool Success { get; set; }
    }
}