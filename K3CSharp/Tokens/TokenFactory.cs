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
    /// Factory for creating appropriate token objects based on TokenType
    /// </summary>
    public static class TokenFactory
    {
        /// <summary>
        /// Create a typed token object based on the TokenType
        /// </summary>
        public static TokenBase CreateToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
        {
            return tokenType switch
            {
                // Literal tokens
                TokenType.INTEGER => new IntegerToken(tokenType, lexeme, position, line, column),
                TokenType.LONG => new LongToken(tokenType, lexeme, position, line, column),
                TokenType.FLOAT => new FloatToken(tokenType, lexeme, position, line, column),
                TokenType.CHARACTER => new CharacterToken(tokenType, lexeme, position, line, column),
                TokenType.CHARACTER_VECTOR => new StringToken(tokenType, lexeme, position, line, column),
                TokenType.SYMBOL => new SymbolToken(tokenType, lexeme, position, line, column),
                TokenType.NULL => new NullToken(tokenType, lexeme, position, line, column),

                // Arithmetic operators
                TokenType.PLUS => new ArithmeticOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.MINUS => new ArithmeticOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.MULTIPLY => new ArithmeticOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.DIVIDE => new ArithmeticOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.MODULUS => new ArithmeticOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.POWER => new ArithmeticOperatorToken(tokenType, lexeme, position, line, column),

                // Comparison operators
                TokenType.LESS => new ComparisonOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.GREATER => new ComparisonOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.EQUAL => new ComparisonOperatorToken(tokenType, lexeme, position, line, column),

                // Logical operators
                TokenType.MIN => new LogicalOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.MAX => new LogicalOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.MATCH => new LogicalOperatorToken(tokenType, lexeme, position, line, column),

                // Delimiters
                TokenType.LEFT_PAREN => new ParenthesisToken(tokenType, lexeme, position, line, column),
                TokenType.RIGHT_PAREN => new ParenthesisToken(tokenType, lexeme, position, line, column),
                TokenType.LEFT_BRACKET => new BracketToken(tokenType, lexeme, position, line, column),
                TokenType.RIGHT_BRACKET => new BracketToken(tokenType, lexeme, position, line, column),
                TokenType.LEFT_BRACE => new BraceToken(tokenType, lexeme, position, line, column),
                TokenType.RIGHT_BRACE => new BraceToken(tokenType, lexeme, position, line, column),
                TokenType.SEMICOLON => new SemicolonToken(tokenType, lexeme, position, line, column),
                TokenType.COLON => new ColonToken(tokenType, lexeme, position, line, column),
                TokenType.JOIN => new CommaToken(tokenType, lexeme, position, line, column),

                // Adverbs
                TokenType.ADVERB_SLASH => new K3AdverbToken(tokenType, lexeme, position, line, column),
                TokenType.ADVERB_BACKSLASH => new K3AdverbToken(tokenType, lexeme, position, line, column),
                TokenType.ADVERB_TICK => new K3AdverbToken(tokenType, lexeme, position, line, column),
                TokenType.ADVERB_SLASH_COLON => new K3AdverbToken(tokenType, lexeme, position, line, column),
                TokenType.ADVERB_BACKSLASH_COLON => new K3AdverbToken(tokenType, lexeme, position, line, column),
                TokenType.ADVERB_TICK_COLON => new K3AdverbToken(tokenType, lexeme, position, line, column),

                // Assignment operators
                TokenType.ASSIGNMENT => new AssignmentToken(tokenType, lexeme, position, line, column),
                TokenType.GLOBAL_ASSIGNMENT => new AssignmentToken(tokenType, lexeme, position, line, column),

                // Control flow
                TokenType.IF_FUNC => new ControlFlowToken(tokenType, lexeme, position, line, column),
                TokenType.WHILE => new ControlFlowToken(tokenType, lexeme, position, line, column),
                TokenType.DO => new ControlFlowToken(tokenType, lexeme, position, line, column),

                // Mathematical functions
                TokenType.SIN => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.COS => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.TAN => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.ASIN => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.ACOS => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.ATAN => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SINH => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.COSH => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.TANH => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.LOG => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.EXP => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.ABS => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SQR => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SQRT => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.FLOOR_MATH => new MathFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.CEIL => new MathFunctionToken(tokenType, lexeme, position, line, column),

                // System functions
                TokenType.BD => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DB => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.GETENV => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SETENV => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SIZE => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DRAW => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DIV => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.AND => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.OR => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.XOR => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.NOT => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.ROT => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SHIFT => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DOT_PRODUCT => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.MUL => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.INV => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.LSQ => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.IN => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.BIN => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.BINL => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.LIN => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DV => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DVL => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DI => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.VS => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SV => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SS => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.SM => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.CI => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.IC => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.GTIME => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.LTIME => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.LT => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.JD => new SystemFunctionToken(tokenType, lexeme, position, line, column),
                TokenType.DJ => new SystemFunctionToken(tokenType, lexeme, position, line, column),

                // I/O operators
                TokenType.IO_VERB_0 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_1 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_2 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_3 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_4 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_5 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_6 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_7 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_8 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.IO_VERB_9 => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.TYPE => new IOOperatorToken(tokenType, lexeme, position, line, column),
                TokenType.STRING_REPRESENTATION => new IOOperatorToken(tokenType, lexeme, position, line, column),

                // Special tokens
                TokenType.HASH => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.UNDERSCORE => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.QUESTION => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.DOLLAR => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.APPLY => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.DOT_APPLY => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.ATOM => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.NEWLINE => new SpecialToken(tokenType, lexeme, position, line, column),
                TokenType.EOF => new SpecialToken(tokenType, lexeme, position, line, column),

                // Default to generic token for any remaining types
                _ => new GenericToken(tokenType, lexeme, position, TokenCategory.Special, lexeme, line, column)
            };
        }

        /// <summary>
        /// Check if a TokenType represents a dyadic operator
        /// </summary>
        public static bool IsDyadicOperator(TokenType tokenType)
        {
            return VerbRegistry.IsDyadicOperator(tokenType);
        }

        /// <summary>
        /// Check if a TokenType represents a monadic operator
        /// </summary>
        public static bool IsMonadicOperator(TokenType tokenType)
        {
            return tokenType switch
            {
                // Arithmetic operators that can be monadic
                TokenType.MINUS or TokenType.MODULUS or TokenType.POWER or
                // Logical operators that can be monadic
                TokenType.MATCH or TokenType.NOT or
                // Special operators that are monadic
                TokenType.HASH or TokenType.UNDERSCORE or TokenType.QUESTION or
                TokenType.DOLLAR or TokenType.APPLY or TokenType.ATOM or
                // System functions that are monadic
                TokenType.BD or TokenType.DB or TokenType.GETENV or TokenType.SIZE or
                TokenType.DRAW or TokenType.NOT or TokenType.INV or
                TokenType.VS or TokenType.CI or TokenType.IC or
                TokenType.GTIME or TokenType.LTIME or TokenType.LT or TokenType.JD or TokenType.DJ or
                // I/O operators that can be monadic
                TokenType.IO_VERB_0 or TokenType.IO_VERB_1 or TokenType.IO_VERB_2 or TokenType.IO_VERB_3 or TokenType.TYPE or TokenType.STRING_REPRESENTATION or
                TokenType.IO_VERB_6 =>
                    true,
                _ => false
            };
        }

        /// <summary>
        /// Get the symbol representation for a TokenType
        /// </summary>
        public static string GetSymbol(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.PLUS => "+",
                TokenType.MINUS => "-",
                TokenType.MULTIPLY => "*",
                TokenType.DIVIDE => "%",
                TokenType.MODULUS => "!",
                TokenType.POWER => "^",
                TokenType.LESS => "<",
                TokenType.GREATER => ">",
                TokenType.EQUAL => "=",
                TokenType.MIN => "&",
                TokenType.MAX => "|",
                TokenType.MATCH => "~",
                TokenType.HASH => "#",
                TokenType.UNDERSCORE => "_",
                TokenType.QUESTION => "?",
                TokenType.DOLLAR => "$",
                TokenType.APPLY => "@",
                TokenType.DOT_APPLY => ".",
                TokenType.JOIN => ",",
                TokenType.COLON => ":",
                TokenType.ASSIGNMENT => ":",
                TokenType.GLOBAL_ASSIGNMENT => "::",
                _ => tokenType.ToString().ToLower()
            };
        }
    }
}