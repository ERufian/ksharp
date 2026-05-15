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
    /// Arithmetic operator tokens (+, -, *, %, etc.)
    /// </summary>
    public class ArithmeticOperatorToken : OperatorToken
    {
        public ArithmeticOpType OpType { get; }

        public ArithmeticOperatorToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            OpType = tokenType switch
            {
                TokenType.PLUS => ArithmeticOpType.Add,
                TokenType.MINUS => ArithmeticOpType.Subtract,
                TokenType.MULTIPLY => ArithmeticOpType.Multiply,
                TokenType.DIVIDE => ArithmeticOpType.Divide,
                TokenType.MODULUS => ArithmeticOpType.Modulus,
                TokenType.POWER => ArithmeticOpType.Power,
                _ => throw new ArgumentException($"Invalid arithmetic operator: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.ArithmeticOperator;
        public override string Symbol => OpType switch
        {
            ArithmeticOpType.Add => "+",
            ArithmeticOpType.Subtract => "-",
            ArithmeticOpType.Multiply => "*",
            ArithmeticOpType.Divide => "%",
            ArithmeticOpType.Modulus => "!",
            ArithmeticOpType.Power => "^",
            _ => throw new ArgumentOutOfRangeException(nameof(OpType))
        };

        public override int Precedence => 7; // All arithmetic operators have same precedence in K
        public override bool IsRightAssociative => true; // K uses right-associative evaluation
    }

    /// <summary>
    /// Comparison operator tokens (<, >, =, etc.)
    /// </summary>
    public class ComparisonOperatorToken : OperatorToken
    {
        public ComparisonOpType OpType { get; }

        public ComparisonOperatorToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            OpType = tokenType switch
            {
                TokenType.LESS => ComparisonOpType.Less,
                TokenType.GREATER => ComparisonOpType.Greater,
                TokenType.EQUAL => ComparisonOpType.Equal,
                _ => throw new ArgumentException($"Invalid comparison operator: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.ComparisonOperator;
        public override string Symbol => OpType switch
        {
            ComparisonOpType.Less => "<",
            ComparisonOpType.Greater => ">",
            ComparisonOpType.Equal => "=",
            _ => throw new ArgumentOutOfRangeException(nameof(OpType))
        };

        public override int Precedence => 7; // All operators have same precedence in K
        public override bool IsRightAssociative => true;
    }

    /// <summary>
    /// Logical operator tokens (&, |, ~, etc.)
    /// </summary>
    public class LogicalOperatorToken : OperatorToken
    {
        public LogicalOpType OpType { get; }

        public LogicalOperatorToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            OpType = tokenType switch
            {
                TokenType.MIN => LogicalOpType.Min,
                TokenType.MAX => LogicalOpType.Max,
                TokenType.MATCH => LogicalOpType.Match,
                TokenType.NOT => LogicalOpType.Not,
                _ => throw new ArgumentException($"Invalid logical operator: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.LogicalOperator;
        public override string Symbol => OpType switch
        {
            LogicalOpType.Min => "&",
            LogicalOpType.Max => "|",
            LogicalOpType.Match => "~",
            LogicalOpType.Not => "~",
            _ => throw new ArgumentOutOfRangeException(nameof(OpType))
        };

        public override int Precedence => 7;
        public override bool IsRightAssociative => true;
    }

    /// <summary>
    /// Enumeration of arithmetic operations
    /// </summary>
    public enum ArithmeticOpType
    {
        Add, Subtract, Multiply, Divide, Modulus, Power
    }

    /// <summary>
    /// Enumeration of comparison operations
    /// </summary>
    public enum ComparisonOpType
    {
        Less, Greater, Equal
    }

    /// <summary>
    /// Enumeration of logical operations
    /// </summary>
    public enum LogicalOpType
    {
        Min, Max, Match, Not
    }
}