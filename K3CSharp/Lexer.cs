using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace K3CSharp
{
    public class Lexer
    {
        private string input;
        private int position;
        private char currentChar => position < input.Length ? input[position] : '\0';
        private List<Token>? tokens;
        private int current = 0;

        public Lexer(string input)
        {
            this.input = input;
            position = 0;
        }

        /// <summary>
        /// Preprocess token list to insert @ operators for implicit indexing/apply
        /// Pattern: )( followed by ( -> insert @ between them
        /// This handles the K language feature where consecutive parenthesized expressions
        /// imply implicit indexing (if left is a noun) or implicit apply (if left is a verb)
        /// </summary>
        public List<Token> PreprocessImplicitIndexing(List<Token> tokens)
        {
            var result = new List<Token>();
            
            for (int i = 0; i < tokens.Count; i++)
            {
                result.Add(tokens[i]);
                
                // Check if current token is RIGHT_PAREN and next token is LEFT_PAREN
                // Insert APPLY (@) between them for implicit indexing
                if (i < tokens.Count - 1 && 
                    tokens[i].Type == TokenType.RIGHT_PAREN && 
                    tokens[i + 1].Type == TokenType.LEFT_PAREN)
                {
                    var applyToken = new Token(TokenType.APPLY, "@", tokens[i].Position);
                    result.Add(applyToken);
                }
            }
            
            return result;
        }

        public Token? PeekNextToken()
        {
            if (null == tokens) return null;
            int nextIndex = current + 1;
            if (nextIndex >= tokens.Count) return new Token(TokenType.EOF, "", 0);
            return tokens[nextIndex];
        }

        public List<Token> Tokenize()
        {
            tokens = new List<Token>();
            current = 0;

            while (position < input.Length)
            {
                char c = currentChar;

                if (char.IsWhiteSpace(c))
                {
                    if (c == '\n' && tokens.Count > 0)  // Only add NEWLINE if we have previous tokens
                    {
                        tokens.Add(new Token(TokenType.NEWLINE, "\n", position));
                    }
                    Advance();
                    SkipWhitespace();
                }
                else if (c == '`')
                {
                    tokens.Add(ReadSymbol());
                }
                else if (c == '(')
                {
                    tokens.Add(new Token(TokenType.LEFT_PAREN, "(", position));
                    Advance();
                }
                else if (c == ')')
                {
                    tokens.Add(new Token(TokenType.RIGHT_PAREN, ")", position));
                    Advance();
                }
                else if (c == '[')
                {
                    tokens.Add(new Token(TokenType.LEFT_BRACKET, "[", position));
                    Advance();
                }
                else if (c == ']')
                {
                    tokens.Add(new Token(TokenType.RIGHT_BRACKET, "]", position));
                    Advance();
                }
                else if (c == '{')
                {
                    tokens.Add(new Token(TokenType.LEFT_BRACE, "{", position));
                    Advance();
                }
                else if (c == '}')
                {
                    tokens.Add(new Token(TokenType.RIGHT_BRACE, "}", position));
                    Advance();
                }
                else if (c == '+')
                {
                    tokens.Add(new Token(TokenType.PLUS, "+", position));
                    Advance();
                }
                else if (c == '-')
                {
                    // Check if this is a negative special value
                    if (position + 2 < input.Length && 
                        input[position + 1] == '0' && 
                        (input[position + 2] == 'I' || input[position + 2] == 'i'))
                    {
                        if (position + 3 < input.Length && input[position + 3] == 'j')
                        {
                            // -0Ij
                            tokens.Add(new Token(TokenType.LONG, "-0Ij", position));
                            position += 4;
                        }
                        else
                        {
                            // -0I or -0i
                            string special = input.Substring(position, 3);
                            if (input[position + 2] == 'i')
                                tokens.Add(new Token(TokenType.FLOAT, special, position));
                            else
                                tokens.Add(new Token(TokenType.INTEGER, special, position));
                            position += 3;
                        }
                    }
                    else if (position + 1 < input.Length && char.IsDigit(input[position + 1]))
                    {
                        // Determine if '-' is a binary minus or the start of a negative literal.
                        // '-' is a binary minus operator when the immediately preceding character
                        // (no whitespace gap) is a value-producing token: ), ], }, alphanumeric, _.
                        // With any whitespace gap, '-' starts a new negative literal.
                        bool preceded_directly_by_value = position > 0 &&
                            (input[position - 1] == ')' || input[position - 1] == ']' || input[position - 1] == '}' ||
                             char.IsLetterOrDigit(input[position - 1]) || input[position - 1] == '_');
                        if (preceded_directly_by_value)
                        {
                            // '-' is a binary minus operator, not part of a negative literal
                            tokens.Add(new Token(TokenType.MINUS, "-", position));
                            Advance();
                        }
                        else
                        {
                            // Negative number like -2147483648
                            tokens.Add(ReadNumber());
                        }
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.MINUS, "-", position));
                        Advance();
                    }
                }
                else if (c == '*')
                {
                    tokens.Add(new Token(TokenType.MULTIPLY, "*", position));
                    Advance();
                }
                else if (c == '%')
                {
                    tokens.Add(new Token(TokenType.DIVIDE, "%", position));
                    Advance();
                }
                else if (c == '_')
                {
                    // Check if underscore is part of a name (identifier or symbol)
                    // Look ahead to see if this could be part of a name
                    if (position > 0 && 
                        (char.IsLetterOrDigit(input[position - 1]) || input[position - 1] == '_' || input[position - 1] == '.') &&
                        position + 1 < input.Length && 
                        (char.IsLetterOrDigit(input[position + 1]) || input[position + 1] == '_'))
                    {
                        // Underscore follows alphanumeric/dot - could be part of identifier
                        // or a system function (e.g., 2_sv). Try system function first.
                        var systemFunc = ReadSystemFunction();
                        if (systemFunc != null)
                        {
                            tokens.Add(systemFunc);
                        }
                        else
                        {
                            tokens.Add(ReadIdentifier());
                        }
                    }
                    else
                    {
                        // Check for _gethint first (greedy precedence)
                        if (position + 7 < input.Length && input.Substring(position, 8) == "_gethint")
                        {
                            tokens.Add(new Token(TokenType.GETHINT, "_gethint", position));
                            Advance(); // Skip _
                            for (int i = 0; i < 7; i++) Advance(); // Skip "gethint"
                        }
                        // Check for _sethint next (greedy precedence)
                        else if (position + 7 < input.Length && input.Substring(position, 8) == "_sethint")
                        {
                            tokens.Add(new Token(TokenType.SETHINT, "_sethint", position));
                            Advance(); // Skip _
                            for (int i = 0; i < 7; i++) Advance(); // Skip "sethint"
                        }
                        // Check for _parse next (greedy precedence)
                        else if (position + 5 < input.Length && input.Substring(position, 6) == "_parse")
                        {
                            tokens.Add(new Token(TokenType.PARSE, "_parse", position));
                            Advance(); // Skip _
                            for (int i = 0; i < 5; i++) Advance(); // Skip "parse"
                        }
                        // Check for _eval next (greedy precedence)
                        else if (position + 4 < input.Length && input.Substring(position, 5) == "_eval")
                        {
                            tokens.Add(new Token(TokenType.EVAL, "_eval", position));
                            Advance(); // Skip _
                            for (int i = 0; i < 4; i++) Advance(); // Skip "eval"
                        }
                        // Check for _n null value (only if not followed by letters)
                        else if (position + 1 < input.Length && input[position + 1] == 'n')
                        {
                            // Check if this is just _n (not followed by more letters)
                            bool isJustN = (position + 2 >= input.Length) || !char.IsLetter(input[position + 2]);
                            
                            if (isJustN)
                            {
                                tokens.Add(new Token(TokenType.NULL, "_n", position));
                                Advance(); // Skip _
                                Advance(); // Skip n
                            }
                            else
                            {
                                // This is _n followed by letters, let ReadSystemFunction handle it
                                var systemFunction = ReadSystemFunction();
                                if (systemFunction != null)
                                {
                                    tokens.Add(systemFunction);
                                }
                                else
                                {
                                    tokens.Add(new Token(TokenType.UNDERSCORE, "_", position));
                                }
                            }
                        }
                        else
                        {
                            // Check for system functions
                            var systemFunction = ReadSystemFunction();
                            if (systemFunction != null)
                            {
                                tokens.Add(systemFunction!);
                            }
                            else
                            {
                                tokens.Add(new Token(TokenType.UNDERSCORE, "_", position));
                                Advance();
                            }
                        }
                    }
                }
                else if (c == '/')
                {
                    // Check if this is a comment: / at start of line or after whitespace
                    // Note: // after a non-whitespace character (like +//) is two consecutive adverbs, not a comment
                    if (position == 0 || (position > 0 && char.IsWhiteSpace(input[position - 1])))
                    {
                        // / comment at start of line or after space - skip to end of line
                        while (position < input.Length && currentChar != '\n')
                        {
                            Advance();
                        }
                    }
                    else if (position + 1 < input.Length && input[position + 1] == ':')
                    {
                        // /: adverb (each-right)
                        tokens.Add(new Token(TokenType.ADVERB_SLASH_COLON, "/:", position));
                        Advance(); // Skip /
                        Advance(); // Skip :
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.ADVERB_SLASH, "/", position));
                        Advance();
                    }
                }
                else if (c == '"')
                {
                    tokens.Add(ReadString());
                }
                else if (c == '\\')
                {
                    if (position + 1 < input.Length && input[position + 1] == ':')
                    {
                        // \: adverb (each-left)
                        tokens.Add(new Token(TokenType.ADVERB_BACKSLASH_COLON, "\\:", position));
                        Advance(); // Skip \
                        Advance(); // Skip :
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.ADVERB_BACKSLASH, "\\", position));
                        Advance();
                    }
                }
                else if (c == '\'')
                {
                    if (position + 1 < input.Length && input[position + 1] == ':')
                    {
                        // ': adverb (each-prior)
                        tokens.Add(new Token(TokenType.ADVERB_TICK_COLON, "':", position));
                        Advance(); // Skip '
                        Advance(); // Skip :
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.ADVERB_TICK, "'", position));
                        Advance();
                    }
                }
                else if (c == '&')
                {
                    tokens.Add(new Token(TokenType.MIN, "&", position));
                    Advance();
                }
                else if (c == '|')
                {
                    tokens.Add(new Token(TokenType.MAX, "|", position));
                    Advance();
                }
                else if (c == '<')
                {
                    tokens.Add(new Token(TokenType.LESS, "<", position));
                    Advance();
                }
                else if (c == '>')
                {
                    tokens.Add(new Token(TokenType.GREATER, ">", position));
                    Advance();
                }
                else if (c == '=')
                {
                    tokens.Add(new Token(TokenType.EQUAL, "=", position));
                    Advance();
                }
                else if (c == '^')
                {
                    tokens.Add(new Token(TokenType.POWER, "^", position));
                    Advance();
                }
                else if (c == '!')
                {
                    tokens.Add(new Token(TokenType.MODULUS, "!", position));
                    Advance();
                }
                else if (c == '~')
                {
                    tokens.Add(new Token(TokenType.MATCH, "~", position));
                    Advance();
                }
                else if (c == ',')
                {
                    tokens.Add(new Token(TokenType.JOIN, ",", position));
                    Advance();
                }
                else if (c == '#')
                {
                    tokens.Add(new Token(TokenType.HASH, "#", position));
                    Advance();
                }
                else if (char.IsLetter(c))
                {
                    tokens.Add(ReadIdentifier());
                }
                else if (c == '?')
                {
                    tokens.Add(new Token(TokenType.QUESTION, "?", position));
                    Advance();
                }
                else if (c == '@')
                {
                    // Let parser disambiguate between monadic ATOM and binary APPLY
                    tokens.Add(new Token(TokenType.APPLY, "@", position));
                    Advance();
                }
                else if (c == '$')
                {
                    tokens.Add(new Token(TokenType.DOLLAR, "$", position));
                    Advance();
                }
                else if (c == '.')
                {
                    // Check if this is a decimal number starting with '.' (e.g., .5)
                    // If followed by a digit, parse it as a number
                    if (position + 1 < input.Length && char.IsDigit(input[position + 1]))
                    {
                        tokens.Add(ReadNumber());
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.DOT_APPLY, ".", position));
                        Advance();
                    }
                }
                else if (c == ':')
                {
                    // Check for digit-colon operators (0: through 9:)
                    if (position > 0 && char.IsDigit(input[position - 1]) && tokens.Count > 0 && tokens[tokens.Count - 1].Type == TokenType.INTEGER)
                    {
                        char digitChar = input[position - 1];
                        int digit = digitChar - '0';
                        
                        // Replace the previous token with appropriate I/O operator
                        tokens.RemoveAt(tokens.Count - 1);
                        
                        switch (digit)
                        {
                            case 0:
                                // Check if this is an I/O verb context or type context
                                // For now, assume it's always I/O verb - we'll handle type in evaluator
                                tokens.Add(new Token(TokenType.IO_VERB_0, "0:", position - 1));
                                break;
                            case 1:
                                tokens.Add(new Token(TokenType.IO_VERB_1, "1:", position - 1));
                                break;
                            case 2:
                                tokens.Add(new Token(TokenType.IO_VERB_2, "2:", position - 1));
                                break;
                            case 3:
                                tokens.Add(new Token(TokenType.IO_VERB_3, "3:", position - 1));
                                break;
                            case 4:
                                tokens.Add(new Token(TokenType.IO_VERB_4, "4:", position - 1));
                                break;
                            case 5:
                                tokens.Add(new Token(TokenType.IO_VERB_5, "5:", position - 1));
                                break;
                            case 6:
                                tokens.Add(new Token(TokenType.IO_VERB_6, "6:", position - 1));
                                break;
                            case 7:
                                tokens.Add(new Token(TokenType.IO_VERB_7, "7:", position - 1));
                                break;
                            case 8:
                                tokens.Add(new Token(TokenType.IO_VERB_8, "8:", position - 1));
                                break;
                            case 9:
                                tokens.Add(new Token(TokenType.IO_VERB_9, "9:", position - 1));
                                break;
                            default:
                                throw new Exception($"Unsupported I/O verb digit: {digit}");
                        }
                        Advance();
                    }
                    // Check for :: global assignment operator
                    else if (position + 1 < input.Length && input[position + 1] == ':')
                    {
                        tokens.Add(new Token(TokenType.GLOBAL_ASSIGNMENT, "::", position));
                        Advance();
                        Advance();
                    }
                    // Always treat colon as separate token - let parser handle disambiguation based on context
                    else if (position > 0 && IsOperatorChar(input[position - 1]) && input[position] == ':')
                    {
                        // Always add colon as separate token
                        tokens.Add(new Token(TokenType.COLON, ":", position));
                        Advance();
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.COLON, ":", position));
                        Advance();
                    }
                }
                else if (c == ';')
                {
                    tokens.Add(new Token(TokenType.SEMICOLON, ";", position));
                    Advance();
                }
                else if (c == '"')
                {
                    tokens.Add(ReadString());
                }
                else if (c == '`')
                {
                    tokens.Add(ReadSymbol());
                }
                else if (char.IsDigit(c))
                {
                    tokens.Add(ReadNumber());
                }
                else
                {
                    throw new Exception($"Unexpected character: {c} at position {position}");
                }
            }

            tokens.Add(new Token(TokenType.EOF, "", position));
            return tokens;
        }

        private Token ReadString()
        {
            int start = position;
            Advance(); // Skip opening quote
            
            string value = "";
            while (currentChar != '"' && currentChar != '\0')
            {
                if (currentChar == '\\')
                {
                    Advance(); // Skip backslash
                    if (currentChar == '\0')
                    {
                        // End of input after backslash - treat backslash as literal
                        value += '\\';
                        break;
                    }
                    
                    // Handle escape sequences
                    if (currentChar >= '0' && currentChar <= '7')
                    {
                        // Octal escape sequence - read up to 3 octal digits
                        string octalDigits = currentChar.ToString();
                        Advance();
                        
                        // Read up to 2 more octal digits
                        for (int i = 0; i < 2 && currentChar >= '0' && currentChar <= '7'; i++)
                        {
                            octalDigits += currentChar;
                            Advance();
                        }
                        
                        // Convert octal to character
                        try
                        {
                            char octalChar = (char)Convert.ToInt32(octalDigits, 8);
                            value += octalChar;
                        }
                        catch
                        {
                            // Invalid octal - treat as literal characters
                            value += "\\" + octalDigits;
                        }
                    }
                    else
                    {
                        switch (currentChar)
                        {
                            case '"':
                                value += '"';
                                break;
                            case '\\':
                                value += '\\';
                                break;
                            case 'b':
                                value += '\b';
                                break;
                            case 'n':
                                value += '\n';
                                break;
                            case 't':
                                value += '\t';
                                break;
                            case 'r':
                                value += '\r';
                                break;
                            default:
                                // Any other character after backslash - treat as character itself
                                value += currentChar;
                                break;
                        }
                        Advance();
                    }
                }
                else if (currentChar == '\r')
                {
                    // Per speclet: \r\n combinations are converted to \n inside strings
                    Advance();
                    if (currentChar == '\n')
                    {
                        value += '\n';
                        Advance();
                    }
                    // standalone \r: ignore (omit from value)
                }
                else
                {
                    value += currentChar;
                    Advance();
                }
            }
            
            if (currentChar == '"')
            {
                Advance(); // Skip closing quote
                
                // Return CHARACTER token for single-character strings, CHARACTER_VECTOR otherwise
                if (value.Length == 1)
                {
                    return new Token(TokenType.CHARACTER, value, start);
                }
                else
                {
                    return new Token(TokenType.CHARACTER_VECTOR, value, start);
                }
            }
            else
            {
                throw new Exception("Unterminated string literal");
            }
        }
        
        private Token ReadSymbol()
        {
            int start = position;
            Advance(); // Skip backtick
            
            // Handle quoted symbols
            if (currentChar == '"')
            {
                Advance(); // Skip opening quote
                string quotedValue = "";
                while (currentChar != '"' && currentChar != '\0')
                {
                    if (currentChar == '\\')
                    {
                        Advance(); // Skip backslash
                        if (currentChar == '\0')
                        {
                            // End of input after backslash - treat backslash as literal
                            quotedValue += '\\';
                            break;
                        }
                        
                        // Handle escape sequences in symbols
                        if (currentChar >= '0' && currentChar <= '7')
                        {
                            // Octal escape sequence - read up to 3 octal digits
                            string octalDigits = currentChar.ToString();
                            Advance();
                            
                            // Read up to 2 more octal digits
                            for (int i = 0; i < 2 && currentChar >= '0' && currentChar <= '7'; i++)
                            {
                                octalDigits += currentChar;
                                Advance();
                            }
                            
                            // Convert octal to character
                            try
                            {
                                char octalChar = (char)Convert.ToInt32(octalDigits, 8);
                                quotedValue += octalChar;
                            }
                            catch
                            {
                                // Invalid octal - treat as literal characters
                                quotedValue += "\\" + octalDigits;
                            }
                        }
                        else
                        {
                            switch (currentChar)
                            {
                                case '"':
                                    quotedValue += '"';
                                    break;
                                case '\\':
                                    quotedValue += '\\';
                                    break;
                                case 'b':
                                    quotedValue += '\b';
                                    break;
                                case 'n':
                                    quotedValue += '\n';
                                    break;
                                case 't':
                                    quotedValue += '\t';
                                    break;
                                case 'r':
                                    quotedValue += '\r';
                                    break;
                                default:
                                    // Any other character after backslash - treat as character itself
                                    quotedValue += currentChar;
                                    break;
                            }
                            Advance();
                        }
                    }
                    else
                    {
                        quotedValue += currentChar;
                        Advance();
                    }
                }
                
                if (currentChar == '"')
                {
                    Advance(); // Skip closing quote
                }
                
                return new Token(TokenType.SYMBOL, quotedValue, start);
            }
            else
            {
                // Regular symbol
                string value = "";
                if (currentChar == '.')
                {
                    value += currentChar;
                    Advance();
                }
                while (currentChar != '\0' && (char.IsLetterOrDigit(currentChar) || currentChar == '_' || currentChar == '.') && currentChar != '`')
                {
                    value += currentChar;
                    Advance();
                }
                return new Token(TokenType.SYMBOL, value, start);
            }
        }
        
        private Token ReadIdentifier()
        {
            int start = position;
            string value = "";
            
            while (currentChar != '\0' && (char.IsLetterOrDigit(currentChar) || currentChar == '_'))
            {
                value += currentChar;
                Advance();
            }
            
            // Check for control flow keywords
            switch (value)
            {
                case "do":
                    return new Token(TokenType.DO, value, start);
                case "if":
                    return new Token(TokenType.IF_FUNC, value, start);
                case "while":
                    return new Token(TokenType.WHILE, value, start);
                default:
                    return new Token(TokenType.IDENTIFIER, value, start);
            }
        }

        private bool IsOperatorChar(char c)
        {
            return c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '^' || c == '!' || 
                   c == '<' || c == '>' || c == '=' || c == ',' || c == '&' || c == '|' || c == '#' || 
                   c == '_' || c == '?' || c == '$' || c == '@';
        }

        private Token ReadNumber()
        {
            int start = position;
            string number = "";
            bool hasDecimal = false;
            bool hasExponent = false;
            
            // Handle negative numbers FIRST (including special values)
            if (currentChar == '-' && position + 1 < input.Length && char.IsDigit(input[position + 1]))
            {
                number += currentChar;
                Advance();
            }
            
            // Handle negative special values like -0I, -0i
            if (number == "-" && position + 1 < input.Length && input[position] == '0' && 
                position + 2 < input.Length && (input[position + 1] == 'I' || input[position + 1] == 'i'))
            {
                if (position + 3 < input.Length && input[position + 2] == 'j')
                {
                    // -0Ij
                    number += "0Ij";
                    position += 3;
                    var token = new Token(TokenType.LONG, number, start);
                    return token;
                }
                else
                {
                    // -0I or -0i
                    string special = input.Substring(position, 2);
                    number += special;
                    position += 2;
                    if (input[position - 1] == 'i')
                    {
                        var token = new Token(TokenType.FLOAT, number, start);
                        return token;
                    }
                    else
                    {
                        var token = new Token(TokenType.INTEGER, number, start);
                        return token;
                    }
                }
            }
            
            // Check for special values starting with 0
            if (currentChar == '0' && position + 1 < input.Length)
            {
                char nextChar = input[position + 1];
                if (nextChar == 'I' || nextChar == 'N' || nextChar == 'i' || nextChar == 'n')
                {
                    // Handle special values
                    if (position + 2 < input.Length && input[position + 2] == 'j')
                    {
                        // Long special values: 0Ij, 0Nj, -0Ij
                        string special = input.Substring(position, 3);
                        position += 3;
                        return new Token(TokenType.LONG, special, start);
                    }
                    else
                    {
                        // Integer or float special values: 0I, 0N, 0i, 0n
                        string special = input.Substring(position, 2);
                        position += 2;
                        if (nextChar == 'i' || nextChar == 'n')
                            return new Token(TokenType.FLOAT, special, start);
                        else
                            return new Token(TokenType.INTEGER, special, start);
                    }
                }
            }
            
            while (position < input.Length && (char.IsDigit(currentChar) || currentChar == '.' || currentChar == 'e' || currentChar == 'E'))
            {
                if (currentChar == '.')
                {
                    if (hasDecimal) break;
                    hasDecimal = true;
                }
                else if (currentChar == 'e' || currentChar == 'E')
                {
                    if (hasExponent) break;
                    hasExponent = true;
                    number += currentChar;
                    Advance();
                    if (currentChar == '+' || currentChar == '-')
                    {
                        number += currentChar;
                        Advance();
                    }
                    continue;
                }
                
                number += currentChar;
                Advance();
            }
            
            // Check for j suffix for long integers
            if (currentChar == 'j' && !hasDecimal && !hasExponent)
            {
                number += currentChar;
                Advance();
                return new Token(TokenType.LONG, number, start);
            }
            
            if (hasDecimal || hasExponent)
            {
                return new Token(TokenType.FLOAT, number, start);
            }
            
            return new Token(TokenType.INTEGER, number, start);
        }

        private Token? ReadSystemFunction()
        {
            int start = position;
            
            if (currentChar != '_')
                return null;
                
            Advance(); // Skip _
            
            if (position >= input.Length)
            {
                position = start; // Reset position
                return null;
            }
            
            // Only proceed if next character is a letter (for valid math operations)
            if (!char.IsLetter(currentChar))
            {
                position = start; // Reset position
                return null;
            }
            
            string opName = "_" + currentChar;
            int charCount = 1;
            Advance();
            
            // Check for multi-character operations
            if (position < input.Length && char.IsLetter(currentChar))
            {
                while (position < input.Length && char.IsLetter(currentChar))
                {
                    opName += currentChar;
                    charCount++;
                    Advance();
                }
            }
            
            // Map operation names to tokens
            Token? result = opName switch
            {
                // Mathematical floating point operations
                "_log" => new Token(TokenType.LOG, opName, start),
                "_exp" => new Token(TokenType.EXP, opName, start),
                "_abs" => new Token(TokenType.ABS, opName, start),
                "_sqr" => new Token(TokenType.SQR, opName, start),
                "_sqrt" => new Token(TokenType.SQRT, opName, start),
                "_floor" => new Token(TokenType.FLOOR_MATH, opName, start),
                "_sin" => new Token(TokenType.SIN, opName, start),
                "_cos" => new Token(TokenType.COS, opName, start),
                "_tan" => new Token(TokenType.TAN, opName, start),
                "_asin" => new Token(TokenType.ASIN, opName, start),
                "_acos" => new Token(TokenType.ACOS, opName, start),
                "_atan" => new Token(TokenType.ATAN, opName, start),
                "_sinh" => new Token(TokenType.SINH, opName, start),
                "_cosh" => new Token(TokenType.COSH, opName, start),
                "_tanh" => new Token(TokenType.TANH, opName, start),
                
                // Linear algebra operations
                "_dot" => new Token(TokenType.DOT_PRODUCT, opName, start),
                "_mul" => new Token(TokenType.MUL, opName, start),
                "_inv" => new Token(TokenType.INV, opName, start),
                
                // System functions
                "_draw" => new Token(TokenType.DRAW, opName, start),
                "_getenv" => new Token(TokenType.GETENV, opName, start),
                "_setenv" => new Token(TokenType.SETENV, opName, start),
                "_size" => new Token(TokenType.SIZE, opName, start),
                "_dispose" => new Token(TokenType.DISPOSE, opName, start),
                "_bd" => new Token(TokenType.BD, opName, start),
                "_db" => new Token(TokenType.DB, opName, start),
                "_in" => new Token(TokenType.IN, opName, start),
                "_binl" => new Token(TokenType.BINL, opName, start),
                "_bin" => new Token(TokenType.BIN, opName, start),
                "_lsq" => new Token(TokenType.LSQ, opName, start),
                "_lin" => new Token(TokenType.LIN, opName, start),
                "_ceil" => new Token(TokenType.CEIL, opName, start),
                "_gtime" => new Token(TokenType.GTIME, opName, start),
                "_ltime" => new Token(TokenType.LTIME, opName, start),
                "_lt" => new Token(TokenType.LT, opName, start),
                "_jd" => new Token(TokenType.JD, opName, start),
                "_dj" => new Token(TokenType.DJ, opName, start),
                "_vs" => new Token(TokenType.VS, opName, start),
                "_sv" => new Token(TokenType.SV, opName, start),
                "_ss" => new Token(TokenType.SS, opName, start),
                "_ssr" => new Token(TokenType.SSR, opName, start),
                "_sm" => new Token(TokenType.SM, opName, start),
                "_ci" => new Token(TokenType.CI, opName, start),
                "_ic" => new Token(TokenType.IC, opName, start),
                "_dv" => new Token(TokenType.DV, opName, start),
                "_dvl" => new Token(TokenType.DVL, opName, start),
                "_di" => new Token(TokenType.DI, opName, start),
                "_exit" => new Token(TokenType.EXIT, opName, start),
                "_host" => new Token(TokenType.HOST_DNS, opName, start),
                "_parse" => new Token(TokenType.PARSE, opName, start),
                "_eval" => new Token(TokenType.EVAL, opName, start),
                "_gethint" => new Token(TokenType.GETHINT, opName, start),
                "_sethint" => new Token(TokenType.SETHINT, opName, start),
                
                // Integer and bitwise operations

                "_div" => new Token(TokenType.DIV, opName, start),
                "_and" => new Token(TokenType.AND, opName, start),
                "_or" => new Token(TokenType.OR, opName, start),
                "_xor" => new Token(TokenType.XOR, opName, start),
                "_not" => new Token(TokenType.NOT, opName, start),
                "_rot" => new Token(TokenType.ROT, opName, start),
                "_shift" => new Token(TokenType.SHIFT, opName, start),

                // Control flow functions without underscores (K3 syntax)
                "do" => new Token(TokenType.DO, opName, start),
                "while" => new Token(TokenType.WHILE, opName, start),
                "if" => new Token(TokenType.IF_FUNC, opName, start),

                // Niladic system variables
                "_d" => new Token(TokenType.DIRECTORY, opName, start),
                "_t" => new Token(TokenType.TIME, opName, start),
                "_T" => new Token(TokenType.DAYS, opName, start),
                "_v" => new Token(TokenType.VARIABLE, opName, start),
                "_i" => new Token(TokenType.INDEX, opName, start),
                "_f" => new Token(TokenType.FUNCTION, opName, start),
                "_s" => new Token(TokenType.SPACE, opName, start),
                "_h" => new Token(TokenType.HOST, opName, start),
                "_p" => new Token(TokenType.PORT, opName, start),
                "_P" => new Token(TokenType.PID, opName, start),
                "_w" => new Token(TokenType.WHO, opName, start),
                "_u" => new Token(TokenType.USER, opName, start),
                "_a" => new Token(TokenType.ADDRESS, opName, start),
                "_k" => new Token(TokenType.VERSION, opName, start),
                "_o" => new Token(TokenType.OS, opName, start),
                "_c" => new Token(TokenType.CORES, opName, start),
                "_r" => new Token(TokenType.RAM, opName, start),
                "_m" => new Token(TokenType.MACHID, opName, start),
                "_y" => new Token(TokenType.STACK, opName, start),
                                                
                _ => null
            };
            
            // If no valid operation found, reset position
            if (result == null)
            {
                position = start;
            }
            
            return result;
        }

        private void SkipWhitespace()
        {
            while (position < input.Length && char.IsWhiteSpace(currentChar) && currentChar != '\n')
            {
                Advance();
            }
        }

        private void Advance()
        {
            position++;
        }
    }
}
