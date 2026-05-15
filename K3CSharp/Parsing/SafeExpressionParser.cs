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
    /// Specialized parser for safe expression parsing
    /// Handles expression parsing with proper error handling
    /// </summary>
    public class SafeExpressionParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for safe expression parsing
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - SafeParseExpression is the main entry point
            throw new NotImplementedException("Use SafeParseExpression method instead");
        }

        /// <summary>
        /// Parse an expression with proper error handling
        /// Throws an exception if parsing fails to ensure proper error reporting
        /// </summary>
        public static ASTNode SafeParseExpression(ParseContext context)
        {
            // In a full implementation, this would delegate to ExpressionParser
            // For now, we'll use a simple implementation that delegates to the existing ParseExpression
            // This is a placeholder that demonstrates the modular approach
            
            // Save current position for error recovery
            var savedPosition = context.Current;
            
            try
            {
                // This would normally delegate to ExpressionParser.ParseExpression(context)
                // For now, we'll simulate the behavior by creating a simple literal
                if (!context.IsAtEnd())
                {
                    var token = context.CurrentToken();
                    if (token.Type == TokenType.INTEGER)
                    {
                        context.Advance();
                        var lexeme = token.Lexeme;
                        if (int.TryParse(lexeme, out int intValue))
                        {
                            return ASTNode.MakeLiteral(new IntegerValue(intValue));
                        }
                    }
                    else if (token.Type == TokenType.IDENTIFIER)
                    {
                        context.Advance();
                        return ASTNode.MakeVariable(token.Lexeme);
                    }
                }
                
                // If we get here, parsing failed - restore position and throw
                context.Current = savedPosition;
                throw new Exception("Expected expression but found statement separator");
            }
            catch
            {
                // Restore position on any error
                context.Current = savedPosition;
                throw;
            }
        }
    }
}