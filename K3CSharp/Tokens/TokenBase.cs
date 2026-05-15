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
    /// Base class for all token types with common properties and behavior
    /// </summary>
    public abstract class TokenBase
    {
        public TokenType TokenType { get; protected set; }
        public string Lexeme { get; protected set; }
        public int Position { get; protected set; }
        public int Line { get; protected set; }
        public int Column { get; protected set; }

        protected TokenBase(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
        {
            TokenType = tokenType;
            Lexeme = lexeme;
            Position = position;
            Line = line;
            Column = column;
        }

        public abstract TokenCategory Category { get; }
        public abstract string Symbol { get; }

        public override string ToString()
        {
            return $"{TokenType}:{Lexeme} at {Line}:{Column}";
        }
    }

    /// <summary>
    /// Categories for token classification and handler routing
    /// </summary>
    public enum TokenCategory
    {
        Literal,
        ArithmeticOperator,
        ComparisonOperator,
        LogicalOperator,
        MathFunction,
        SystemFunction,
        IOOperator,
        Delimiter,
        Adverb,
        Assignment,
        ControlFlow,
        Special
    }

    /// <summary>
    /// Base class for operator tokens (arithmetic, comparison, logical)
    /// </summary>
    public abstract class OperatorToken : TokenBase
    {
        protected OperatorToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public abstract int Precedence { get; }
        public abstract bool IsRightAssociative { get; }
    }

    /// <summary>
    /// Base class for literal value tokens (numbers, strings, symbols)
    /// </summary>
    public abstract class LiteralToken : TokenBase
    {
        protected LiteralToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public abstract object GetValue();
    }

    /// <summary>
    /// Base class for structural delimiter tokens (parentheses, brackets, braces)
    /// </summary>
    public abstract class DelimiterToken : TokenBase
    {
        protected DelimiterToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public abstract bool IsOpening { get; }
        public abstract TokenType MatchingClosing { get; }
    }

    /// <summary>
    /// Base class for function tokens (math, system, IO functions)
    /// </summary>
    public abstract class FunctionToken : TokenBase
    {
        protected FunctionToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public abstract int[] SupportedArities { get; }
        public abstract bool IsSystemVariable { get; }
    }

    /// <summary>
    /// Base class for adverb tokens (/, \, ', /:, \:, ':)
    /// </summary>
    public abstract class AdverbToken : TokenBase
    {
        protected AdverbToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public abstract AdverbType AdverbType { get; }
    }

    /// <summary>
    /// Types of adverbs for specialized handling
    /// </summary>
    public enum AdverbType
    {
        Over,           // /
        Scan,           // \
        Each,           // '
        EachRight,      // /:
        EachLeft,       // \:
        EachPrior       // ':
    }

    /// <summary>
    /// Generic token for types that don't need specialized behavior
    /// </summary>
    public class GenericToken : TokenBase
    {
        private readonly TokenCategory _category;
        private readonly string _symbol;

        public GenericToken(TokenType tokenType, string lexeme, int position, TokenCategory category, string symbol, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            _category = category;
            _symbol = symbol;
        }

        public override TokenCategory Category => _category;
        public override string Symbol => _symbol;
    }
}