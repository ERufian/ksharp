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
    /// Specialized parser for backtracking functionality
    /// Handles parser position backtracking for error recovery
    /// </summary>
    public class BacktrackHandler : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for backtracking
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - Backtrack is the main entry point
            throw new NotImplementedException("Use Backtrack method instead");
        }

        /// <summary>
        /// Handle parser position backtracking for error recovery
        /// Moves the current position back if we're not at the beginning
        /// </summary>
        public static void Backtrack(ParseContext context)
        {
            // Move the current position back if we're not at the beginning
            if (context.Current > 0)
            {
                context.Current--;
            }
        }
    }
}