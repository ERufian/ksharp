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
    /// Assignment parser module for handling assignments and apply-and-assign operations
    /// </summary>
    public class AssignmentParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // Assignment parser handles assignment operators and apply-and-assign operations
            return currentToken == TokenType.ASSIGNMENT ||
                   currentToken == TokenType.GLOBAL_ASSIGNMENT ||
                   currentToken == TokenType.COLON ||
                   currentToken == TokenType.IDENTIFIER;
        }

        public ASTNode? Parse(ParseContext context)
        {
            var token = context.CurrentToken();
            
            return token.Type switch
            {
                TokenType.ASSIGNMENT => ParseAssignment(context),
                TokenType.GLOBAL_ASSIGNMENT => ParseGlobalAssignment(context),
                TokenType.COLON => ParseApplyAndAssign(context),
                TokenType.IDENTIFIER => ParseIdentifierAssignment(context),
                _ => throw new Exception($"Unexpected assignment token: {token.Type}({token.Lexeme})")
            };
        }

        private ASTNode ParseAssignment(ParseContext context)
        {
            context.Advance(); // Consume ':'
            
            // Check if this is a variable assignment
            if (context.Check(TokenType.IDENTIFIER))
            {
                var variableName = context.CurrentToken().Lexeme;
                context.Advance(); // Consume variable name
                
                // Check for apply-and-assign pattern: variable:op expression
                if (IsDyadicOperator(context.CurrentToken().Type))
                {
                    return ParseApplyAndAssignVariable(variableName, context);
                }
                
                // Parse the value to assign
                var value = ParseAssignmentValue(context);
                if (value == null)
                {
                    throw new Exception("Expected expression after assignment operator");
                }
                
                return ASTNode.MakeAssignment(variableName, value);
            }
            
            // Simple assignment expression
            var expression = ParseAssignmentValue(context);
            if (expression == null)
            {
                throw new Exception("Expected expression after assignment operator");
            }
            
            return expression;
        }

        private ASTNode ParseGlobalAssignment(ParseContext context)
        {
            context.Advance(); // Consume '::'
            
            if (!context.Check(TokenType.IDENTIFIER))
            {
                throw new Exception("Expected variable name after global assignment operator");
            }
            
            var variableName = context.CurrentToken().Lexeme;
            context.Advance(); // Consume variable name
            
            var value = ParseAssignmentValue(context);
            if (value == null)
            {
                throw new Exception("Expected expression after global assignment operator");
            }
            
            return ASTNode.MakeGlobalAssignment(variableName, value);
        }

        private ASTNode ParseApplyAndAssign(ParseContext context)
        {
            context.Advance(); // Consume ':'
            
            if (!context.Check(TokenType.IDENTIFIER))
            {
                throw new Exception("Expected variable name after apply-and-assign operator");
            }
            
            var variableName = context.CurrentToken().Lexeme;
            context.Advance(); // Consume variable name
            
            return ParseApplyAndAssignVariable(variableName, context);
        }

        private ASTNode ParseApplyAndAssignVariable(string variableName, ParseContext context)
        {
            if (!IsDyadicOperator(context.CurrentToken().Type))
            {
                throw new Exception("Expected dyadic operator after variable name in apply-and-assign");
            }
            
            var opToken = context.CurrentToken();
            context.Advance(); // Consume operator
            
            var rightArgument = ParseAssignmentValue(context);
            if (rightArgument == null)
            {
                throw new Exception("Expected right argument in apply-and-assign operation");
            }
            
            return ASTNode.MakeApplyAndAssign(variableName, opToken.Type, rightArgument);
        }

        private ASTNode ParseIdentifierAssignment(ParseContext context)
        {
            var identifier = context.CurrentToken().Lexeme;
            context.Advance(); // Consume identifier
            
            // Check if this is followed by an assignment operator
            if (context.Check(TokenType.ASSIGNMENT))
            {
                return ParseAssignmentWithIdentifier(identifier, context);
            }
            else if (context.Check(TokenType.GLOBAL_ASSIGNMENT))
            {
                return ParseGlobalAssignmentWithIdentifier(identifier, context);
            }
            else if (context.Check(TokenType.COLON))
            {
                return ParseApplyAndAssignWithIdentifier(identifier, context);
            }
            
            // Not an assignment, return as variable
            return ASTNode.MakeVariable(identifier);
        }

        private ASTNode ParseAssignmentWithIdentifier(string identifier, ParseContext context)
        {
            context.Advance(); // Consume ':'
            
            var value = ParseAssignmentValue(context);
            if (value == null)
            {
                throw new Exception("Expected expression after assignment operator");
            }
            
            return ASTNode.MakeAssignment(identifier, value);
        }

        private ASTNode ParseGlobalAssignmentWithIdentifier(string identifier, ParseContext context)
        {
            context.Advance(); // Consume '::'
            
            var value = ParseAssignmentValue(context);
            if (value == null)
            {
                throw new Exception("Expected expression after global assignment operator");
            }
            
            return ASTNode.MakeGlobalAssignment(identifier, value);
        }

        private ASTNode ParseApplyAndAssignWithIdentifier(string identifier, ParseContext context)
        {
            context.Advance(); // Consume ':'
            
            if (!IsDyadicOperator(context.CurrentToken().Type))
            {
                throw new Exception("Expected dyadic operator after variable name in apply-and-assign");
            }
            
            var opToken = context.CurrentToken();
            context.Advance(); // Consume operator
            
            var rightArgument = ParseAssignmentValue(context);
            if (rightArgument == null)
            {
                throw new Exception("Expected right argument in apply-and-assign operation");
            }
            
            return ASTNode.MakeApplyAndAssign(identifier, opToken.Type, rightArgument);
        }

        private ASTNode? ParseAssignmentValue(ParseContext context)
        {
            if (context.IsAtEnd())
            {
                return null;
            }
            
            // Use expression parser for assignment values
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
            
            return null;
        }

        private bool IsDyadicOperator(TokenType tokenType)
        {
            return VerbRegistry.IsDyadicOperator(tokenType);
        }

        /// <summary>
        /// Check if a token represents an assignment operation
        /// </summary>
        public static bool IsAssignmentOperator(TokenType tokenType)
        {
            return tokenType == TokenType.ASSIGNMENT ||
                   tokenType == TokenType.GLOBAL_ASSIGNMENT ||
                   tokenType == TokenType.COLON;
        }

        /// <summary>
        /// Parse assignment with left-hand side already parsed
        /// </summary>
        public static ASTNode? ParseAssignmentWithLHS(ASTNode? leftSide, ParseContext context)
        {
            if (leftSide == null || leftSide.Type != ASTNodeType.Variable)
            {
                return leftSide;
            }
            
            var variableName = leftSide.Value is SymbolValue symbol ? symbol.Value : leftSide.Value?.ToString() ?? "";
            
            if (context.Check(TokenType.ASSIGNMENT))
            {
                context.Advance(); // Consume ':'
                var assignmentParser = new AssignmentParser();
                var value = assignmentParser.ParseAssignmentValue(context);
                
                if (value != null)
                {
                    return ASTNode.MakeAssignment(variableName, value);
                }
            }
            else if (context.Check(TokenType.GLOBAL_ASSIGNMENT))
            {
                context.Advance(); // Consume '::'
                var assignmentParser = new AssignmentParser();
                var value = assignmentParser.ParseAssignmentValue(context);
                
                if (value != null)
                {
                    return ASTNode.MakeGlobalAssignment(variableName, value);
                }
            }
            else if (context.Check(TokenType.COLON))
            {
                context.Advance(); // Consume ':'
                return new AssignmentParser().ParseApplyAndAssignVariable(variableName, context);
            }
            
            return leftSide;
        }
    }
}