using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Monadic operator parsing for LRS parser
    /// Handles monadic operations with proper verb-agnostic detection
    /// </summary>
    public class LRSMonadicParser
    {
        private readonly LRSParser parentParser;
        
        public LRSMonadicParser(LRSParser parentParser)
        {
            this.parentParser = parentParser;
        }
        
        /// <summary>
        /// Parse monadic operator expression from tokens
        /// Enhanced to handle all monadic operations without delegation
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <returns>AST node representing monadic operation</returns>
        public ASTNode? ParseMonadicOperator(List<Token> tokens)
        {
            if (tokens.Count < 1) return null;
            
            var operatorToken = tokens[0];
            
            // Check if operator supports monadic operations
            if (!OperatorDetector.SupportsMonadic(operatorToken.Type))
                return null;
            
            // Handle projection case (no operand)
            if (tokens.Count == 1 || IsProjectionContext(tokens))
            {
                return CreateProjectionNode(operatorToken);
            }
            
            // Parse operand
            var operandTokens = tokens.GetRange(1, tokens.Count - 1);
            var operand = ParseOperandTokens(operandTokens);
            
            if (operand == null)
                return null;
            
            return CreateMonadicNode(operatorToken, operand);
        }
        
        /// <summary>
        /// Check if this is a projection context (no operand or ending with delimiter)
        /// </summary>
        private bool IsProjectionContext(List<Token> tokens)
        {
            if (tokens.Count <= 1) return true;
            
            // Check if the last token is a delimiter indicating projection
            var lastToken = tokens[tokens.Count - 1];
            
            // Special case: if we have a complete parenthesized/bracketed expression,
            // it's not a projection even if it ends with a delimiter
            if (lastToken.Type == TokenType.RIGHT_PAREN ||
                lastToken.Type == TokenType.RIGHT_BRACKET ||
                lastToken.Type == TokenType.RIGHT_BRACE)
            {
                // Check if we have matching opening delimiter earlier in the tokens
                // This indicates a complete expression, not a projection
                var openingType = GetMatchingOpeningDelimiter(lastToken.Type);
                if (openingType != null && tokens.Any(t => t.Type == openingType))
                {
                    return false; // Complete expression, not a projection
                }
            }
            
            // Otherwise, check if it's a projection delimiter
            return lastToken.Type == TokenType.RIGHT_PAREN ||
                   lastToken.Type == TokenType.RIGHT_BRACKET ||
                   lastToken.Type == TokenType.RIGHT_BRACE ||
                   lastToken.Type == TokenType.SEMICOLON ||
                   lastToken.Type == TokenType.NEWLINE;
        }
        
        /// <summary>
        /// Get the matching opening delimiter for a closing delimiter
        /// </summary>
        private TokenType? GetMatchingOpeningDelimiter(TokenType closingType)
        {
            return closingType switch
            {
                TokenType.RIGHT_PAREN => TokenType.LEFT_PAREN,
                TokenType.RIGHT_BRACKET => TokenType.LEFT_BRACKET,
                TokenType.RIGHT_BRACE => TokenType.LEFT_BRACE,
                _ => null
            };
        }
        
        /// <summary>
        /// Create projection node for monadic operator
        /// </summary>
        private ASTNode CreateProjectionNode(Token operatorToken)
        {
            var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
            
            // Convert token type to operator symbol using VerbRegistry
            var operatorSymbol = VerbRegistry.TokenTypeToVerbName(operatorToken.Type);
            
            projectedNode.Value = new SymbolValue(operatorSymbol);
            
            // Determine arity from VerbRegistry - use highest supported arity as default
            var verb = VerbRegistry.GetVerb(operatorSymbol);
            int defaultArity = verb?.SupportedArities?.Max() ?? 2;
            projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(defaultArity)));
            
            return projectedNode;
        }
        
        /// <summary>
        /// Parse operand tokens using LRS parsing
        /// </summary>
        private ASTNode? ParseOperandTokens(List<Token> tokens)
        {
            if (tokens.Count == 0) return null;
            if (tokens.Count == 1) 
            {
                return CreateNodeFromToken(tokens[0]);
            }
            
            // Check if the first token is LEFT_PAREN - handle as grouped expression
            if (tokens[0].Type == TokenType.LEFT_PAREN)
            {
                // Use parent parser to handle the grouped expression
                var position = 0;
                var groupedResult = parentParser.ParseSubExpressionForMonadic(tokens, ref position);

                // If ParseSubExpressionForMonadic consumed all tokens and returned a result, use it directly
                // This avoids double-wrapping when the parent parser already handled remaining tokens
                if (position >= tokens.Count && groupedResult != null)
                {
                    return groupedResult;
                }

                // Check if there are remaining tokens after the parenthesized expression
                // This handles cases like (&x)!2 where !2 should form a dyadic operation with (&x)
                if (position < tokens.Count)
                {
                    // There are remaining tokens - create a dyadic operation
                    // The parenthesized result is the left operand, remaining tokens form right operand
                    var remainingTokens = tokens.GetRange(position, tokens.Count - position);

                    // Check if the first remaining token is an operator (dyadic)
                    if (remainingTokens.Count > 0 && OperatorDetector.SupportsDyadic(remainingTokens[0].Type))
                    {
                        // The first remaining token is the operator
                        var opToken = remainingTokens[0];
                        var rightTokens = remainingTokens.GetRange(1, remainingTokens.Count - 1);

                        // Parse the right operand
                        var rightPos = 0;
                        var rightNode = parentParser.ParseSubExpressionForMonadic(rightTokens, ref rightPos);

                        // Create dyadic node: groupedResult OP rightNode
                        var dyadicNode = new ASTNode(ASTNodeType.DyadicOp);
                        dyadicNode.Value = new SymbolValue(VerbRegistry.TokenTypeToVerbName(opToken.Type));
                        if (groupedResult != null) dyadicNode.Children.Add(groupedResult);
                        if (rightNode != null) dyadicNode.Children.Add(rightNode);
                        return dyadicNode;
                    }
                    else
                    {
                        // No operator found - just return the grouped result
                        return groupedResult;
                    }
                }

                return groupedResult;
            }
            
            // For multiple tokens, check if they form a vector (no operators)
            bool allAtomic = AllTokensAreAtomic(tokens);
            
            if (allAtomic)
            {
                // Check for IDENTIFIER-led function application: e.g., "log10 x"
                // IDENTIFIER followed by another token is function application, not a vector
                if (tokens.Count >= 2 && tokens[0].Type == TokenType.IDENTIFIER &&
                    !VerbRegistry.IsVerbToken(tokens[0].Type))
                {
                    var fnNode = CreateNodeFromToken(tokens[0]);
                    var argTokens = tokens.GetRange(1, tokens.Count - 1);
                    ASTNode? argNode = argTokens.Count == 1
                        ? CreateNodeFromToken(argTokens[0])
                        : CreateVectorFromTokens(argTokens);
                    if (fnNode != null && argNode != null)
                        return ASTNode.MakeFunctionCall(fnNode, new System.Collections.Generic.List<ASTNode> { argNode });
                }
                // Create a vector from these atomic tokens
                return CreateVectorFromTokens(tokens);
            }
            
            // Check if we have enough tokens for a dyadic operation and the first token supports dyadic
            bool firstSupportsDyadic = tokens.Count >= 3 && OperatorDetector.SupportsDyadic(tokens[0].Type);
            
            if (firstSupportsDyadic)
            {
                // Try dyadic operation first when we have enough tokens
                var position2 = 0;
                return parentParser.ParseSubExpressionForMonadic(tokens, ref position2);
            }
            
            // Check if the first token of the remaining expression is another monadic operator
            bool firstIsMonadic = tokens.Count >= 1 && LRSMonadicParser.CouldBeMonadicOperator(tokens[0].Type);
            
            if (firstIsMonadic)
            {
                // This is a nested monadic operation like ^,`a
                // Parse it recursively using parent parser's mode
                var position = 0;
                var nestedResult = parentParser.ParseSubExpressionForMonadic(tokens, ref position);
                return nestedResult;
            }
            
            // Otherwise, use parent parser for dyadic operations
            var position3 = 0;
            return parentParser.ParseSubExpressionForMonadic(tokens, ref position3);
        }
        
        /// <summary>
        /// Check if all tokens are atomic (no operators and no grouping delimiters)
        /// </summary>
        private bool AllTokensAreAtomic(List<Token> tokens)
        {
            foreach (var token in tokens)
            {
                if (IsOperatorToken(token.Type))
                    return false;
                // Also check for grouping delimiters - they are not atomic
                if (IsGroupingDelimiter(token.Type))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check if token is a grouping delimiter (parenthesis, bracket, brace)
        /// </summary>
        private bool IsGroupingDelimiter(TokenType tokenType)
        {
            return tokenType == TokenType.LEFT_PAREN || tokenType == TokenType.RIGHT_PAREN ||
                   tokenType == TokenType.LEFT_BRACKET || tokenType == TokenType.RIGHT_BRACKET ||
                   tokenType == TokenType.LEFT_BRACE || tokenType == TokenType.RIGHT_BRACE ||
                   tokenType == TokenType.SEMICOLON;
        }
        
        /// <summary>
        /// Check if token is an operator
        /// </summary>
        private bool IsOperatorToken(TokenType tokenType)
        {
            return OperatorDetector.IsDyadicOperator(tokenType) || 
                   LRSMonadicParser.CouldBeMonadicOperator(tokenType) ||
                   VerbRegistry.IsVerbToken(tokenType);
        }
        
        /// <summary>
        /// Create vector AST node from multiple atomic tokens
        /// </summary>
        private ASTNode CreateVectorFromTokens(List<Token> tokens)
        {
            // Create a proper Vector node for data vectors
            var elements = new List<ASTNode>();
            foreach (var token in tokens)
            {
                var atomicNode = CreateNodeFromToken(token);
                if (atomicNode != null)
                    elements.Add(atomicNode);
            }
            return ASTNode.MakeVector(elements);
        }
        
        /// <summary>
        /// Create AST node for monadic operation
        /// </summary>
        private ASTNode CreateMonadicNode(Token operatorToken, ASTNode operand)
        {
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue(VerbRegistry.TokenTypeToVerbName(operatorToken.Type));
            node.Children.Add(operand);
            return node;
        }
        
        /// <summary>
        /// Check if the first token could be a monadic operator
        /// </summary>
        public static bool CouldBeMonadicOperator(TokenType tokenType)
        {
            return OperatorDetector.SupportsMonadic(tokenType);
        }
        
        /// <summary>
        /// Create AST node from atomic token using LRSAtomicParser
        /// </summary>
        private ASTNode CreateNodeFromToken(Token token)
        {
            // Handle adverb tokens by delegating to parent parser
            if (VerbRegistry.IsAdverbToken(token.Type))
            {
                if (parentParser != null)
                {
                    return parentParser.CreateNodeFromToken(token);
                }
                throw new Exception($"Cannot process adverb token without parent parser: {token.Type}({token.Lexeme})");
            }
            
            // Handle verb tokens by delegating to parent parser
            if (VerbRegistry.IsVerbToken(token.Type))
            {
                if (parentParser != null)
                {
                    return parentParser.CreateNodeFromToken(token);
                }
                throw new Exception($"Cannot process verb token without parent parser: {token.Type}({token.Lexeme})");
            }
            
            // Handle COLON tokens by delegating to parent parser (for disambiguating colon patterns)
            if (token.Type == TokenType.COLON)
            {
                if (parentParser != null)
                {
                    return parentParser.CreateNodeFromToken(token);
                }
                throw new Exception($"Cannot process COLON token without parent parser: {token.Type}({token.Lexeme})");
            }
            
            if (!LRSAtomicParser.CanBeParsedByAtomicParser(token.Type))
            {
                throw new Exception($"CreateNodeFromToken called with non-atomic token: {token.Type}({token.Lexeme})");
            }
            return LRSAtomicParser.ParseAtomicToken(token);
        }
    }
}

