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
    /// Enhanced Expression Boundary Detection for LRS parser
    /// Improves delimiter tracking for complex expressions
    /// Handles nested structures better using verb properties
    /// Fixes partial consumption issues (e.g., "consumed: 18/19")
    /// </summary>
    public class LRSExpressionBoundaryDetector
    {
        private readonly List<Token> tokens;
        private readonly DelimiterDepthTracker depthTracker;
        
        public LRSExpressionBoundaryDetector(List<Token> tokens)
        {
            this.tokens = tokens;
            this.depthTracker = new DelimiterDepthTracker();
        }
        
        /// <summary>
        /// Read tokens until we hit a valid expression boundary
        /// Enhanced version with better delimiter tracking and verb-aware boundary detection
        /// </summary>
        /// <param name="position">Starting position, updated to end of expression</param>
        /// <param name="context">Parsing context for better boundary detection</param>
        /// <returns>List of tokens representing the expression</returns>
        public List<Token> ReadExpressionTokens(ref int position, ExpressionContext context = ExpressionContext.Default)
        {
            var expressionTokens = new List<Token>();
            var startPosition = position;
            
            while (position < tokens.Count)
            {
                var token = tokens[position];
                
                // Check if we should stop at this token
                if (ShouldStopAtToken(token, position, startPosition, context))
                    break;
                
                // Update delimiter depth
                depthTracker.UpdateDepth(token.Type);
                
                // Check for delimiter depth violations
                if (depthTracker.HasNegativeDepth())
                {
                    // We've closed too many delimiters, this is likely an error
                    // Return what we have so far and let the caller handle the error
                    break;
                }
                
                // Check for complete expression boundary
                if (IsAtExpressionBoundary(token, position, context))
                    break;
                
                expressionTokens.Add(token);
                position++;
            }
            
            return expressionTokens;
        }
        
        /// <summary>
        /// Enhanced boundary detection using verb properties and context
        /// </summary>
        /// <param name="token">Current token to evaluate</param>
        /// <param name="position">Current position in token stream</param>
        /// <param name="startPosition">Starting position of the expression</param>
        /// <param name="context">Parsing context</param>
        /// <returns>True if parsing should stop at this token</returns>
        private bool ShouldStopAtToken(Token token, int position, int startPosition, ExpressionContext context)
        {
            // Always stop at end of tokens
            if (position >= tokens.Count)
                return true;
            
            // Don't stop if we haven't consumed any tokens yet (unless it's a clear error)
            if (position == startPosition && !IsErrorToken(token.Type))
                return false;
            
            // Stop at separators when at base level and we have content
            if (depthTracker.IsAtBaseLevel() && IsSeparatorToken(token.Type) && position > startPosition)
                return true;
            
            // Context-specific boundary detection
            return context switch
            {
                ExpressionContext.AdverbArgument => ShouldStopForAdverbArgument(token),
                ExpressionContext.FunctionArgument => ShouldStopForFunctionArgument(token),
                ExpressionContext.BracketContents => ShouldStopForBracketContents(token),
                ExpressionContext.Default => ShouldStopForDefaultContext(token),
                _ => false
            };
        }
        
        /// <summary>
        /// Check if we're at an expression boundary
        /// K has no operator precedence - all operators evaluate right-to-left
        /// </summary>
        private bool IsAtExpressionBoundary(Token token, int position, ExpressionContext context)
        {
            // Check for verb boundaries using verb-agnostic approach
            if (IsVerbBoundary(token, position))
                return true;
            
            // Check for adverb boundaries - but NOT if preceded by a verb (verb-immediate-left pattern)
            if (VerbRegistry.IsAdverbToken(token.Type) && depthTracker.IsAtBaseLevel())
            {
                // Don't treat adverb as boundary if previous token is a verb (e.g., +/ should stay together)
                // If position is 0 (no previous token) OR previous token is not a verb, it's a boundary
                if (position == 0 || !VerbRegistry.IsVerbToken(tokens[position - 1].Type))
                    return true;
                // If preceded by verb, adverb is NOT a boundary (e.g., +/ is one expression)
            }
            
            // No operator precedence in K - removed IsOperatorBoundary check
            
            return false;
        }
        
        /// <summary>
        /// Verb-agnostic verb boundary detection
        /// </summary>
        private bool IsVerbBoundary(Token token, int position)
        {
            // Check if this token represents a verb that could form a boundary
            var verbName = GetVerbName(token);
            if (string.IsNullOrEmpty(verbName))
                return false;
            
            // Use verb properties to determine boundary behavior
            var isMonadic = VerbQueryExtensions.IsMonadicOperation(verbName);
            var isDyadic = VerbQueryExtensions.IsDyadicOperation(verbName);
            
            // Multi-arity verbs (like #, _, $, @) can create complex boundaries
            if (isMonadic && isDyadic)
            {
                return ShouldCreateBoundaryForMultiArityVerb(token, position);
            }
            
            return false;
        }
        
        /// <summary>
        /// Handle boundary detection for multi-arity verbs
        /// </summary>
        private bool ShouldCreateBoundaryForMultiArityVerb(Token token, int position)
        {
            // For context-sensitive verbs, we need to look at surrounding tokens
            var hasLeftOperand = position > 0 && !IsSeparatorToken(tokens[position - 1].Type);
            var hasRightOperand = position + 1 < tokens.Count && !IsSeparatorToken(tokens[position + 1].Type);
            
            // Use VerbRegistry to determine preferred arity
            var preferredArity = VerbRegistry.GetPreferredArity(token.Lexeme, hasLeftOperand, hasRightOperand);
            
            // If we prefer dyadic but don't have a right operand, this might be a boundary
            if (preferredArity == 2 && !hasRightOperand)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Context-specific boundary detection for adverb arguments
        /// K has no operator precedence - all operators evaluate right-to-left
        /// </summary>
        private bool ShouldStopForAdverbArgument(Token token)
        {
            // Adverb arguments stop at separators when at base level
            // No operator precedence in K - all operators have equal precedence
            return depthTracker.IsAtBaseLevel() && IsSeparatorToken(token.Type);
        }
        
        /// <summary>
        /// Context-specific boundary detection for function arguments
        /// </summary>
        private bool ShouldStopForFunctionArgument(Token token)
        {
            // Function arguments stop at separators or closing delimiters
            return depthTracker.IsAtBaseLevel() && (IsSeparatorToken(token.Type) || IsClosingDelimiter(token.Type));
        }
        
        /// <summary>
        /// Context-specific boundary detection for bracket contents
        /// </summary>
        private bool ShouldStopForBracketContents(Token token)
        {
            // Bracket contents stop at closing brackets or separators (when not nested)
            return token.Type == TokenType.RIGHT_BRACKET || 
                   (depthTracker.IsAtBaseLevel() && IsSeparatorToken(token.Type));
        }
        
        /// <summary>
        /// Default context boundary detection
        /// </summary>
        private bool ShouldStopForDefaultContext(Token token)
        {
            // Default: stop at separators when at base level
            return depthTracker.IsAtBaseLevel() && IsSeparatorToken(token.Type);
        }
        
        /// <summary>
        /// Extract verb name from token using VerbRegistry
        /// </summary>
        private string GetVerbName(Token token)
        {
            // Use VerbRegistry for proper token-to-verb mapping
            string verbName;
            if (token.Type == TokenType.IDENTIFIER)
                verbName = token.Lexeme;
            else
                verbName = VerbRegistry.TokenTypeToVerbName(token.Type);
            
            return verbName;
        }
        
        /// <summary>
        /// Check if token type is a separator
        /// </summary>
        private bool IsSeparatorToken(TokenType tokenType)
        {
            return tokenType == TokenType.SEMICOLON || 
                   tokenType == TokenType.NEWLINE ||
                   tokenType == TokenType.EOF;
        }
        
        /// <summary>
        /// Check if token type is a closing delimiter
        /// </summary>
        private bool IsClosingDelimiter(TokenType tokenType)
        {
            return tokenType == TokenType.RIGHT_PAREN ||
                   tokenType == TokenType.RIGHT_BRACKET ||
                   tokenType == TokenType.RIGHT_BRACE;
        }
        
        /// <summary>
        /// Check if token type represents an error
        /// </summary>
        private bool IsErrorToken(TokenType tokenType)
        {
            // Since TokenType.ERROR doesn't exist, we'll handle this differently
            // For now, we'll consider unknown tokens as errors
            return tokenType == TokenType.UNKNOWN;
        }
        
        /// <summary>
        /// Get current delimiter depth for debugging
        /// </summary>
        public DelimiterDepth GetDepthInfo()
        {
            return depthTracker.GetCurrentDepth();
        }
        
        /// <summary>
        /// Reset the detector for reuse
        /// </summary>
        public void Reset()
        {
            depthTracker.Reset();
        }
    }
    
    /// <summary>
    /// Enhanced delimiter depth tracker
    /// </summary>
    public class DelimiterDepthTracker
    {
        private int parenDepth = 0;
        private int bracketDepth = 0;
        private int braceDepth = 0;
        
        /// <summary>
        /// Update depth based on token type
        /// </summary>
        public void UpdateDepth(TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.LEFT_PAREN:
                    parenDepth++;
                    break;
                case TokenType.RIGHT_PAREN:
                    parenDepth--;
                    break;
                case TokenType.LEFT_BRACKET:
                    bracketDepth++;
                    break;
                case TokenType.RIGHT_BRACKET:
                    bracketDepth--;
                    break;
                case TokenType.LEFT_BRACE:
                    braceDepth++;
                    break;
                case TokenType.RIGHT_BRACE:
                    braceDepth--;
                    break;
            }
        }
        
        /// <summary>
        /// Check if we're at base level (all delimiters balanced)
        /// </summary>
        public bool IsAtBaseLevel()
        {
            return parenDepth == 0 && bracketDepth == 0 && braceDepth == 0;
        }
        
        /// <summary>
        /// Check if we have negative depth (too many closing delimiters)
        /// </summary>
        public bool HasNegativeDepth()
        {
            return parenDepth < 0 || bracketDepth < 0 || braceDepth < 0;
        }
        
        /// <summary>
        /// Get current depth information
        /// </summary>
        public DelimiterDepth GetCurrentDepth()
        {
            return new DelimiterDepth
            {
                ParenDepth = parenDepth,
                BracketDepth = bracketDepth,
                BraceDepth = braceDepth
            };
        }
        
        /// <summary>
        /// Reset all depths to zero
        /// </summary>
        public void Reset()
        {
            parenDepth = 0;
            bracketDepth = 0;
            braceDepth = 0;
        }
    }
    
    /// <summary>
    /// Expression context for better boundary detection
    /// </summary>
    public enum ExpressionContext
    {
        Default,
        AdverbArgument,
        FunctionArgument,
        BracketContents
    }
    
    /// <summary>
    /// Delimiter depth information
    /// </summary>
    public class DelimiterDepth
    {
        public int ParenDepth { get; set; }
        public int BracketDepth { get; set; }
        public int BraceDepth { get; set; }
        
        public int TotalDepth => ParenDepth + BracketDepth + BraceDepth;
        
        public override string ToString()
        {
            return $"Paren:{ParenDepth}, Bracket:{BracketDepth}, Brace:{BraceDepth}";
        }
    }
}