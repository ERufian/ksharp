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
    /// Specialized parser for position advancement functionality
    /// Handles advancing the parser's current token position
    /// </summary>
    public class PositionAdvancer : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for position advancement
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - Advance is the main entry point
            throw new NotImplementedException("Use Advance method instead");
        }

        /// <summary>
        /// Advance the parser's current position by one
        /// Moves to the next token in the token stream
        /// </summary>
        public static void Advance(ParseContext context)
        {
            context.Current++;
        }
    }
}