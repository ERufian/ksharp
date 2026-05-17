// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;

namespace K3CSharp
{
    public enum ValueType
    {
        Integer,
        Long,
        Float,
        Character,
        Symbol,
        Vector,
        Function,
        Null,
        Dictionary,
        List
    }

    public abstract class K3Value
    {
        public ValueType Type { get; protected set; }
        public SymbolValue? Hint { get; set; }

        // Note: Arithmetic operations are handled by the Evaluator to maintain
        // proper separation of concerns. The Evaluator uses VerbRegistry for
        // dispatch and handles type promotion, vector operations, etc.
        public abstract override string ToString();
    }

    public class IntegerValue : K3Value
    {
        public int Value { get; }
        public bool IsSpecial { get; }
        public string SpecialName { get; }

        public IntegerValue(int value, SymbolValue? hint = null)
        {
            Value = value;
            Type = ValueType.Integer;
            Hint = hint;
            
            // Check if this value matches any special integer patterns
            if (value == int.MaxValue)
            {
                IsSpecial = true;
                SpecialName = "0I";
            }
            else if (value == int.MinValue)
            {
                IsSpecial = true;
                SpecialName = "0N";
            }
            else if (value == int.MinValue + 1)
            {
                IsSpecial = true;
                SpecialName = "-0I";
            }
            else
            {
                IsSpecial = false;
                SpecialName = "";
            }
        }

        public IntegerValue(string specialName)
        {
            SpecialName = specialName;
            IsSpecial = true;
            Type = ValueType.Integer;
            
            // Set the actual integer values for special cases
            switch (specialName)
            {
                case "0I": Value = int.MaxValue; break;
                case "0N": Value = int.MinValue; break;
                case "-0I": Value = int.MinValue + 1; break;
                default: throw new ArgumentException($"Unknown special integer: {specialName}");
            }
        }

        public override string ToString()
        {
            if (IsSpecial)
                return SpecialName;
            return Value.ToString();
        }
    }

    public class LongValue : K3Value
    {
        public long Value { get; }

        public LongValue(long value, SymbolValue? hint = null)
        {
            Value = value;
            Type = ValueType.Long;
            Hint = hint;
        }

        public override string ToString()
        {
            // Handle special display cases
            if (Value == long.MaxValue)
                return "0Ij";
            else if (Value == -long.MaxValue)
                return "-0Ij";
            else if (Value == long.MinValue)
                return "0Nj";
            
            return Value.ToString() + "j";
        }
    }

    public class FloatValue : K3Value
    {
        public double Value { get; }
        public bool IsSpecial { get; }
        public string? SpecialName { get; }
        public bool HasZeroFractionalPart { get; }

        public FloatValue(double value, SymbolValue? hint = null)
        {
            Value = value;
            Type = ValueType.Float;
            Hint = hint;
            IsSpecial = false;
            HasZeroFractionalPart = (Math.Abs(value % 1) < 1e-10); // Use more reasonable epsilon
            
            // Check if this value should be treated as special
            if (double.IsNaN(value))
            {
                IsSpecial = true;
                SpecialName = "0n";
            }
            else if (double.IsPositiveInfinity(value))
            {
                IsSpecial = true;
                SpecialName = "0i";
            }
            else if (double.IsNegativeInfinity(value))
            {
                IsSpecial = true;
                SpecialName = "-0i";
            }
            // Detect signed zero
            else if (value == 0.0 && double.IsNegative(value))
            {
                IsSpecial = true;
                SpecialName = "-0.0";
            }
        }

        public FloatValue(string specialName)
        {
            SpecialName = specialName;
            IsSpecial = true;
            Type = ValueType.Float;
            HasZeroFractionalPart = false; // Special values don't have fractional parts
            
            // Set the actual double values for special cases
            switch (specialName)
            {
                case "0i": Value = double.PositiveInfinity; break;
                case "0n": Value = double.NaN; break;
                case "-0i": Value = double.NegativeInfinity; break;
                default: throw new ArgumentException($"Unknown special float: {specialName}");
            }
        }

        public override string ToString()
        {
            if (IsSpecial)
                return SpecialName ?? "";
            
            // Use exponential notation for very large or very small numbers
            var absValue = Math.Abs(Value);
            if (absValue >= 1e15 || (absValue > 0 && absValue < 1e-10))
            {
                var expFormat = $"E{Evaluator.floatPrecision}";
                var formatted = Value.ToString(expFormat);
                // Convert to lowercase 'e'
                formatted = formatted.Replace('E', 'e');
                
                // Handle trailing zeroes in exponential notation
                var eIndex = formatted.IndexOf('e');
                if (eIndex > 0)
                {
                    var mantissa = formatted.Substring(0, eIndex);
                    var exponent = formatted.Substring(eIndex);
                    
                    // Remove trailing zeroes from mantissa
                    if (mantissa.Contains('.'))
                    {
                        mantissa = mantissa.TrimEnd('0');
                        // If decimal portion is zero, remove decimal point
                        if (mantissa.EndsWith('.'))
                        {
                            mantissa = mantissa.TrimEnd('.');
                        }
                    }
                    
                    formatted = mantissa + exponent;
                }
                
                return formatted;
            }
            
            // Use significant digits precision for regular floating point numbers
            if (Value != 0)
            {
                // Use G format with significant digits, then ensure consistent decimal notation
                var precision = Evaluator.floatPrecision;
                var formatted = Math.Round(Value, precision, MidpointRounding.AwayFromZero).ToString("G15");
                
                // Convert to scientific notation if it's too long or has too many decimal places
                if (formatted.Contains('E') || formatted.Length > 15)
                {
                    var expFormat = $"E{precision}";
                    formatted = Value.ToString(expFormat);
                    // Convert to lowercase 'e'
                    formatted = formatted.Replace('E', 'e');
                    
                    // Handle trailing zeroes in exponential notation
                    var eIndex = formatted.IndexOf('e');
                    if (eIndex > 0)
                    {
                        var mantissa = formatted.Substring(0, eIndex);
                        var exponent = formatted.Substring(eIndex);
                        
                        // Remove trailing zeroes from mantissa
                        if (mantissa.Contains('.'))
                        {
                            mantissa = mantissa.TrimEnd('0');
                            // If decimal portion is zero, remove decimal point
                            if (mantissa.EndsWith('.'))
                            {
                                mantissa = mantissa.TrimEnd('.');
                            }
                        }
                        
                        formatted = mantissa + exponent;
                    }
                    
                    return formatted;
                }
                
                // Ensure we have the right number of significant digits
                var significantDigits = CountSignificantDigits(Value);
                if (significantDigits > precision)
                {
                    // Round to the correct number of significant digits
                    var scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(Value))) - precision + 1);
                    var rounded = Math.Round(Value / scale) * scale;
                    formatted = rounded.ToString("G15");
                }
                
                // Handle decimal notation trailing zeroes
                if (formatted.Contains('.'))
                {
                    var decimalIndex = formatted.IndexOf('.');
                    var integerPart = formatted.Substring(0, decimalIndex);
                    var decimalPart = formatted.Substring(decimalIndex + 1);
                    
                    // Remove trailing zeroes from decimal part
                    decimalPart = decimalPart.TrimEnd('0');
                    
                    // If all decimal digits were zeroes, preserve one zero
                    if (decimalPart.Length == 0)
                    {
                        decimalPart = "0";
                    }
                    
                    // Reconstruct
                    formatted = integerPart + "." + decimalPart;
                    
                    // If this float was originally created with zero fractional part, preserve the .0
                    if (HasZeroFractionalPart && decimalPart == "0")
                    {
                        formatted = integerPart + ".0";
                    }
                }
                
                // Ensure decimal notation for display consistency
                if (formatted.Contains('.') || formatted.Contains('e'))
                {
                    return formatted;
                }
                else
                {
                    // Add .0 for whole numbers that were originally floats
                    if (HasZeroFractionalPart)
                    {
                        return formatted + ".0";
                    }
                    return formatted;
                }
            }
            
            return "0.0";
        }
        
        private static int CountSignificantDigits(double value)
        {
            if (value == 0) return 1;
            
            var absValue = Math.Abs(value);
            var log10 = Math.Log10(absValue);
            var integerDigits = (int)Math.Floor(log10) + 1;
            
            // Count digits in string representation, excluding decimal point and leading zeros
            var str = absValue.ToString("G15");
            var count = 0;
            var seenNonZero = false;
            
            foreach (char c in str)
            {
                if (c == '.' || c == 'E' || c == '-') continue;
                if (c != '0') seenNonZero = true;
                if (seenNonZero) count++;
            }
            
            return count;
        }
    }

}