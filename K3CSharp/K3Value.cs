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

        public K3Value Minimum(VectorValue other)
        {
            if (Elements.Count != other.Elements.Count)
                throw new InvalidOperationException("Vector size mismatch for minimum");
            
            var result = new List<K3Value>();
            for (int i = 0; i < Elements.Count; i++)
            {
                // Handle nested vectors (matrices) recursively
                if (Elements[i] is VectorValue vecA && other.Elements[i] is VectorValue vecB)
                {
                    result.Add(vecA.Minimum(vecB));
                }
                else if (Elements[i] is IntegerValue intA && other.Elements[i] is IntegerValue intB)
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
                // Handle nested vectors (matrices) recursively
                if (element is VectorValue vecA)
                {
                    result.Add(vecA.Minimum(scalar));
                }
                else if (element is IntegerValue intA && scalar is IntegerValue intB)
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
                // Handle nested vectors (matrices) recursively
                if (Elements[i] is VectorValue vecA && other.Elements[i] is VectorValue vecB)
                {
                    result.Add(vecA.Maximum(vecB));
                }
                else if (Elements[i] is IntegerValue intA && other.Elements[i] is IntegerValue intB)
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
                // Handle nested vectors (matrices) recursively
                if (element is VectorValue vecA)
                {
                    result.Add(vecA.Maximum(scalar));
                }
                else if (element is IntegerValue intA && scalar is IntegerValue intB)
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
        /// <summary>
        /// Bound arguments for bracket projections (e.g., +[3 3 5] binds left arg).
        /// null entries represent unbound (missing) argument positions.
        /// null list means no bound arguments (simple projection like (+)).
        /// </summary>
        public List<K3Value?>? BoundArguments { get; }

        public ProjectedFunctionValue(string operatorName, int requiredArguments, List<K3Value?>? boundArguments = null)
        {
            Type = ValueType.Function; // Treat as a function type
            OperatorName = operatorName;
            RequiredArguments = requiredArguments;
            BoundArguments = boundArguments;
        }

        public override string ToString()
        {
            // If there are bound arguments, display in bracket notation like k.exe
            if (BoundArguments != null && BoundArguments.Count > 0)
            {
                var parts = new List<string>();
                foreach (var arg in BoundArguments)
                {
                    parts.Add(arg?.ToString() ?? "");
                }
                return OperatorName + "[" + string.Join(";", parts) + "]";
            }
            // Monadic projections (arity 1) should be displayed with a colon
            // Dyadic projections (arity 2) are displayed without colon
            return RequiredArguments == 1 ? OperatorName + ":" : OperatorName;
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
        public K3Value? BoundLeftArgument { get; }
        public FunctionValue? FuncValue { get; }

        public AdverbProjectedFunctionValue(string adverbName, string verb, int requiredArguments, K3Value? boundLeftArgument = null, FunctionValue? funcValue = null)
        {
            Type = ValueType.Function; // Treat as a function type
            AdverbName = adverbName;
            Verb = verb;
            RequiredArguments = requiredArguments;
            BoundLeftArgument = boundLeftArgument;
            FuncValue = funcValue;
        }

        public override string ToString()
        {
            return $"{AdverbName}({Verb})";
        }

        public override bool Equals(object? obj)
        {
            if (obj is AdverbProjectedFunctionValue other)
                return AdverbName == other.AdverbName && Verb == other.Verb && RequiredArguments == other.RequiredArguments &&
                       (BoundLeftArgument == null ? other.BoundLeftArgument == null : BoundLeftArgument.Equals(other.BoundLeftArgument));
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AdverbName, Verb, RequiredArguments, BoundLeftArgument);
        }
    }

    /// <summary>
    /// Represents a deferred take-projection: n#f where f is a function/projection.
    /// When applied to x, computes n#(f x) — used for patterns like 3#,: applied to a value.
    /// </summary>
    public class DeferredTakeProjection : K3Value
    {
        public IntegerValue Count { get; }
        public K3Value Func { get; }
        public Evaluator Evaluator { get; }

        public DeferredTakeProjection(IntegerValue count, K3Value func, Evaluator evaluator)
        {
            Count = count;
            Func = func;
            Evaluator = evaluator;
            Type = ValueType.Function;
        }

        public override string ToString() => $"{Count}#{Func}";
    }
}
