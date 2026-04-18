using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using K3CSharp.Parsing;

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

        public abstract K3Value Add(K3Value other);
        public abstract K3Value Subtract(K3Value other);
        public abstract K3Value Multiply(K3Value other);
        public abstract K3Value Divide(K3Value other);
        public abstract override string ToString();
        
        /// <summary>
        /// Determines the result of 0%0 division in a vector context.
        /// Returns infinity (0i/-0i) if adjacent elements would produce infinity,
        /// otherwise returns 0/0.0 based on the operand types.
        /// </summary>
        protected static K3Value ResolveZeroDividedByZero(K3Value dividend, K3Value divisor, 
            K3Value? prevDividend = null, K3Value? nextDividend = null)
        {
            // Check if this is truly a 0%0 case
            bool isZeroDividend = dividend is IntegerValue iv && iv.Value == 0 ||
                                   dividend is LongValue lv && lv.Value == 0 ||
                                   dividend is FloatValue fv && fv.Value == 0.0;
            bool isZeroDivisor = divisor is IntegerValue id && id.Value == 0 ||
                                 divisor is LongValue ld && ld.Value == 0 ||
                                 divisor is FloatValue fd && fd.Value == 0.0;
            
            if (!isZeroDividend || !isZeroDivisor)
                return dividend.Divide(divisor); // Not a 0%0 case, use normal division
            
            // Check if adjacent elements would produce infinity
            // "Very large" numbers are those close to the limits of numeric representation
            // where division by a small number would result in infinity
            const double VeryLargeThreshold = 1e300; // Threshold for considering a number "very large"
            
            bool WouldProduceInfinity(K3Value? adjDividend)
            {
                if (adjDividend == null) return false;
                
                double value = 0;
                if (adjDividend is IntegerValue i) value = Math.Abs(i.Value);
                else if (adjDividend is LongValue l) value = Math.Abs(l.Value);
                else if (adjDividend is FloatValue f) 
                {
                    if (double.IsNaN(f.Value) || f.Value == 0.0) return false;
                    value = Math.Abs(f.Value);
                }
                else return false;
                
                // A number is "very large" if it exceeds the threshold
                return value >= VeryLargeThreshold;
            }
            
            // Check if adjacent element would produce positive or negative infinity
            double GetInfinitySign(K3Value? adjDividend)
            {
                if (adjDividend is IntegerValue i) return Math.Sign(i.Value);
                if (adjDividend is LongValue l) return Math.Sign(l.Value);
                if (adjDividend is FloatValue f) return Math.Sign(f.Value);
                return 1.0;
            }
            
            bool prevProducesInfinity = WouldProduceInfinity(prevDividend);
            bool nextProducesInfinity = WouldProduceInfinity(nextDividend);
            
            if (prevProducesInfinity || nextProducesInfinity)
            {
                // Determine the sign of infinity
                double sign = 1.0;
                if (prevProducesInfinity)
                    sign = GetInfinitySign(prevDividend);
                else if (nextProducesInfinity)
                    sign = GetInfinitySign(nextDividend);
                
                // Return appropriate infinity based on operand types
                if (dividend is LongValue || divisor is LongValue)
                    return new LongValue(sign > 0 ? long.MaxValue : -long.MaxValue);
                else if (dividend is FloatValue || divisor is FloatValue || 
                         (prevDividend is FloatValue) || (nextDividend is FloatValue))
                    return new FloatValue(sign > 0 ? double.PositiveInfinity : double.NegativeInfinity);
                else
                    return new IntegerValue(sign > 0 ? int.MaxValue : int.MinValue + 1);
            }
            
            // No adjacent infinity-producing elements, return 0/0.0 based on types
            if (dividend is LongValue || divisor is LongValue)
                return new LongValue(0);
            else if (dividend is FloatValue || divisor is FloatValue)
                return new FloatValue(0.0);
            else
                return new IntegerValue(0);
        }
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

        public override K3Value Add(K3Value other)
        {
            if (other is IntegerValue intVal)
            {
                // Use unchecked arithmetic for all integers to allow natural overflow/underflow
                unchecked
                {
                    return new IntegerValue(Value + intVal.Value);
                }
            }
            if (other is LongValue longVal)
                return new LongValue(Value + longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value + floatVal.Value);
            if (other is VectorValue vec)
            {
                // Scalar + vector: add scalar to each element
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(element.Add(this));
                }
                return new VectorValue(result);
            }
            
            throw new InvalidOperationException($"Cannot add Integer to {other.Type}");
        }

        public override K3Value Subtract(K3Value other)
        {
            if (other is IntegerValue intVal)
            {
                // Use unchecked arithmetic for all integers to allow natural overflow/underflow
                unchecked
                {
                    return new IntegerValue(Value - intVal.Value);
                }
            }
            if (other is LongValue longVal)
                return new LongValue(Value - longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value - floatVal.Value);
            
            throw new InvalidOperationException($"Cannot subtract Integer from {other.Type}");
        }

        public override K3Value Multiply(K3Value other)
        {
            if (other is IntegerValue intVal)
                return new IntegerValue(Value * intVal.Value);
            if (other is LongValue longVal)
                return new LongValue(Value * longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value * floatVal.Value);
            
            throw new InvalidOperationException($"Cannot multiply Integer by {other.Type}");
        }

        public override K3Value Divide(K3Value other)
        {
            if (other is IntegerValue intVal)
                return new IntegerValue(Value / intVal.Value);
            if (other is LongValue longVal)
                return new LongValue(Value / longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value / floatVal.Value);
            
            throw new InvalidOperationException($"Cannot divide Integer by {other.Type}");
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

        public override K3Value Add(K3Value other)
        {
            if (other is IntegerValue intVal)
            {
                // Use unchecked arithmetic to allow natural overflow/underflow
                unchecked
                {
                    return new LongValue(Value + intVal.Value);
                }
            }
            if (other is LongValue longVal)
            {
                // Use unchecked arithmetic to allow natural overflow/underflow
                unchecked
                {
                    return new LongValue(Value + longVal.Value);
                }
            }
            if (other is FloatValue floatVal)
                return new FloatValue(Value + floatVal.Value);
            if (other is VectorValue vec)
            {
                // Scalar + vector: add scalar to each element
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(element.Add(this));
                }
                return new VectorValue(result);
            }
            
            throw new InvalidOperationException($"Cannot add Long to {other.Type}");
        }

        public override K3Value Subtract(K3Value other)
        {
            if (other is IntegerValue intVal)
            {
                // Use unchecked arithmetic to allow natural overflow/underflow
                unchecked
                {
                    return new LongValue(Value - intVal.Value);
                }
            }
            if (other is LongValue longVal)
            {
                // Use unchecked arithmetic to allow natural overflow/underflow
                unchecked
                {
                    return new LongValue(Value - longVal.Value);
                }
            }
            if (other is FloatValue floatVal)
                return new FloatValue(Value - floatVal.Value);
            
            throw new InvalidOperationException($"Cannot subtract from Long");
        }

        public override K3Value Multiply(K3Value other)
        {
            if (other is IntegerValue intVal)
            {
                // Use unchecked arithmetic to allow natural overflow/underflow
                unchecked
                {
                    return new LongValue(Value * intVal.Value);
                }
            }
            if (other is LongValue longVal)
            {
                // Use unchecked arithmetic to allow natural overflow/underflow
                unchecked
                {
                    return new LongValue(Value * longVal.Value);
                }
            }
            if (other is FloatValue floatVal)
                return new FloatValue(Value * floatVal.Value);
            
            throw new InvalidOperationException($"Cannot multiply Long by {other.Type}");
        }

        public override K3Value Divide(K3Value other)
        {
            if (other is IntegerValue intVal)
                return new LongValue(Value / intVal.Value);
            if (other is LongValue longVal)
                return new LongValue(Value / longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value / floatVal.Value);
            
            throw new InvalidOperationException($"Cannot divide Long by {other.Type}");
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

        public override K3Value Add(K3Value other)
        {
            if (other is IntegerValue intVal)
                return new FloatValue(Value + intVal.Value);
            if (other is LongValue longVal)
                return new FloatValue(Value + longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value + floatVal.Value);
            if (other is VectorValue vec)
            {
                // Scalar + vector: add scalar to each element
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(element.Add(this));
                }
                return new VectorValue(result);
            }
            
            throw new InvalidOperationException($"Cannot add Float to {other.Type}");
        }

        public override K3Value Subtract(K3Value other)
        {
            if (other is IntegerValue intVal)
                return new FloatValue(Value - intVal.Value);
            if (other is LongValue longVal)
                return new FloatValue(Value - longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value - floatVal.Value);
            
            throw new InvalidOperationException($"Cannot subtract Float from {other.Type}");
        }

        public override K3Value Multiply(K3Value other)
        {
            if (other is IntegerValue intVal)
                return new FloatValue(Value * intVal.Value);
            if (other is LongValue longVal)
                return new FloatValue(Value * longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value * floatVal.Value);
            
            throw new InvalidOperationException($"Cannot multiply Float by {other.Type}");
        }

        public override K3Value Divide(K3Value other)
        {
            if (other is IntegerValue intVal)
                return new FloatValue(Value / intVal.Value);
            if (other is LongValue longVal)
                return new FloatValue(Value / longVal.Value);
            if (other is FloatValue floatVal)
                return new FloatValue(Value / floatVal.Value);
            
            throw new InvalidOperationException($"Cannot divide Float by {other.Type}");
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

    public class CharacterValue : K3Value
    {
        public string Value { get; }

        public CharacterValue(string value, SymbolValue? hint = null)
        {
            // Validate that CharacterValue can only be created with single characters
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            
            // Un-escape character sequences before length validation
            string unescapedValue = UnescapeCharacterString(value);
            
            // After un-escaping, check if we have exactly one character OR serialization data
            // Serialization data (from _bd) contains non-printable characters and should be allowed
            if (unescapedValue.Length != 1)
            {
                throw new ArgumentException($"CharacterValue can only be created with single characters, but got string of length {unescapedValue.Length}: '{value}'. Use VectorValue for multi-character strings.");
            }
            
            Value = unescapedValue;
            Type = ValueType.Character;
            Hint = hint;
        }
        
        private static string UnescapeCharacterString(string input)
        {
            if (input.Length == 1)
                return input; // Single character, no escaping needed
                
            var result = new StringBuilder();
            int i = 0;
            
            while (i < input.Length)
            {
                if (input[i] == '\\' && i + 1 < input.Length)
                {
                    switch (input[i + 1])
                    {
                        case '\\':
                            result.Append('\\');
                            i += 2;
                            break;
                        case 'b':
                            result.Append('\b');
                            i += 2;
                            break;
                        case 't':
                            result.Append('\t');
                            i += 2;
                            break;
                        case 'n':
                            result.Append('\n');
                            i += 2;
                            break;
                        case 'r':
                            result.Append('\r');
                            i += 2;
                            break;
                        case '"':
                            result.Append('"');
                            i += 2;
                            break;
                        case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7':
                            // Octal sequence \OOO (up to 3 digits)
                            if (i + 3 < input.Length && char.IsDigit(input[i + 2]) && char.IsDigit(input[i + 3]))
                            {
                                string octalStr = input.Substring(i + 1, 3);
                                int octalValue = 0;
                                foreach (char c in octalStr)
                                {
                                    octalValue = octalValue * 8 + (c - '0');
                                }
                                result.Append((char)octalValue);
                                i += 4;
                                break;
                            }
                            else if (i + 2 < input.Length && char.IsDigit(input[i + 2]))
                            {
                                string octalStr = input.Substring(i + 1, 2);
                                int octalValue = 0;
                                foreach (char c in octalStr)
                                {
                                    octalValue = octalValue * 8 + (c - '0');
                                }
                                result.Append((char)octalValue);
                                i += 3;
                                break;
                            }
                            // If not a valid octal sequence, treat as literal backslash
                            result.Append('\\');
                            i += 1;
                            break;
                        default:
                            // Unknown escape sequence, treat as literal backslash
                            result.Append('\\');
                            i += 1;
                            break;
                    }
                }
                else
                {
                    result.Append(input[i]);
                    i++;
                }
            }
            
            return result.ToString();
        }

        public override K3Value Add(K3Value other)
        {
            throw new InvalidOperationException("Cannot add Character values");
        }

        public override K3Value Subtract(K3Value other)
        {
            throw new InvalidOperationException("Cannot subtract Character values");
        }

        public override K3Value Multiply(K3Value other)
        {
            throw new InvalidOperationException("Cannot multiply Character values");
        }

        public override K3Value Divide(K3Value other)
        {
            throw new InvalidOperationException("Cannot divide Character values");
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append('"');
            
            foreach (char c in Value)
            {
                switch (c)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;
                    case '\b':
                        result.Append("\\b");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '"':
                        result.Append("\\\"");
                        break;
                    default:
                        if (c >= ' ' && c <= '~')
                        {
                            // Printable characters (space to tilde)
                            result.Append(c);
                        }
                        else
                        {
                            // Non-printable or extended characters - use 3-digit octal
                            string octalValue = Convert.ToString(Convert.ToInt32(c), 8);
                            result.Append($"\\{octalValue.PadLeft(3, '0')}");
                        }
                        break;
                }
            }
            
            result.Append('"');
            return result.ToString();
        }
    }

    public class SymbolValue : K3Value
    {
        public string Value { get; }

        public SymbolValue(string value, SymbolValue? hint = null)
        {
            Value = value;
            Type = ValueType.Symbol;
            Hint = hint;
        }

        public override K3Value Add(K3Value other)
        {
            throw new InvalidOperationException("Cannot add Symbol values");
        }

        public override K3Value Subtract(K3Value other)
        {
            throw new InvalidOperationException("Cannot subtract Symbol values");
        }

        public override K3Value Multiply(K3Value other)
        {
            throw new InvalidOperationException("Cannot multiply Symbol values");
        }

        public override K3Value Divide(K3Value other)
        {
            throw new InvalidOperationException("Cannot divide Symbol values");
        }

        public override string ToString()
        {
            // Check if symbol is empty - in K, empty symbols display as `
            if (string.IsNullOrEmpty(Value))
                return "`";
            
            // Check if symbol is a valid variable name according to K spec
            if (IsValidVariableName(Value))
            {
                // Symbol is a valid variable name, display with backtick only
                return "`" + Value;
            }
            else
            {
                // Symbol is not a valid variable name, display with quotes and backtick
                // Escape special characters to match k.exe display behavior
                var escaped = Value
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
                return $"`\"{escaped}\"";
            }
        }
        
        private static bool IsValidVariableName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            
            // Must contain at least one upper or lower case alphabetic character
            bool hasAlphabetic = false;
            
            foreach (char c in value)
            {
                if (char.IsLetter(c))
                {
                    hasAlphabetic = true;
                }
                
                // If character is not alphanumeric, underscore, or period, it's invalid
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                {
                    return false;
                }
            }
            
            return hasAlphabetic;
        }
        
        public string ToStringForFormat()
        {
            // For monadic $ formatting, return symbol name in quotes without backtick
            return $"\"{Value}\"";
        }

        public override bool Equals(object? obj)
        {
            if (obj is SymbolValue other)
            {
                return Value == other.Value;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }
    }

    public class VectorValue : K3Value
    {
        public List<K3Value> Elements { get; }
        public int? VectorType { get; private set; } // Track type for empty vectors

        public VectorValue(List<K3Value> elements, SymbolValue? hint = null)
        {
            Elements = elements;
            Type = ValueType.Vector;
            Hint = hint;
            VectorType = DetermineVectorTypeFromElements(elements);
        }

        public VectorValue(List<K3Value> elements, int vectorType, SymbolValue? hint = null)
        {
            Elements = elements;
            Type = ValueType.Vector;
            Hint = hint;
            VectorType = vectorType;
        }

        private static int DetermineVectorTypeFromElements(List<K3Value> elements)
        {
            if (elements.Count == 0)
                return 0; // Default to mixed list for empty vectors
                
            // Check if all elements are floats
            bool allFloats = true;
            foreach (var element in elements)
            {
                if (!(element is FloatValue))
                {
                    allFloats = false;
                    break;
                }
            }
            if (allFloats)
                return -2; // Float vector
                
            // Check if any element is a float (mixed integers and floats)
            bool hasFloats = false;
            foreach (var element in elements)
            {
                if (element is FloatValue)
                {
                    hasFloats = true;
                    break;
                }
            }
            
            // Check if all elements are integers/longs (ignoring floats for this check)
            bool allIntegersOrLongs = true;
            foreach (var element in elements)
            {
                if (!(element is IntegerValue || element is LongValue))
                {
                    allIntegersOrLongs = false;
                    break;
                }
            }
            
            // Check if all elements are symbols
            bool allSymbols = true;
            foreach (var element in elements)
            {
                if (!(element is SymbolValue))
                {
                    allSymbols = false;
                    break;
                }
            }
            
            // Determine type based on element composition
            if (allFloats)
                return -2; // Float vector
            else if (allIntegersOrLongs && !hasFloats)
                return -1; // Integer vector
            else if (allSymbols)
                return -4; // Symbol vector
            else if (elements[0] is CharacterValue)
                return -3; // Character vector
            else
                return 0; // Default to mixed list (for mixed types)
        }

        public override K3Value Add(K3Value other)
        {
            if (other is VectorValue otherVec)
            {
                if (Elements.Count != otherVec.Elements.Count)
                    throw new InvalidOperationException("Vector size mismatch for addition");
                
                var result = new List<K3Value>();
                for (int i = 0; i < Elements.Count; i++)
                {
                    result.Add(Elements[i].Add(otherVec.Elements[i]));
                }
                return new VectorValue(result);
            }
            
            // Scalar addition
            var scalarResult = new List<K3Value>();
            foreach (var element in Elements)
            {
                scalarResult.Add(element.Add(other));
            }
            return new VectorValue(scalarResult);
        }

        public override K3Value Subtract(K3Value other)
        {
            if (other is VectorValue otherVec)
            {
                if (Elements.Count != otherVec.Elements.Count)
                    throw new InvalidOperationException("Vector size mismatch for subtraction");
                
                var result = new List<K3Value>();
                for (int i = 0; i < Elements.Count; i++)
                {
                    result.Add(Elements[i].Subtract(otherVec.Elements[i]));
                }
                return new VectorValue(result);
            }
            
            // Scalar subtraction
            var scalarResult = new List<K3Value>();
            foreach (var element in Elements)
            {
                scalarResult.Add(element.Subtract(other));
            }
            return new VectorValue(scalarResult);
        }

        public override K3Value Multiply(K3Value other)
        {
            if (other is VectorValue otherVec)
            {
                if (Elements.Count != otherVec.Elements.Count)
                    throw new InvalidOperationException("Vector size mismatch for multiplication");
                
                var result = new List<K3Value>();
                for (int i = 0; i < Elements.Count; i++)
                {
                    result.Add(Elements[i].Multiply(otherVec.Elements[i]));
                }
                return new VectorValue(result);
            }
            
            // Scalar multiplication
            var scalarResult = new List<K3Value>();
            foreach (var element in Elements)
            {
                scalarResult.Add(element.Multiply(other));
            }
            return new VectorValue(scalarResult);
        }

        public override K3Value Divide(K3Value other)
        {
            if (other is VectorValue otherVec)
            {
                if (Elements.Count != otherVec.Elements.Count)
                    throw new InvalidOperationException("Vector size mismatch for division");
                
                // Check if all elements are integers and all divisions are exact
                bool allIntegerDivision = true;
                bool allExactDivision = true;
                
                for (int i = 0; i < Elements.Count; i++)
                {
                    if (Elements[i] is IntegerValue intElem && otherVec.Elements[i] is IntegerValue intDiv)
                    {
                        // Check for 0%0 special case
                        if (intDiv.Value == 0 && intElem.Value == 0)
                        {
                            // 0%0 will be handled later with adjacent element checking
                            allExactDivision = false; // Force non-exact path to use FloatValue
                        }
                        else if (intDiv.Value == 0)
                            throw new InvalidOperationException("Division by zero");
                        else if (intElem.Value % intDiv.Value != 0)
                            allExactDivision = false;
                    }
                    else if (Elements[i] is LongValue longElem && otherVec.Elements[i] is LongValue longDiv)
                    {
                        // Check for 0j%0j special case
                        if (longDiv.Value == 0 && longElem.Value == 0)
                        {
                            // 0j%0j will be handled later
                            allExactDivision = false;
                        }
                        else if (longDiv.Value == 0)
                            throw new InvalidOperationException("Division by zero");
                        else if (longElem.Value % longDiv.Value != 0)
                            allExactDivision = false;
                    }
                    else
                    {
                        allIntegerDivision = false;
                        break;
                    }
                }
                
                var result = new List<K3Value>();
                for (int i = 0; i < Elements.Count; i++)
                {
                    // Check for 0%0 special case first
                    bool isZeroDividend = (Elements[i] is IntegerValue iv && iv.Value == 0) ||
                                          (Elements[i] is LongValue lv && lv.Value == 0);
                    bool isZeroDivisor = (otherVec.Elements[i] is IntegerValue id && id.Value == 0) ||
                                        (otherVec.Elements[i] is LongValue ld && ld.Value == 0);
                    
                    if (isZeroDividend && isZeroDivisor)
                    {
                        // Use special 0%0 resolution with adjacent elements
                        K3Value? prevDiv = i > 0 ? Elements[i - 1] : null;
                        K3Value? nextDiv = i < Elements.Count - 1 ? Elements[i + 1] : null;
                        result.Add(ResolveZeroDividedByZero(Elements[i], otherVec.Elements[i], prevDiv, nextDiv));
                    }
                    else if (allIntegerDivision && allExactDivision)
                    {
                        // All exact integer division - return integer vector
                        if (Elements[i] is IntegerValue intElem && otherVec.Elements[i] is IntegerValue intDiv)
                            result.Add(new IntegerValue(intElem.Value / intDiv.Value));
                        else if (Elements[i] is LongValue longElem && otherVec.Elements[i] is LongValue longDiv)
                            result.Add(new LongValue(longElem.Value / longDiv.Value));
                    }
                    else if (allIntegerDivision && !allExactDivision)
                    {
                        // Integer division but not all exact - return float vector
                        if (Elements[i] is IntegerValue intElem && otherVec.Elements[i] is IntegerValue intDiv)
                            result.Add(new FloatValue((double)intElem.Value / intDiv.Value));
                        else if (Elements[i] is LongValue longElem && otherVec.Elements[i] is LongValue longDiv)
                            result.Add(new FloatValue((double)longElem.Value / longDiv.Value));
                    }
                    else
                    {
                        // Mixed types - use element-wise division
                        result.Add(Elements[i].Divide(otherVec.Elements[i]));
                    }
                }
                return new VectorValue(result);
            }
            
            // Scalar division with smart division rules
            if (other is IntegerValue intScalar)
            {
                int scalarValue = intScalar.Value;
                
                // Check if all elements are integers
                bool allIntegerElements = true;
                bool allExactDivision = true;
                
                foreach (var element in Elements)
                {
                    if (element is IntegerValue intElem)
                    {
                        if (scalarValue != 0 && intElem.Value % scalarValue != 0)
                            allExactDivision = false;
                    }
                    else if (element is LongValue longElem)
                    {
                        if (scalarValue != 0 && longElem.Value % scalarValue != 0)
                            allExactDivision = false;
                    }
                    else
                    {
                        allIntegerElements = false;
                        break;
                    }
                }
                
                var scalarResult = new List<K3Value>();
                for (int i = 0; i < Elements.Count; i++)
                {
                    var element = Elements[i];
                    
                    // Check for 0%0 special case
                    bool isZeroDividend = (element is IntegerValue iv && iv.Value == 0) ||
                                          (element is LongValue lv && lv.Value == 0);
                    
                    if (scalarValue == 0 && isZeroDividend)
                    {
                        // Use special 0%0 resolution with adjacent elements
                        K3Value? prevDiv = i > 0 ? Elements[i - 1] : null;
                        K3Value? nextDiv = i < Elements.Count - 1 ? Elements[i + 1] : null;
                        scalarResult.Add(ResolveZeroDividedByZero(element, intScalar, prevDiv, nextDiv));
                    }
                    else if (scalarValue == 0)
                    {
                        throw new InvalidOperationException("Division by zero");
                    }
                    else if (allIntegerElements && allExactDivision)
                    {
                        // All exact integer division
                        if (element is IntegerValue intElem)
                            scalarResult.Add(new IntegerValue(intElem.Value / scalarValue));
                        else if (element is LongValue longElem)
                            scalarResult.Add(new LongValue(longElem.Value / scalarValue));
                    }
                    else if (allIntegerElements && !allExactDivision)
                    {
                        // Integer division but not all exact - convert to float
                        if (element is IntegerValue intElem)
                            scalarResult.Add(new FloatValue((double)intElem.Value / scalarValue));
                        else if (element is LongValue longElem)
                            scalarResult.Add(new FloatValue((double)longElem.Value / scalarValue));
                    }
                    else
                    {
                        // Mixed types - use element-wise division
                        scalarResult.Add(element.Divide(other));
                    }
                }
                return new VectorValue(scalarResult);
            }
            
            // Handle LongValue scalar division
            if (other is LongValue longScalar)
            {
                long scalarValue = longScalar.Value;
                
                var longResult = new List<K3Value>();
                for (int i = 0; i < Elements.Count; i++)
                {
                    var element = Elements[i];
                    
                    // Check for 0%0 special case
                    bool isZeroDividend = (element is IntegerValue iv && iv.Value == 0) ||
                                          (element is LongValue lv && lv.Value == 0);
                    
                    if (scalarValue == 0 && isZeroDividend)
                    {
                        // Use special 0%0 resolution with adjacent elements
                        K3Value? prevDiv = i > 0 ? Elements[i - 1] : null;
                        K3Value? nextDiv = i < Elements.Count - 1 ? Elements[i + 1] : null;
                        longResult.Add(ResolveZeroDividedByZero(element, longScalar, prevDiv, nextDiv));
                    }
                    else if (scalarValue == 0)
                    {
                        throw new InvalidOperationException("Division by zero");
                    }
                    else
                    {
                        longResult.Add(element.Divide(other));
                    }
                }
                return new VectorValue(longResult);
            }
            
            // Handle FloatValue scalar division
            if (other is FloatValue floatScalar)
            {
                double scalarValue = floatScalar.Value;
                
                var floatResult = new List<K3Value>();
                for (int i = 0; i < Elements.Count; i++)
                {
                    var element = Elements[i];
                    
                    // Check for 0%0 special case
                    bool isZeroDividend = (element is IntegerValue iv && iv.Value == 0) ||
                                          (element is LongValue lv && lv.Value == 0) ||
                                          (element is FloatValue fv && fv.Value == 0.0);
                    
                    if (scalarValue == 0.0 && isZeroDividend)
                    {
                        // Use special 0%0 resolution with adjacent elements
                        K3Value? prevDiv = i > 0 ? Elements[i - 1] : null;
                        K3Value? nextDiv = i < Elements.Count - 1 ? Elements[i + 1] : null;
                        floatResult.Add(ResolveZeroDividedByZero(element, floatScalar, prevDiv, nextDiv));
                    }
                    else if (scalarValue == 0.0)
                    {
                        throw new InvalidOperationException("Division by zero");
                    }
                    else
                    {
                        floatResult.Add(element.Divide(other));
                    }
                }
                return new VectorValue(floatResult);
            }
            
            // Default scalar division for other types
            var defaultScalarResult = new List<K3Value>();
            foreach (var element in Elements)
            {
                defaultScalarResult.Add(element.Divide(other));
            }
            return new VectorValue(defaultScalarResult);
        }

        public K3Value Minimum(VectorValue other)
        {
            if (Elements.Count != other.Elements.Count)
                throw new InvalidOperationException("Vector size mismatch for minimum");
            
            var result = new List<K3Value>();
            for (int i = 0; i < Elements.Count; i++)
            {
                if (Elements[i] is IntegerValue intA && other.Elements[i] is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Min(intA.Value, intB.Value)));
                else if (Elements[i] is LongValue longA && other.Elements[i] is LongValue longB)
                    result.Add(new LongValue(Math.Min(longA.Value, longB.Value)));
                else if (Elements[i] is FloatValue floatA && other.Elements[i] is FloatValue floatB)
                    result.Add(new FloatValue(Math.Min(floatA.Value, floatB.Value)));
                else
                    throw new InvalidOperationException("Cannot find minimum of mixed types");
            }
            return new VectorValue(result);
        }

        public K3Value Minimum(K3Value scalar)
        {
            var result = new List<K3Value>();
            foreach (var element in Elements)
            {
                if (element is IntegerValue intA && scalar is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Min(intA.Value, intB.Value)));
                else if (element is LongValue longA && scalar is LongValue longB)
                    result.Add(new LongValue(Math.Min(longA.Value, longB.Value)));
                else if (element is FloatValue floatA && scalar is FloatValue floatB)
                    result.Add(new FloatValue(Math.Min(floatA.Value, floatB.Value)));
                else
                    throw new InvalidOperationException("Cannot find minimum of mixed types");
            }
            return new VectorValue(result);
        }

        public K3Value Maximum(VectorValue other)
        {
            if (Elements.Count != other.Elements.Count)
                throw new InvalidOperationException("Vector size mismatch for maximum");
            
            var result = new List<K3Value>();
            for (int i = 0; i < Elements.Count; i++)
            {
                if (Elements[i] is IntegerValue intA && other.Elements[i] is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Max(intA.Value, intB.Value)));
                else if (Elements[i] is LongValue longA && other.Elements[i] is LongValue longB)
                    result.Add(new LongValue(Math.Max(longA.Value, longB.Value)));
                else if (Elements[i] is FloatValue floatA && other.Elements[i] is FloatValue floatB)
                    result.Add(new FloatValue(Math.Max(floatA.Value, floatB.Value)));
                else
                    throw new InvalidOperationException("Cannot find maximum of mixed types");
            }
            return new VectorValue(result);
        }

        public K3Value Maximum(K3Value scalar)
        {
            var result = new List<K3Value>();
            foreach (var element in Elements)
            {
                if (element is IntegerValue intA && scalar is IntegerValue intB)
                    result.Add(new IntegerValue(Math.Max(intA.Value, intB.Value)));
                else if (element is LongValue longA && scalar is LongValue longB)
                    result.Add(new LongValue(Math.Max(longA.Value, longB.Value)));
                else if (element is FloatValue floatA && scalar is FloatValue floatB)
                    result.Add(new FloatValue(Math.Max(floatA.Value, floatB.Value)));
                else
                    throw new InvalidOperationException("Cannot find maximum of mixed types");
            }
            return new VectorValue(result);
        }

        public override string ToString()
        {
            // 1) Handle empty vectors
            if (Elements.Count == 0)
            {
                if (VectorType.HasValue)
                {
                    return VectorType.Value switch
                    {
                        -4 => "0#`",    // Empty symbol vector
                        -3 => "\"\"",    // Empty character vector
                        -2 => "0#0.0",   // Empty float vector
                        -1 => "!0",      // Empty integer vector
                        -64 => "!0j",     // Empty long vector (same as integer)
                        0 => "()",       // Empty list
                        _ => "()"        // Default to empty list
                    };
                }
                return "()"; // Default to empty list if no type specified
            }
                        
            // 2) Handle single-element generic lists (enlist) - only for type 0
            if (Elements.Count == 1)
            {
                return "," + Elements[0].ToString();
            }

            // 3) Check if this is a projection (contains :: symbols) - always use generic list format
            bool hasProjectionMarker = Elements.Any(e => e is SymbolValue sv && sv.Value == "::");
            if (hasProjectionMarker)
            {
                return FormatGenericList();
            }

            // 4) Identify vector type and apply appropriate rules
            var vectorType = VectorType ?? 0; // Default to generic list if no type specified

            // For typed vectors (-1, -2, -3, -4, -64), use type-specific rules
            return vectorType switch
            {
                -1 => FormatNumericVector(),    // Integer vector
                -2 => FormatNumericVector(),    // Float vector
                -64 => FormatNumericVector(),   // Long vector
                -3 => FormatCharacterVector(),  // Character vector
                -4 => FormatSymbolVector(),     // Symbol vector
                _ => FormatGenericList()        // Default to generic list
            };
            
            string FormatNumericVector()
            {
                // Numeric types: elements separated by spaces
                return string.Join(" ", Elements.Select(e => e.ToString()));
            }
            
            string FormatCharacterVector()
            {
                // Character vector: concatenate the string representations of individual characters
                // and remove the surrounding quotes from each character
                var result = "\"";
                foreach (var element in Elements)
                {
                    if (element is CharacterValue cv)
                    {
                        // Get the string representation and remove surrounding quotes
                        var charStr = cv.ToString();
                        if (charStr.StartsWith("\"") && charStr.EndsWith("\"") && charStr.Length > 2)
                        {
                            result += charStr.Substring(1, charStr.Length - 2);
                        }
                        else
                        {
                            result += charStr;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Character vector contains non-character elements");
                    }
                }
                result += "\"";
                return result;
            }
            
            string FormatSymbolVector()
            {
                // Symbol vector: elements with no separation
                return string.Concat(Elements.Select(e => e.ToString()));
            }
            
            string FormatGenericList()
            {
                // Generic list: enclosing parentheses and elements separated by semicolons
                // Special handling for :: projection markers - display without quotes
                var elementsStr = string.Join(";", Elements.Select(e => 
                {
                    if (e is SymbolValue sv && sv.Value == "::")
                        return "::";
                    return e.ToString();
                }));
                return "(" + elementsStr + ")";
            }
        }
    }

    public class FunctionValue : K3Value
    {
        public string BodyText { get; }
        public string OriginalSourceText { get; }
        public List<string> Parameters { get; }
        public int Valence { get; }
        public List<Token> PreParsedTokens { get; }
        
        // AST cache for performance optimization
        private ASTNode? _cachedAst;
        private readonly object _astCacheLock = new object();

        // Associated K tree for anonymous functions
        public KTree AssociatedKTree { get; private set; }
        
        // For adverb chaining: store the right argument when this function is created by an adverb
        public K3Value? RightArgument { get; set; }
        
        public FunctionValue(string bodyText, List<string> parameters, List<Token> preParsedTokens = null!, string originalSourceText = "", SymbolValue? hint = null)
        {
            BodyText = bodyText;
            OriginalSourceText = originalSourceText;
            Parameters = parameters;
            Type = ValueType.Function;
            Hint = hint;
            Valence = parameters.Count;
            PreParsedTokens = preParsedTokens;
            AssociatedKTree = new KTree(); // Create associated K tree for anonymous functions
        }
        
        // Get or create cached AST (thread-safe)
        public ASTNode? GetCachedAst()
        {
            if (_cachedAst != null)
            {
                return _cachedAst;
            }
            
            lock (_astCacheLock)
            {
                // Double-check pattern for thread safety
                if (_cachedAst != null)
                {
                    return _cachedAst;
                }

                // Parse and cache the AST
                ASTNode? ast;
                if (PreParsedTokens != null && PreParsedTokens.Count > 0)
                {
                    ast = ParserConfig.ParseWithConfig(PreParsedTokens, BodyText);
                }
                else
                {
                    var lexer = new Lexer(BodyText);
                    var tokens = lexer.Tokenize();
                    ast = ParserConfig.ParseWithConfig(tokens, BodyText);
                }
                
                _cachedAst = ast;
                return _cachedAst;
            }
        }

        // Cache an already parsed AST (thread-safe)
        public void CacheAst(ASTNode ast)
        {
            lock (_astCacheLock)
            {
                _cachedAst = ast;
            }
        }

        public override K3Value Add(K3Value other)
        {
            throw new InvalidOperationException("Cannot add Function values");
        }

        public override K3Value Subtract(K3Value other)
        {
            throw new InvalidOperationException("Cannot subtract Function values");
        }

        public override K3Value Multiply(K3Value other)
        {
            throw new InvalidOperationException("Cannot multiply Function values");
        }

        public override K3Value Divide(K3Value other)
        {
            throw new InvalidOperationException("Cannot divide Function values");
        }

        public override string ToString()
        {
            // Use the original source text if available for exact representation
            if (!string.IsNullOrEmpty(OriginalSourceText))
                return OriginalSourceText;
                
            // Fall back to reconstructed representation for backward compatibility
            var paramsStr = Parameters.Count > 0 ? "[" + string.Join(";", Parameters) + "] " : "";
            return "{" + paramsStr + BodyText + "}";
        }
    }

    public class NullValue : K3Value
    {
        public NullValue()
        {
            Type = ValueType.Null;
        }

        public override K3Value Add(K3Value other)
        {
            throw new InvalidOperationException("Cannot add Null values");
        }

        public override K3Value Subtract(K3Value other)
        {
            throw new InvalidOperationException("Cannot subtract Null values");
        }

        public override K3Value Multiply(K3Value other)
        {
            throw new InvalidOperationException("Cannot multiply Null values");
        }

        public override K3Value Divide(K3Value other)
        {
            throw new InvalidOperationException("Cannot divide Null values");
        }

        public override string ToString()
        {
            return "";
        }
    }

    public class DictionaryValue : K3Value
    {
        public Dictionary<SymbolValue, (K3Value Value, DictionaryValue? Attribute)> Entries { get; }

        public DictionaryValue()
        {
            Type = ValueType.Dictionary;
            Entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
        }

        public DictionaryValue(Dictionary<SymbolValue, (K3Value, DictionaryValue?)> entries)
        {
            Type = ValueType.Dictionary;
            Entries = entries;
        }

        public override K3Value Add(K3Value other)
        {
            throw new InvalidOperationException("Cannot add Dictionary values");
        }

        public override K3Value Subtract(K3Value other)
        {
            throw new InvalidOperationException("Cannot subtract Dictionary values");
        }

        public override K3Value Multiply(K3Value other)
        {
            throw new InvalidOperationException("Cannot multiply Dictionary values");
        }

        public override K3Value Divide(K3Value other)
        {
            throw new InvalidOperationException("Cannot divide Dictionary values");
        }

        public override string ToString()
        {
            if (Entries.Count == 0)
                return ".()";
            
            var entries = new List<string>();
            foreach (var kvp in Entries)
            {
                var key = kvp.Key.ToString();
                var value = kvp.Value.Value;
                var attr = kvp.Value.Attribute;
                
                var valueStr = value is NullValue ? "" : value.ToString();
                
                if (attr != null)
                {
                    entries.Add($"({key};{valueStr};{attr})");
                }
                else
                {
                    // For null attributes, show semicolon
                    entries.Add($"({key};{valueStr};)");
                }
            }
            
            // Handle single-element case with comma prefix (per specification)
            if (entries.Count == 1)
            {
                // Remove the outer parentheses from the single entry to avoid double parentheses
                var singleEntry = entries[0];
                if (singleEntry.StartsWith("(") && singleEntry.EndsWith(")"))
                {
                    singleEntry = singleEntry.Substring(1, singleEntry.Length - 2);
                }
                return ".,(" + singleEntry + ")";
            }
            
            return ".(" + string.Join(";", entries) + ")";
        }
    }

    public class ProjectedFunctionValue : K3Value
    {
        public string OperatorName { get; }
        public int RequiredArguments { get; }

        public ProjectedFunctionValue(string operatorName, int requiredArguments)
        {
            Type = ValueType.Function; // Treat as a function type
            OperatorName = operatorName;
            RequiredArguments = requiredArguments;
        }

        public override string ToString()
        {
            // Monadic projections (arity 1) should be displayed with a colon
            // Dyadic projections (arity 2) are displayed without colon
            return RequiredArguments == 1 ? OperatorName + ":" : OperatorName;
        }

        public override K3Value Add(K3Value other)
        {
            throw new InvalidOperationException("Cannot add ProjectedFunction values");
        }

        public override K3Value Subtract(K3Value other)
        {
            throw new InvalidOperationException("Cannot subtract ProjectedFunction values");
        }

        public override K3Value Multiply(K3Value other)
        {
            throw new InvalidOperationException("Cannot multiply ProjectedFunction values");
        }

        public override K3Value Divide(K3Value other)
        {
            throw new InvalidOperationException("Cannot divide ProjectedFunction values");
        }

        public override bool Equals(object? obj)
        {
            if (obj is ProjectedFunctionValue otherProjected)
                return OperatorName == otherProjected.OperatorName && RequiredArguments == otherProjected.RequiredArguments;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OperatorName, RequiredArguments);
        }
    }

    public class AdverbProjectedFunctionValue : K3Value
    {
        public string AdverbName { get; }
        public string Verb { get; }
        public int RequiredArguments { get; }

        public AdverbProjectedFunctionValue(string adverbName, string verb, int requiredArguments)
        {
            Type = ValueType.Function; // Treat as a function type
            AdverbName = adverbName;
            Verb = verb;
            RequiredArguments = requiredArguments;
        }

        public override string ToString()
        {
            return $"{AdverbName}({Verb})";
        }

        public override K3Value Add(K3Value other)
        {
            throw new InvalidOperationException("Cannot add AdverbProjectedFunction values");
        }

        public override K3Value Subtract(K3Value other)
        {
            throw new InvalidOperationException("Cannot subtract AdverbProjectedFunction values");
        }

        public override K3Value Multiply(K3Value other)
        {
            throw new InvalidOperationException("Cannot multiply AdverbProjectedFunction values");
        }

        public override K3Value Divide(K3Value other)
        {
            throw new InvalidOperationException("Cannot divide AdverbProjectedFunction values");
        }

        public override bool Equals(object? obj)
        {
            if (obj is AdverbProjectedFunctionValue other)
                return AdverbName == other.AdverbName && Verb == other.Verb && RequiredArguments == other.RequiredArguments;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AdverbName, Verb, RequiredArguments);
        }
    }
}
