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
    /// Adverb parser module for handling K3 adverb operations and nesting
    /// </summary>
    public class AdverbParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // Adverb parser handles all adverb tokens
            return VerbRegistry.IsAdverbToken(currentToken);
        }

        public ASTNode? Parse(ParseContext context)
        {
            var adverbToken = context.CurrentToken();
            var adverbType = adverbToken.Type;
            context.Advance(); // Consume the adverb token
            
            // Parse the left argument (verb) for the adverb
            var leftArg = ParseAdverbLeftArgument(context);
            if (leftArg == null)
            {
                throw new Exception($"Expected expression before adverb {adverbToken.Lexeme}");
            }
            
            // Parse the right argument for the adverb
            var rightArg = ParseAdverbRightArgument(context);
            
            // Create adverb node: ADVERB(adverbType, verb, rightArg)
            var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
            adverbNode.Value = new SymbolValue(adverbType.ToString());
            adverbNode.Children.Add(leftArg); // verb
            if (rightArg != null) adverbNode.Children.Add(rightArg); // right argument
            
            return adverbNode;
        }

        private ASTNode? ParseAdverbLeftArgument(ParseContext context)
        {
            // For adverbs, the left argument is typically the verb or expression that precedes the adverb
            // This would need to be handled by the calling context, as adverbs are postfix operators
            // For now, we'll parse the current token as the left argument
            if (context.IsAtEnd())
            {
                return null;
            }
            
            var token = context.CurrentToken();
            
            // Handle different types of left arguments
            return token.Type switch
            {
                TokenType.IDENTIFIER => ParseIdentifier(context),
                TokenType.SYMBOL => ParseSymbol(context),
                // Add more cases as needed
                _ => throw new Exception($"Invalid left argument for adverb: {token.Type}({token.Lexeme})")
            };
        }

        private ASTNode? ParseAdverbRightArgument(ParseContext context)
        {
            if (context.IsAtEnd())
            {
                return null;
            }
            
            // Use expression parser for the right argument
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

        private ASTNode ParseIdentifier(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            return ASTNode.MakeVariable(token.Lexeme);
        }

        private ASTNode ParseSymbol(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            var symbolValue = lexeme.Trim('`');
            return ASTNode.MakeLiteral(new SymbolValue(symbolValue));
        }

        /// <summary>
        /// Parse adverb operations in nesting context (verb-adverb binding)
        /// </summary>
        public static ASTNode? ParseAdverbNesting(ASTNode? verb, ParseContext context)
        {
            if (verb == null || !new AdverbParser().CanHandle(context.CurrentToken().Type))
            {
                return verb;
            }
            
            var adverbParser = new AdverbParser();
            var adverbNode = adverbParser.Parse(context);
            
            // The adverb parser will have created the node with the verb and right argument
            return adverbNode;
        }
    }
}