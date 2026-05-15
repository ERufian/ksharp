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
    /// Adverb tokens for K3 adverb operations
    /// </summary>
    public class K3AdverbToken : AdverbToken
    {
        public K3AdverbToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Adverb;
        public override string Symbol => Lexeme;

        public override AdverbType AdverbType => TokenType switch
        {
            TokenType.ADVERB_SLASH => AdverbType.Over,
            TokenType.ADVERB_BACKSLASH => AdverbType.Scan,
            TokenType.ADVERB_TICK => AdverbType.Each,
            TokenType.ADVERB_SLASH_COLON => AdverbType.EachRight,
            TokenType.ADVERB_BACKSLASH_COLON => AdverbType.EachLeft,
            TokenType.ADVERB_TICK_COLON => AdverbType.EachPrior,
            _ => throw new ArgumentException($"Invalid adverb token: {TokenType}")
        };
    }

    /// <summary>
    /// Assignment operator tokens (:=, :, etc.)
    /// </summary>
    public class AssignmentToken : OperatorToken
    {
        public AssignmentType AssignType { get; }

        public AssignmentToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            AssignType = tokenType switch
            {
                TokenType.ASSIGNMENT => AssignmentType.Simple,
                TokenType.GLOBAL_ASSIGNMENT => AssignmentType.Global,
                _ => throw new ArgumentException($"Invalid assignment operator: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.Assignment;
        public override string Symbol => AssignType switch
        {
            AssignmentType.Simple => ":",
            AssignmentType.Global => "::",
            _ => throw new ArgumentOutOfRangeException(nameof(AssignType))
        };

        public override int Precedence => 1; // Assignment has lowest precedence
        public override bool IsRightAssociative => true;
    }

    /// <summary>
    /// Control flow tokens (if, while, do)
    /// </summary>
    public class ControlFlowToken : FunctionToken
    {
        public ControlFlowType FlowType { get; }

        public ControlFlowToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            FlowType = tokenType switch
            {
                TokenType.IF_FUNC => ControlFlowType.If,
                TokenType.WHILE => ControlFlowType.While,
                TokenType.DO => ControlFlowType.Do,
                _ => throw new ArgumentException($"Invalid control flow token: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.ControlFlow;
        public override string Symbol => FlowType switch
        {
            ControlFlowType.If => "if",
            ControlFlowType.While => "while",
            ControlFlowType.Do => "do",
            _ => throw new ArgumentOutOfRangeException(nameof(FlowType))
        };

        public override int[] SupportedArities => FlowType switch
        {
            ControlFlowType.If => new[] { 2, 3 }, // if[condition;true_expr] or if[condition;true_expr;false_expr]
            ControlFlowType.While => new[] { 2 }, // while[condition;body]
            ControlFlowType.Do => new[] { 1 }, // do[body]
            _ => new[] { 1 }
        };

        public override bool IsSystemVariable => false;
    }

    /// <summary>
    /// Special tokens with unique behavior
    /// </summary>
    public class SpecialToken : TokenBase
    {
        public SpecialType SpecialType { get; }

        public SpecialToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            SpecialType = tokenType switch
            {
                TokenType.HASH => SpecialType.Count,
                TokenType.UNDERSCORE => SpecialType.Floor,
                TokenType.QUESTION => SpecialType.Unique,
                TokenType.DOLLAR => SpecialType.Format,
                TokenType.APPLY => SpecialType.Apply,
                TokenType.DOT_APPLY => SpecialType.DotApply,
                TokenType.ATOM => SpecialType.Atom,
                TokenType.NEWLINE => SpecialType.Newline,
                TokenType.EOF => SpecialType.Eof,
                _ => throw new ArgumentException($"Invalid special token: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.Special;
        public override string Symbol => SpecialType switch
        {
            SpecialType.Count => "#",
            SpecialType.Floor => "_",
            SpecialType.Unique => "?",
            SpecialType.Format => "$",
            SpecialType.Apply => "@",
            SpecialType.DotApply => ".",
            SpecialType.Atom => "@",
            SpecialType.Newline => "\n",
            SpecialType.Eof => "EOF",
            _ => throw new ArgumentOutOfRangeException(nameof(SpecialType))
        };
    }

    /// <summary>
    /// Enumerations for special token types
    /// </summary>
    public enum AssignmentType
    {
        Simple, Global
    }

    public enum ControlFlowType
    {
        If, While, Do
    }

    public enum SpecialType
    {
        Count, Floor, Unique, Format, Apply, DotApply, Atom,
        Newline, Eof
    }
}