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
    /// Parenthesis delimiter tokens ( ( ) )
    /// </summary>
    public class ParenthesisToken : DelimiterToken
    {
        public ParenthesisToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Delimiter;
        public override string Symbol => Lexeme;

        public override bool IsOpening => TokenType == TokenType.LEFT_PAREN;
        public override TokenType MatchingClosing => TokenType switch
        {
            TokenType.LEFT_PAREN => TokenType.RIGHT_PAREN,
            TokenType.RIGHT_PAREN => TokenType.LEFT_PAREN,
            _ => throw new ArgumentException($"Invalid parenthesis token: {TokenType}")
        };
    }

    /// <summary>
    /// Bracket delimiter tokens ( [ ] )
    /// </summary>
    public class BracketToken : DelimiterToken
    {
        public BracketToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Delimiter;
        public override string Symbol => Lexeme;

        public override bool IsOpening => TokenType == TokenType.LEFT_BRACKET;
        public override TokenType MatchingClosing => TokenType switch
        {
            TokenType.LEFT_BRACKET => TokenType.RIGHT_BRACKET,
            TokenType.RIGHT_BRACKET => TokenType.LEFT_BRACKET,
            _ => throw new ArgumentException($"Invalid bracket token: {TokenType}")
        };
    }

    /// <summary>
    /// Brace delimiter tokens ( { } )
    /// </summary>
    public class BraceToken : DelimiterToken
    {
        public BraceToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Delimiter;
        public override string Symbol => Lexeme;

        public override bool IsOpening => TokenType == TokenType.LEFT_BRACE;
        public override TokenType MatchingClosing => TokenType switch
        {
            TokenType.LEFT_BRACE => TokenType.RIGHT_BRACE,
            TokenType.RIGHT_BRACE => TokenType.LEFT_BRACE,
            _ => throw new ArgumentException($"Invalid brace token: {TokenType}")
        };
    }

    /// <summary>
    /// Semicolon delimiter token
    /// </summary>
    public class SemicolonToken : DelimiterToken
    {
        public SemicolonToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Delimiter;
        public override string Symbol => Lexeme;

        public override bool IsOpening => false; // Semicolons don't come in pairs
        public override TokenType MatchingClosing => TokenType.SEMICOLON;
    }

    /// <summary>
    /// Colon delimiter token
    /// </summary>
    public class ColonToken : DelimiterToken
    {
        public ColonToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Delimiter;
        public override string Symbol => Lexeme;

        public override bool IsOpening => false; // Colons don't come in pairs
        public override TokenType MatchingClosing => TokenType.COLON;
    }

    /// <summary>
    /// Comma delimiter token
    /// </summary>
    public class CommaToken : DelimiterToken
    {
        public CommaToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
        }

        public override TokenCategory Category => TokenCategory.Delimiter;
        public override string Symbol => Lexeme;

        public override bool IsOpening => false; // Commas don't come in pairs
        public override TokenType MatchingClosing => TokenType.JOIN;
    }
}