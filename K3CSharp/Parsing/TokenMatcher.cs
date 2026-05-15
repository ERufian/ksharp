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
    /// Specialized parser for token matching functionality
    /// Handles checking if current token matches expected type and advancing position
    /// </summary>
    public class TokenMatcher : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for token matching
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - Match is the main entry point
            throw new NotImplementedException("Use Match method instead");
        }

        /// <summary>
        /// Check if the current token matches the expected type and advance position if it does
        /// Returns true if matched and advanced, false otherwise
        /// </summary>
        public static bool Match(ParseContext context, TokenType type)
        {
            if (context.CurrentToken().Type == type)
            {
                context.Advance();
                return true;
            }
            return false;
        }
    }
}