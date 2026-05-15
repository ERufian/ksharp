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
    /// Specialized parser for token lexeme reconstruction
    /// Handles reconstructing token source representations with proper delimiter handling
    /// </summary>
    public class TokenLexemeReconstructor : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for token lexeme reconstruction
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - ReconstructTokenLexeme is the main entry point
            throw new NotImplementedException("Use ReconstructTokenLexeme method instead");
        }

        /// <summary>
        /// Reconstruct a token's source representation including delimiters that the lexer strips
        /// CHARACTER and CHARACTER_VECTOR tokens lose their surrounding quotes,
        /// and SYMBOL tokens lose their leading backtick.
        /// </summary>
        public static string ReconstructTokenLexeme(Token token)
        {
            switch (token.Type)
            {
                case TokenType.CHARACTER:
                case TokenType.CHARACTER_VECTOR:
                    // Re-add surrounding double quotes, escaping any embedded quotes or backslashes
                    return "\"" + token.Lexeme.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                case TokenType.SYMBOL:
                    // Re-add the leading backtick
                    return "`" + token.Lexeme;
                default:
                    return token.Lexeme;
            }
        }
    }
}