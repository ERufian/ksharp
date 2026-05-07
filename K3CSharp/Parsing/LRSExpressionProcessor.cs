using System;
using System.Collections.Generic;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Processes expressions in the LRS parser by coordinating token processing
    /// Fixed: ProcessMonadicExpression now handles adverb tokens correctly
    /// </summary>
    /// Replaces ExpressionParser.ParsePrimary() calls with LRS-based processing
    /// Routes tokens to appropriate LRS parsers using verb-agnostic design
    /// </summary>
    public class LRSExpressionProcessor
    {
        private readonly List<Token> tokens;
        private readonly bool buildParseTree;
        private readonly LRSParser? parentParser;
        
        public LRSExpressionProcessor(List<Token> tokens, bool buildParseTree = false, LRSParser? parentParser = null)
        {
            this.tokens = tokens;
            this.buildParseTree = buildParseTree;
            this.parentParser = parentParser;
        }

        /// <summary>
        /// Process expression from current position
        /// </summary>
        /// <param name="position">Reference to current position, updated to end of expression</param>
        /// <returns>AST node representing processed expression</returns>
        public ASTNode? ProcessExpression(ref int position)
        {
            if (position >= tokens.Count)
                return null;

            var token = tokens[position];
            
            // Route to appropriate parser based on token type
            if (LRSAtomicParser.CanBeParsedByAtomicParser(token.Type))
            {
                return ProcessAtomicExpression(ref position);
            }
            
            switch (token.Type)
            {
                case TokenType.LEFT_PAREN:
                    return ProcessParenthesesExpression(ref position);
                    
                case TokenType.LEFT_BRACKET:
                    return ProcessBracketExpression(ref position);
                    
                case TokenType.LEFT_BRACE:
                    return ProcessBraceExpression(ref position);
                    
                // Handle monadic operators
                case TokenType.PLUS:
                case TokenType.MINUS:
                case TokenType.MULTIPLY:
                case TokenType.DIVIDE:
                case TokenType.MODULUS:
                case TokenType.POWER:
                case TokenType.JOIN:
                case TokenType.MATCH:
                case TokenType.NOT:
                case TokenType.HASH:
                case TokenType.UNDERSCORE:
                case TokenType.QUESTION:
                case TokenType.DOLLAR:
                case TokenType.APPLY:
                    return ProcessMonadicExpression(ref position);
                    
                // Handle special functions
                case TokenType.PARSE:
                case TokenType.EVAL:
                    return ProcessFunctionExpression(ref position);
                    
                // Don't handle adverbs here - let the expression-level parsing handle them
                default:
                    if (VerbRegistry.IsAdverbToken(token.Type))
                    {
                        // Skip adverb tokens to let expression-level parsing handle nested adverbs
                        position++;
                        return null;
                    }
                    
                    // Handle system functions using VerbRegistry
                    if (OperatorDetector.IsFunction(token.Type))
                    {
                        return ProcessFunctionExpression(ref position);
                    }
                    
                    // Handle dyadic operators (should be handled at higher level)
                    if (OperatorDetector.IsDyadicOperator(token.Type))
                    {
                        return null; // Let caller handle dyadic operations
                    }
                    
                    throw new Exception($"Unexpected token in expression: {token.Type}({token.Lexeme})");
            }
        }

        /// <summary>
        /// Process atomic expression (literals, identifiers)
        /// </summary>
        private ASTNode? ProcessAtomicExpression(ref int position)
        {
            if (position >= tokens.Count)
                return null;
                
            var token = tokens[position];
            position++;
            
            return LRSAtomicParser.ParseAtomicToken(token);
        }

        /// <summary>
        /// Process parentheses expression using LRSGroupingParser
        /// </summary>
        private ASTNode? ProcessParenthesesExpression(ref int position)
        {
            var groupingParser = new LRSGroupingParser(tokens, buildParseTree, parentParser);
            return groupingParser.ParseParentheses(ref position);
        }

        /// <summary>
        /// Process bracket expression using LRSGroupingParser
        /// </summary>
        private ASTNode? ProcessBracketExpression(ref int position)
        {
            var groupingParser = new LRSGroupingParser(tokens, buildParseTree, parentParser);
            return groupingParser.ParseBrackets(ref position);
        }

        /// <summary>
        /// Process brace expression using LRSGroupingParser
        /// </summary>
        private ASTNode? ProcessBraceExpression(ref int position)
        {
            var groupingParser = new LRSGroupingParser(tokens, buildParseTree, parentParser);
            return groupingParser.ParseBraces(ref position);
        }

        /// <summary>
        /// Process monadic expression using LRSMonadicParser
        /// </summary>
        private ASTNode? ProcessMonadicExpression(ref int position)
        {
            if (position >= tokens.Count)
                return null;
                
            // Extract tokens for monadic expression
            var monadicTokens = new List<Token>();
            var startPos = position;
            
            // Add the monadic operator
            monadicTokens.Add(tokens[position]);
            position++;
            
            // Add operand tokens until we hit a dyadic operator or delimiter
            int depth = 0;
            while (position < tokens.Count)
            {
                var currentToken = tokens[position];
                
                // Track nesting depth for grouping constructs
                if (currentToken.Type == TokenType.LEFT_PAREN || 
                    currentToken.Type == TokenType.LEFT_BRACKET || 
                    currentToken.Type == TokenType.LEFT_BRACE)
                {
                    depth++;
                }
                else if (currentToken.Type == TokenType.RIGHT_PAREN || 
                         currentToken.Type == TokenType.RIGHT_BRACKET || 
                         currentToken.Type == TokenType.RIGHT_BRACE)
                {
                    depth--;
                }
                
                if (VerbRegistry.IsAdverbToken(currentToken.Type))
                {
                    monadicTokens.Add(currentToken);               
                }
                
                // Stop if we hit a dyadic operator (but not an adverb) or expression delimiter at depth 0
                // Only stop on semicolons/newlines at depth 0 (not inside parentheses)
                bool shouldBreak = (OperatorDetector.IsDyadicOperator(currentToken.Type) && !VerbRegistry.IsAdverbToken(currentToken.Type)) ||
                                   (depth == 0 && (currentToken.Type == TokenType.SEMICOLON ||
                                                   currentToken.Type == TokenType.NEWLINE));
                
                // For closing delimiters at depth 0, include them in tokens then break
                // This ensures complete parenthesized expressions are captured
                if (depth == 0 && (currentToken.Type == TokenType.RIGHT_PAREN ||
                                   currentToken.Type == TokenType.RIGHT_BRACKET ||
                                   currentToken.Type == TokenType.RIGHT_BRACE))
                {
                    monadicTokens.Add(currentToken);
                    position++;
                    break;
                }
                
                if (shouldBreak)
                {
                    break;
                }
                
                monadicTokens.Add(currentToken);
                position++;
            }
            
            // Parse the monadic expression
            if (monadicTokens.Count >= 2)
            {
                var monadicParser = new LRSMonadicParser(new LRSParser(tokens, buildParseTree));
                return monadicParser.ParseMonadicOperator(monadicTokens);
            }
            
            // Fallback to atomic parsing if only operator
            if (monadicTokens.Count == 1)
            {
                // Handle operator tokens (verbs) that can't be parsed as atomic tokens
                if (VerbRegistry.IsVerbToken(monadicTokens[0].Type) || OperatorDetector.IsDyadicOperator(monadicTokens[0].Type))
                {
                    return LRSAtomicParser.CreateOperatorNode(monadicTokens[0].Type);
                }
                return LRSAtomicParser.ParseAtomicToken(monadicTokens[0], parentParser);
            }
            
            return null;
        }

        /// <summary>
        /// Process function expression using LRSFunctionParser
        /// </summary>
        private ASTNode? ProcessFunctionExpression(ref int position)
        {
            if (position >= tokens.Count)
                return null;
                
            // Extract tokens for function call
            var funcTokens = new List<Token>();
            var startPos = position;
            
            // Add the function token
            funcTokens.Add(tokens[position]);
            position++;
            
            // Add argument tokens until we hit a delimiter
            while (position < tokens.Count)
            {
                var currentToken = tokens[position];
                
                // Stop if we hit an expression delimiter
                if (currentToken.Type == TokenType.SEMICOLON ||
                    currentToken.Type == TokenType.NEWLINE ||
                    currentToken.Type == TokenType.RIGHT_PAREN ||
                    currentToken.Type == TokenType.RIGHT_BRACKET ||
                    currentToken.Type == TokenType.RIGHT_BRACE)
                {
                    break;
                }
                
                funcTokens.Add(currentToken);
                position++;
            }
            
            // Parse the function call
            var functionParser = new LRSFunctionParser(tokens, buildParseTree);
            return functionParser.ParseFunctionCall(funcTokens);
        }

        /// <summary>
        /// Check if processor can handle the current token
        /// </summary>
        public bool CanHandle(TokenType tokenType)
        {
            return LRSAtomicParser.CanBeParsedByAtomicParser(tokenType) ||
                   tokenType == TokenType.LEFT_PAREN ||
                   tokenType == TokenType.LEFT_BRACKET ||
                   tokenType == TokenType.LEFT_BRACE ||
                   OperatorDetector.SupportsMonadic(tokenType) ||
                   OperatorDetector.IsFunction(tokenType) ||
                   tokenType == TokenType.PARSE ||
                   tokenType == TokenType.EVAL;
        }

        /// <summary>
        /// Process complex expression structure (multiple tokens)
        /// </summary>
        public ASTNode? ProcessComplexExpression(List<Token> expressionTokens)
        {
            if (expressionTokens.Count == 0)
                return null;
                
            if (expressionTokens.Count == 1)
                return LRSAtomicParser.ParseAtomicToken(expressionTokens[0]);
            
            // Use LRS parser for complex expressions
            var lrsParser = new LRSParser(expressionTokens, buildParseTree);
            var position = 0;
            return lrsParser.ParseExpression(ref position);
        }
    }
}
