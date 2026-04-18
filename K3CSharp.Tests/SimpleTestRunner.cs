using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using K3CSharp;
using K3CSharp.Verbs;
using K3CSharp.Parsing;

namespace K3CSharp.Tests

{

    public class SimpleTestRunner

    {

        private readonly string testScriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "TestScripts");

        static SimpleTestRunner()
        {
            // Enable Safe LRS mode - production configuration
            // ParserConfig.EnableLRSSafely();
            ParserConfig.EnablePureLRS();
            ParserConfig.EnableDebugging = false;
            ParserConfig.LogConfigChange("TestRunner initialization - Safe LRS mode");
        }



        public static void Main(string[] args)

        {

            // Check for diagnostics mode
            if (args.Length > 0 && args[0] == "--diagnose-pure-lrs")
            {
                PureLRSDiagnosticsRunner.RunDiagnostics();
                return;
            }
            
            // Check for LRS bug analysis mode
            if (args.Length > 0 && args[0] == "--analyze-lrs-bugs")
            {
                LRSBugAnalyzer.AnalyzeLRSBugs();
                return;
            }

            // Set PROMPT environment variable to "$P$G" for consistency with test expectations

            Environment.SetEnvironmentVariable("PROMPT", "$P$G");



            RunAllTests(args.Length > 0 ? args[0] : null);

        }



        private static void WriteResultsTable(List<TestResult> testResults)

        {

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "results_table.txt");

            // Calculate the maximum filename length for auto-sizing
            var maxFileNameLength = testResults.Count > 0 ? Math.Max(25, testResults.Max(t => t.FileName.Length) + 2) : 25; // +2 for padding

            var totalWidth = maxFileNameLength + 67; // 67 for other columns and borders

            using (var writer = new StreamWriter(outputPath))

            {

                writer.WriteLine("╔" + new string('═', totalWidth - 2) + "╗");

                writer.WriteLine("║" + "K3 INTERPRETER TEST RESULTS TABLE".PadLeft((totalWidth - 2) / 2 + 20) + "║");

                writer.WriteLine("╠" + new string('═', totalWidth - 2) + "╣");

                writer.WriteLine("║ " + "Test File".PadRight(maxFileNameLength - 2) + " │ " + "Input".PadRight(20) + " │ " + "Actual Output".PadRight(20) + " │ " + "Expected".PadRight(20) + " ║");

                writer.WriteLine("╠" + new string('═', totalWidth - 2) + "╣");



                foreach (var test in testResults)

                {

                    var input = GetTestInput(test.FileName);

                    var expected = test.Passed ? "" : test.Expected;



                    // Truncate long outputs for table display

                    var actualOutput = test.ActualOutput.Length > 18 ? test.ActualOutput.Substring(0, 15) + "..." : test.ActualOutput;

                    var expectedOutput = expected.Length > 18 ? expected.Substring(0, 15) + "..." : expected;



                    writer.WriteLine("║ " + test.FileName.PadRight(maxFileNameLength - 2) + " │ " + input.PadRight(20) + " │ " + actualOutput.PadRight(20) + " │ " + expectedOutput.PadRight(20) + " ║");

                }



                writer.WriteLine("╠" + new string('═', totalWidth - 2) + "╣");

                var passedCount = testResults.Count(t => t.Passed);

                var totalCount = testResults.Count;

                writer.WriteLine("║ " + $"SUMMARY: {passedCount}/{totalCount} tests passed ({(passedCount * 100.0 / totalCount):F1}%)".PadRight(totalWidth - 4) + " ║");

                writer.WriteLine("╚" + new string('═', totalWidth - 2) + "╝");



                // Write detailed failing tests section

                var failingTests = testResults.Where(t => !t.Passed).ToList();

                if (failingTests.Any())

                {

                    writer.WriteLine();

                    writer.WriteLine("FAILING TESTS DETAILS:");

                    writer.WriteLine("═════════════════════════════════════════════════════════════════════════════════════");



                    foreach (var test in failingTests)

                    {

                        writer.WriteLine($"Test: {test.FileName}");

                        writer.WriteLine($"Input: {GetTestInput(test.FileName)}");

                        writer.WriteLine($"Expected: {test.Expected}");

                        writer.WriteLine($"Actual: {test.ActualOutput}");

                        writer.WriteLine("──────────────────────────────────────────────────────────────────────────");

                    }

                }

            }



            Console.WriteLine($"Detailed results table written to: {outputPath}");

        }



        private static string GetTestInput(string fileName)

        {

            try

            {

                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "TestScripts", fileName);

                var script = File.ReadAllText(scriptPath);

                // Trim trailing whitespace and empty lines as per K specification
                // Also strip comments (/) which can be at start of line or after space
                var lines = script
                    .Split('\n')
                    .Select(line => {
                        var trimmed = line.Trim();
                        // Strip comments: / at start or space followed by /
                        var commentIndex = trimmed.StartsWith("/") ? 0 : trimmed.IndexOf(" /");
                        if (commentIndex >= 0)
                            trimmed = trimmed.Substring(0, commentIndex).Trim();
                        return trimmed;
                    })
                    .Where(line => !string.IsNullOrEmpty(line))
                    .ToArray();

                return string.Join("\n", lines);
            }

            catch

            {

                return "[File not found]";

            }

        }



        public class TestResult

        {

            public string FileName { get; set; } = "";

            public string ActualOutput { get; set; } = "";

            public string Expected { get; set; } = "";

            public bool Passed { get; set; }

        }



        public static void RunAllTests(string? filter = null)

        {

            // Comprehensive test list with expected results from K.exe

            var allTests = new[]

            {

                // Adverb Each tests (from K.exe results)

                ("adverb_each_vector_minus.k", "9 18 27"),

                ("adverb_each_vector_multiply.k", "4 10 18"),

                ("adverb_each_vector_plus.k", "6 8 10 12"),

                

                // Adverb Over tests

                ("adverb_over_divide.k", "10.0"),

                ("adverb_over_max.k", "5"),

                ("adverb_over_min.k", "1"),

                ("adverb_over_minus.k", "4"),

                ("adverb_over_multiply.k", "24"),

                ("adverb_over_plus.k", "15"),

                ("plus_over_empty.k", "0"),

                ("multiply_over_empty.k", "1"),

                ("adverb_over_power.k", "64"),

                ("adverb_over_with_initialization_1.k", "15"),

                ("adverb_over_with_initialization_2.k", "12"),

                

                // Adverb Scan tests

                ("adverb_scan_divide.k", "(100;50.0;10.0)"),

                ("adverb_scan_max.k", "1 3 3 5 5"),

                ("adverb_scan_min.k", "5 3 3 1 1"),

                ("adverb_scan_minus.k", "10 8 5 4"),

                ("adverb_scan_multiply.k", "1 2 6 24"),

                ("adverb_scan_plus.k", "1 3 6 10 15"),

                ("adverb_scan_power.k", "2 8 64"),

                ("adverb_scan_with_initialization_1.k", "1 3 6 10 15"),

                ("adverb_scan_with_initialization_2.k", "2 3 5 8 12"),

                ("adverb_scan_with_initialization_divide.k", "(2;2.0;1.0;0.3333333;0.08333333)"), // Test %\ scan with divide

                ("adverb_scan_with_initialization_minus.k", "2 1 -1 -4 -8"),

                ("adverb_scan_with_initialization.k", "2 2 4 12"),

                ("adverb_scan_monad_do_transpose.k", "((0 1 2;3 4 5);(0 3;1 4;2 5))"),

                ("adverb_scan_monad_do_reverse.k", "(1 2 3 4 5;5 4 3 2 1)"),

                ("adverb_scan_monad_fixedpoint_first.k", "((((0 1;2 3);(4 5;6 7));((0 1;2 3);(4 5;6 7)));((0 1;2 3);(4 5;6 7));(0 1;2 3);0 1;0)"),

                

                // Anonymous Function tests

                ("anonymous_function_double_param.k", "{[op1;op2]op1*op2}"),

                ("anonymous_function_empty.k", "{}"),

                ("anonymous_function_over_adverb.k", "0.01666667"),

                ("anonymous_function_scan_adverb.k", "(10;0.5;0.01666667)"),

                // Function Variable tests

                ("function_variable_over_adverb.k", "0.01666667"),

                ("function_variable_scan_adverb.k", "(10;0.5;0.01666667)"),

                // Projected Function tests

                ("test_projected_function.k", "%"),
                ("test_projected_function_monadic.k", "%:"),

                // Atom tests

                ("atom_scalar.k", "1"),

                ("atom_vector.k", "0"),
                
                // LRS Parser tests
                ("lrs_atomic_parser_basic.k", "1 2 3 4 5"),
                ("lrs_adverb_parser_each.k", "0.5 1.0 1.5"),
                ("lrs_adverb_parser_basic.k", "1 2 3 4 5"),
                ("lrs_expression_processor_test.k", "3"),
                ("lrs_parser_validation.k", "3"),

                

                // Attribute Handle tests

                ("attribute_handle_symbol.k", "`a."),

                ("attribute_handle_vector.k", "`a.`b.`c."),

                

                // Character tests

                ("character_single.k", "\"f\""),

                ("character_vector.k", "\"hello\""),

                

                // String Literal tests

                ("test_string_basic.k", "\"hello\""),

                ("test_symbol_quoted.k", "`hello"),

                ("test_character_single.k", "\"a\""),

                ("test_string_escape.k", "\"hello\\nworld\""),

                ("test_symbol_escape.k", "`\"hello\\nworld\""),

                

                // Complex Function tests

                ("complex_function.k", "205.0"),

                

                // Count operator

                ("count_operator.k", "3"),

                

                // Cut vector

                ("cut_vector.k", "(0 1;2 3;4 5 6 7)"),

                

                // Dictionary tests

                ("dictionary_empty.k", ".()"),

                ("dictionary_index.k", "1"),

                ("dictionary_index_attr.k", ".((`c;3;);(`d;4;))"),

                ("dictionary_index_value.k", "1"),

                ("dictionary_index_value2.k", "2"),

                ("dictionary_make_symbol_vector.k", ".,(`a;`b;)"),

                ("dictionary_multiple.k", ".((`a;1;);(`b;2;))"),

                ("dictionary_null_attributes.k", ".((`a;1;);(`b;2;))"),

                ("dictionary_single.k", ".,(`a;`b;)"),

                ("dictionary_type.k", "5"),

                ("dictionary_with_null_value.k", ".((`a;1;);(`b;;);(`c;3;))"),

                ("dictionary_period_index_all_attributes.k", "(.((`format;,`n;);(`name;`ID;));.((`format;,`c;);(`name;`Color;));.((`format;,`c;);(`name;`Retailer;)))"),

                ("test_minimal_dict.k", ",.,(`x;2;)"),

                ("test_simple_period.k", ",.((`x;2;);(`y;3;))"),

                ("test_attr_access.k", ".((`x;2;);(`y;3;))"),

                ("test_dict_create.k", ".((`a;1;);(`b;2;.((`x;2;);(`y;3;))))"),

                ("test_dict_with_attr.k", ".((`a;1;);(`b;2;.((`x;2;);(`y;3;))))"),

                ("test_show_dict.k", ".((`col01;11 12 13 14 15;.((`format;,`n;);(`name;`ID;)));(`col02;`yellow`white`blue`red`black;.((`format;,`c;);(`name;`Color;)));(`col03;(\"Home Depot\";\"Lowes\";\"Ace\";\"Neighborhood Paints\";\"Supply Co.\");.((`format;,`c;);(`name;`Retailer;))))"),

                ("test_simple_dict_create.k", ".,(`a;1;)"),

                ("test_specific_attr.k", ".((`format;,`n;);(`name;`ID;))"),

                ("test_specific_attr_fixed.k", ".((`format;,`n;);(`name;`ID;))"),

                

                // Empty value tests

                ("empty_char_vector.k", "\"\""),

                ("empty_float_vector_test.k", "0#0.0"),

                ("empty_symbol_atomic.k", "0#`"),

                ("empty_dictionary.k", ".()"),

                ("empty_list.k", "()"),

                ("test_symbol_take.k", "0#`"),

                ("test_symbol_parsing.k", "0#`"),

                

                // Drop tests

                ("drop_negative.k", "0 1 2 3"),

                ("drop_positive.k", "4 5 6 7"),

                

                // Empty mixed vector

                ("empty_mixed_vector.k", "()"),

                

                // Enlist operator

                ("enlist_operator.k", ",5"),

                

                // Enumerate tests

                ("enumerate_empty_int.k", "!0"),

                ("enumerate_operator.k", "0 1 2 3 4"),

                

                // Equal operator

                ("equal_operator.k", "0"),

                ("char_compare_equal.k", "1"),

                ("char_compare_different.k", "0"),

                ("char_vector_equal.k", "1 1 1"),

                ("char_vector_different.k", "1 1 0"),

                ("char_vector_match_equal.k", "1"),

                ("char_vector_match_different.k", "0"),

                ("float_infinity_match_equal.k", "1"),

                ("float_neg_infinity_match_equal.k", "1"),

                ("float_infinity_match_different.k", "0"),

                ("float_infinity_equal.k", "1"),

                ("float_neg_infinity_equal.k", "1"),

                ("float_infinity_equal_different.k", "0"),

                

                // First operator

                ("first_operator.k", "1"),

                

                // Float tests

                ("float_decimal_point.k", "10.0"),

                ("float_exponential.k", "170.0"),

                ("float_exponential_large.k", "1e+015"),

                ("float_exponential_small.k", "1e-020"),

                ("float_types.k", "3.14"),

                

                // Function tests

                ("function_add7.k", "12"),

                ("function_call_anonymous.k", "13"),

                ("function_call_chain.k", "20"),

                ("function_call_double.k", "32"),

                ("function_call_simple.k", "12"),

                ("function_foo_chain.k", "20"),

                ("function_mul.k", "32"),

                ("lambda_string_assign.k", "\"hello\""),

                ("lambda_string_literal.k", "\"hello\""),

                ("lambda_symbol_literal.k", "`abc"),

                ("named_function_over.k", "0.01666667"),

                ("named_function_scan.k", "(10;0.5;0.01666667)"),

                

                // Where operator

                ("where_generate_scalar.k", "0 0 0 0"),

                

                // Grade operators

                ("grade_down_operator.k", "1 2 3 4 0"),

                ("grade_up_operator.k", "0 4 2 3 1"),

                

                // Greater than operator

                ("greater_than_operator.k", "0"),

                

                // Integer types

                ("integer_types_int.k", "42"),

                ("integer_types_long.k", "123456789j"),

                

                // Join operator

                ("join_operator.k", "3 5"),

                ("join_chain_parens_string.k", "\"vartest:y\""),

                

                // Less than operator

                ("less_than_operator.k", "1"),

                

                // Math function tests

                ("math_abs.k", "5"),

                ("math_exp.k", "7.389056"),

                ("math_log.k", "2.302585"),

                ("math_sin.k", "0.0"),

                ("math_sqrt.k", "4.0"),

                ("math_vector.k", "0.841471 0.9092974 0.14112"),

                ("math_exp_basic.k", "2.718282"),

                ("math_floor_nan.k", "0N"),

                ("math_floor_negative_infinity.k", "-0I"),

                ("math_floor_special_values.k", "0I"),

                ("math_hyperbolic_basic.k", "1.175201"),

                ("math_inv_matrix_2x2.k", "(-2 1.0;1.5 -0.5)"),

                ("math_inv_matrix_3x3.k", "(-24.0 18.0 5.0;20.0 -15.0 -4.0;-5.0 4.0 1.0)"),

                ("math_inv_matrix_identity_3x3.k", "(1.0 0.0 0.0;0.0 1.0 0.0;0.0 0.0 1.0)"),

                ("math_log_negative.k", "0n"),

                ("math_log_zero.k", "-0i"),

                ("math_mul_matrix_2x2.k", "(19 22;43 50)"),

                ("math_mul_matrix_2x3_3x2.k", "(58 64;139 154)"),

                ("math_mul_matrix_3x3.k", "(30 24 18;84 69 54;138 114 90)"),

                ("math_mul_matrix_4x2_2x4.k", "(35 38 41 44;79 86 93 100;123 134 145 156;167 182 197 212)"),

                ("math_trig_basic.k", "0.0"),

                ("math_trig_pi.k", "-1.0"),

                

                // Maximum operator

                ("maximum_operator.k", "5"),

                

                // Minimum operator

                ("minimum_operator.k", "3"),

                

                // Mixed list with null

                ("mixed_list_with_null.k", "(1;;`test;42.5)"),

                

                // Mixed vector tests

                ("mixed_vector_empty_position.k", "(1;;2)"),

                ("mixed_vector_multiple_empty.k", "(1;;;3)"),

                ("mixed_vector_whitespace_position.k", "(1;;2)"),

                

                // Mod tests

                ("mod_integer.k", "1"),

                ("mod_rotate.k", "3 4 1 2"),

                ("mod_vector.k", "1 0 1 0"),

                ("modulus_operator.k", "1"),

                

                // Negate operator

                ("negate_operator.k", "1"),

                

                // Nested vector test

                ("nested_vector_test.k", "(1 2 3;4 5 6)"),

                

                // Overflow tests

                ("overflow_int_max_plus1.k", "0N"),

                ("overflow_int_neg_inf.k", "0N"),

                ("overflow_int_neg_inf_minus2.k", "0I"),

                ("overflow_int_null_minus1.k", "0I"),

                ("overflow_int_pos_inf.k", "0N"),

                ("overflow_int_pos_inf_plus2.k", "-0I"),

                ("overflow_long_max_plus1.k", "0Nj"),

                ("overflow_long_min_minus1.k", "0Nj"),

                ("overflow_long_neg_inf.k", "0Nj"),

                ("overflow_long_neg_inf_minus2.k", "0Ij"),

                ("overflow_long_pos_inf.k", "0Nj"),

                ("overflow_long_pos_inf_plus2.k", "-0Ij"),

                ("overflow_regular_int.k", "-2147483639"),

                ("underflow_regular_int.k", "2147483617"),

                

                // Parentheses tests

                ("parentheses_basic.k", "7"),

                ("parentheses_grouping.k", "9"),

                ("debug_mult_only.k", "21"),
                
                ("debug_no_outer_paren.k", "15"),
                
                ("debug_left_paren.k", "3"),
                
                ("debug_simple_mult.k", "21"),
                
                ("debug_double_nested.k", "21"),
                
                ("parentheses_nested.k", "7"),

                ("parentheses_nested1.k", "21"),

                ("parentheses_nested2.k", "0.7142857"),

                ("parentheses_nested3.k", "24"),

                ("parentheses_nested4.k", "15"),

                ("parentheses_nested5.k", "29"),

                ("parentheses_precedence.k", "7"),

                ("parenthesized_vector.k", "1 2 3 4"),

                // List null handling tests (Phase 1.7.2)
                ("list_null_consecutive_semicolons.k", "(1;;2)"),
                ("list_null_multiple_semicolons.k", "(;;)"),
                ("list_null_empty_parens.k", "()"), // Empty list (0 items), not null

                // Power operator

                ("power_operator.k", "8"),

                

                // Precedence tests

                ("precedence_chain1.k", "1410"),

                ("precedence_chain2.k", "0.07092199"),

                ("precedence_complex1.k", "0.7142857"),

                ("precedence_complex2.k", "10.01667"),

                ("precedence_mixed1.k", "0"),

                ("precedence_mixed2.k", "25"),

                ("precedence_mixed3.k", "11"),

                ("precedence_power1.k", "128"),

                ("precedence_power2.k", "83"),

                ("precedence_spec1.k", "11.57143"),

                ("precedence_spec2.k", "6.0"),

                

                // Reciprocal operator

                ("reciprocal_operator.k", "0.25"),

                

                // Reverse operator

                ("reverse_operator.k", "3 2 1"),

                

                // Scalar vector tests

                ("scalar_vector_addition.k", "4 5"),

                ("scalar_vector_multiplication.k", "3 6"),

                

                // Shape operator tests

                ("shape_operator.k", ",3"),

                ("shape_operator_empty_vector.k", ",0"),

                ("shape_operator_jagged.k", ",3"),

                

                // Dyadic operator tests - vector/atom combinations

                ("dyadic_plus_vector_vector.k", "5 7 9"),

                ("dyadic_plus_atom_vector.k", "6 7 8"),

                ("dyadic_plus_vector_atom.k", "6 7 8"),

                ("dyadic_minus_vector_vector.k", "4 4 4"),

                ("dyadic_minus_atom_vector.k", "9 8 7"),

                ("dyadic_minus_vector_atom.k", "3 4 5"),

                ("dyadic_times_vector_vector.k", "6 12 20"),

                ("dyadic_times_atom_vector.k", "3 6 9"),

                ("dyadic_times_vector_atom.k", "4 8 12"),

                ("dyadic_divide_vector_vector.k", "3 2 2"),

                ("dyadic_divide_atom_vector.k", "6.0 4.0 3.0"),

                ("dyadic_divide_vector_atom.k", "2 3 4"),

                ("dyadic_min_vector_vector.k", "2 8 3"),

                ("dyadic_min_atom_vector.k", "3 2 3"),

                ("dyadic_min_vector_atom.k", "4 2 4"),

                ("dyadic_max_vector_vector.k", "5 9 4"),

                ("dyadic_max_atom_vector.k", "6 8 6"),

                ("dyadic_max_vector_atom.k", "5 8 5"),

                ("dyadic_less_vector_vector.k", "1 0 1"),

                ("dyadic_less_atom_vector.k", "1 0 1"),

                ("dyadic_less_vector_atom.k", "1 0 1"),

                ("dyadic_more_vector_vector.k", "1 1 1"),

                ("dyadic_more_atom_vector.k", "1 0 1"),

                ("dyadic_more_vector_atom.k", "1 1 0"),

                ("dyadic_equal_vector_vector.k", "1 0 1"),

                ("dyadic_equal_atom_vector.k", "1 0 0"),

                ("dyadic_equal_vector_atom.k", "1 0 0"),

                ("dyadic_power_vector_vector.k", "8 9 4"),

                ("dyadic_power_atom_vector.k", "2 4 8"),

                ("dyadic_power_vector_atom.k", "4 9 16"),

                ("shape_operator_jagged_3d.k", "2 2"),

                ("shape_operator_jagged_matrix.k", ",3"),

                ("shape_operator_matrix.k", "3 3"),

                ("shape_operator_matrix_2x3.k", "2 3"),

                ("shape_operator_matrix_3x3.k", "3 3"),

                ("shape_operator_scalar.k", "!0"),

                ("shape_operator_tensor_2x2x3.k", "2 2 3"),

                ("shape_operator_tensor_3d.k", "3 2 2"),

                ("shape_operator_vector.k", ",5"),

                

                // Simple arithmetic tests

                ("simple_addition.k", "3"),

                ("divide_float.k", "0.6"),

                ("divide_integer.k", "2.5"),

                ("simple_multiplication.k", "12"),

                ("simple_nested_test.k", "1 2 3"),

                ("minus_integer.k", "2"),

                

                // Multi-arity and advanced adverb tests
                ("test_triadic_dot.k", "1 -2 3"),
                ("test_adverb_aware_evaluation.k", "6"),

                

                // Special values tests

                ("special_float_neg_inf.k", "-0i"),

                ("special_float_null.k", "0n"),

                ("special_float_pos_inf.k", "0i"),

                ("special_int_neg_inf.k", "-0I"),

                ("special_int_null.k", "0N"),

                ("special_int_pos_inf.k", "0I"),

                ("special_long_neg_inf.k", "-0Ij"),

                ("special_long_null.k", "0Nj"),

                ("special_long_pos_inf.k", "0Ij"),

                ("special_null.k", ""),

                

                // Additional special value arithmetic tests (separated from special_values_arithmetic.k)

                ("special_int_pos_inf_plus_1.k", "0N"),

                ("special_int_null_plus_1.k", "-0I"),

                ("special_int_neg_inf_plus_1.k", "-2147483646"),

                ("special_float_null_plus_1.k", "0n"),

                ("special_1_plus_int_pos_inf.k", "0N"),

                ("special_1_plus_int_null.k", "-0I"),

                ("special_int_vector.k", "0I 0N -0I"),

                ("special_float_vector.k", "0i 0n -0i"),

                

                // Square bracket tests

                ("square_bracket_function.k", "2.0"),

                ("square_bracket_vector_multiple.k", "14 16"),

                ("square_bracket_vector_single.k", "14"),

                ("bracket_sysverb_ic.k", "32 97 65 48"),

                ("bracket_sysverb_atan.k", "0.5235988"),

                

                // String representation tests

                ("string_representation_int.k", "\"42\""),

                ("string_representation_mixed.k", "\"(1;2.5;\\\"a\\\")\""),

                ("string_representation_symbol.k", "\"`symbol\""),

                ("string_representation_vector.k", "\"1 2 3\""),

                

                // Symbol tests

                ("symbol_quoted.k", "`\"a symbol\""),

                ("symbol_simple.k", "`foo"),

                ("symbol_vector_compact.k", "`a`b`c"),

                ("symbol_vector_spaces.k", "`a`b`c"),

                ("symbol_period_foo.k", "`foo."),

                ("symbol_period_foobar.k", "`foo.bar"),

                ("symbol_period_dotbar.k", "`.bar"),

                ("symbol_period_dotk.k", "`.k"),

                

                // Data I/O tests

                ("io_read_int.k", "123456"),

                ("io_read_float.k", "0i"),

                ("io_read_symbol.k", "`helllo"),

                ("io_read_intvec.k", "5 6 7"),

                ("io_read_debug.k", "123456"),

                ("io_write_int.k", ""),

                ("io_roundtrip.k", "(1;2.5;\"hello\")"),

                

                // Monadic 1: memory-mapped I/O tests

                ("io_monadic_1_int_vector.k", "5 6 7"),

                ("io_monadic_1_float_vector.k", "0i"),

                ("io_monadic_1_char_vector.k", "\"hello world\""),

                ("io_monadic_1_int_vector_index.k", "5"),

                ("io_monadic_1_int_vector_last_index.k", "7"),

                ("io_monadic_1_char_vector_index.k", "\"h\""),

                ("io_monadic_1_char_vector_last_index.k", "\"d\""),

                ("io_monadic_1_vs_2_int_vector.k", "1"),

                ("io_monadic_1_vs_2_float_vector.k", "1"),

                ("io_monadic_1_vs_2_char_vector.k", "1"),

                ("io_monadic_1_symbol_fallback.k", "`helllo"),

                ("match_simple_vectors.k", "1"),

                

                // Take operator tests

                ("take_operator_basic.k", "1 2 3"),

                ("take_operator_empty_float.k", "0#0.0"),

                ("take_operator_empty_symbol.k", "0#`"),

                ("take_operator_overflow.k", "1 2 3 1 2 3 1 2 3 1"),

                ("take_operator_scalar.k", "42 42 42"),



                // Reshape operator tests (vector left arg to #)

                ("reshape_basic.k", "(0 1 2 3;4 5 6 7;8 9 10 11)"),

                // Test division rules

                ("division_float_4_2.0.k", "2.0"),

                ("division_float_5_2.5.k", "2.0"),

                ("division_int_4_2.k", "2.0"),

                ("division_int_5_2.k", "2.5"),

                ("division_rules_10_3.k", "3.333333"),

                ("division_rules_12_4.k", "3.0"),

                ("division_rules_4_2.k", "2.0"),

                ("division_rules_5_2.k", "2.5"),

                

                // Test enumerate

                ("enumerate.k", "0 1"),

                

                // Test grade operators

                ("grade_down_no_parens.k", "1 2 3 4 0"),

                ("grade_up_no_parens.k", "0 4 2 3 1"),

                

                // Test mixed types

                ("mixed_types.k", "(42;3.14;\"hello\";`symbol)"),

                

                // Test multiline function

                ("multiline_function_single.k", "20"),

                

                // Test null vector

                ("null_vector.k", "(;1;2)"),

                

                // Test scoping

                ("scoping_single.k", "60"),

                

                // Test semicolon tests

                ("semicolon_simple.k", "(7;11;-20.45)"),

                ("semicolon_vars.k", "30 200 -10"),

                ("semicolon_vector.k", "(7;3 4;-20.45)"),

                ("test_semicolon.k", "3 4"),

                

                // Test simple scalar div

                ("simple_scalar_div.k", "2.5"),

                

                // Test single no semicolon

                ("single_no_semicolon.k", "42"),

                

                // Test smart division

                ("smart_division1.k", "2.5 5.0"),

                ("smart_division2.k", "2 4"),

                ("smart_division3.k", "2 4 6"),

                

                // Test special values

                ("special_0i_plus_1.k", "0N"),

                ("special_0n_plus_1.k", "-0I"),

                ("special_1_plus_neg0i.k", "-2147483646"),

                ("special_neg0i_plus_1.k", "-2147483646"),

                ("special_underflow.k", "2147483622"),

                ("special_underflow_2.k", "2147483549"),

                ("special_underflow_3.k", "2147482649"),

                

                // Test type operators

                ("type_char.k", "3"),

                ("type_float.k", "2"),

                ("type_null.k", "6"),

                ("type_space.k", "3"),

                ("type_symbol.k", "4"),

                ("type_vector.k", "-1"),

                

                // Test vector

                ("vector.k", "1 2 3"),

                

                // Type operator tests

                ("type_operator_char.k", "3"),

                ("type_operator_float.k", "2"),

                ("type_operator_null.k", "6"),

                ("type_operator_symbol.k", "4"),

                ("type_operator_vector_char.k", "-3"),

                ("type_operator_vector_float.k", "-2"),

                ("type_operator_vector_int.k", "-1"),

                ("type_operator_vector_mixed.k", "0"),

                ("type_operator_vector_symbol.k", "-3"),

                

                // Type promotion tests

                ("type_promotion_float_int.k", "3.5"),

                ("type_promotion_float_long.k", "2.5"),

                ("type_promotion_int_float.k", "3.5"),

                ("type_promotion_int_long.k", "3j"),

                ("type_promotion_long_float.k", "2.5"),

                ("type_promotion_long_int.k", "3j"),

                

                // Unary minus operator

                ("unary_minus_operator.k", "-5"),

                

                // Unique operator

                ("unique_operator.k", "1 2 3"),

                

                // Variable tests

                ("amend_item_simple_no_semicolon.k", "1 12 3"),

                ("variable_assignment.k", ""),

                ("variable_reassignment.k", "7.2 4.5"),

                ("variable_scoping_global_access.k", "150"),

                ("variable_scoping_global_assignment.k", "30"),

                ("variable_scoping_global_unchanged.k", "100"),

                ("variable_scoping_local_hiding.k", "60"),

                ("variable_scoping_nested_functions.k", "140"),

                ("variable_usage.k", "30"),

                ("dot_execute.k", "4"),

                ("dot_execute_context.k", "8"),

                ("dictionary_enumerate.k", "`a`b"),

                

                // New spec features

                ("null_operations.k", "7"),

                ("dictionary_dot_apply.k", "1"),

                

                // $ operator tests - monadic format

                ("monadic_format_basic.k", ",\"1\""),

                ("monadic_format_types.k", "\"42.5\""),

                ("monadic_format_vector.k", "(,\"1\";,\"2\";,\"3\")"),

                ("monadic_format_string_hello.k", "\"hello\""),

                ("monadic_format_string_a.k", ",\"a\""),

                ("monadic_format_symbol_hello.k", "\"hello\""),

                ("monadic_format_symbol_simple.k", "\"test\""),

                ("monadic_format_dictionary.k", "\".((`a;1;);(`b;2;);(`c;3;))\""),

                ("monadic_format_nested_list.k", "((,\"1\";,\"2\";,\"3\");(,\"4\";,\"5\";,\"6\"))"),

                ("monadic_format_integer.k", "\"42\""),

                ("monadic_format_float.k", "\"3.14\""),

                ("monadic_format_vector_simple.k", "(,\"1\";,\"2\";,\"3\")"),

                

                // $ operator tests - binary form/type conversion

                ("format_integer.k", "\"\""),

                ("ci_adverb_vector.k", "\"a^P\""),

                ("adverb_each_count.k", "3 5 2"),

                ("format_float_numeric.k", ",\"1\""),

                ("form_long.k", "42j"),

                ("format_numeric.k", "\"    1\""),

                ("form_string_pad_left.k", "\"  hello\""),

                ("format_symbol_pad_left.k", "\"     hello\""),

                ("format_symbol_pad_left_8.k", "\"   hello\""),

                ("format_pad_left.k", "\"   42\""),

                ("format_pad_right.k", "\"42   \""),

                ("format_float_width_precision.k", "\"      3.14\""),

                ("format_float_precision.k", "\"    3.14\""),

                

                // Additional format tests

                ("format_0_1.k", "\"\""),

                ("format_1_1.k", ",\"1\""),

                ("format_symbol_string_mixed_vector.k", "`hello`world`test"),

                ("form_integer_charvector.k", "42"),

                ("form_character_charvector.k", "\"aaa\""),

                ("dot_execute_variables.k", "0.6"),

                ("format_braces_expressions.k", "8 15 2"),

                ("format_braces_nested_expr.k", "(12 20;(7;3.333333))"),

                ("format_braces_complex.k", "8.25 12.0 9.0"),

                ("format_braces_string.k", "(\"John\";\"is\";25;\"years old\")"),

                ("format_braces_mixed_type.k", "(42;\"hello\";`test;47;\"helloworld\")"),

                ("format_braces_simple.k", "8"),

                ("format_braces_arith.k", "(8;15;2;12;20;3.333333)"),

                ("format_braces_nested_arith.k", "(7 10;(2;2.5);5 -1)"),

                ("format_braces_float.k", "4.0 3.75 0.6 -1.0"),

                ("format_braces_mixed_arith.k", "17.5 32.5 4.5"),

                ("format_braces_example.k", "8 9"),

                ("format_braces_function_calls.k", "5 20 12"),

                ("format_braces_nested_function_calls.k", "5 8 25"),

                

                // Test underscore functions

                ("log.k", "2.302585"),

                ("time_t.k", ".((`type;1;);(`shape;!0;))"),

                ("rand_draw_select.k", ".((`type;-1;);(`shape;,10;))"),

                ("rand_draw_deal.k", ".((`type;-1;);(`shape;,4;);(`allitemsunique;1;))"),

                ("rand_draw_probability.k", ".((`type;-2;);(`shape;,10;))"),

                ("rand_draw_vector_select.k", ".((`type;0;);(`shape;2 3;))"),

                ("rand_draw_vector_deal.k", ".((`type;0;);(`shape;2 3;);(`allitemsunique;1;))"),

                ("rand_draw_vector_probability.k", ".((`type;0;);(`shape;2 3;))"),

                ("time_gtime.k", "20350101 0"),

                ("time_lt.k", "-18000"),

                ("time_jd.k", "-3251"),

                ("time_dj.k", "20350101"),

                ("time_ltime.k", "20341231 190000"),

                ("in.k", "1"),

                ("assignment_lrs_return_value.k", "47 94"),

                

                // List operations tests

                ("list_dv_basic.k", "3 5"),

                ("list_dv_nomatch.k", "3 4 4 5"),

                ("list_di_basic.k", "3 4 5"),

                ("list_di_multiple.k", "3 4"),

                ("list_sv_base10.k", "1995"),

                ("list_sv_base2.k", "9"),

                ("list_sv_mixed.k", "1995"),

                

                // Environment and file system tests

                ("list_getenv.k", "\"$P$G\""), // PROMPT environment variable

                ("list_setenv.k", ""), // _setenv returns null (nothing) per spec

                ("list_size_existing.k", "11264.0"), // Test with existing project file using absolute path

                ("test_ci_basic.k", "\"A\""),

                ("test_ci_vector.k", "\"ABC\""),

                ("test_ic_basic.k", "65"),

                ("test_vs_dyadic.k", "1 9 9 5"),

                ("list_vs_vector_right.k", "(0 0 0 0 1 1 1 1;0 0 1 1 0 0 1 1;0 1 0 1 0 1 0 1)"),

                ("test_ic_vector.k", "65 66 67"),

                ("test_monadic_colon.k", "42"),

                ("test_sm_basic.k", "1"),

                ("test_sm_simple.k", "1"),

                ("test_ss_basic.k", ",6"),

                

                // Statement parsing tests
                ("statement_assignment_basic.k", ""),
                ("statement_assignment_inline.k", "43"),
                ("statement_conditional_basic.k", "2"),
                ("statement_do_basic.k", "3"),
                ("statement_do_simple.k", ""),
                ("semicolon_vars_test.k", ""),
                ("apply_and_assign_simple.k", "1"),
                ("apply_and_assign_multiline.k", "1"),
                //("apply_and_assign_debug.k", "3"),

                ("io_read_basic.k", "(\"line1\";\"line2\";\"line3\")"),

                ("io_write_basic.k", "(\"line1\";\"line2\";\"line3\")"),

                // New I/O verb tests for 5: and 6: operations

                ("io_append_simple.k", ""),

                ("io_append_basic.k", ""),

                ("io_append_multiple.k", ""),

                ("io_read_bytes_basic.k", "\"\\357\\273\\277hello\\r\\n\""), // \357\273\277 is the BOM

                ("io_read_bytes_empty.k", "\"\\357\\273\\277\""), // \357\273\277 is the BOM

                ("io_write_bytes_basic.k", ""),

                ("io_write_bytes_overwrite.k", ""),

                ("io_write_bytes_binary.k", ""),

                                

                // New search function tests

                ("search_in_basic.k", "1"),

                ("search_in_notfound.k", "0"),

                ("search_bin_basic.k", "1"),

                ("search_binl_eachleft.k", "0 1 1 2 2"),

                ("search_lin_intersection.k", "1 1 1 0 0"),

                // Amend Item tests - only valid cases with 3+ arguments

                ("amend_item_basic.k", "1 12 3"),

                ("amend_item_multiple.k", "11 2 13"),

                ("amend_item_monadic.k", "1 4 3"),

                ("amend_item_symbol_path.k", "1 12 3 4 5"),

                // Amend dot tests
                ("amend_dot_symbol_path.k", "1 12 3 4 5"),

                ("amend_dot_empty_index.k", "11 12 13"),

                ("amend_dot_null_index_each.k", "11 12 13"),

                // Existing amend tests - only valid cases with 3+ arguments

                ("amend_test.k", "(1 2 13 4 5;6 7 8 9 10)"),

                // Function projection with blank arguments
                ("function_projection_basic.k", "6"),

                // Statement-form amend: v[i]+:y
                ("statement_amend_index_assign.k", "(1 2 3;104 5 106)"),

                // Find operator tests

                ("find_basic.k", "2"),

                ("find_notfound.k", "7"),

                // Form specifiers on mixed vectors

                ("format_float_precision_vector_simple.k", "(\"       1.5\";\"       2.5\")"),

                ("format_float_precision_mixed_vector.k", "(\"   1.50\";\"   2.70\";\"   3.14\";\"   4.20\")"),

                ("format_pad_mixed_vector.k", "(\"         1\";\"         2\";\"         3\")"),

                ("format_pad_negative_mixed_vector.k", "(\"1         \";\"2         \";\"3         \")"),

                

// Vector notation tests

("vector_notation_empty.k", "()"),

("vector_notation_functions.k", "10 20 30"),

("vector_notation_mixed_types.k", "(42;3.14;\"hello\";`symbol)"),

("vector_notation_nested.k", "3 7 11"),

("vector_notation_semicolon.k", "(7;11;-20.45)"),

("vector_notation_single_group.k", "42"),

("vector_notation_space.k", "1 2 3 4 5"),

("vector_notation_variables.k", "30 200 -10"),

                

                // Vector operations

                ("vector_addition.k", "4 6"),

                ("vector_division.k", "0.3333333 0.5"),

                ("vector_index_duplicate.k", "5 5"),

                ("vector_index_first.k", "5"),

                ("vector_index_multiple.k", "8 9"),

                ("vector_index_reverse.k", "9 8"),

                ("vector_index_single.k", "4"),

                ("vector_multiplication.k", "3 8"),

                ("vector_subtraction.k", "-2 -2"),

                ("vector_with_null.k", "(;1;2)"),

                ("vector_with_null_middle.k", "(1;;3)"),

                

                // Where operator

                ("where_operator.k", "0 2 3"),

                ("where_monadic_lin_dyadic.k", "0 2 5 7"),

                ("where_vector_counts.k", "0 0 0 1 1 2"),

                

                // Floor operator

                ("floor_operator.k", "3"),

                

                // Missing adverb tests

                ("adverb_backslash_colon_basic.k", "(5 6 7;6 7 8;7 8 9)"),

                ("adverb_slash_colon_basic.k", "(5 6 7;6 7 8;7 8 9)"),

                ("adverb_tick_colon_basic.k", "4 1 3 8"),

                // Empty bracket index tests
                ("empty_brackets_vector.k", "1 2 3 4"),

                ("empty_brackets_dictionary.k", "1 2"),

                

                // Missing amend tests

                ("amend_apply.k", "(1 2 13 4 5;6 7 8 9 10)"),

                ("amend_dot_test.k", "1 12 3"),

                ("amend_parenthesized.k", "11 2 3"),

                ("amend_test_anonymous_func.k", "11 2 3"),

                

                // More missing tests

                ("amend_test_func_var.k", "11 2 3"),

                // Error trap tests
                ("trap_dot_add.k", "(0;4 6)"),
                ("trap_dot_success.k", "(0;1 3)"),
                ("amend_colon_direct.k", "1 99 3"),
                ("amend_triadic_monadic.k", "1 -2 3"),

                ("conditional_bracket_test.k", "\"true\""),

                ("conditional_false.k", "\"false\""),

                ("conditional_simple_test.k", "\"true\""),

                ("conditional_true.k", "\"true\""),

                ("dictionary_null_index.k", "1 2"),

                ("dictionary_unmake.k", "((`a;1;);(`b;2;))"),

                ("do_bracket_test.k", ""),

                ("do_loop.k", ""),

                ("do_simple.k", ""),

                

                // Dyadic bracket tests

                ("dyadic_divide_bracket.k", "5.0"),

                ("dyadic_minus_bracket.k", "7"),

                ("dyadic_multiply_bracket.k", "24"),

                ("dyadic_plus_bracket.k", "8"),

                

                // Dyadic dot-apply tests

                ("dyadic_divide_dot_apply.k", "5.0"),

                ("dyadic_minus_dot_apply.k", "7"),

                ("dyadic_multiply_dot_apply.k", "24"),

                ("dyadic_plus_dot_apply.k", "8"),

                

                

                // Format tests

                ("format_braces_complex_expressions.k", "14 20 10"),

                ("format_float_precision_complex_mixed.k", "(\"     1.234\";\"     2.567\";\"     3.890\";\"     4.123\")"),

                ("format_float_vector.k", "(,\"1\";,\"2\";,\"3\";\"42\")"),

                ("format_int_vector.k", "(\"\";\"\";\"\";\"\")"),

                ("form_0_string.k", "123"),

                ("form_0_vector.k", "123 456"),

                ("form_0_float_string.k", "3.14"),

                ("form_0_float_vector.k", "3.14 1e+048 1.4e-027"),

                ("form_symbol_string.k", "`abc"),

                ("form_symbol_vector.k", "`abc`de`f"),

                ("form_braces_string_new.k", "{y*z+x}"),

                ("form_braces_complex_new.k", "({y*z+x};{[t;a;v;s]s+(v*t)+.5*a*t*t})"),
                ("format_string_pad_left.k", "\"     hello\""),

                ("format_string_pad_right.k", "\"test      \""),

                ("format_vector_int.k", "(\"\";\"\";\"\")"),

                ("group_operator.k", "(0 1 6;2 7 16;3 5 12 15 17;,4;8 9;,10;,11;,13;14 18;,19)"),

                ("if_bracket_test.k", ""),

                ("if_simple_test.k", ""),

                ("if_true.k", ""),

                ("in_basic.k", "1"),

                ("in_notfound.k", "0"),

                

                // Final remaining tests

                ("in_simple.k", "0"),

                ("isolated.k", "0.6"),

                ("modulo.k", "0.6"),

                ("monadic_format_mixed_vector.k", "(,\"1\";\"2.5\";\"hello\";\"symbol\")"),

                ("over_plus_empty.k", "0"),

                ("simple_division.k", "4.0"),

                ("simple_subtraction.k", "2"),

                ("string_parse.k", "30"),

                

                // K Tree tests - Following One Test Per File principle

                ("k_tree_assignment_absolute_foo.k", ""), // Absolute path assignment to foo

                ("k_tree_retrieve_absolute_foo.k", "42"),  // Absolute path retrieval from foo

                ("k_tree_retrieval_relative.k", "42"),         // Relative path retrieval only

                ("k_tree_enumerate.k", "`k`t"),     // Root enumeration - compact symbol vector format

                ("k_tree_current_branch.k", "`.k"),           // Current branch command - returns K tree branch name

                ("k_tree_dictionary_indexing.k", "42"),       // Dictionary indexing

                ("k_tree_nested_indexing.k", "2"),          // Nested indexing

                ("k_tree_verify_root.k", ""),               // Root verification - null displays as empty string

                ("k_tree_flip_dictionary.k", ".((`a;1;);(`b;2;);(`c;3;))"), // Test flip + make dictionary - matches k.exe

                

                // Proper single-test files following BEST.md principles

                ("k_tree_null_to_dict_conversion.k", ".,(`foo;42;)"), // Test .k converts from null to dict (single-item dict list)

                ("k_tree_dictionary_assignment.k", ".((`a;1;);(`b;2;);(`c;3;))"), // Test dictionary assignment in K tree (triplets format)

                ("k_tree_test_bracket_indexing.k", "2"),   // Test bracket indexing with regular dictionary

                ("k_tree_flip_test.k", "((`a;1);(`b;2);(`c;3))"), // Test flip operation - matches k.exe

                

                // Final remaining tests

                ("vector_null_index.k", "1 2 3 4"),

                ("while_bracket_test.k", ""),

                ("while_safe_test.k", ""),

                ("while_simple_test.k", ""),

                

                // K Serialization tests (based on actual implementation output)

                ("serialization_bd_db_integer.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000*\\000\\000\\000\""),

                ("serialization_bd_db_float.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000n\\206\\033\\360\\371!\\t@\""),

                ("serialization_bd_db_character.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000a\\000\\000\\000\""),

                ("serialization_bd_db_symbol.k", "\"\\001\\000\\000\\000\\013\\000\\000\\000\\004\\000\\000\\000symbol\\000\""),

                ("serialization_bd_db_null.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_db_integervector.k", "\"\\001\\000\\000\\000\\024\\000\\000\\000\\377\\377\\377\\377\\003\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\003\\000\\000\\000\""),

                ("serialization_bd_db_floatvector.k", "\"\\001\\000\\000\\000 \\000\\000\\000\\376\\377\\377\\377\\003\\000\\000\\000\\232\\231\\231\\231\\231\\231\\361?\\232\\231\\231\\231\\231\\231\\001@ffffff\\n@\""),

                ("serialization_bd_db_charactervector.k", "\"\\001\\000\\000\\000\\016\\000\\000\\000\\375\\377\\377\\377\\005\\000\\000\\000hello\\000\""),

                ("serialization_bd_db_symbolvector.k", "\"\\001\\000\\000\\000\\016\\000\\000\\000\\374\\377\\377\\377\\003\\000\\000\\000a\\000b\\000c\\000\""),

                ("serialization_bd_db_list.k", "\"\\001\\000\\000\\000(\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\001\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\004@\\003\\000\\000\\000a\\000\\000\\000\""),

                ("serialization_bd_db_dictionary.k", "\"\\001\\000\\000\\000H\\000\\000\\000\\005\\000\\000\\000\\002\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000a\\000\\000\\000\\004\\000\\000\\0001\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000b\\000\\000\\000\\004\\000\\000\\0002\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_db_anonymousfunction.k", "\"\\001\\000\\000\\000\\016\\000\\000\\000\\n\\000\\000\\000\\000{[x]x+1}\\000\""),

                ("serialization_bd_db_roundtrip_integer.k", "42"),

                ("serialization_bd_db_roundtrip_list.k", "(1;2.5;\"a\")"),

                ("serialization_bd_ic_symbol.k", "1 0 0 0 6 0 0 0 4 0 0 0 65 0"),

                

                // Comprehensive _db deserialization tests - Edge cases and examples from SerializationExplorer

                ("db_basic_integer.k", "42"),

                ("db_float.k", "3.14"),

                ("db_symbol.k", "`test"),

                ("db_int_vector.k", "1 2 3"),

                ("db_symbol_vector.k", "`a`b`c"),

                ("db_char_vector.k", "\"hello\""),

                ("db_list_simple.k", "1 2 3"),

                ("db_dict_simple.k", ".((`a;1;);(`b;2;);(`c;3;))"),

                ("db_function_simple.k", "{+}"),

                ("db_function_params.k", "{x+y}"),

                ("db_null.k", "0N"),

                ("db_empty_list.k", "()"),

                ("db_character.k", "\"a\""),

                ("db_float_simple.k", "1.5"),

                ("db_int_vector_long.k", "1 2 3 4 5"),

                ("db_float_vector.k", "1.1 2.2 3.3"),

                ("db_char_vector_sentence.k", "\"hello world\""),

                ("db_symbol_simple.k", "`hello"),

                ("db_list_longer.k", "1 2 3 4 5"),

                ("db_list_mixed_types.k", "(1;`test;3.14;\"hello\")"),

                ("db_function_complex.k", "{[x;y]x*y+z}"),

                ("db_function_simple_math.k", "{x+y}"),

                ("db_nested_dict_vectors.k", ".((`a;1 2 3;);(`b;`hello`world`test;))"),

                ("db_nested_lists.k", "(1 2 3;`hello`world`test;4.5 6.7)"),

                ("db_mixed_list.k", "(1;`test;3.14)"),

                ("db_dict_single_entry.k", ".,(`a;1;)"),

                ("db_dict_symbol_2char.k", ".,(`ab;1 2;)"),

                ("db_dict_symbol_8char.k", ".,(`abcdefgh;1 2 3 4 5 6 7 8;)"),

                ("db_dict_multi_entry.k", ".((`a;1;);(`b;2;))"),

                ("db_dict_five_entries.k", ".((`a;1;);(`b;2;);(`c;3;);(`d;4;);(`e;5;))"),

                ("db_dict_complex_attributes.k", ".((`col01;11 12 13 14 15;.((`format;,`n;);(`name;`ID;)));(`col02;`yellow`white`blue`red`black;.((`format;,`c;);(`name;`Color;)));(`col03;(\"Home Depot\";\"Lowes\";\"Ace\";\"Neighborhood Paints\";\"Supply Co.\");.((`format;,`c;);(`name;`Retailer;))))"),

                ("db_dict_empty.k", ".()"),

                ("db_dict_with_null_attrs.k", ".,(`a;1;.())"),

                ("db_dict_with_empty_attrs.k", ".((`a;1;);(`b;2;.()))"),

                ("db_enlist_single_int.k", ",5"),

                ("db_enlist_single_symbol.k", ",`test"),

                ("db_enlist_single_string.k", ",\"hello\""),

                ("db_enlist_vector.k", ",1 2 3"),

                

                // Comprehensive _bd serialization tests - Edge cases and random examples

                ("serialization_bd_null_edge_0.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_integer_edge_0.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_integer_edge_1.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\001\\000\\000\\000\""),

                ("serialization_bd_integer_edge_-1.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\377\\377\\377\\377\""),

                ("serialization_bd_integer_edge_2147483647.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\377\\377\\377\\177\""),

                ("serialization_bd_integer_edge_-2147483648.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\001\\000\\000\\200\""),

                ("serialization_bd_integer_edge_0N.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\200\""),

                ("serialization_bd_integer_edge_0I.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\377\\377\\377\\177\""),

                ("serialization_bd_integer_edge_-0I.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\001\\000\\000\\000\\001\\000\\000\\200\""),

                ("serialization_bd_float_edge_0.0.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_float_edge_1.0.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\360?\""),

                ("serialization_bd_float_edge_-1.0.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\360\\277\""),

                ("serialization_bd_float_edge_0.5.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\340?\""),

                ("serialization_bd_float_edge_-0.5.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\340\\277\""),

                ("serialization_bd_float_edge_0n.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\370\\377\""),

                ("serialization_bd_float_edge_0i.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\360\\177\""),

                ("serialization_bd_float_edge_-0i.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\360\\377\""),

                ("serialization_bd_symbol_edge_a.k", "\"\\001\\000\\000\\000\\006\\000\\000\\000\\004\\000\\000\\000a\\000\""),

                ("serialization_bd_symbol_edge_symbol.k", "\"\\001\\000\\000\\000\\013\\000\\000\\000\\004\\000\\000\\000symbol\\000\""),

                ("serialization_bd_symbol_edge_test123.k", "\"\\001\\000\\000\\000\\014\\000\\000\\000\\004\\000\\000\\000test123\\000\""),

                ("serialization_bd_symbol_edge_underscore.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\004\\000\\000\\000_underscore\\000\""),

                ("serialization_bd_symbol_edge_hello.k", "\"\\001\\000\\000\\000\\n\\000\\000\\000\\004\\000\\000\\000hello\\000\""),

                ("serialization_bd_symbol_edge_newline_tab.k", "\"\\001\\000\\000\\000\\007\\000\\000\\000\\004\\000\\000\\000\\n\\t\\000\""),

                ("serialization_bd_symbol_edge_001.k", "\"\\001\\000\\000\\000\\006\\000\\000\\000\\004\\000\\000\\000\\001\\000\""),

                ("serialization_bd_symbol_edge_empty.k", "\"\\001\\000\\000\\000\\005\\000\\000\\000\\004\\000\\000\\000\\000\""),

                ("serialization_bd_character_edge_a.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000a\\000\\000\\000\""),

                ("serialization_bd_character_edge_b.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000b\\000\\000\\000\""),

                ("serialization_bd_character_edge_z.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000z\\000\\000\\000\""),

                ("serialization_bd_character_edge_A_upper.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000A\\000\\000\\000\""),

                ("serialization_bd_character_edge_Z_upper.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000Z\\000\\000\\000\""),

                ("serialization_bd_character_edge_0.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\0000\\000\\000\\000\""),

                ("serialization_bd_character_edge_9.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\0009\\000\\000\\000\""),

                ("serialization_bd_character_edge_space.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000 \\000\\000\\000\""),

                ("serialization_bd_character_edge_newline.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000\\n\\000\\000\\000\""),

                ("serialization_bd_character_edge_tab.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000\\t\\000\\000\\000\""),

                ("serialization_bd_character_edge_carriage.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000\\r\\000\\000\\000\""),

                ("serialization_bd_character_edge_null.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_character_edge_001.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000\\001\\000\\000\\000\""),

                ("serialization_bd_character_edge_377.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000\\377\\000\\000\\000\""),

                ("serialization_bd_character_edge_backspace.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000\\b\\000\\000\\000\""),

                ("serialization_bd_charactervector_edge_empty.k", "\"\\001\\000\\000\\000\\t\\000\\000\\000\\375\\377\\377\\377\\000\\000\\000\\000\\000\""),

                ("serialization_bd_charactervector_edge_a.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\003\\000\\000\\000a\\000\\000\\000\""),

                ("serialization_bd_charactervector_edge_hello.k", "\"\\001\\000\\000\\000\\016\\000\\000\\000\\375\\377\\377\\377\\005\\000\\000\\000hello\\000\""),

                ("serialization_bd_charactervector_edge_whitespace.k", "\"\\001\\000\\000\\000\\014\\000\\000\\000\\375\\377\\377\\377\\003\\000\\000\\000\\n\\t\\r\\000\""),

                ("serialization_bd_integervector_edge_empty.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\377\\377\\377\\377\\000\\000\\000\\000\""),

                ("serialization_bd_integervector_edge_single.k", "\"\\001\\000\\000\\000\\014\\000\\000\\000\\377\\377\\377\\377\\001\\000\\000\\000\\001\\000\\000\\000\""),

                ("serialization_bd_integervector_edge_123.k", "\"\\001\\000\\000\\000\\024\\000\\000\\000\\377\\377\\377\\377\\003\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\003\\000\\000\\000\""),

                ("serialization_bd_integervector_edge_special.k", "\"\\001\\000\\000\\000\\024\\000\\000\\000\\377\\377\\377\\377\\003\\000\\000\\000\\000\\000\\000\\200\\377\\377\\377\\177\\001\\000\\000\\200\""),

                ("serialization_bd_list_edge_empty.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_list_edge_null.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\000\\000\\000\\000\\001\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_list_edge_mixed.k", "\"\\001\\000\\000\\000(\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\001\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\000\\000\\004@\\003\\000\\000\\000a\\000\\000\\000\""),

                ("serialization_bd_list_edge_complex.k", "\"\\001\\000\\000\\0000\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\\004\\000\\000\\000symbol\\000\\000\\000\\000\\000\\000\\n\\000\\000\\000\\000{[]}\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_list_edge_nested.k", "\"\\001\\000\\000\\000(\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\377\\377\\377\\377\\002\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\377\\377\\377\\377\\002\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000\""),

                ("serialization_bd_list_edge_dicts.k", "\"\\001\\000\\000\\000X\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\005\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000a\\000\\000\\000\\001\\000\\000\\000\\001\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\\005\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000b\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_anonymousfunction_random_1.k", "\"\\001\\000\\000\\000\\027\\000\\000\\000\\n\\000\\000\\000\\000{[x]x+7;x$1;x<=2}\\000\""),

                ("serialization_bd_anonymousfunction_random_2.k", "\"\\001\\000\\000\\000\\021\\000\\000\\000\\n\\000\\000\\000\\000{[]0|4;0&3}\\000\""),

                ("serialization_bd_anonymousfunction_random_3.k", "\"\\001\\000\\000\\000\\023\\000\\000\\000\\n\\000\\000\\000.k\\000{[xyz]xy|3}\\000\""),

                ("serialization_bd_floatvector_random_1.k", "\"\\001\\000\\000\\000(\\000\\000\\000\\376\\377\\377\\377\\004\\000\\000\\000sj\\325/\\312\\006\\bAl\\315\\227\\234\\217\\353\\023\\301\\026\\270Yp\\027/\\n\\301Z`\\257\\331\\350\\303\\306@\""),

                ("serialization_bd_floatvector_random_2.k", "\"\\001\\000\\000\\000\\020\\000\\000\\000\\002\\000\\000\\000\\001\\000\\000\\000:\\321\\254\\250b*\\002A\""),

                ("serialization_bd_floatvector_random_3.k", "\"\\001\\000\\000\\000(\\000\\000\\000\\376\\377\\377\\377\\004\\000\\000\\0002\\377\\004(g\\334!A\\253p.\\342\\211b!\\301\\317\\3376\\002\\n*\\342@zIO\\0230;\\030A\""),

                ("serialization_bd_symbolvector_random_1.k", "\"\\001\\000\\000\\000&\\000\\000\\000\\374\\377\\377\\377\\006\\000\\000\\000qzUM7\\000g8X6P\\000iay\\000KgNQ5i\\000< +\\000b5\\000\""),

                ("serialization_bd_symbolvector_random_2.k", "\"\\001\\000\\000\\0001\\000\\000\\000\\374\\377\\377\\377\\n\\000\\000\\000O 0\\000D\\000qCBI1b\\000*H \\000SS\\000ULsyI\\000F~\\000C\\000Mont\\000O25B\\000\""),

                ("serialization_bd_symbolvector_random_3.k", "\"\\001\\000\\000\\000!\\000\\000\\000\\374\\377\\377\\377\\006\\000\\000\\000o3\\000EE5ijP\\000trD0LuE\\000OW\\000.\\000y\\000\""),

                

                // Symbol tests

                ("test_quoted_symbol.k", "`\".\""),

                ("test_quoted_symbol_serialization.k", "\"\\001\\000\\000\\000\\006\\000\\000\\000\\004\\000\\000\\000.\\000\""),

                ("test_simple_symbol.k", "`a`\".\"`b"),

                ("test_single_quoted_symbol.k", "`\".\""),

                ("test_symbol_vector_with_quoted.k", "`a`b`\".\"`c"),

                

                // New tests for dictionary/list serialization patterns

                ("serialization_bd_dictionary_with_symbol_vectors.k",

                "\"\\001\\000\\000\\000p\\000\\000\\000\\005\\000\\000\\000\\002\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000colA\\000\\000\\000\\000\\000\\000\\000\\000\\374\\377\\377\\377\\003\\000\\000\\000a\\000b\\000c\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000colB\\000\\000\\000\\000\\000\\000\\000\\000\\374\\377\\377\\377\\003\\000\\000\\000dd\\000eee\\000ffff\\000\\000\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_dictionary_with_vectors.k","\"\\001\\000\\000\\000x\\000\\000\\000\\005\\000\\000\\000\\002\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000col1\\000\\000\\000\\000\\000\\000\\000\\000\\377\\377\\377\\377\\004\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000col2\\000\\000\\000\\000\\000\\000\\000\\000\\377\\377\\377\\377\\004\\000\\000\\000\\005\\000\\000\\000\\006\\000\\000\\000\\007\\000\\000\\000\\b\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_list_with_explicit_nulls.k", "\"\\001\\000\\000\\000H\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000a\\000\\000\\000\\004\\000\\000\\0001\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000b\\000\\000\\000\\004\\000\\000\\0002\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("serialization_bd_list_with_vectors.k", "\"\\001\\000\\000\\000h\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\004\\000\\000\\000col1\\000\\000\\000\\000\\000\\000\\000\\000\\377\\377\\377\\377\\004\\000\\000\\000\\001\\000\\000\\000\\002\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\004\\000\\000\\000col2\\000\\000\\000\\000\\000\\000\\000\\000\\377\\377\\377\\377\\004\\000\\000\\000\\005\\000\\000\\000\\006\\000\\000\\000\\007\\000\\000\\000\\b\\000\\000\\000\""),

                ("serialization_bd_list_with_symbol_vectors.k", "\"\\001\\000\\000\\000`\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\004\\000\\000\\000colA\\000\\000\\000\\000\\000\\000\\000\\000\\374\\377\\377\\377\\003\\000\\000\\000a\\000b\\000c\\000\\000\\000\\000\\000\\000\\000\\002\\000\\000\\000\\004\\000\\000\\000colB\\000\\000\\000\\000\\000\\000\\000\\000\\374\\377\\377\\377\\003\\000\\000\\000dd\\000eee\\000ffff\\000\\000\\000\\000\\000\""),

                

                // Missing tests added from validation

                ("bd_dict_single_entry.k", "\"\\001\\000\\000\\000(\\000\\000\\000\\005\\000\\000\\000\\001\\000\\000\\000\\000\\000\\000\\000\\003\\000\\000\\000\\004\\000\\000\\000a\\000\\000\\000\\001\\000\\000\\000\\001\\000\\000\\000\\006\\000\\000\\000\\000\\000\\000\\000\""),

                ("db_dict_larger.k", ".((`a;1;);(`b;2;);(`c;3;);(`d;4;))"),

                ("db_dict_mixed_types.k", ".((`key1;`value1;);(`key2;42;);(`key3;3.14;))"),

                ("db_float_vector_longer.k", "1.1 2.2 3.3 4.4 5.5"),

                ("db_int_vector_longer.k", "1 2 3 4 5 6 7 8 9 10"),

                ("db_nested_structures.k", ".((`a;1 2 3;);(`b;4 5 6;))"),

                ("db_string_hello.k", "\"hello\""),

                ("db_symbol_hello.k", "`hello"),

                ("db_symbol_vector_longer.k", "`hello`world`test"),

                ("test_dict_larger.k", ".((`a;1;);(`b;2;);(`c;3;);(`d;4;))"),

                ("test_dict_simple.k", ".((`a;1;);(`b;2;);(`c;3;))"),

                ("symbol_special_chars.k", "`\"hello-world!\""),

                ("type_empty_int_vector.k", "-1"),

                ("bd_empty_list.k", "\"\\001\\000\\000\\000\\b\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\\000\""),

                ("bd_enlist_single_int.k", "\"\\001\\000\\000\\000\\014\\000\\000\\000\\377\\377\\377\\377\\001\\000\\000\\000\\005\\000\\000\\000\""),

                ("bd_enlist_single_string.k", "\"\\001\\000\\000\\000\\030\\000\\000\\000\\000\\000\\000\\000\\001\\000\\000\\000\\375\\377\\377\\377\\005\\000\\000\\000hello\\000\\000\\000\""),

                ("bd_symbol_vector_longer.k", "\"\\001\\000\\000\\000\\031\\000\\000\\000\\374\\377\\377\\377\\003\\000\\000\\000hello\\000world\\000test\\000\""),

                ("bd_enlist_single_symbol.k", "\"\\001\\000\\000\\000\\r\\000\\000\\000\\374\\377\\377\\377\\001\\000\\000\\000test\\000\""),

                

                // Math function tests

                ("math_and_basic.k", "1"),

                ("math_and_vector.k", "1 2 3"),

                ("math_ceil_basic.k", "5.0"),

                ("math_ceil_integer.k", "5.0"),

                ("math_ceil_negative.k", "-3.0"),

                ("math_ceil_vector.k", "2.0 3.0 4.0"),

                ("math_div_float.k", "3"),

                ("math_div_integer.k", "2"),

                ("math_div_vector.k", "2 4 7"),

                ("math_dot_basic.k", "32"),

                ("math_dot_matrix_matrix.k", "47 71 99"),

                ("math_dot_matrix_2x2.k", "26 44"),

                ("math_dot_matrix_each_right.k", "(47 64 81;52 71 90;57 78 99)"),

                ("math_dot_vector_each_left.k", "(8 10;16 20)"),

                ("adverb_complex_vector_each_right.k", "(11 13 15;14 16 18;17 19 21)"),

                ("adverb_complex_matrix_each_right.k", "((11 12 13;14 15 16;17 18 19);(12 13 14;15 16 17;18 19 20);(13 14 15;16 17 18;19 20 21))"),

                ("adverb_nesting_join_each_left.k", "((1 2 3 9 8 7;1 2 3 6 5 4;1 2 3 3 2 1);(4 5 6 9 8 7;4 5 6 6 5 4;4 5 6 3 2 1);(7 8 9 9 8 7;7 8 9 6 5 4;7 8 9 3 2 1))"),

                ("adverb_complex_string_each_left.k", "(((\"hello\";\"world.\";\" \");(\"hello\";\"world.\";\" \"));((\"It's\";\"me\";\"ksharp.\";\" \");(\"It's\";\"me\";\"ksharp.\";\" \"));((\"Have\";\"fun\";\"with\";\"me!\";\" \");(\"Have\";\"fun\";\"with\";\"me!\";\" \")))"),

                ("join_each_left.k", "(1 4 5 6;2 4 5 6;3 4 5 6)"),

                ("test_nested_adverb.k", "((1 4;1 5;1 6);(2 4;2 5;2 6);(3 4;3 5;3 6))"),

                ("math_lsq_non_square.k", "-1.0 2.0"),

                ("math_lsq_high_rank.k", "-8.0 9.0"),

                ("math_lsq_complex.k", "0.1232877 1.506849"),

                ("math_lsq_regression.k", "0.5 0.6428571"),

                ("math_mul_basic.k", "1 2 3"),

                ("math_not_basic.k", "-6"),

                ("math_not_vector.k", "1 2 3"),

                ("math_or_basic.k", "7"),

                ("math_or_vector.k", "1 2 3"),

                ("math_rot_basic.k", "32"),

                ("math_shift_basic.k", "32"),

                ("math_shift_vector.k", "32 64 128 256"),

                ("math_xor_basic.k", "6"),

                ("math_xor_vector.k", "1 2 3"),

                

                // FFI (Foreign Function Interface) tests

                //("ffi_hint_system.k", "`int"),

                ("ffi_simple_assembly.k", "`._dotnet.System.String"),

                ("ffi_assembly_load.k", "`._dotnet.System.String"),

                ("ffi_type_marshalling_float.k", "`float"),
                ("ffi_type_marshalling_string.k", "`string"),
                ("ffi_type_marshalling_list.k", "`list"),



                //("ffi_object_management.k", "\"HELLO\""),



("ffi_constructor.k", ".((`real;2.0;);(`imag;3.0;);(`instance;1;);(`type;1;))"),



                ("ffi_dispose.k", "`Disposed"),



                ("ffi_complete_workflow.k", ".((`real;2.0;);(`imag;3.0;);(`magnitude;3.605551;);(`conj_real;2.0;);(`conj_imag;-3.0;);(`instance_more_than_5_methods;1;);(`type_more_than_20_methods;1;))"),




                // Idioms Chapter 01: Direct Application of Verbs

                ("idioms_01_575_kronecker_delta.k", "1 0 0 1"),

                ("idioms_01_571_xbutnoty.k", "0 1 0 0"),

                ("idioms_01_570_implies.k", "1 0 1 1"),

                ("idioms_01_573_exclusive_or.k", "0 1 1 0"),

                ("idioms_01_41_indices_ones.k", "2 4 8"),

                ("idioms_01_516_multiply_columns.k", "(10 20 30 40 50 60;700 800 900 1000 1100 1200)"),

                ("idioms_01_566_zero_boolean.k", "0 0 0 0 0 0 0 0 0 0 0"),

                ("idioms_01_624_zero_array.k", "(0 0 0;0 0 0)"),

                ("idioms_01_622_retain_marked.k", "3 0 15 1 0"),

                ("idioms_01_331_identity_max.k", "-1e+100"),

                ("idioms_01_337_identity_min.k", "1e+100"),

                ("idioms_01_357_match.k", "1"),

                ("idioms_01_328_number_items.k", "4"),

                ("idioms_01_411_number_rows.k", "2"),

                ("idioms_01_445_number_columns.k", "3"),
                
                // Parse tree verb tests
                ("test_parse_verb.k", "(`\"+\";,1;,2)"),
                ("test_eval_verb.k", "3"),
                ("test_parse_eval_together.k", "3"),
                ("idioms_01_388_drop_rows.k", "(6 7 8;9 10 11;12 13 14;15 16 17)"),

                ("idioms_01_154_range.k", "\"wirls\""),

                ("idioms_01_70_remove_duplicates.k", "(\"to\";\"be\";\"or\";\"not\")"),

                ("idioms_01_143_indices_distinct.k", "(0 3 7;1 4 6;2 5)"),

                ("idioms_01_228_is_row.k", "1"),

                ("idioms_01_232_is_row_in.k", "1"),

                ("idioms_01_559_first_marker.k", "2"),

                ("idioms_01_78_eval_number.k", "1998 51"),

                ("idioms_01_88_name_variable.k", "(0 1 2;3 4 5)"),

                ("idioms_01_96_conditional_execution.k", "0 15"),

                ("idioms_01_493_choose_boolean.k", "\"xyz\""),

                ("idioms_01_434_replace_first.k", "\"tbbccdefcdab\""),

                ("idioms_01_433_replace_last.k", "\"abbccdefcdat\""),

                ("idioms_01_406_add_last.k", "1 2 3 4 105"),

                ("idioms_01_449_limit_between.k", "(58 30 37 70 39 70;60 30 45 70 70 35;49 70 70 70 30 30;46 61 30 51 30 34;31 51 30 35 30 70)"),

                ("idioms_01_495_indices_occurrences.k", "0 2 5 7"),

                ("idioms_01_504_replace_satisfying.k", "\" bcd f  i \""),

                ("idioms_01_569_change_to_one.k", "10 1 7 1 1"),

                ("idioms_01_556_all_indices.k", "0 1 2 3"),

                ("idioms_01_535_avoid_parentheses.k", "5 1"),

                ("idioms_01_591_reshape_2column.k", "(\"ab\";\"cd\";\"ef\";\"gh\")"),

                ("idioms_01_595_one_row_matrix.k", ",2 3 5 7 11"),

                ("idioms_01_616_scalar_from_vector.k", "8"),

                ("idioms_01_509_remove_y.k", "\"bcdebc\""),

                ("idioms_01_510_remove_blanks.k", "\"bcdebc\""),

                ("idioms_01_496_remove_punctuation.k", "\"oh no stop it you will\""),

                ("idioms_01_177_string_search.k", "11 20 32"),

                ("idioms_01_45_binary_representation.k", "1 0 0 0 0"),

                ("idioms_01_84_scalar_boolean.k", "157"),

                ("idioms_01_129_arctangent.k", "0.5235988"),

                ("idioms_01_561_numeric_code.k", "32 97 65 48"),

                ("idioms_01_241_sum_subsets.k", "(4 6 4;12 14 12;20 22 20)"),

                ("idioms_01_61_cyclic_counter.k", "1 2 3 4 5 6 7 8 1 2"),

                ("idioms_01_384_drop_1st_postpend.k", "4 5 6 0"),

                ("idioms_01_385_drop_last_prepend.k", "0 3 4 5"),

                ("idioms_01_178_first_occurrence.k", "12"),

                ("idioms_01_447_conditional_drop.k", "(0 1 2;3 4 5;6 7 8;9 10 11)"),

                ("idioms_01_448_conditional_drop_last.k", "((0 1 2;3 4 5;6 7 8;9 10 11);(0 1 2;3 4 5;6 7 8))"),

                ("idioms_01_549_alphabetic_comparison.k", "1"),

                // Chapter 2 idioms: Extending verbs with adverbs

                ("idioms_02_335_maximum.k", "7"),
                ("idioms_02_339_minimum.k", "2"),
                ("idioms_02_356_any.k", "1"),
                ("idioms_02_360_all.k", "0"),
                ("idioms_02_355_none.k", "1"),
                ("idioms_02_334_nonneg_max.k", "0"),
                ("idioms_02_222_max_weighted.k", "9"),
                ("idioms_02_223_min_weighted.k", "5"),
                ("idioms_02_368_product.k", "120"),
                ("idioms_02_374_sum.k", "15"),
                ("idioms_02_370_count_ones.k", "4"),
                ("idioms_02_362_count_occur.k", "3"),
                ("idioms_02_239_sum_recip.k", "39"),
                ("division_integer_zero_by_zero.k", "0"),
                ("division_long_zero_by_zero.k", "0j"),
                ("division_float_zero_by_zero.k", "0.0"),
                ("division_mixed_int_float_zero_by_zero.k", "0.0"),
                ("division_mixed_float_int_zero_by_zero.k", "0.0"),
                ("division_mixed_long_float_zero_by_zero.k", "0.0"),
                ("division_mixed_float_long_zero_by_zero.k", "0.0"),
                ("division_vector_zero_by_zero_normal.k", "(10.0;0;10.0)"),
                ("division_vector_zero_by_zero_large_positive.k", "(1e+308;0i;10)"),
                ("division_vector_zero_by_zero_large_negative.k", "(-1e+308;-0i;10)"),
                ("idioms_02_242_sum_squares.k", "55"),
                ("idioms_02_243_dot_product.k", "550"),
                ("idioms_02_372_sum_columns.k", "15 18 21 24"),
                ("idioms_02_361_parity.k", "0 1 1 0 1 0 0 1"),
                ("idioms_02_310_running_sum.k", "1 21 321 4321"),
                ("idioms_02_285_moving_sum.k", "6 9 12"),
                ("idioms_02_309_running_parity.k", "0 1 0 1 0 0 1 1 1"),
                ("idioms_02_306_invert_after_0.k", "1 1 0 0 0 0 0"),
                ("idioms_02_189_add_each_row.k", "(3 5 7 9;7 9 11 13;11 13 15 17)"),
                ("idioms_02_192_add_each_col.k", "(4 5 6 7 8;10 11 12 13 14)"),
                ("idioms_02_273_join_scalar_each.k", "(\"a0\";\"a1\";\"a2\";\"a3\";\"a4\")"),
                ("idioms_02_282_index_first_blank.k", "2"),
                ("idioms_02_344_pairwise_match.k", "1 0 1 0"),
                ("idioms_02_371_scalar_from_1list.k", "5"),
                ("idioms_02_373_sum_rows.k", "10 26 42"),
                ("idioms_02_383_pairwise_diff.k", "6 -2 3 2"),
                ("idioms_02_398_diag_from_cols.k", "(1 2 3 4 5;10 6 7 8 9;14 15 11 12 13;18 19 20 16 17;22 23 24 25 21)"),
                ("idioms_02_399_cols_from_diag.k", "(1 2 3 4 5;6 7 8 9 10;11 12 13 14 15;16 17 18 19 20;21 22 23 24 25)"),
                ("idioms_02_419_pairwise_ratios.k", "5.0 5.0 2.0"),
                ("idioms_02_431_differ_from_next.k", "1 0 1 0 0 1 1 1 1 1 1"),
                ("idioms_02_432_differ_from_prev.k", "1 1 0 1 0 0 1 1 1 1 1"),
                ("idioms_02_442_first_diff.k", "1 2 3 4 5"),
                ("idioms_02_484_right_left_scan.k", "15 14 12 9 5"),
                ("idioms_02_511_apply_over_all.k", "300"),
                ("idioms_02_098_exec_rows.k", "4 9"),
                ("idioms_02_518_cond_transpose.k", "(0 3;1 4;2 5)"),
                ("idioms_02_533_cond_reverse.k", "5 4 3 2 1"),
                ("idioms_02_562_index_y_in_x.k", "6 1 4 5 4 0 8 5 1 4"),
                ("idioms_02_589_2col_matrix.k", "(\"ae\";\"bf\";\"cg\";\"dh\")"),
                ("idioms_02_592_vector_from_array.k", "0 1 2 3 4 5 6 7"),
                ("idioms_02_611_mult_rows_by_vec.k", "(1 20 300 40000;5 60 700 80000;9 100 1100 120000)"),
                ("idioms_02_615_first_atom.k", "0"),
                ("idioms_02_249_offset_enum.k", "10 11 12"),
                ("idioms_02_236_count_occur_matrix.k", "2"),

                // KTree (K Tree namespace) tests

                ("ktree_enumerate_relative_name.k", "`keyA`keyB"),

                ("ktree_enumerate_relative_path.k", "`keyA`keyB"),

                ("ktree_enumerate_absolute_path.k", "`keyA`keyB"),

                ("ktree_enumerate_root.k", "`k`t"),

                ("ktree_indexing_relative_name.k", "1 3 5"),

                ("ktree_indexing_absolute_name.k", "1 2 3"),

                ("ktree_indexing_relative_path.k", "1 2 3"),

                ("ktree_indexing_absolute_path.k", "1 3 5"),

                ("ktree_dot_apply_relative_name.k", "1 2 3"),

                ("ktree_dot_apply_absolute_name.k", "1 3 5"),

                ("ktree_dot_apply_relative_path.k", "1 2 3"),

                ("ktree_dot_apply_absolute_path.k", "1 3 5"),
                ("test_semicolon_parsing.k", "1 2 3"),
                ("test_parse_monadic_star.k", $"(`\"*:\";1 2 3 4)"),
                ("parse_atomic_value_no_verb.k", ",`a"),
                ("parse_projection_dyadic_plus.k", "(`\"+\";::;::)"),
                ("parse_projection_dyadic_plus_fixed_left.k", "(`\"+\";,1;::)"),
                ("parse_projection_dyadic_plus_fixed_right.k", "(`\"+\";::;,2)"),
                ("parse_monadic_shape_atomic.k", "(`\"^:\";(`\",:\";,`a))"),
                ("parse_apply_and_assign.k", "(`\"+:\";`i;1)"),
                ("eval_dyadic_plus.k", "6 8 10 12"),
                ("eval_monadic_star_nested.k", "22"),
                ("eval_dot_execute_path.k", "`a`b`c`d`e`f"),
                ("eval_dot_repl_dir.k", "``.k"),
                ("eval_dot_parse_and_eval.k", "11"),
                ("test_eval_monadic_star.k", "1"),
                ("test_eval_monadic_star_atomic.k", "1"),
            };

            // Filter tests if a pattern was provided

            var tests = filter != null
                ? allTests.Where(t => t.Item1.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray()
                : allTests;

            if (filter != null)
            {
                Console.WriteLine($"Filter: '{filter}' — matched {tests.Length} of {allTests.Length} tests");
                if (tests.Length == 0)
                {
                    Console.WriteLine("No tests matched the filter.");

                }

                Console.WriteLine("=========================");

            }



            var testResults = new List<TestResult>();

            var testScriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "TestScripts");



            // Validate test count vs actual test files (skip when filtering)

            if (filter == null)

            {

                var actualTestFiles = Directory.GetFiles(testScriptsPath, "*.k", SearchOption.AllDirectories)

                    .Select(Path.GetFileName)

                    .OrderBy(f => f)

                    .ToList();



                var expectedTestFiles = allTests.Select(t => t.Item1).ToList();

                // Check for duplicates in expected test files
                var duplicateTestFiles = expectedTestFiles
                    .GroupBy(f => f)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                var missingFromRunner = actualTestFiles.Except(expectedTestFiles).ToList();
                var extraInRunner = expectedTestFiles.Except(actualTestFiles).ToList();



                Console.WriteLine($"Test File Validation:");

                Console.WriteLine($"  Expected tests: {allTests.Length}");

                Console.WriteLine($"  Actual .k files: {actualTestFiles.Count}");

                // Report duplicates if found
                if (duplicateTestFiles.Any())
                {
                    Console.WriteLine($"  ❌ DUPLICATE TEST FILES ({duplicateTestFiles.Count}):");
                    foreach (var duplicate in duplicateTestFiles.Take(10))
                    {
                        Console.WriteLine($"    - {duplicate}");
                    }
                    if (duplicateTestFiles.Count > 10)
                    {
                        Console.WriteLine($"    ... and {duplicateTestFiles.Count - 10} more");
                    }
                }

                if (missingFromRunner.Any())

                {

                    Console.WriteLine($"  ❌ MISSING FROM RUNNER ({missingFromRunner.Count}):");

                    foreach (var missing in missingFromRunner.Take(10))

                    {

                        Console.WriteLine($"    - {missing}");

                    }

                    if (missingFromRunner.Count > 10)

                    {

                        Console.WriteLine($"    ... and {missingFromRunner.Count - 10} more");

                    }

                    Console.WriteLine();

                    Console.WriteLine("ERROR: Test files exist but are not included in test runner!");

                    Console.WriteLine("Please add missing test cases to the test runner or remove duplicate files.");

                    return;

                }



                if (extraInRunner.Any())

                {

                    Console.WriteLine($"  ⚠️  EXTRA IN RUNNER ({extraInRunner.Count}):");

                    foreach (var extra in extraInRunner)

                    {

                        Console.WriteLine($"    - {extra}");

                    }

                    Console.WriteLine();

                }



                if (missingFromRunner.Count == 0 && extraInRunner.Count == 0)

                {

                    Console.WriteLine($"  ✅ Test counts match perfectly!");

                }

                Console.WriteLine("=========================");

            }



            Console.WriteLine($"Running K3CSharp Tests...");

            Console.WriteLine($"Total tests: {tests.Length}");

            Console.WriteLine("=========================");



            foreach (var (fileName, expected) in tests)

            {

                try

                {
                    var scriptPath = Path.Combine(testScriptsPath, fileName);

                    if (!File.Exists(scriptPath))

                    {

                        Console.WriteLine($"✗ {fileName}: File not found");

                        testResults.Add(new TestResult { FileName = fileName, ActualOutput = "File not found", Expected = expected, Passed = false });

                        continue;

                    }

                    var script = File.ReadAllText(scriptPath);

                    // Trim trailing whitespace and empty lines as per K specification
                    // When evaluating whole file, empty lines at end should be trimmed
                    // Also strip comments (/) which can be at start of line or after space
                    var lines = script
                        .Split('\n')
                        .Select(line => {
                            var trimmed = line.Trim();
                            // Strip comments: / at start or space followed by /
                            var commentIndex = trimmed.StartsWith("/") ? 0 : trimmed.IndexOf(" /");
                            if (commentIndex >= 0)
                            {
                                trimmed = trimmed.Substring(0, commentIndex).Trim();
                            }
                            return trimmed;
                        })
                        .Where(line => !string.IsNullOrEmpty(line))
                        .ToArray();



                    var evaluator = new Evaluator();



                    // Reset K tree before each test to ensure isolation

                    evaluator.ResetKTree();



                    K3Value? lastResult = null;

                    string accumulatedLine = "";



                    // Process each line in the script

                    foreach (var line in lines)

                    {

                        var trimmedLine = line.Trim();

                        if (string.IsNullOrEmpty(trimmedLine)) continue;

                        // Skip comment-only lines (lines starting with /)
                        if (trimmedLine.StartsWith("/")) continue;



                        // Handle REPL commands (starting with \)

                        if (string.IsNullOrEmpty(accumulatedLine) && trimmedLine.StartsWith("\\"))

                        {

                            // Handle REPL command directly

                            var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);



                            switch (parts[0])

                            {

                                case "\\r":

                                    // Handle random seed get/set

                                    if (parts.Length == 1)

                                    {

                                        // Display current random seed (no output for test)

                                    }

                                    else if (parts.Length == 2)

                                    {

                                        // Set random seed

                                        if (int.TryParse(parts[1], out int newSeed))

                                        {

                                            Evaluator.RandomSeed = newSeed;

                                        }

                                    }

                                    break;

                                default:

                                    // Ignore other REPL commands for now

                                    break;

                            }

                        }

                        else

                        {

                            // Accumulate lines for multiline expressions (unbalanced braces/brackets/parens)

                            if (string.IsNullOrEmpty(accumulatedLine))

                            {

                                accumulatedLine = trimmedLine;

                            }

                            else

                            {

                                accumulatedLine += "\n" + trimmedLine;

                            }



                            // Check if expression is complete (all delimiters balanced) using source-level scan.
                            // This handles unterminated string literals without throwing a Lexer exception.
                            bool isIncomplete = ParserConfig.IsSourceIncomplete(accumulatedLine);

                            if (isIncomplete)
                            {
                                // Expression is incomplete - continue accumulating
                                continue;
                            }



                            // Handle regular K expressions
                            var lexer = new Lexer(accumulatedLine);
                            var tokens = lexer.Tokenize();

                            // Set current test name for failure tracking
                            LRSParserWrapper.SetCurrentTestName(fileName);

                            // Use LRS parser for all tests (now the default)
                            ASTNode? ast = ParserConfig.ParseWithConfig(tokens, accumulatedLine);

                            // Clear current test name after parsing
                            LRSParserWrapper.ClearCurrentTestName();

                            lastResult = evaluator.Evaluate(ast);

                            accumulatedLine = "";

                        }

                    }



                    var actualOutput = (lastResult ?? new NullValue()).ToString().Trim();

                    var passed = actualOutput == expected;



                    if (passed)

                    {

                        Console.WriteLine($"✓ {fileName}: {actualOutput}");

                    }

                    else

                    {

                        Console.WriteLine($"✗ {fileName}: Expected '{expected}', got '{actualOutput}'");

                    }



                    testResults.Add(new TestResult { FileName = fileName, ActualOutput = actualOutput, Expected = expected, Passed = passed });

                }

                catch (Exception ex)

                {

                    Console.WriteLine($"✗ {fileName}: Error - {ex.Message}");

                    testResults.Add(new TestResult { FileName = fileName, ActualOutput = $"Error: {ex.Message}", Expected = expected, Passed = false });

                }

            }



            var passedCount = testResults.Count(t => t.Passed);

            var totalCount = testResults.Count;



            Console.WriteLine();

            Console.WriteLine($"Test Results: {passedCount}/{totalCount} passed ({(passedCount * 100.0 / totalCount):F1}%)");



            WriteResultsTable(testResults);

            // Generate parser analysis report if enabled
            try
            {
                var reportGenerator = new ParserReportGenerator();
                reportGenerator.GenerateReport(testResults);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to generate parser report: {ex.Message}");
            }

        }

    }

}

