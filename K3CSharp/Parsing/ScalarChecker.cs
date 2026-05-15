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
    /// Specialized parser for scalar value checking
    /// Handles checking if an AST node represents a scalar value
    /// </summary>
    public class ScalarChecker : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for scalar checking
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - IsScalar is the main entry point
            throw new NotImplementedException("Use IsScalar method instead");
        }

        /// <summary>
        /// Check if an AST node represents a scalar value
        /// A scalar is a single value (not a vector or function)
        /// </summary>
        public static bool IsScalar(ASTNode node)
        {
            return node.Type == ASTNodeType.Literal && 
                   (node.Value is IntegerValue || node.Value is LongValue || 
                    node.Value is FloatValue || node.Value is CharacterValue || 
                    node.Value is SymbolValue || node.Value is NullValue);
        }
    }
}