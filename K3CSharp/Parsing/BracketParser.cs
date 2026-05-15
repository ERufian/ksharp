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
    /// Bracket parser module for handling bracket contents, vectors, and function calls
    /// </summary>
    public class BracketParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // Bracket parser handles bracket tokens and their contents
            return currentToken == TokenType.LEFT_BRACKET || 
                   currentToken == TokenType.RIGHT_BRACKET ||
                   currentToken == TokenType.LEFT_PAREN ||
                   currentToken == TokenType.RIGHT_PAREN ||
                   currentToken == TokenType.LEFT_BRACE ||
                   currentToken == TokenType.RIGHT_BRACE;
        }

        public ASTNode? Parse(ParseContext context)
        {
            var token = context.CurrentToken();
            
            return token.Type switch
            {
                TokenType.LEFT_BRACKET => ParseBracketContents(context),
                TokenType.LEFT_PAREN => ParseParenthesesContents(context),
                TokenType.LEFT_BRACE => ParseBraceContents(context),
                _ => throw new Exception($"Unexpected delimiter token: {token.Type}({token.Lexeme})")
            };
        }

        private ASTNode ParseBracketContents(ParseContext context)
        {
            context.Advance(); // Consume '['
            
            // Parse bracket contents with proper semicolon separation
            var elements = ParseBracketContentsAsCommaEnlisted(context);
            
            return elements;
        }

        private ASTNode ParseBracketContentsAsCommaEnlisted(ParseContext context)
        {
            var elements = new List<ASTNode>();
            
            if (context.Check(TokenType.RIGHT_BRACKET))
            {
                context.Advance(); // Consume ']'
                return ASTNode.MakeVector(elements);
            }
            
            // Parse first element
            var firstElement = ParseBracketArgument(context);
            if (firstElement == null)
            {
                throw new Exception("Expected expression in brackets");
            }
            
            // Check if this should be treated as indexing instead of function call
            if (firstElement.Type == ASTNodeType.Variable && !context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
            {
                // This is indexing: vector[index]
                var index = ParseBracketArgument(context);
                if (index == null)
                {
                    throw new Exception("Expected index in brackets");
                }
                
                context.Advance(); // Consume ']'
                return ASTNode.MakeFunctionCall(firstElement, new List<ASTNode> { index });
            }
            
            // Check if this should be treated as function call
            if (firstElement.Type == ASTNodeType.Variable && !context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
            {
                // This is a function call: func[args]
                var args = ParseBracketArguments(context);
                context.Advance(); // Consume ']'
                return ASTNode.MakeFunctionCall(firstElement, args);
            }
            
            // Otherwise, treat as vector construction
            elements.Add(firstElement);
            
            // Parse additional elements separated by semicolons
            while (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
            {
                if (context.Match(TokenType.SEMICOLON))
                {
                    // Skip empty lines
                    while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                    {
                        context.Match(TokenType.NEWLINE);
                    }
                    
                    if (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
                    {
                        elements.Add(ParseBracketArgument(context)!);
                    }
                }
                else
                {
                    elements.Add(ParseBracketArgument(context)!);
                }
            }
            
            context.Advance(); // Consume ']'
            return ASTNode.MakeVector(elements);
        }

        private ASTNode? ParseBracketArgument(ParseContext context)
        {
            if (context.IsAtEnd())
            {
                return null;
            }
            
            // Use expression parser for bracket arguments
            var expressionParser = new ExpressionParser();
            if (expressionParser.CanHandle(context.CurrentToken().Type))
            {
                return expressionParser.Parse(context);
            }
            
            // Fall back to primary parser
            var primaryParser = new PrimaryParser();
            if (primaryParser.CanHandle(context.CurrentToken().Type))
            {
                return primaryParser.Parse(context);
            }
            
            throw new Exception("Expected expression in brackets");
        }

        private List<ASTNode> ParseBracketArguments(ParseContext context)
        {
            var args = new List<ASTNode>();
            
            do
            {
                var arg = ParseBracketArgument(context);
                if (arg != null)
                {
                    args.Add(arg);
                }
                
                // Skip empty lines
                while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                {
                    context.Match(TokenType.NEWLINE);
                }
            } while (!context.IsAtEnd() && context.Match(TokenType.SEMICOLON));
            
            return args;
        }

        private ASTNode ParseParenthesesContents(ParseContext context)
        {
            context.Advance(); // Consume '('
            
            // Check for empty parentheses () which should be an empty list
            if (context.Check(TokenType.RIGHT_PAREN))
            {
                context.Advance(); // Consume ')'
                return ASTNode.MakeVector(new List<ASTNode>());
            }
            
            // Parse parenthesized expression - inside parentheses, semicolons create lists
            // Handle leading semicolons for null elements: (;1;2)
            ASTNode? firstExpr = null;
            bool hasSemicolon = false;

            if (context.CurrentToken().Type == TokenType.SEMICOLON)
            {
                // Leading semicolon: first element is null
                firstExpr = ASTNode.MakeLiteral(new NullValue());
                hasSemicolon = true;
                context.Advance(); // consume the semicolon
            }
            else
            {
                firstExpr = ParseBracketArgument(context);
                if (firstExpr == null)
                {
                    throw new Exception("Expected expression after '('");
                }
                hasSemicolon = context.Match(TokenType.SEMICOLON);
            }

            if (hasSemicolon)
            {
                // Semicolon-separated list
                var elements = new List<ASTNode> { firstExpr! };
                do
                {
                    // Skip empty lines
                    while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                    {
                        context.Match(TokenType.NEWLINE);
                    }
                    
                    if (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_PAREN)
                    {
                        elements.Add(ParseBracketArgument(context)!);
                    }
                } while (!context.IsAtEnd() && context.Match(TokenType.SEMICOLON));
                
                context.Advance(); // Consume ')'
                return ASTNode.MakeVector(elements);
            }
            else
            {
                // Single expression
                context.Advance(); // Consume ')'
                return firstExpr;
            }
        }

        private ASTNode ParseBraceContents(ParseContext context)
        {
            context.Advance(); // Consume '{'
            
            // Parse function body
            var body = ParseFunctionBody(context);
            
            context.Advance(); // Consume '}'
            
            return body;
        }

        private ASTNode ParseFunctionBody(ParseContext context)
        {
            var parameters = new List<string>();
            
            // Parse parameter list if present
            if (context.Check(TokenType.LEFT_BRACKET))
            {
                context.Advance(); // Consume '['
                
                while (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
                {
                    if (context.CurrentToken().Type == TokenType.IDENTIFIER)
                    {
                        parameters.Add(context.CurrentToken().Lexeme);
                        context.Advance();
                        
                        if (context.Check(TokenType.SEMICOLON))
                        {
                            context.Advance(); // Consume ';'
                        }
                    }
                    else
                    {
                        throw new Exception("Expected identifier in parameter list");
                    }
                }
                
                context.Advance(); // Consume ']'
            }
            
            // Parse function body
            var bodyParser = new ExpressionParser();
            var body = bodyParser.Parse(context) ?? ASTNode.MakeLiteral(new NullValue());
            
            return ASTNode.MakeFunction(parameters, body);
        }

        /// <summary>
        /// Parse bracket notation for indexing or function calls (postfix operations)
        /// </summary>
        public static ASTNode? ParseBracketNotation(ASTNode? expression, ParseContext context)
        {
            if (expression == null || !context.Check(TokenType.LEFT_BRACKET))
            {
                return expression;
            }
            
            var bracketParser = new BracketParser();
            return bracketParser.Parse(context);
        }
    }
}