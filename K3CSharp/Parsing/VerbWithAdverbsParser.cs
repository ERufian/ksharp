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
    /// Specialized parser for verbs with adverbs parsing
    /// Handles parsing of verbs followed by prefix adverbs and their arguments
    /// </summary>
    public class VerbWithAdverbsParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for verb with adverbs parsing
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - ParseVerbWithAdverbs is the main entry point
            throw new NotImplementedException("Use ParseVerbWithAdverbs method instead");
        }

        /// <summary>
        /// Parse a verb with possible prefix adverbs
        /// Handles verbs followed by adverbs and their arguments
        /// </summary>
        public static ASTNode? ParseVerbWithAdverbs(ParseContext context)
        {
            // Parse any expression that could be a verb
            // In a full implementation, this would delegate to TermParser
            // For now, we'll simulate the behavior by creating a simple literal
            ASTNode? verb = null;
            if (!context.IsAtEnd())
            {
                var token = context.CurrentToken();
                if (token.Type == TokenType.INTEGER)
                {
                    context.Advance();
                    var lexeme = token.Lexeme;
                    if (int.TryParse(lexeme, out int intValue))
                    {
                        verb = ASTNode.MakeLiteral(new IntegerValue(intValue));
                    }
                }
                else if (token.Type == TokenType.IDENTIFIER)
                {
                    context.Advance();
                    verb = ASTNode.MakeVariable(token.Lexeme);
                }
            }
            
            if (verb == null) return null;
            
            // Check if this verb is followed by an adverb (prefix adverb)
            if (!context.IsAtEnd() && VerbRegistry.IsAdverbToken(context.CurrentToken().Type))
            {
                var adverbType = context.CurrentToken().Type;
                context.Advance(); // Consume the adverb
                
                // Parse the arguments for the adverb
                ASTNode? arguments = null;
                if (!context.IsAtEnd())
                {
                    var argToken = context.CurrentToken();
                    if (argToken.Type == TokenType.INTEGER)
                    {
                        context.Advance();
                        var lexeme = argToken.Lexeme;
                        if (int.TryParse(lexeme, out int intValue))
                        {
                            arguments = ASTNode.MakeLiteral(new IntegerValue(intValue));
                        }
                    }
                    else if (argToken.Type == TokenType.IDENTIFIER)
                    {
                        context.Advance();
                        arguments = ASTNode.MakeVariable(argToken.Lexeme);
                    }
                }
                
                // Create the proper adverb structure: ADVERB(verb, ::, arguments)
                // Use :: as initialization to signal "consume first element" for monadic derived verbs
                var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
                adverbNode.Value = new SymbolValue(adverbType.ToString().Replace("TokenType.", ""));
                if (verb != null) adverbNode.Children.Add(verb);
                adverbNode.Children.Add(new ASTNode(ASTNodeType.Literal, new NullValue())); // Use :: for monadic derived verbs
                if (arguments != null) adverbNode.Children.Add(arguments);
                
                // Recursively check for more adverbs (derived verbs)
                return ParseVerbWithAdverbsRecursive(context, adverbNode);
            }
            
            return verb;
        }

        /// <summary>
        /// Recursively parse additional adverbs for derived verbs
        /// </summary>
        private static ASTNode ParseVerbWithAdverbsRecursive(ParseContext context, ASTNode derivedVerb)
        {
            // Check if this derived verb is followed by another adverb
            if (!context.IsAtEnd() && VerbRegistry.IsAdverbToken(context.CurrentToken().Type))
            {
                var adverbType = context.CurrentToken().Type;
                context.Advance(); // Consume the adverb
                
                // Parse the arguments for the adverb
                ASTNode? arguments = null;
                if (!context.IsAtEnd())
                {
                    var argToken = context.CurrentToken();
                    if (argToken.Type == TokenType.INTEGER)
                    {
                        context.Advance();
                        var lexeme = argToken.Lexeme;
                        if (int.TryParse(lexeme, out int intValue))
                        {
                            arguments = ASTNode.MakeLiteral(new IntegerValue(intValue));
                        }
                    }
                    else if (argToken.Type == TokenType.IDENTIFIER)
                    {
                        context.Advance();
                        arguments = ASTNode.MakeVariable(argToken.Lexeme);
                    }
                }
                
                // Create the proper adverb structure: ADVERB(derivedVerb, ::, arguments)
                var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
                adverbNode.Value = new SymbolValue(adverbType.ToString().Replace("TokenType.", ""));
                adverbNode.Children.Add(derivedVerb);
                adverbNode.Children.Add(new ASTNode(ASTNodeType.Literal, new NullValue())); // Use :: for monadic derived verbs
                if (arguments != null) adverbNode.Children.Add(arguments);
                
                // Recursively check for more adverbs
                return ParseVerbWithAdverbsRecursive(context, adverbNode);
            }
            
            return derivedVerb;
        }

        }
}