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
    /// Specialized parser for bracket arguments
    /// Handles parsing expressions for bracket arguments, treating semicolons as separators
    /// This is similar to ParseExpression but doesn't stop at semicolons
    /// </summary>
    public class BracketArgumentParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for parsing bracket arguments
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - ParseBracketArgument is the main entry point
            throw new NotImplementedException("Use ParseBracketArgument method instead");
        }

        /// <summary>
        /// Parse an expression for bracket arguments, treating semicolons as separators
        /// This is similar to ParseExpression but doesn't stop at semicolons
        /// </summary>
        public static ASTNode? ParseBracketArgument(ParseContext context)
        {
            // Check for standalone operator as function reference (e.g., + in @[x; i; +; y])
            // or operator followed by adverb (e.g., +/ in @[+/; args; :])
            if (!context.IsAtEnd() && IsDyadicOperator(context.CurrentToken().Type))
            {
                var nextIdx = context.Current + 1;
                var nextType = nextIdx < context.Tokens.Count ? context.Tokens[nextIdx].Type : TokenType.EOF;
                
                if (nextType == TokenType.SEMICOLON || nextType == TokenType.RIGHT_BRACKET || nextType == TokenType.RIGHT_PAREN || nextType == TokenType.EOF)
                {
                    // Standalone operator - treat as symbol
                    var opToken = context.CurrentToken();
                    var opSymbol = GetOperatorSymbol(opToken.Type, opToken.Lexeme);
                    context.Match(opToken.Type);
                    return ASTNode.MakeLiteral(new SymbolValue(opSymbol));
                }
                else if (VerbRegistry.IsAdverbToken(nextType))
                {
                    // Operator followed by adverb - create projected function
                    var opToken = context.CurrentToken();
                    var adverbToken = context.Tokens[nextIdx];
                    
                    var opSymbol = GetOperatorSymbol(opToken.Type, opToken.Lexeme);
                    var adverbType = GetAdverbType(adverbToken.Type);
                    
                    // Create projected function
                    var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                    projectedNode.Value = new SymbolValue(adverbType);
                    projectedNode.Children.Add(ASTNode.MakeLiteral(new SymbolValue(opSymbol))); // Store the verb
                    projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1))); // Needs 1 more argument
                    
                    // Consume both tokens
                    context.Match(opToken.Type);
                    context.Match(adverbToken.Type);
                    
                    return projectedNode;
                }
            }

            var left = ParseTerm(context);
            if (left == null)
            {
                return null;
            }

            // Handle assignment operators (including modified assignments like +:, -:, *:)
            if (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.ASSIGNMENT)
            {
                var assignToken = context.CurrentToken();
                context.Match(TokenType.ASSIGNMENT);
                var right = ParseBracketArgument(context);
                if (right == null)
                {
                    throw new Exception("Expected expression after assignment operator");
                }
                // Extract variable name from left side
                if (left.Type == ASTNodeType.Variable)
                {
                    var variableName = left.Value is SymbolValue sym ? sym.Value : left.Value?.ToString() ?? "";
                    
                    // Validate variable name - cannot start with underscore
                    if (variableName.StartsWith("_"))
                    {
                        throw new Exception("parse error");
                    }
                    
                    return ASTNode.MakeDyadicOp(TokenType.ASSIGNMENT, left, right);
                }
                else
                {
                    throw new Exception("Assignment requires a variable name on the left side");
                }
            }

            // Handle global assignment operator
            if (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.GLOBAL_ASSIGNMENT)
            {
                context.Match(TokenType.GLOBAL_ASSIGNMENT);
                var right = ParseBracketArgument(context);
                if (right == null)
                {
                    throw new Exception("Expected expression after ::");
                }
                // Extract variable name from left side
                if (left.Type == ASTNodeType.Variable)
                {
                    var variableName = left.Value is SymbolValue sym ? sym.Value : left.Value?.ToString() ?? "";
                    
                    // Validate variable name - cannot start with underscore
                    if (variableName.StartsWith("_"))
                    {
                        throw new Exception("parse error");
                    }
                    
                    return ASTNode.MakeDyadicOp(TokenType.GLOBAL_ASSIGNMENT, left, right);
                }
                else
                {
                    throw new Exception("Global assignment requires a variable name on the left side");
                }
            }

            // Handle apply-and-assign operators (+:, -:, *:, etc.)
            if (!context.IsAtEnd() && IsApplyAndAssignOperator(context.CurrentToken().Type))
            {
                var opToken = context.CurrentToken();
                context.Match(opToken.Type);
                
                // Check if this is a colon operator (apply-and-assign)
                if (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.COLON)
                {
                    context.Advance(); // Skip the colon token
                    
                    // Parse the right argument after the colon
                    var rightArgument = ParseBracketArgument(context);
                    if (rightArgument == null)
                    {
                        throw new Exception("Expected expression after operator in apply and assign");
                    }
                    
                    // Extract variable name from left side
                    if (left.Type == ASTNodeType.Variable)
                    {
                        var variableName = left.Value is SymbolValue sym ? sym.Value : left.Value?.ToString() ?? "";
                        
                        // Validate variable name - cannot start with underscore
                        if (variableName.StartsWith("_"))
                        {
                            throw new Exception("parse error");
                        }
                        
                        var opSymbol = GetOperatorSymbol(opToken.Type, opToken.Lexeme);
                        return ASTNode.MakeDyadicOp(TokenType.ASSIGNMENT, left, rightArgument);
                    }
                    else
                    {
                        throw new Exception("Apply and assign requires a variable name on the left side");
                    }
                }
                else
                {
                    // Regular dyadic operation with right-associativity
                    var right = ParseBracketArgument(context);
                    if (right == null)
                    {
                        throw new Exception($"Expected right operand after {opToken.Type}");
                    }
                    
                    return ASTNode.MakeDyadicOp(opToken.Type, left, right);
                }
            }

            return left;
        }

        private static ASTNode? ParseTerm(ParseContext context)
        {
            // Simplified term parsing for bracket arguments
            // In a full implementation, this would delegate to ExpressionParser
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
            
            var result = ParseBracketArgument(context);
            
            if (!context.Match(TokenType.RIGHT_PAREN))
            {
                throw new Exception("Expected ')' after parenthesized expression");
            }
            
            return result ?? throw new Exception("Expected expression inside parentheses");
        }

        private static ASTNode ParseBraceExpression(ParseContext context)
        {
            context.Advance(); // Consume '{'
            
            var result = ParseBracketArgument(context);
            
            if (!context.Match(TokenType.RIGHT_BRACE))
            {
                throw new Exception("Expected '}' after brace expression");
            }
            
            return result ?? throw new Exception("Expected expression inside braces");
        }

        private static ASTNode ParseBracketExpression(ParseContext context)
        {
            context.Advance(); // Consume '['
            
            var result = ParseBracketArgument(context);
            
            if (!context.Match(TokenType.RIGHT_BRACKET))
            {
                throw new Exception("Expected ']' after bracket expression");
            }
            
            return result ?? throw new Exception("Expected expression inside brackets");
        }

        private static string GetOperatorSymbol(TokenType tokenType, string lexeme)
        {
            return tokenType.ToString() switch
            {
                "PLUS" => "+",
                "MINUS" => "-",
                "MULTIPLY" => "*",
                "DIVIDE" => "%",
                "POWER" => "^",
                "MODULUS" => "!",
                "MIN" => "&",
                "MAX" => "|",
                "LESS" => "<",
                "GREATER" => ">",
                "EQUAL" => "=",
                "JOIN" => ",",
                "COLON" => ":",
                "HASH" => "#",
                "UNDERSCORE" => "_",
                "QUESTION" => "?",
                "DOLLAR" => "$",
                "APPLY" => "@",
                _ => lexeme
            };
        }

        private static string GetAdverbType(TokenType tokenType)
        {
            return VerbRegistry.GetAdverbType(tokenType);
        }

        private static bool IsDyadicOperator(TokenType tokenType)
        {
            return VerbRegistry.IsDyadicOperator(tokenType);
        }

        private static bool IsApplyAndAssignOperator(TokenType tokenType)
        {
            return tokenType == TokenType.PLUS || tokenType == TokenType.MINUS || tokenType == TokenType.MULTIPLY ||
                   tokenType == TokenType.DIVIDE || tokenType == TokenType.MODULUS || tokenType == TokenType.POWER ||
                   tokenType == TokenType.MIN || tokenType == TokenType.MAX || tokenType == TokenType.LESS ||
                   tokenType == TokenType.GREATER || tokenType == TokenType.EQUAL || tokenType == TokenType.MATCH ||
                   tokenType == TokenType.DIV || tokenType == TokenType.AND || tokenType == TokenType.OR ||
                   tokenType == TokenType.XOR || tokenType == TokenType.ROT || tokenType == TokenType.SHIFT ||
                   tokenType == TokenType.DOT_PRODUCT || tokenType == TokenType.MUL || tokenType == TokenType.IN ||
                   tokenType == TokenType.BIN || tokenType == TokenType.BINL || tokenType == TokenType.LIN ||
                   tokenType == TokenType.DV || tokenType == TokenType.DI || tokenType == TokenType.SV ||
                   tokenType == TokenType.SS || tokenType == TokenType.SM || tokenType == TokenType.LSQ ||
                   tokenType == TokenType.JOIN || tokenType == TokenType.COLON || tokenType == TokenType.HASH ||
                   tokenType == TokenType.UNDERSCORE || tokenType == TokenType.QUESTION || tokenType == TokenType.DOLLAR ||
                   tokenType == TokenType.APPLY;
        }
    }
}