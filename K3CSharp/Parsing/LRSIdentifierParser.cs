// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// LRS Identifier Parser for variables and system variables
    /// Verb-agnostic design using VerbRegistry for identifier classification
    /// </summary>
    public class LRSIdentifierParser
    {
        private readonly List<Token> tokens;
        
        public LRSIdentifierParser(List<Token> tokens)
        {
            this.tokens = tokens;
        }
        
        /// <summary>
        /// Check if token can be parsed as identifier
        /// </summary>
        public static bool IsIdentifierToken(TokenType tokenType)
        {
            return tokenType == TokenType.IDENTIFIER || tokenType == TokenType.SYMBOL;
        }
        
        /// <summary>
        /// Parse identifier token into AST node
        /// </summary>
        public static ASTNode? ParseIdentifier(Token token)
        {
            if (token.Type != TokenType.IDENTIFIER && token.Type != TokenType.SYMBOL)
                return null;
                
            var identifierName = token.Lexeme;
            
            // Check if it's a system variable using VerbRegistry
            if (VerbRegistry.IsSystemVariable(identifierName))
            {
                return CreateSystemVariableNode(identifierName);
            }
            
            // Regular variable
            return ASTNode.MakeVariable(identifierName);
        }
        
        /// <summary>
        /// Create system variable node
        /// </summary>
        private static ASTNode CreateSystemVariableNode(string variableName)
        {
            var node = new ASTNode(ASTNodeType.Variable);
            node.Value = new SymbolValue(variableName);
            node.Children.Add(ASTNode.MakeLiteral(new SymbolValue("system")));
            return node;
        }
        
        /// <summary>
        /// Get identifier type using VerbRegistry
        /// </summary>
        public static string GetIdentifierType(string identifierName)
        {
            var verbType = VerbRegistry.GetVerbType(identifierName);
            return verbType switch
            {
                VerbType.SystemVariable => "system_variable",
                VerbType.Function => "function",
                VerbType.Operator => "operator",
                _ => "variable"
            };
        }
        
        /// <summary>
        /// Check if identifier is a function
        /// </summary>
        public static bool IsFunction(string identifierName)
        {
            var verbType = VerbRegistry.GetVerbType(identifierName);
            return verbType == VerbType.Function;
        }
        
        /// <summary>
        /// Check if identifier is a system variable
        /// </summary>
        public static bool IsSystemVariable(string identifierName)
        {
            return VerbRegistry.IsSystemVariable(identifierName);
        }
        
        /// <summary>
        /// Check if identifier is an operator
        /// </summary>
        public static bool IsOperator(string identifierName)
        {
            var verbType = VerbRegistry.GetVerbType(identifierName);
            return verbType == VerbType.Operator;
        }
        
        /// <summary>
        /// Get verb information for identifier
        /// </summary>
        public static VerbInfo? GetVerbInfo(string identifierName)
        {
            return VerbRegistry.GetVerb(identifierName);
        }
    }
}