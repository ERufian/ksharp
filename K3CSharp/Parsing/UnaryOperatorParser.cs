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
    /// Specialized parser for monadic operators with consistent handling
    /// This handles monadic +, -, *, %, ^, &, |, ~, !, #, _, ?, $, @ operators
    /// </summary>
    public class MonadicOperatorParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            return IsMonadicOperatorToken(currentToken);
        }

        public ASTNode? Parse(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            return token.Type switch
            {
                TokenType.PLUS => ParseMonadicPlus(context),
                TokenType.MINUS => ParseMonadicMinus(context),
                TokenType.MULTIPLY => ParseMonadicFirst(context),
                TokenType.DIVIDE => ParseMonadicReciprocal(context),
                TokenType.MODULUS => ParseMonadicReciprocal(context),
                TokenType.POWER => ParseMonadicPower(context),
                TokenType.MIN => ParseMonadicMin(context),
                TokenType.MAX => ParseMonadicMax(context),
                TokenType.MATCH => ParseMonadicMatch(context),
                TokenType.NOT => ParseMonadicNot(context),
                TokenType.HASH => ParseMonadicCount(context),
                TokenType.UNDERSCORE => ParseMonadicFloor(context),
                TokenType.QUESTION => ParseMonadicUnique(context),
                TokenType.DOLLAR => ParseMonadicFormat(context),
                TokenType.APPLY => ParseMonadicApply(context),
                TokenType.PARSE => ParseMonadicParse(context),
                TokenType.EVAL => ParseMonadicEval(context),
                _ => throw new Exception($"Unexpected monadic operator: {token.Type}({token.Lexeme})")
            };
        }

        /// <summary>
        /// Parse monadic operator with proper context checking
        /// </summary>
        public static ASTNode? ParseMonadicOperator(TokenType tokenType, ParseContext context, bool isAtExpressionStart)
        {
            context.Advance();
            
            return tokenType switch
            {
                TokenType.PLUS => ParseMonadicPlus(context),
                TokenType.MINUS => ParseMonadicMinus(context),
                TokenType.MULTIPLY => ParseMonadicFirst(context),
                TokenType.DIVIDE => ParseMonadicReciprocal(context),
                TokenType.MODULUS => ParseMonadicReciprocal(context),
                TokenType.POWER => ParseMonadicPower(context),
                TokenType.MIN => ParseMonadicMin(context),
                TokenType.MAX => ParseMonadicMax(context),
                TokenType.MATCH => ParseMonadicMatch(context),
                TokenType.NOT => ParseMonadicNot(context),
                TokenType.HASH => ParseMonadicCount(context),
                TokenType.UNDERSCORE => ParseMonadicFloor(context),
                TokenType.QUESTION => ParseMonadicUnique(context),
                TokenType.DOLLAR => ParseMonadicFormat(context),
                TokenType.APPLY => ParseMonadicApply(context),
                _ => throw new Exception($"Unexpected monadic operator: {tokenType}")
            };
        }

        private static ASTNode ParseMonadicPlus(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            
            // Check if this is a projection (has semicolon structure)
            if (operand != null && operand.Type == ASTNodeType.Vector && HasProjectionStructure(operand))
            {
                // Create a vector structure for projection: (operator, ::, args...)
                var projectionVector = new List<ASTNode>();
                projectionVector.Add(ASTNode.MakeLiteral(new SymbolValue("+"))); // Add operator
                projectionVector.Add(ASTNode.MakeLiteral(new SymbolValue("::"))); // Add projection symbol
                
                // Add the projection arguments
                foreach (var child in operand.Children)
                {
                    projectionVector.Add(child);
                }
                
                return ASTNode.MakeVector(projectionVector);
            }
            
            // Regular monadic plus
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("+");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static bool HasProjectionStructure(ASTNode node)
        {
            // Check if this vector represents a projection (has missing arguments)
            // For now, we'll consider any vector from bracket parsing as potentially a projection
            // The ParseTreeConverter will handle the actual projection logic
            return node.Type == ASTNodeType.Vector && node.Children.Count > 0;
        }

        private static ASTNode ParseMonadicMinus(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("-");
            
            // Ensure we always have at least one child for monadic operators
            if (operand != null)
            {
                node.Children.Add(operand);
            }
            else
            {
                // Create a placeholder operand for incomplete monadic expression
                node.Children.Add(ASTNode.MakeLiteral(new NullValue()));
            }
            
            return node;
        }

        private static ASTNode ParseMonadicFirst(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("*");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicReciprocal(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            if (operand == null)
            {
                // Create a projected function instead of an empty dyadic op
                var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                projectedNode.Value = new SymbolValue("%");
                projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1))); // Needs 1 more argument
                return projectedNode;
            }
            
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("%");
            node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicPower(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("^");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicMin(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("&");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicMax(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("|");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicMatch(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("~");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicNot(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("~");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicCount(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("#");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicFloor(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("_");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicUnique(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("?");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicFormat(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("$");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicApply(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("@");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicParse(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("_parse");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private static ASTNode ParseMonadicEval(ParseContext context)
        {
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("_eval");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        /// <summary>
        /// Parse bracket argument for monadic operators
        /// </summary>
        private static ASTNode? ParseBracketArgument(ParseContext context)
        {
            if (context.IsAtEnd())
            {
                return null;
            }
            
            // For now, use a simple approach - delegate to existing parsing
            // In a full implementation, this would use the ExpressionParser
            return ParseSimpleExpression(context);
        }

        /// <summary>
        /// Simple expression parsing for monadic operator arguments
        /// </summary>
        private static ASTNode? ParseSimpleExpression(ParseContext context)
        {
            if (context.IsAtEnd())
            {
                return null;
            }

            var token = context.CurrentToken();
            
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
                _ => throw new Exception($"Unexpected token in monadic operator argument: {token.Type}({token.Lexeme})")
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
            
            var result = ParseSimpleExpression(context);
            
            if (!context.Match(TokenType.RIGHT_PAREN))
            {
                throw new Exception("Expected ')' after parenthesized expression");
            }
            
            return result ?? throw new Exception("Expected expression inside parentheses");
        }

        private static ASTNode ParseBraceExpression(ParseContext context)
        {
            context.Advance(); // Consume '{'
            
            var result = ParseSimpleExpression(context);
            
            if (!context.Match(TokenType.RIGHT_BRACE))
            {
                throw new Exception("Expected '}' after brace expression");
            }
            
            return result ?? throw new Exception("Expected expression inside braces");
        }

        private static ASTNode ParseBracketExpression(ParseContext context)
        {
            context.Advance(); // Consume '['
            
            // Use the same projection logic as PrimaryParser
            var result = ParseBracketContentsWithProjection(context);
            
            if (!context.Match(TokenType.RIGHT_BRACKET))
            {
                throw new Exception("Expected ']' after bracket expression");
            }
            
            return result ?? throw new Exception("Expected expression inside brackets");
        }

        private static ASTNode ParseBracketContentsWithProjection(ParseContext context)
        {
            var elements = new List<ASTNode>();
            var projectionSlots = new List<ASTNode?>();
            var hasSemicolon = false;
            
            if (context.Check(TokenType.RIGHT_BRACKET))
            {
                return ASTNode.MakeVector(elements);
            }
            
            // Check if the first token is a semicolon (indicating a projection)
            if (context.Match(TokenType.SEMICOLON))
            {
                hasSemicolon = true;
                projectionSlots.Add(null); // Missing first argument
                
                // Skip empty lines
                while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                {
                    context.Match(TokenType.NEWLINE);
                }
            }
            
            // Parse first element if not at end
            if (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
            {
                var firstElement = ParseSimpleExpression(context);
                if (firstElement == null)
                {
                    throw new Exception("Expected expression in brackets");
                }
                
                if (hasSemicolon)
                {
                    projectionSlots.Add(firstElement);
                }
                else
                {
                    elements.Add(firstElement);
                }
            }
            
            // Parse additional elements separated by semicolons
            while (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
            {
                if (context.Match(TokenType.SEMICOLON))
                {
                    hasSemicolon = true;
                    
                    // Add null for missing argument
                    projectionSlots.Add(null);
                    
                    // Skip empty lines
                    while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                    {
                        context.Match(TokenType.NEWLINE);
                    }
                    
                    if (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
                    {
                        var element = ParseSimpleExpression(context);
                        if (element != null)
                        {
                            projectionSlots[projectionSlots.Count - 1] = element; // Replace the null
                        }
                    }
                }
                else
                {
                    var element = ParseSimpleExpression(context);
                    if (element != null)
                    {
                        if (hasSemicolon)
                        {
                            projectionSlots.Add(element);
                        }
                        else
                        {
                            elements.Add(element);
                        }
                    }
                }
            }
            
            // If we detected semicolons (projection syntax), create a projection node
            if (hasSemicolon)
            {
                // For projections, we need to create a vector with the projection arguments
                var projectionElements = new List<ASTNode>();
                
                // Add all non-null projection arguments
                var nonNullElements = projectionSlots.Where(x => x != null).Cast<ASTNode>().ToList();
                projectionElements.AddRange(nonNullElements);
                
                return ASTNode.MakeVector(projectionElements);
            }
            
            return ASTNode.MakeVector(elements);
        }

        public static bool IsMonadicOperatorToken(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.PLUS or TokenType.MINUS or TokenType.MULTIPLY or TokenType.DIVIDE or 
                TokenType.MODULUS or TokenType.POWER or TokenType.MIN or TokenType.MAX or
                TokenType.MATCH or TokenType.NOT or TokenType.HASH or TokenType.UNDERSCORE or
                TokenType.QUESTION or TokenType.DOLLAR or TokenType.APPLY or 
                TokenType.PARSE or TokenType.EVAL => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if a token could be a monadic operator in the current context
        /// </summary>
        public static bool CouldBeMonadicOperator(TokenType tokenType, bool isAtExpressionStart)
        {
            if (!IsMonadicOperatorToken(tokenType))
                return false;
            
            // Most monadic operators are only valid at the start of an expression
            // except for specific cases like function arguments
            return isAtExpressionStart || tokenType == TokenType.PLUS;
        }
    }
}