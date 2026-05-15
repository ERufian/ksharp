// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    /// <summary>
    /// Specialized parser for extracting implicit parameters from function body tokens
    /// In K, implicit parameters are single lowercase letters that appear in the function body
    /// The parameters are extracted in alphabetical order of first appearance
    /// </summary>
    public class ImplicitParameterExtractor : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for parameter extraction
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - ExtractImplicitParameters is the main entry point
            throw new NotImplementedException("Use ExtractImplicitParameters method instead");
        }

        /// <summary>
        /// Extract implicit parameters from function body tokens
        /// In K, implicit parameters are single lowercase letters that appear in the function body
        /// The parameters are extracted in alphabetical order of first appearance
        /// </summary>
        public static List<string> ExtractImplicitParameters(List<Token> bodyTokens)
        {
            var assignedLocals = new HashSet<string>();
            var seen = new HashSet<string>();
            
            // First pass: identify all locally-assigned variables (identifier followed by colon)
            for (int i = 0; i < bodyTokens.Count; i++)
            {
                var token = bodyTokens[i];
                if (token.Type == TokenType.IDENTIFIER &&
                    token.Lexeme.Length == 1 &&
                    char.IsLower(token.Lexeme[0]) &&
                    i + 1 < bodyTokens.Count &&
                    bodyTokens[i + 1].Type == TokenType.COLON)
                {
                    assignedLocals.Add(token.Lexeme);
                }
            }
            
            // Collect which of x/y/z appear as non-local identifiers in the body
            foreach (var token in bodyTokens)
            {
                if (token.Type == TokenType.IDENTIFIER &&
                    (token.Lexeme == "x" || token.Lexeme == "y" || token.Lexeme == "z") &&
                    !assignedLocals.Contains(token.Lexeme))
                {
                    seen.Add(token.Lexeme);
                }
            }
            
            // K implicit parameter rules:
            //   z present -> [x;y;z]
            //   y present (no z) -> [x;y]
            //   x present (no y,z) -> [x]
            //   none present -> []
            if (seen.Contains("z"))
                return new List<string> { "x", "y", "z" };
            if (seen.Contains("y"))
                return new List<string> { "x", "y" };
            if (seen.Contains("x"))
                return new List<string> { "x" };
            return new List<string>();
        }
    }
}