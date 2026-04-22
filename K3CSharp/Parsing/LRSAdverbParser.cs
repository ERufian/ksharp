using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Adverb processing for LRS parser
    /// Handles adverb operations (each, over, scan, converge) using VerbRegistry categories
    /// Supports verb-agnostic adverb parsing without PrimaryParser dependency
    /// </summary>
    public class LRSAdverbParser
    {
        private readonly List<Token> tokens;
        private readonly bool buildParseTree;
        
        public LRSAdverbParser(List<Token> tokens, bool buildParseTree = false)
        {
            this.tokens = tokens;
            this.buildParseTree = buildParseTree;
        }

        /// <summary>
        /// Parse adverb operation from tokens
        /// The verb must be immediately to the LEFT of the adverb (per K spec line 847-848)
        /// </summary>
        /// <param name="position">Reference to current position, updated to end of adverb expression</param>
        /// <returns>AST node representing adverb operation</returns>
        public ASTNode? ParseAdverbOperation(ref int position)
        {
            if (position >= tokens.Count)
                return null;

            var adverbToken = tokens[position];
            
            // Check if this is an adverb using VerbRegistry
            if (!VerbRegistry.IsAdverbToken(adverbToken.Type))
                return null;

            // Per K spec line 847: "Spaces are not allowed between a verb and its adverb"
            // Per K spec line 848: "The binding of an adverb to a verb has higher lexical precedence"
            // The verb must be immediately to the LEFT of the adverb
            
            // First, find the verb by looking BACKWARDS from the adverb position
            var leftArg = ParseVerbBeforeAdverb(position);
            if (leftArg == null)
                throw new Exception($"Expected verb immediately before adverb {adverbToken.Lexeme}");
            
            position++; // Consume adverb token

            // Parse the right argument for the adverb
            var rightArg = ParseAdverbArgument(ref position);
            if (rightArg == null)
                throw new Exception($"Expected expression after adverb {adverbToken.Lexeme}");

            // Create adverb node: ADVERB(adverb, verb, rightArg)
            return CreateAdverbNode(adverbToken, leftArg, rightArg);
        }
        
        /// <summary>
        /// Parse the verb that appears immediately BEFORE the adverb at the given position
        /// Per K spec: "Spaces are not allowed between a verb and its adverb"
        /// Handles disambiguating colons: colon between verb and adverb indicates monadic interpretation
        /// E.g., #:' means monadic # (count) with each adverb
        /// Also handles modified verbs (verbs with adverbs attached): #:' is itself a valid verb for another adverb
        /// E.g., #:'' means (count each) each
        /// </summary>
        /// <param name="adverbPosition">Position of the adverb token</param>
        /// <returns>AST node for the verb to the left of the adverb</returns>
        private ASTNode? ParseVerbBeforeAdverb(int adverbPosition)
        {
            if (adverbPosition <= 0)
                return null;
            
            // The verb is immediately before the adverb
            int verbPosition = adverbPosition - 1;
            var verbToken = tokens[verbPosition];
            
            // Handle nested adverbs: if the token before us is an adverb, 
            // then what precedes it is a modified verb (e.g., #:' in #:'' )
            // Recursively parse that adverb-modified verb as our verb
            if (VerbRegistry.IsAdverbToken(verbToken.Type))
            {
                // The verb is actually an adverb-modified verb
                // Parse it as an adverb operation - this gives us the modified verb
                var modifiedVerbPosition = verbPosition;
                var modifiedVerb = ParseAdverbOperation(ref modifiedVerbPosition);
                if (modifiedVerb != null)
                {
                    // The position after parsing should be where the actual base verb starts
                    // We need to adjust because ParseAdverbOperation consumed tokens
                    // Return the modified verb as our verb
                    return modifiedVerb;
                }
            }
            
            // Handle disambiguating colon: verb + colon + adverb means monadic verb + adverb
            // E.g., #:' where : indicates monadic # (count) rather than dyadic # (take)
            if (verbToken.Type == TokenType.COLON && verbPosition > 0)
            {
                int beforeColonPosition = verbPosition - 1;
                var beforeColonToken = tokens[beforeColonPosition];
                
                // Check if token before colon is a verb that can use disambiguating colon
                string? verbName = GetVerbNameFromToken(beforeColonToken);
                
                if (verbName != null && VerbRegistry.HasVerb(verbName))
                {
                    // Return the verb with disambiguating colon (monadic interpretation)
                    return ASTNode.MakeLiteral(new SymbolValue(verbName));
                }
                
                // Handle operators that can be disambiguated with colon
                if (IsDyadicOperator(beforeColonToken.Type) || 
                    OperatorDetector.SupportsMonadic(beforeColonToken.Type))
                {
                    return CreateOperatorNode(beforeColonToken);
                }
            }
            
            // Handle system verbs and other registered verbs using VerbRegistry
            string? verbNameDirect = GetVerbNameFromToken(verbToken);
            if (verbNameDirect != null && VerbRegistry.HasVerb(verbNameDirect))
            {
                return ASTNode.MakeLiteral(new SymbolValue(verbNameDirect));
            }
            
            // Handle atomic verbs (identifiers, symbols) as fallback
            if (LRSAtomicParser.CanBeParsedByAtomicParser(verbToken.Type))
            {
                return LRSAtomicParser.ParseAtomicToken(verbToken);
            }
            
            // Handle operator verbs as fallback
            if (IsDyadicOperator(verbToken.Type))
            {
                return CreateOperatorNode(verbToken);
            }
            
            // Handle monadic operators as verbs
            if (OperatorDetector.SupportsMonadic(verbToken.Type))
            {
                return CreateOperatorNode(verbToken);
            }
            
            return null;
        }

        /// <summary>
        /// Parse adverb argument (right side of adverb)
        /// </summary>
        private ASTNode? ParseAdverbArgument(ref int position)
        {
            if (position >= tokens.Count)
                return null;

            // Check if all remaining tokens are atomic - if so, create a vector
            // But only for actual literals, not identifiers which need runtime resolution
            bool allAtomic = true;
            bool hasIdentifier = false;
            for (int i = position; i < tokens.Count; i++)
            {
                if (!LRSAtomicParser.CanBeImplicitVectorElement(tokens[i].Type))
                {
                    allAtomic = false;
                    break;
                }
                if (tokens[i].Type == TokenType.IDENTIFIER)
                    hasIdentifier = true;
            }

            // Single identifier: return as variable node for runtime resolution
            if (allAtomic && tokens.Count - position == 1 && hasIdentifier)
            {
                var node = LRSAtomicParser.ParseAtomicToken(tokens[position]);
                position++;
                return node;
            }

            if (allAtomic && !hasIdentifier && tokens.Count > position)
            {
                // Create a VectorValue from all remaining atomic literal tokens
                var values = new List<K3Value>();
                while (position < tokens.Count)
                {
                    var atomicNode = LRSAtomicParser.ParseAtomicToken(tokens[position]);
                    if (atomicNode.Value is K3Value kv)
                    {
                        values.Add(kv);
                    }
                    position++;
                }
                var vectorValue = new VectorValue(values);
                return ASTNode.MakeLiteral(vectorValue);
            }

            // Use LRS expression processor to parse the argument
            var expressionProcessor = new LRSExpressionProcessor(tokens, buildParseTree);
            return expressionProcessor.ProcessExpression(ref position);
        }

        /// <summary>
        /// Parse simple two-glyph adverb operation
        /// The verb must be immediately to the LEFT of the adverb (per K spec)
        /// </summary>
        /// <param name="position">Reference to current position, updated to end of adverb expression</param>
        /// <returns>AST node representing two-glyph adverb operation</returns>
        public ASTNode? ParseSimpleTwoGlyphAdverb(ref int position)
        {
            if (position >= tokens.Count)
                return null;

            var adverbToken = tokens[position];
            
            // Check if this is a simple two-glyph adverb
            if (!IsSimpleTwoGlyphAdverb(adverbToken.Type))
                return null;

            // Per K spec: verb must be immediately to the LEFT of the adverb
            var leftArg = ParseVerbBeforeAdverb(position);
            if (leftArg == null)
                throw new Exception($"Expected verb immediately before adverb {adverbToken.Lexeme}");

            position++; // Consume adverb token

            // Parse the right argument for the adverb
            var rightArg = ParseAdverbArgument(ref position);
            if (rightArg == null)
                throw new Exception($"Expected expression after adverb {adverbToken.Lexeme}");

            // Create two-glyph adverb node
            return CreateTwoGlyphAdverbNode(adverbToken, leftArg, rightArg);
        }
        
        /// <summary>
        /// Parse verb-immediate-left adverb pattern (verb on left side only)
        /// The verb must be immediately to the LEFT of the adverb (per K spec)
        /// </summary>
        /// <param name="position">Reference to current position, updated to end of adverb expression</param>
        /// <returns>AST node representing verb-immediate-left adverb operation</returns>
        public ASTNode? ParseVerbImmediateLeftAdverb(ref int position)
        {
            if (position >= tokens.Count)
                return null;

            var adverbToken = tokens[position];
            
            // Check if this is a single-glyph adverb that can have verb-immediate-left pattern
            if (!IsVerbImmediateLeftAdverb(adverbToken.Type))
                return null;

            // Per K spec: verb must be immediately to the LEFT of the adverb
            var leftArg = ParseVerbBeforeAdverb(position);
            if (leftArg == null)
                throw new Exception($"Expected verb immediately before adverb {adverbToken.Lexeme}");

            position++; // Consume adverb token

            // Parse the right argument for the adverb
            var rightArg = ParseAdverbArgument(ref position);
            if (rightArg == null)
                throw new Exception($"Expected expression after adverb {adverbToken.Lexeme}");

            // Create verb-immediate-left adverb node
            return CreateVerbImmediateLeftAdverbNode(adverbToken, leftArg, rightArg);
        }
        
        /// <summary>
        /// Check if token type is a simple two-glyph adverb
        /// </summary>
        private bool IsSimpleTwoGlyphAdverb(TokenType tokenType)
        {
            return tokenType == TokenType.ADVERB_SLASH_COLON ||
                   tokenType == TokenType.ADVERB_BACKSLASH_COLON ||
                   tokenType == TokenType.ADVERB_TICK_COLON;
        }
        
        /// <summary>
        /// Check if token type supports verb-immediate-left pattern
        /// </summary>
        private bool IsVerbImmediateLeftAdverb(TokenType tokenType)
        {
            return tokenType == TokenType.ADVERB_SLASH ||
                   tokenType == TokenType.ADVERB_BACKSLASH ||
                   tokenType == TokenType.ADVERB_TICK;
        }
        
        /// <summary>
        /// Get verb name from token using VerbRegistry (verb-agnostic)
        /// </summary>
        /// <param name="token">Token to convert</param>
        /// <returns>Verb name or null if not a verb</returns>
        private string? GetVerbNameFromToken(Token token)
        {
            // Use VerbRegistry to get verb name from token type
            var verbName = VerbRegistry.TokenTypeToVerbName(token.Type);
            // Return null if it's not a recognized verb (TokenTypeToVerbName returns the token type string for unknown verbs)
            return VerbRegistry.HasVerb(verbName) ? verbName : null;
        }
        
        /// <summary>
        /// Check if token type is a dyadic operator using VerbRegistry (verb-agnostic)
        /// </summary>
        private bool IsDyadicOperator(TokenType tokenType)
        {
            return VerbRegistry.IsDyadicOperatorToken(tokenType);
        }
        
        /// <summary>
        /// Create operator node from token
        /// </summary>
        private ASTNode CreateOperatorNode(Token token)
        {
            return new ASTNode(ASTNodeType.DyadicOp, new SymbolValue(token.Lexeme), new List<ASTNode>());
        }
        
        /// <summary>
        /// Create two-glyph adverb node with correct arity
        /// </summary>
        private ASTNode CreateTwoGlyphAdverbNode(Token adverbToken, ASTNode leftArg, ASTNode rightArg)
        {
            // Two-glyph adverbs (/: \: ':) are always dyadic per K spec
            var children = new List<ASTNode> { leftArg, rightArg };
            return new ASTNode(ASTNodeType.DyadicOp, new SymbolValue(adverbToken.Lexeme), children);
        }
        
        /// <summary>
        /// Create verb-immediate-left adverb node with correct arity
        /// Note: Uses DyadicOp for all adverbs (K3CSharp convention), evaluator handles arity
        /// Evaluator expects 3 children for adverb operations: verb, leftArg, rightArg
        /// For monadic each (verb-immediate-left), leftArg is 0
        /// </summary>
        private ASTNode CreateVerbImmediateLeftAdverbNode(Token adverbToken, ASTNode leftArg, ASTNode rightArg)
        {
            // Note: K3CSharp uses DyadicOp for all operators regardless of arity
            // The evaluator determines actual arity from the adverb type and verb characteristics
            // Evaluator expects: children[0] = verb, children[1] = leftArg (0 for monadic), children[2] = rightArg
            var zeroNode = ASTNode.MakeLiteral(new NullValue());
            var children = new List<ASTNode> { leftArg, zeroNode, rightArg };
            return new ASTNode(ASTNodeType.DyadicOp, new SymbolValue(GetAdverbSymbol(adverbToken.Type)), children);
        }
        
        /// <summary>
        /// Parse adverb verb (left side of adverb)
        /// </summary>
        private ASTNode? ParseAdverbVerb(ref int position)
        {
            if (position >= tokens.Count)
                return null;

            // Check if we have a verb before the adverb
            var currentToken = tokens[position];
            
            // Handle different types of verbs
            if (LRSAtomicParser.CanBeParsedByAtomicParser(currentToken.Type))
            {
                // Atomic value (literal, identifier, symbol)
                position++;
                return LRSAtomicParser.ParseAtomicToken(currentToken);
            }
            
            // Handle grouping constructs as verbs
            switch (currentToken.Type)
            {
                case TokenType.LEFT_PAREN:
                    var groupingParser = new LRSGroupingParser(tokens, buildParseTree);
                    return groupingParser.ParseParentheses(ref position);
                    
                case TokenType.LEFT_BRACKET:
                    var bracketParser = new LRSGroupingParser(tokens, buildParseTree);
                    return bracketParser.ParseBrackets(ref position);
                    
                case TokenType.LEFT_BRACE:
                    var braceParser = new LRSGroupingParser(tokens, buildParseTree);
                    return braceParser.ParseBraces(ref position);
                    
                // Handle monadic operators as verbs
                default:
                    if (OperatorDetector.SupportsMonadic(currentToken.Type))
                    {
                        var monadicTokens = new List<Token> { currentToken };
                        
                        // Check if there are more tokens for the operand
                        if (position + 1 < tokens.Count)
                        {
                            var nextToken = tokens[position + 1];
                            if (!OperatorDetector.IsDyadicOperator(nextToken.Type) &&
                                !VerbRegistry.IsAdverbToken(nextToken.Type))
                            {
                                // Include the operand
                                monadicTokens.Add(nextToken);
                                position += 2;
                            }
                            else
                            {
                                position++;
                            }
                        }
                        else
                        {
                            position++;
                        }
                        
                        var monadicParser = new LRSMonadicParser(new LRSParser(tokens, buildParseTree));
                        return monadicParser.ParseMonadicOperator(monadicTokens);
                    }
                    
                    throw new Exception($"Unexpected token in adverb verb: {currentToken.Type}({currentToken.Lexeme})");
            }
        }

        /// <summary>
        /// Parse adverb chain (multiple adverbs applied to same verb)
        /// </summary>
        public ASTNode? ParseAdverbChain(ref int position)
        {
            if (position >= tokens.Count)
                return null;

            var nodes = new List<ASTNode>();
            
            // Parse the first verb
            var verb = ParseAdverbVerb(ref position);
            if (verb == null)
                return null;

            nodes.Add(verb);

            // Parse subsequent adverbs
            while (position < tokens.Count && VerbRegistry.IsAdverbToken(tokens[position].Type))
            {
                var adverbToken = tokens[position];
                position++;

                // Parse adverb argument
                var arg = ParseAdverbArgument(ref position);
                if (arg == null)
                    throw new Exception($"Expected expression after adverb {adverbToken.Lexeme}");

                // Create adverb node with previous result as verb
                var adverbNode = CreateAdverbNode(adverbToken, nodes[nodes.Count - 1], arg);
                nodes[nodes.Count - 1] = adverbNode; // Replace with adverb result
            }

            return nodes.Count > 0 ? nodes[0] : null;
        }

        /// <summary>
        /// Validate that a verb is compatible with the given adverb
        /// </summary>
        /// <param name="verbNode">The verb AST node to validate</param>
        /// <param name="adverbType">The adverb type to check compatibility for</param>
        /// <returns>True if the verb is compatible with the adverb</returns>
        private bool ValidateVerbAdverbCompatibility(ASTNode verbNode, string adverbType)
        {
            // Extract verb name from the AST node
            var verbName = ExtractVerbName(verbNode);
            if (string.IsNullOrEmpty(verbName))
                return false;
            
            // Use VerbRegistry.SupportsAdverbs directly for more comprehensive validation
            return VerbRegistry.SupportsAdverbs(verbName, adverbType);
        }
        
        /// <summary>
        /// Extract verb name from AST node
        /// </summary>
        /// <param name="verbNode">The AST node containing the verb</param>
        /// <returns>Verb name or null if not found</returns>
        private string? ExtractVerbName(ASTNode verbNode)
        {
            if (verbNode.Value is SymbolValue symbol)
                return symbol.Value;
            
            // For variable nodes, check the node type and extract name differently
            if (verbNode.Type == ASTNodeType.Variable)
            {
                // Variable nodes store the name in a different way
                // For now, we'll handle this case by returning null
                // This could be enhanced based on how variable nodes are actually implemented
                return null;
            }
            
            return null;
        }
        
        /// <summary>
        /// Create AST node for adverb operation with validation
        /// Creates correct node type (MonadicOp or DyadicOp) based on modified verb's arity
        /// </summary>
        private ASTNode CreateAdverbNode(Token adverbToken, ASTNode verb, ASTNode? argument = null)
        {
            var adverbType = VerbRegistry.GetAdverbType(adverbToken.Type);
            
            // Validate verb-adverb compatibility using verb-agnostic approach
            if (!ValidateVerbAdverbCompatibility(verb, adverbType))
            {
                var verbName = ExtractVerbName(verb) ?? "unknown";
                throw new Exception($"Verb '{verbName}' is not compatible with adverb '{adverbToken.Lexeme}' ({adverbType})");
            }
            
            // Note: K3CSharp uses DyadicOp for all operators regardless of arity
            // The evaluator determines actual arity from:
            // - K spec (lines 1092-1098): / \ can be monadic or dyadic, ' preserves arity,
            //   \: /: are always dyadic, ': can be monadic or dyadic
            // - Disambiguating colon: verb: before adverb forces monadic interpretation
            // - Default: dyadic interpretation for polymorphic verbs (K spec line 844)
            
            var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
            adverbNode.Value = new SymbolValue(GetAdverbSymbol(adverbToken.Type));
            adverbNode.Children.Add(verb);
            if (argument != null)
                adverbNode.Children.Add(argument);
            return adverbNode;
        }
        
        /// <summary>
        /// Get adverb symbol for token type
        /// Returns evaluator-expected symbols (e.g., "each" not "'")
        /// </summary>
        private string GetAdverbSymbol(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.ADVERB_TICK => "each",
                TokenType.ADVERB_SLASH => "over",
                TokenType.ADVERB_BACKSLASH => "scan",
                TokenType.ADVERB_SLASH_COLON => "each-right",
                TokenType.ADVERB_TICK_COLON => "each-prior",
                TokenType.ADVERB_BACKSLASH_COLON => "each-left",
                _ => tokenType.ToString().ToLower()
            };
        }

        /// <summary>
        /// Check if token type represents an adverb
        /// </summary>
        public static bool CouldBeAdverb(TokenType tokenType)
        {
            return VerbRegistry.IsAdverbToken(tokenType);
        }

        /// <summary>
        /// Parse complex adverb expression with proper precedence
        /// </summary>
        public ASTNode? ParseComplexAdverbExpression(List<Token> adverbTokens)
        {
            if (adverbTokens.Count == 0)
                return null;

            var position = 0;
            return ParseAdverbChain(ref position);
        }
    }
}
