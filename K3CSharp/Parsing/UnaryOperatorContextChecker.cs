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
    /// Specialized parser for monadic operator context checking
    /// Handles checking if the parser is in a monadic operator context
    /// </summary>
    public class MonadicOperatorContextChecker : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for monadic operator context checking
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - IsMonadicOperatorContext is the main entry point
            throw new NotImplementedException("Use IsMonadicOperatorContext method instead");
        }

        /// <summary>
        /// Check if we're at the start of an expression or after a binary operator
        /// This is a simplified check - in a full implementation we'd need more sophisticated parsing
        /// </summary>
        public static bool IsMonadicOperatorContext(ParseContext context)
        {
            if (context.IsAtEnd()) return false;
            
            var currentToken = context.CurrentToken();
            return currentToken.Type == TokenType.EOF ||
                   currentToken.Type == TokenType.SEMICOLON ||
                   currentToken.Type == TokenType.NEWLINE ||
                   currentToken.Type == TokenType.RIGHT_PAREN ||
                   currentToken.Type == TokenType.RIGHT_BRACE ||
                   currentToken.Type == TokenType.RIGHT_BRACKET ||
                   currentToken.Type == TokenType.ASSIGNMENT;
        }
    }
}