// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;

namespace K3CSharp
{
    /// <summary>
    /// Specialized parser for semicolon vector elements
    /// Handles parsing expressions in semicolon-separated vectors with proper nested structure handling
    /// </summary>
    public class SemicolonVectorParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for parsing elements in semicolon-separated vectors
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - ParseElement is the main entry point
            throw new NotImplementedException("Use ParseElement method instead");
        }

        /// <summary>
        /// Parse an element for semicolon-separated vectors, handling nested structures properly
        /// This method parses expressions but doesn't stop at semicolons at the top level of nested parentheses
        /// </summary>
        public static ASTNode? ParseElement(ParseContext context)
        {
            var left = ParseTerm(context);
            if (left == null)
            {
                return null;
            }

            // Handle dyadic operators but be careful about semicolons in nested structures
            while (!context.IsAtEnd() && 
                   context.CurrentToken().Type != TokenType.SEMICOLON && 
                   context.CurrentToken().Type != TokenType.RIGHT_PAREN &&
                   context.CurrentToken().Type != TokenType.RIGHT_BRACE &&
                   context.CurrentToken().Type != TokenType.RIGHT_BRACKET &&
                   IsDyadicOperator(context.CurrentToken().Type))
            {
                var op = MatchAndGetOperator(context);
                if (op == null) break;
                
                var right = ParseTerm(context);
                if (right == null) break;
                
                left = ASTNode.MakeDyadicOp(op.Type, left, right);
            }

            return left;
        }

        private static ASTNode? ParseTerm(ParseContext context)
        {
            // Simplified term parsing - just parse primary expressions
            // In a full implementation, this would call ExpressionParser
            return ParsePrimary(context);
        }

        private static ASTNode? ParsePrimary(ParseContext context)
        {
            if (context.IsAtEnd())
            {
                return null;
            }

            var token = context.CurrentToken();
            
            // Handle basic literals and identifiers
            return token.Type switch
            {
                TokenType.INTEGER => ParseInteger(context),
                TokenType.FLOAT => ParseFloat(context),
                TokenType.SYMBOL => ParseSymbol(context),
                TokenType.QUOTE => ParseString(context),
                TokenType.IDENTIFIER => ParseIdentifier(context),
                TokenType.LEFT_PAREN => ParseParenthesizedExpression(context),
                TokenType.LEFT_BRACE => ParseBraceExpression(context),
                TokenType.LEFT_BRACKET => ParseBracketExpression(context),
                _ => throw new Exception($"Unexpected token in semicolon vector element: {token.Type}({token.Lexeme})")
            };
        }

        private static ASTNode ParseInteger(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            if (lexeme == "0I" || lexeme == "0N" || lexeme == "-0I")
                return ASTNode.MakeLiteral(new IntegerValue(lexeme));
            
            if (int.TryParse(lexeme, out int intValue))
            {
                if (intValue >= 2147483647)
                    return ASTNode.MakeLiteral(new IntegerValue("0I"));
                else if (intValue <= -2147483648)
                    return ASTNode.MakeLiteral(new IntegerValue("-0I"));
                else
                    return ASTNode.MakeLiteral(new IntegerValue(intValue));
            }
            
            throw new ArgumentException($"Invalid integer literal: {lexeme}");
        }

        private static ASTNode ParseFloat(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            if (double.TryParse(lexeme, out double floatValue))
            {
                return ASTNode.MakeLiteral(new FloatValue(floatValue));
            }
            
            throw new ArgumentException($"Invalid float literal: {lexeme}");
        }

        private static ASTNode ParseSymbol(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            var symbolValue = lexeme.Trim('`');
            return ASTNode.MakeLiteral(new SymbolValue(symbolValue));
        }

        private static ASTNode ParseString(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            if (lexeme.Length >= 2 && lexeme[0] == '"' && lexeme[^1] == '"')
            {
                var stringValue = lexeme.Substring(1, lexeme.Length - 2);
                
                // According to spec:
                // - If length is 1, produce a character
                // - If length is other than 1 (including 0 or >1), produce a character vector
                if (stringValue.Length == 0)
                    return ASTNode.MakeLiteral(new VectorValue(new List<K3Value>(), -3)); // Empty character vector
                else if (stringValue.Length == 1)
                    return ASTNode.MakeLiteral(new CharacterValue(stringValue)); // Single character
                else
                {
                    var charValues = stringValue.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
                    return ASTNode.MakeLiteral(new VectorValue(charValues)); // Character vector
                }
            }
            
            throw new ArgumentException($"Invalid string literal: {lexeme}");
        }

        private static ASTNode ParseIdentifier(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            return ASTNode.MakeVariable(token.Lexeme);
        }

        private static ASTNode ParseParenthesizedExpression(ParseContext context)
        {
            context.Advance(); // Consume '('
            
            var result = ParseElement(context);
            
            if (!context.Match(TokenType.RIGHT_PAREN))
            {
                throw new Exception("Expected ')' after parenthesized expression");
            }
            
            return result ?? throw new Exception("Expected expression inside parentheses");
        }

        private static ASTNode ParseBraceExpression(ParseContext context)
        {
            context.Advance(); // Consume '{'
            
            var result = ParseElement(context);
            
            if (!context.Match(TokenType.RIGHT_BRACE))
            {
                throw new Exception("Expected '}' after brace expression");
            }
            
            return result ?? throw new Exception("Expected expression inside braces");
        }

        private static ASTNode ParseBracketExpression(ParseContext context)
        {
            context.Advance(); // Consume '['
            
            var result = ParseElement(context);
            
            if (!context.Match(TokenType.RIGHT_BRACKET))
            {
                throw new Exception("Expected ']' after bracket expression");
            }
            
            return result ?? throw new Exception("Expected expression inside brackets");
        }

        private static Token? MatchAndGetOperator(ParseContext context)
        {
            if (context.IsAtEnd())
            {
                return null;
            }

            var token = context.CurrentToken();
            
            // Check if this is a dyadic operator
            if (IsDyadicOperator(token.Type))
            {
                context.Advance();
                return token;
            }
            
            return null;
        }

        private static bool IsDyadicOperator(TokenType tokenType)
        {
            return VerbRegistry.IsDyadicOperator(tokenType);
        }
    }
}