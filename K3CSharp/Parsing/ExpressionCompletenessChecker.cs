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
    /// Specialized parser for checking expression completeness
    /// Handles checking for unmatched brackets, parentheses, braces, strings, and symbols
    /// </summary>
    public class ExpressionCompletenessChecker : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for expression completeness checking
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - IsIncompleteExpression is the main entry point
            throw new NotImplementedException("Use IsIncompleteExpression method instead");
        }

        /// <summary>
        /// Check if the current expression is incomplete by looking for unmatched delimiters
        /// Returns true if there are unmatched brackets, parentheses, braces, or unclosed strings/symbols
        /// </summary>
        public static bool IsIncompleteExpression(List<Token> tokens)
        {
            // Check for unmatched brackets, parentheses, or braces
            int parentheses = 0;
            int brackets = 0;
            int braces = 0;
            bool inString = false;
            bool inSymbol = false;
            
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                
                if (token.Type == TokenType.QUOTE && !inString)
                {
                    inString = true;
                }
                else if (token.Type == TokenType.QUOTE && inString)
                {
                    inString = false;
                }
                else if (token.Type == TokenType.BACKTICK && !inSymbol)
                {
                    inSymbol = true;
                }
                else if ((token.Type == TokenType.SYMBOL || token.Type == TokenType.BACKTICK) && inSymbol)
                {
                    inSymbol = false;
                }
                else if (!inString && !inSymbol)
                {
                    switch (token.Type)
                    {
                        case TokenType.LEFT_PAREN:
                            parentheses++;
                            break;
                        case TokenType.RIGHT_PAREN:
                            parentheses--;
                            break;
                        case TokenType.LEFT_BRACKET:
                            brackets++;
                            break;
                        case TokenType.RIGHT_BRACKET:
                            brackets--;
                            break;
                        case TokenType.LEFT_BRACE:
                            braces++;
                            break;
                        case TokenType.RIGHT_BRACE:
                            braces--;
                            break;
                    }
                }
            }
            
            // Expression is incomplete if any brackets are unmatched
            return parentheses != 0 || brackets != 0 || braces != 0 || inString || inSymbol;
        }
    }
}