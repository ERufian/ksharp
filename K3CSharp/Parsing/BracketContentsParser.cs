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
    /// Specialized parser for bracket contents with comma enlistment functionality
    /// This handles parsing of [x], [x;y;z], [] and similar bracket expressions
    /// </summary>
    public class BracketContentsParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            return currentToken == TokenType.LEFT_BRACKET;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This parser specifically handles the contents of brackets
            // not the bracket tokens themselves
            throw new NotImplementedException("Use ParseContents method instead");
        }

        /// <summary>
        /// Parse bracket contents to handle the comma enlistment part of f[x] is (f) .,(x)
        /// </summary>
        public static ASTNode ParseContents(ParseContext context)
        {
            // Expect to be positioned at LEFT_BRACKET
            if (!context.Match(TokenType.LEFT_BRACKET))
            {
                throw new Exception("Expected '[' at start of bracket contents");
            }
            
            if (context.Match(TokenType.RIGHT_BRACKET))
            {
                // Empty brackets [] - return null (which becomes _n when comma-enlisted)
                return ASTNode.MakeLiteral(new NullValue());
            }
            
            // Parse expressions separated by semicolons (for function arguments)
            var expressions = new List<ASTNode>();
            
            // Parse first argument using a method that doesn't stop at semicolons
            var firstArg = ParseBracketArgument(context);
            if (firstArg != null) expressions.Add(firstArg);
            
            // Handle semicolon-separated arguments
            while (context.Match(TokenType.SEMICOLON))
            {
                var nextArg = ParseBracketArgument(context);
                if (nextArg != null) expressions.Add(nextArg);
            }
            
            if (!context.Match(TokenType.RIGHT_BRACKET))
            {
                throw new Exception("Expected ']' after bracket expression");
            }
            
            // If multiple expressions, create a vector; otherwise return single expression
            if (expressions.Count > 1)
            {
                return ASTNode.MakeVector(expressions);
            }
            else
            {
                return expressions[0];
            }
        }

        /// <summary>
        /// Parse an expression for bracket arguments, treating semicolons as separators
        /// </summary>
        private static ASTNode? ParseBracketArgument(ParseContext context)
        {
            // Parse an expression for bracket arguments, treating semicolons as separators
            // This is similar to ParseExpression but doesn't stop at semicolons

            // Check for standalone operator as function reference
            if (!context.IsAtEnd() && IsDyadicOperator(context.CurrentToken().Type))
            {
                var nextIdx = context.Current + 1;
                if (nextIdx < context.Tokens.Count)
                {
                    var nextToken = context.Tokens[nextIdx];
                    // Check if next token would end the expression (making this a function reference)
                    if (nextToken.Type == TokenType.SEMICOLON || nextToken.Type == TokenType.RIGHT_BRACKET ||
                        nextToken.Type == TokenType.RIGHT_PAREN || nextToken.Type == TokenType.RIGHT_BRACE ||
                        nextToken.Type == TokenType.NEWLINE || nextToken.Type == TokenType.EOF)
                    {
                        // This is a function reference
                        context.Advance();
                        return ASTNode.MakeLiteral(new SymbolValue(context.PreviousToken().Lexeme));
                    }
                }
            }

            // Parse regular expression
            return ParseBracketExpression(context);
        }

        /// <summary>
        /// Parse expression within brackets (simplified version that doesn't stop at semicolons)
        /// </summary>
        private static ASTNode? ParseBracketExpression(ParseContext context)
        {
            // For now, use a simple approach - parse a primary expression
            // In a full implementation, this would handle dyadic operators properly
            
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
                _ => throw new Exception($"Unexpected token in bracket argument: {token.Type}({token.Lexeme})")
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
            
            // For simplicity, just parse until matching ')'
            var result = ParseBracketExpression(context);
            
            if (!context.Match(TokenType.RIGHT_PAREN))
            {
                throw new Exception("Expected ')' after parenthesized expression");
            }
            
            return result ?? throw new Exception("Expected expression inside parentheses");
        }

        private static ASTNode ParseBraceExpression(ParseContext context)
        {
            context.Advance(); // Consume '{'
            
            // For simplicity, just parse until matching '}'
            var result = ParseBracketExpression(context);
            
            if (!context.Match(TokenType.RIGHT_BRACE))
            {
                throw new Exception("Expected '}' after brace expression");
            }
            
            return result ?? throw new Exception("Expected expression inside braces");
        }

        private static bool IsDyadicOperator(TokenType tokenType)
        {
            return VerbRegistry.IsDyadicOperator(tokenType);
        }
    }
}