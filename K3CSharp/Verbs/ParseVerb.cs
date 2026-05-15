// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using K3CSharp;
using K3CSharp.Parsing;

namespace K3CSharp.Verbs
{
    /// <summary>
    /// Implementation of _parse verb for K3CSharp
    /// Parses character vectors into AST nodes and converts to K list representations
    /// </summary>
    public static class ParseVerbHandler
    {
        /// <summary>
        /// Main _parse entry point
        /// </summary>
        public static K3Value Parse(string expressionText)
        {
            return ParseExpression(expressionText);
        }
        
        /// <summary>
        /// Parse verb implementation matching delegate signature
        /// </summary>
        public static K3Value Parse(K3Value[] arguments)
        {
            if (arguments.Length == 0)
                throw new Exception("_parse: requires an argument");
                
            var expressionText = arguments[0].ToString();
            return Parse(expressionText);
        }
        
        /// <summary>
        /// Parse character vector expression using LRS parser
        /// </summary>
        private static K3Value ParseExpression(string expressionText)
        {
            try
            {
                // Use same pattern as Program.cs
                var lexer = new Lexer(expressionText);
                var tokens = lexer.Tokenize();
                
                // Check if we have a single CHARACTER_VECTOR token (from a quoted string)
                if (tokens.Count == 2 && tokens[0].Type == TokenType.CHARACTER_VECTOR && tokens[1].Type == TokenType.EOF)
                {
                    // Extract the content from the quoted string and parse it
                    var content = tokens[0].Lexeme;
                    var contentLexer = new Lexer(content);
                    var contentTokens = contentLexer.Tokenize();
                    
                    // Use LRS parser with parse tree building mode
                    var lrsParser = new LRSParser(contentTokens, buildParseTree: true);
                    var position = 0;
                    var astNode = lrsParser.ParseExpression(ref position);
                    
                    if (astNode == null)
                        throw new Exception("Failed to parse expression");
                    
                    // Convert AST to K list representation
                    var result = ParseTreeConverter.ToKList(astNode);
                    return result;
                }
                else
                {
                    // Use LRS parser with parse tree building mode for _parse function
                    var lrsParser = new LRSParser(tokens, buildParseTree: true);
                    var position = 0;
                    var astNode = lrsParser.ParseExpression(ref position);
                    
                    if (astNode == null)
                        throw new Exception("Failed to parse expression");
                    
                    // Convert AST to K list representation
                    var result = ParseTreeConverter.ToKList(astNode);
                    return result;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Parse error: {ex.Message}");
            }
        }
    }
}