// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;

namespace K3CSharp
{
    /// <summary>
    /// Integer literal tokens
    /// </summary>
    public class IntegerToken : LiteralToken
    {
        public IntegerToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Literal;
        public override string Symbol => Lexeme;

        public override object GetValue()
        {
            // Handle special integer values per K specification
            if (Lexeme == "0I" || Lexeme == "-0I")
                return Lexeme;
            
            if (int.TryParse(Lexeme, out int intValue))
            {
                // Convert extreme values to special values per spec
                if (intValue >= 2147483647)
                    return "0I";
                return intValue;
            }
            
            throw new ArgumentException($"Invalid integer literal: {Lexeme}");
        }
    }

    /// <summary>
    /// Long integer literal tokens
    /// </summary>
    public class LongToken : LiteralToken
    {
        public LongToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Literal;
        public override string Symbol => Lexeme;

        public override object GetValue()
        {
            // Handle special long values per K specification
            if (Lexeme == "0Ij")
                return long.MaxValue;
            if (Lexeme == "-0Ij")
                return -long.MaxValue;
            if (Lexeme == "0Nj")
                return long.MinValue;
            
            // Parse with bounds checking
            var numberPart = Lexeme.Substring(0, Lexeme.Length - 1); // Remove 'j'
            double parsedValue = double.Parse(numberPart);
            
            if (parsedValue >= long.MaxValue)
                return long.MaxValue;
            else if (parsedValue <= -long.MaxValue)
                return -long.MaxValue;
            else
                return long.Parse(numberPart);
        }
    }

    /// <summary>
    /// Float literal tokens
    /// </summary>
    public class FloatToken : LiteralToken
    {
        public FloatToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Literal;
        public override string Symbol => Lexeme;

        public override object GetValue()
        {
            // Handle special float values
            if (Lexeme == "0i" || Lexeme == "0n" || Lexeme == "-0i")
                return Lexeme;
            
            return double.Parse(Lexeme);
        }
    }

    /// <summary>
    /// Character literal tokens
    /// </summary>
    public class CharacterToken : LiteralToken
    {
        public CharacterToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Literal;
        public override string Symbol => Lexeme;

        public override object GetValue()
        {
            // Handle escape sequences in quoted format (e.g., '\n', '\t')
            if (Lexeme.Length == 3 && Lexeme[0] == '\'' && Lexeme[2] == '\'')
            {
                char c = Lexeme[1];
                return c switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '\'' => '\'',
                    _ => c
                };
            }
            
            // Handle unquoted single character (e.g., "f", "a" from double-quoted strings)
            if (Lexeme.Length == 1)
            {
                return Lexeme[0];
            }
            
            throw new ArgumentException($"Invalid character literal: {Lexeme}");
        }
    }

    /// <summary>
    /// Symbol literal tokens
    /// </summary>
    public class SymbolToken : LiteralToken
    {
        public SymbolToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Literal;
        public override string Symbol => Lexeme;

        public override object GetValue()
        {
            // Remove backticks for symbol value
            return Lexeme.Trim('`');
        }
    }

    /// <summary>
    /// String literal tokens
    /// </summary>
    public class StringToken : LiteralToken
    {
        public StringToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Literal;
        public override string Symbol => Lexeme;

        public override object GetValue()
        {
            // Remove quotes and handle escape sequences
            if (Lexeme.Length >= 2 && Lexeme[0] == '"' && Lexeme[^1] == '"')
            {
                return Lexeme.Substring(1, Lexeme.Length - 2);
            }
            
            throw new ArgumentException($"Invalid string literal: {Lexeme}");
        }
    }

    /// <summary>
    /// Null value token
    /// </summary>
    public class NullToken : LiteralToken
    {
        public NullToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Literal;
        public override string Symbol => Lexeme;

        public override object GetValue()
        {
            return new NullValue();
        }
    }
}