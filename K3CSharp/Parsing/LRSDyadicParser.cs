using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Dyadic operator parsing for LRS parser
    /// Handles dyadic operations with right-associative LRS semantics
    /// </summary>
    public class LRSDyadicParser
    {
        private readonly List<Token> tokens;
        private readonly LRSParser? parentParser;
        private readonly LRSGroupingParser? groupingParser;
        
        public LRSDyadicParser(List<Token> tokens, LRSParser? parentParser = null)
        {
            this.tokens = tokens;
            this.parentParser = parentParser;
            this.groupingParser = new LRSGroupingParser(tokens, parentParser?.BuildParseTree ?? false);
        }
        
        /// <summary>
        /// Find the main dyadic operator using LRS (Long Right Scope) parsing
        /// The main operator is the leftmost operator at depth 0 whose left operand is simple.
        /// This gives it the longest right scope (takes the most to its right as right operand).
        /// Treats VERB ADVERB as atomic units.
        /// </summary>
        /// <param name="tokens">Tokens to search</param>
        /// <returns>Index of dyadic operator, or -1 if none found</returns>
        public int FindRightmostOperator(List<Token> tokens)
        {
            // Use R->L scan to find rightmost operator with simple left operand
            // This implements LRS (Long Right Scope) - the operator with the 
            // longest right scope is the rightmost one with a complete left operand
            int depth = 0;
            
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                var currentToken = tokens[i];
                
                // Track grouping depth (reverse direction for R->L)
                if (currentToken.Type == TokenType.RIGHT_PAREN || 
                    currentToken.Type == TokenType.RIGHT_BRACKET || 
                    currentToken.Type == TokenType.RIGHT_BRACE)
                {
                    depth++;
                    continue;
                }
                else if (currentToken.Type == TokenType.LEFT_PAREN || 
                         currentToken.Type == TokenType.LEFT_BRACKET || 
                         currentToken.Type == TokenType.LEFT_BRACE)
                {
                    depth--;
                    continue;
                }
                
                // Skip adverbs - they're handled as part of modified verbs
                if (IsAdverbToken(currentToken.Type))
                {
                    continue;
                }
                
                // Only consider operators at depth 0
                if (depth == 0 && IsDyadicOperatorDirect(currentToken.Type))
                {
                    // Check if this verb is followed by an adverb (from L->R perspective)
                    // When scanning R->L, we need to look ahead (i+1) to see if there's an adverb
                    if (i + 1 < tokens.Count && IsAdverbToken(tokens[i + 1].Type))
                    {
                        // This VERB is followed by ADVERB, so it's a modified verb
                        // Skip it - the adverb will create the modified verb node
                        continue;
                    }
                    
                    // Check VERB COLON ADVERB pattern
                    if (i + 2 < tokens.Count && 
                        tokens[i + 1].Type == TokenType.COLON && 
                        IsAdverbToken(tokens[i + 2].Type))
                    {
                        // VERB COLON ADVERB pattern
                        continue;
                    }
                    
                    // Check if left operand is simple (atomic or parenthesized)
                    // For LRS: the rightmost operator with a simple left operand
                    // has the longest right scope
                    if (HasSimpleLeftOperand(tokens, i))
                    {
                        return i;
                    }
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// Check if the tokens before position i form a simple (fully-resolved) operand.
        /// For LRS: a left operand is simple if it contains no dyadic operators at depth 0.
        /// This ensures the operator has the longest right scope.
        /// Examples:
        ///   - "1"        -> simple (single atom)
        ///   - "1 2"      -> simple (vector literal, no operators)
        ///   - "(1 + 2)"  -> simple (grouped expression resolves to a value)
        ///   - "1 + 2"    -> NOT simple (contains unresolved + operator)
        /// </summary>
        private static bool HasSimpleLeftOperand(List<Token> tokens, int operatorIndex)
        {
            if (operatorIndex <= 0) return true; // No left operand (monadic)
            
            // Scan the left operand for any dyadic operator at depth 0.
            // If none exist, the left side is a fully-resolved value (simple).
            int depth = 0;
            for (int i = 0; i < operatorIndex; i++)
            {
                var token = tokens[i];
                
                if (token.Type == TokenType.LEFT_PAREN || 
                    token.Type == TokenType.LEFT_BRACKET || 
                    token.Type == TokenType.LEFT_BRACE)
                {
                    depth++;
                }
                else if (token.Type == TokenType.RIGHT_PAREN || 
                         token.Type == TokenType.RIGHT_BRACKET || 
                         token.Type == TokenType.RIGHT_BRACE)
                {
                    depth--;
                }
                else if (depth == 0 && IsDyadicOperatorDirect(token.Type))
                {
                    // Left operand contains an unresolved dyadic operator,
                    // meaning it is not yet a simple value.
                    return false;
                }
                else if (depth == 0 && token.Type == TokenType.COLON &&
                         i > 0 && tokens[i - 1].Type == TokenType.IDENTIFIER)
                {
                    // Assignment prefix (IDENTIFIER COLON) at depth 0: by LRS the colon
                    // captures everything to its right, so operators to the right of this
                    // colon are part of the assignment RHS, not peer dyadic operators.
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if token type represents a verb that supports dyadic arity
        /// Uses VerbRegistry for verb-agnostic detection
        /// </summary>
        private static bool IsDyadicOperatorDirect(TokenType tokenType)
        {
            // Use VerbRegistry to check if the verb supports dyadic arity
            // This ensures any registered verb (operator, system function, etc.) 
            // with dyadic support is automatically recognized
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            
            // Special case: verbs with disambiguating colon (e.g., +:, |:) are monadic-only
            // They should not be treated as dyadic operators
            if (verbName != null && verbName.EndsWith(":"))
            {
                return false;
            }
            
            var result = verb?.SupportedArities.Contains(2) ?? false;
            
            return result;
        }
        
        /// <summary>
        /// Public method to check if token type is a dyadic operator
        /// </summary>
        /// <param name="tokenType">Token type to check</param>
        /// <returns>True if dyadic operator</returns>
        public bool IsDyadicOperator(TokenType tokenType)
        {
            return IsDyadicOperatorDirect(tokenType);
        }
        
        /// <summary>
        /// Parse dyadic operation using LRS right-associative strategy
        /// Scans R->L per spec to find rightmost operator at depth 0, treating VERB ADVERB as atomic
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <returns>AST node representing dyadic operation</returns>
        public ASTNode? ParseDyadicOperation(List<Token> tokens)
        {
            if (tokens.Count < 3) return null; // Need at least: left op right
            
            // FIRST: Use R->L scan to find rightmost regular dyadic operator at depth 0
            // This respects LRS principles and treats VERB ADVERB as atomic units
            var rightmostOpIndex = FindRightmostOperator(tokens);
            
            if (rightmostOpIndex != -1)
            {
                // Found a regular operator - split expression here
                return ParseRegularDyadicOperation(tokens, rightmostOpIndex);
            }
            
            // SECOND: No regular operator found - look for adverb-modified verbs
            // This handles expressions like `(y!x)?/:g` where `?` is part of a modified verb
            return ParseAdverbModifiedOperation(tokens);
        }
        
        /// <summary>
        /// Parse a regular dyadic operation after finding the operator position
        /// </summary>
        private ASTNode? ParseRegularDyadicOperation(List<Token> tokens, int opIndex)
        {
            var leftTokens = tokens.GetRange(0, opIndex);
            var rightTokens = tokens.GetRange(opIndex + 1, tokens.Count - opIndex - 1);
            var opToken = tokens[opIndex];

            // Choose strategy based on parent parser mode
            if (parentParser?.BuildParseTree == true)
            {
                // Build parse tree: recursively parse left and right without evaluation
                var leftNode = BuildParseTreeFromTokens(leftTokens);
                var rightNode = BuildParseTreeFromTokens(rightTokens);
                
                // Check if this should be a monadic operation (no left operand)
                // MUST check this BEFORE replacing null nodes, as null leftNode is valid for monadic
                if (leftTokens.Count == 0 && OperatorDetector.SupportsMonadic(opToken.Type))
                {
                    // Create monadic node when there's no left operand
                    // Handle null right node
                    if (rightNode == null)
                        rightNode = ASTNode.MakeLiteral(new NullValue());
                    return CreateMonadicNode(opToken, rightNode);
                }
                
                // Handle null nodes by creating appropriate literals (for dyadic operations)
                if (leftNode == null)
                    leftNode = ASTNode.MakeLiteral(new NullValue());
                if (rightNode == null)
                    rightNode = ASTNode.MakeLiteral(new NullValue());
                
                // Create dyadic node when there are both operands
                return CreateDyadicNode(opToken, leftNode, rightNode);
            }
            else
            {
                // Original evaluation logic
                var leftNode = ParseSubExpression(leftTokens);
                var rightNode = ParseSubExpression(rightTokens);
                
                // Check if this should be a monadic operation (no left operand)
                // MUST check this BEFORE replacing null nodes, as null leftNode is valid for monadic
                if (leftTokens.Count == 0 && OperatorDetector.SupportsMonadic(opToken.Type))
                {
                    // Create monadic node when there's no left operand
                    // Handle null right node
                    if (rightNode == null)
                        rightNode = ASTNode.MakeLiteral(new NullValue());
                    return CreateMonadicNode(opToken, rightNode);
                }
                
                // Handle null nodes by creating appropriate literals (for dyadic operations)
                if (leftNode == null)
                    leftNode = ASTNode.MakeLiteral(new NullValue());
                if (rightNode == null)
                    rightNode = ASTNode.MakeLiteral(new NullValue());
                
                // Create dyadic node when there are both operands
                return CreateDyadicNode(opToken, leftNode, rightNode);
            }
        }
        
        /// <summary>
        /// Parse adverb-modified operation (e.g., verb/:) when no regular operator found
        /// Scans L->R to find VERB ADVERB patterns at depth 0
        /// </summary>
        private ASTNode? ParseAdverbModifiedOperation(List<Token> tokens)
        {
            int adverbScanDepth = 0;
            
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                if (tokens[i].Type == TokenType.LEFT_PAREN || tokens[i].Type == TokenType.LEFT_BRACKET || tokens[i].Type == TokenType.LEFT_BRACE)
                    adverbScanDepth++;
                else if (tokens[i].Type == TokenType.RIGHT_PAREN || tokens[i].Type == TokenType.RIGHT_BRACKET || tokens[i].Type == TokenType.RIGHT_BRACE)
                    adverbScanDepth--;
                    
                // Also handle VERB COLON ADVERB (disambiguating colon: e.g. +:/)
                bool hasColonAdverb = adverbScanDepth == 0 &&
                                      i + 2 < tokens.Count &&
                                      IsDyadicOperatorDirect(tokens[i].Type) &&
                                      tokens[i + 1].Type == TokenType.COLON &&
                                      IsAdverbToken(tokens[i + 2].Type);
                if (adverbScanDepth == 0 && IsDyadicOperatorDirect(tokens[i].Type) && (IsAdverbToken(tokens[i + 1].Type) || hasColonAdverb))
                {
                    // Skip if the token immediately before this verb is another verb at depth 0
                    // This indicates a monadic verb chain, not a left argument for the adverb
                    if (i > 0 && IsDyadicOperatorDirect(tokens[i - 1].Type) && 
                        OperatorDetector.SupportsMonadic(tokens[i - 1].Type))
                    {
                        continue;
                    }
                    // Skip if the token immediately before this verb is a colon (assignment).
                    if (i > 0 && (tokens[i - 1].Type == TokenType.COLON || 
                                  tokens[i - 1].Type == TokenType.GLOBAL_ASSIGNMENT))
                    {
                        continue;
                    }
                    
                    var verbToken = tokens[i];
                    bool hasDisambiguatingColon = hasColonAdverb;
                    int adverbStart = hasDisambiguatingColon ? i + 2 : i + 1;
                    int adverbEnd = adverbStart;
                    while (adverbEnd < tokens.Count && IsAdverbToken(tokens[adverbEnd].Type))
                        adverbEnd++;
                    
                    var adverbTokens = tokens.GetRange(adverbStart, adverbEnd - adverbStart);
                    if (adverbTokens.Count == 0) continue; // No adverbs found
                    
                    var adverbToken = adverbTokens[adverbTokens.Count - 1];
                    var adverbLeftTokens = tokens.GetRange(0, i);
                    var adverbRightTokens = tokens.GetRange(adverbEnd, tokens.Count - adverbEnd);
                    
                    var leftNode = adverbLeftTokens.Count > 0 ? BuildParseTreeFromTokens(adverbLeftTokens) : null;
                    var rightNode = adverbRightTokens.Count > 0 ? BuildParseTreeFromTokens(adverbRightTokens) : null;
                    
                    ASTNode? verbNode;
                    if (hasDisambiguatingColon)
                    {
                        string monadicVerbName = VerbRegistry.TokenTypeToVerbName(verbToken.Type) + ":";
                        verbNode = new ASTNode(ASTNodeType.Literal, new SymbolValue(monadicVerbName));
                    }
                    else
                    {
                        verbNode = CreateNodeFromToken(verbToken);
                    }
                    if (verbNode == null)
                    {
                        throw new Exception($"Failed to create verb node from token: {verbToken.Type}({verbToken.Lexeme})");
                    }
                    
                    // Build nested verb node for multiple adverbs (one-adverb-at-a-time per spec)
                    ASTNode innerVerbNode = verbNode;
                    for (int a = 0; a < adverbTokens.Count - 1; a++)
                    {
                        var innerAdverbNode = new ASTNode(ASTNodeType.DyadicOp);
                        innerAdverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbTokens[a].Type));
                        innerAdverbNode.Children.Add(innerVerbNode);
                        innerVerbNode = innerAdverbNode;
                    }
                    
                    // Create top-level adverb node
                    var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
                    adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
                    adverbNode.Children.Add(innerVerbNode);
                    
                    if (leftNode != null) adverbNode.Children.Add(leftNode);
                    if (rightNode != null) adverbNode.Children.Add(rightNode);
                    if (adverbNode.Children.Count == 1) // Only verb, no operands
                        adverbNode.Children.Add(ASTNode.MakeLiteral(new NullValue()));
                    
                    return adverbNode;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Build parse tree from tokens (recursive)
        /// </summary>
        /// <param name="tokens">Tokens to build parse tree from</param>
        /// <returns>AST node representing parse tree structure</returns>
        private ASTNode? BuildParseTreeFromTokens(List<Token> tokens)
        {
            if (tokens.Count == 0) return null;
            if (tokens.Count == 1) 
            {
                return CreateNodeFromToken(tokens[0]);
            }
            
            // Check if this is a parenthesized expression and use grouping parser
            if (tokens.Count >= 2 && 
                tokens[0].Type == TokenType.LEFT_PAREN && 
                tokens[tokens.Count - 1].Type == TokenType.RIGHT_PAREN)
            {
                // Use grouping parser for parenthesized expressions
                var subGroupingParser = new LRSGroupingParser(tokens, parentParser?.BuildParseTree ?? false, parentParser);
                int pos = 0;
                try
                {
                    return subGroupingParser.ParseParentheses(ref pos);
                }
                catch
                {
                    // Fall through to dyadic parsing if grouping parser fails
                }
            }
            
            // Check if all tokens are atomic - if so, create a vector
            bool allAtomic = tokens.All(t => LRSAtomicParser.CanBeImplicitVectorElement(t.Type));
            if (allAtomic)
            {
                var argNodes = new List<ASTNode>();
                foreach (var token in tokens)
                {
                    argNodes.Add(LRSAtomicParser.ParseAtomicToken(token));
                }
                return new ASTNode(ASTNodeType.Vector, null, argNodes);
            }
            
            // VERB + COLON: monadic projection e.g. ,: — must be checked BEFORE dyadic/monadic parsing
            if (tokens.Count == 2 && IsDyadicOperatorDirect(tokens[0].Type) && tokens[1].Type == TokenType.COLON)
            {
                var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                projectedNode.Value = new SymbolValue(VerbRegistry.TokenTypeToVerbName(tokens[0].Type));
                projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1)));
                return projectedNode;
            }

            // Check for monadic system function application (e.g., _ci 97+!24)
            // The system function should consume the entire remaining expression as its argument
            if (tokens.Count >= 2 && OperatorDetector.IsFunction(tokens[0].Type) && 
                !IsDyadicOperatorDirect(tokens[0].Type))
            {
                var funcName = VerbRegistry.TokenTypeToVerbName(tokens[0].Type);
                var verb = VerbRegistry.GetVerb(funcName);
                
                // If it's a system function that supports monadic arity, treat rest as its argument
                if (verb?.Type == VerbType.SystemFunction && verb.SupportedArities.Contains(1))
                {
                    var argTokens = tokens.GetRange(1, tokens.Count - 1);
                    var argNode = BuildParseTreeFromTokens(argTokens);
                    if (argNode != null)
                    {
                        var funcCallNode = new ASTNode(ASTNodeType.FunctionCall);
                        funcCallNode.Children.Add(ASTNode.MakeVariable(funcName));
                        funcCallNode.Children.Add(argNode);
                        return funcCallNode;
                    }
                }
            }
            
            // Try dyadic operation first (monadic parsing is handled at main LRS level)
            var result = ParseDyadicOperation(tokens);
            if (result != null)
                return result;
            
            // Try monadic operation: verb + operand (e.g. -x, *y)
            if (tokens.Count == 2 && OperatorDetector.SupportsMonadic(tokens[0].Type))
            {
                var operandNode = CreateNodeFromToken(tokens[1]);
                if (operandNode != null)
                {
                    var monadicNode = new ASTNode(ASTNodeType.DyadicOp);
                    monadicNode.Value = new SymbolValue(VerbRegistry.TokenTypeToVerbName(tokens[0].Type));
                    monadicNode.Children.Add(operandNode);
                    return monadicNode;
                }
            }
            
            // Delegate to parent parser if available
            if (parentParser != null)
                return parentParser.EvaluateFromRight(tokens);
            
            return null;
        }
        
        /// <summary>
        /// Parse sub-expression (could be monadic, dyadic, or atomic)
        /// Pure LRS mode: Enhanced with grouping parser support
        /// </summary>
        private ASTNode? ParseSubExpression(List<Token> tokens)
        {
            if (tokens.Count == 0) return null;
            
            // Check for simple assignment statement (e.g., 'a: 42' in '1 + a: 42')
            // This handles inline assignment where assignment is a sub-expression.
            // Use ParseInlineStatement so the assignment returns the value (not Null).
            if (tokens.Count >= 3 && IsSimpleAssignment(tokens))
            {
                var statementParser = parentParser?.GetStatementParser();
                if (statementParser != null)
                {
                    return statementParser.ParseInlineStatement(tokens);
                }
            }
            
            bool pureLRSMode = parentParser?.PureLRSMode ?? false;
            
            // Check for nested grouping constructs with semicolon-separated expressions
            // This handles dictionary creation cases like ((`a;1);(`b;2)) where semicolons
            // appear at depth >= 1 (inside parentheses), not simple cases like (1;2;3)
            if (tokens.Count >= 7)  // Minimum: ( ( x ; y ) ; ( z ; w ) )
            {
                var firstToken = tokens[0];
                
                if (firstToken.Type == TokenType.LEFT_PAREN || 
                    firstToken.Type == TokenType.LEFT_BRACKET || 
                    firstToken.Type == TokenType.LEFT_BRACE)
                {
                    // Check for semicolons at depth > 1 (inside nested groupings)
                    bool hasDeepSemicolon = false;
                    int depth = 0;
                    TokenType openType = firstToken.Type;
                    TokenType closeType = openType == TokenType.LEFT_PAREN ? TokenType.RIGHT_PAREN :
                                         openType == TokenType.LEFT_BRACKET ? TokenType.RIGHT_BRACKET :
                                         TokenType.RIGHT_BRACE;
                    
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        var token = tokens[i];
                        
                        if (token.Type == openType) 
                        {
                            depth++;
                        }
                        else if (token.Type == closeType) 
                        {
                            depth--;
                        }
                        else if (token.Type == TokenType.SEMICOLON && depth >= 1)
                        {
                            // Semicolon at depth >= 1 means it's inside parentheses (for nested structures like matrices)
                            hasDeepSemicolon = true;
                        }
                        
                        // Early exit if we find a deep semicolon
                        if (hasDeepSemicolon) break;
                    }
                    
                    // Verify it's a complete grouping and has deep semicolons
                    if (hasDeepSemicolon)
                    {
                        // Recalculate depth to verify structure
                        depth = 0;
                        for (int i = 0; i < tokens.Count; i++)
                        {
                            var token = tokens[i];
                            
                            if (token.Type == openType) depth++;
                            else if (token.Type == closeType) depth--;
                            
                            // If we close at the last token, this is a complete grouping
                            if (depth == 0 && i == tokens.Count - 1)
                            {
                                // Use grouping parser for nested semicolon-containing expressions
                                var subGroupingParser = new LRSGroupingParser(tokens, parentParser?.BuildParseTree ?? false, parentParser);
                                int pos = 0;
                                try
                                {
                                    if (openType == TokenType.LEFT_PAREN)
                                        return subGroupingParser.ParseParentheses(ref pos);
                                    else if (openType == TokenType.LEFT_BRACKET)
                                        return subGroupingParser.ParseBrackets(ref pos);
                                    else if (openType == TokenType.LEFT_BRACE)
                                        return subGroupingParser.ParseBraces(ref pos);
                                }
                                catch
                                {
                                    // Fall through to default handling
                                }
                                break;
                            }
                        }
                    }
                }
            }
            
            // Pure LRS mode: Check for single-token grouping constructs
            if (pureLRSMode && tokens.Count == 1)
            {
                var token = tokens[0];
                
                // Handle grouping constructs using LRSGroupingParser
                if (token.Type == TokenType.LEFT_PAREN || 
                    token.Type == TokenType.LEFT_BRACKET || 
                    token.Type == TokenType.LEFT_BRACE)
                {
                    // Create new grouping parser with the sub-expression tokens
                    var subGroupingParser = new LRSGroupingParser(tokens, parentParser?.BuildParseTree ?? false, parentParser);
                    int pos = 0;
                    try
                    {
                        if (token.Type == TokenType.LEFT_PAREN)
                            return subGroupingParser.ParseParentheses(ref pos);
                        else if (token.Type == TokenType.LEFT_BRACKET)
                            return subGroupingParser.ParseBrackets(ref pos);
                        else if (token.Type == TokenType.LEFT_BRACE)
                            return subGroupingParser.ParseBraces(ref pos);
                    }
                    catch
                    {
                        // Fall through to default handling
                    }
                }
            }
            
            if (tokens.Count == 1) 
            {
                var nodeResult = CreateNodeFromToken(tokens[0]);
                return nodeResult;
            }
            
            // Check for empty braces {} form specifier (used with $ operator for string expression evaluation)
            // Pattern: {} followed by $ - must be detected before other parsing
            if (tokens.Count == 2 && 
                tokens[0].Type == TokenType.LEFT_BRACE && 
                tokens[1].Type == TokenType.RIGHT_BRACE)
            {
                // Create a form specifier node for empty braces
                var formSpecifierNode = new ASTNode(ASTNodeType.FormSpecifier);
                formSpecifierNode.Value = new SymbolValue("{}");
                return formSpecifierNode;
            }
            
            // Handle grouping constructs (parentheses, brackets, braces) that wrap the entire expression
            // This ensures proper parsing of parenthesized sub-expressions regardless of pureLRSMode
            if (tokens.Count > 2)
            {
                var firstToken = tokens[0];
                
                if (firstToken.Type == TokenType.LEFT_PAREN || 
                    firstToken.Type == TokenType.LEFT_BRACKET || 
                    firstToken.Type == TokenType.LEFT_BRACE)
                {
                    // Check if this is a complete grouping construct
                    // CRITICAL: Track when the FIRST opening delimiter closes.
                    // Only treat as fully-wrapped if the first open closes at the last token.
                    // E.g., ($x),":y" — first ( closes at index 3, NOT at the end.
                    int depth = 0;
                    bool firstOpenClosed = false;
                    TokenType openType = firstToken.Type;
                    TokenType closeType = openType == TokenType.LEFT_PAREN ? TokenType.RIGHT_PAREN :
                                         openType == TokenType.LEFT_BRACKET ? TokenType.RIGHT_BRACKET :
                                         TokenType.RIGHT_BRACE;
                    
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        if (tokens[i].Type == openType) depth++;
                        else if (tokens[i].Type == closeType) depth--;
                        
                        if (depth == 0 && !firstOpenClosed)
                        {
                            firstOpenClosed = true;
                            // If the first opening closes at the last position, entire expression is wrapped
                            if (i == tokens.Count - 1)
                            {
                                // Create new grouping parser with the sub-expression tokens
                                var subGroupingParser = new LRSGroupingParser(tokens, parentParser?.BuildParseTree ?? false, parentParser);
                                int pos = 0;
                                try
                                {
                                    ASTNode? result;
                                    if (openType == TokenType.LEFT_PAREN)
                                        result = subGroupingParser.ParseParentheses(ref pos);
                                    else if (openType == TokenType.LEFT_BRACKET)
                                        result = subGroupingParser.ParseBrackets(ref pos);
                                    else
                                        result = subGroupingParser.ParseBraces(ref pos);
                                    return result;
                                }
                                catch
                                {
                                    // Fall through to dyadic parsing
                                }
                            }
                            // If first opening closes before the end, expression is NOT fully wrapped
                            break;
                        }
                    }
                }
            }
            
            // Check for implicit vector creation (sequences of atomic literals like "1 2 3")
            // This must happen before dyadic parsing to handle vector left arguments to operators
            if (tokens.Count >= 2)
            {
                var implicitVector = TryCreateImplicitVector(tokens);
                if (implicitVector != null)
                    return implicitVector;
            }

            // Check for monadic system function application (e.g., _ci 97+!24)
            // The system function should consume the entire remaining expression as its argument
            if (tokens.Count >= 2 && OperatorDetector.IsFunction(tokens[0].Type) && 
                !IsDyadicOperatorDirect(tokens[0].Type))
            {
                var funcName = VerbRegistry.TokenTypeToVerbName(tokens[0].Type);
                var verb = VerbRegistry.GetVerb(funcName);
                
                // If it's a system function that supports monadic arity, treat rest as its argument
                if (verb?.Type == VerbType.SystemFunction && verb.SupportedArities.Contains(1))
                {
                    var argTokens = tokens.GetRange(1, tokens.Count - 1);
                    var argNode = ParseSubExpression(argTokens);
                    if (argNode != null)
                    {
                        var funcCallNode = new ASTNode(ASTNodeType.FunctionCall);
                        funcCallNode.Children.Add(ASTNode.MakeVariable(funcName));
                        funcCallNode.Children.Add(argNode);
                        return funcCallNode;
                    }
                }
            }
            
            // Delegate to EvaluateFromRight when there are adverb patterns
            // This ensures correct right-to-left precedence for expressions like ~=':x
            // (monadic verb before verb+adverb)
            if (parentParser != null && tokens.Count >= 3)
            {
                bool hasAdverb = false;
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (IsAdverbToken(tokens[i].Type))
                    {
                        hasAdverb = true;
                        break;
                    }
                }
                if (hasAdverb)
                {
                    var adverbResult = parentParser.EvaluateFromRight(tokens);
                    if (adverbResult != null)
                        return adverbResult;
                }
            }
            
            // Try dyadic operation (monadic parsing is handled at main LRS level)
            var dyadicResult = ParseDyadicOperation(tokens);
            if (dyadicResult != null)
                return dyadicResult;
            
            // VERB + COLON: monadic projection e.g. ,: — must be checked BEFORE generic monadic
            if (tokens.Count == 2 && IsDyadicOperatorDirect(tokens[0].Type) && tokens[1].Type == TokenType.COLON)
            {
                var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                projectedNode.Value = new SymbolValue(VerbRegistry.TokenTypeToVerbName(tokens[0].Type));
                projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1)));
                return projectedNode;
            }
            
            // If dyadic parsing failed and we have exactly 2 tokens (potential monadic: op + operand)
            // try monadic parsing directly
            if (tokens.Count == 2 && OperatorDetector.SupportsMonadic(tokens[0].Type))
            {
                var opToken = tokens[0];
                var operandNode = CreateNodeFromToken(tokens[1]);
                if (operandNode != null)
                {
                    return CreateMonadicNode(opToken, operandNode);
                }
            }
            
            // Last resort: delegate to parent parser's EvaluateFromRight.
            // This handles patterns that dyadic/monadic parsers don't recognize,
            // e.g., identifier[args] sub-expressions like sum[3;4] within larger expressions.
            if (parentParser != null)
                return parentParser.EvaluateFromRight(tokens);
            
            return null;
        }
        
        /// <summary>
        /// Try to create an implicit vector from a sequence of atomic literals
        /// Returns null if tokens don't form a valid implicit vector
        /// </summary>
        private ASTNode? TryCreateImplicitVector(List<Token> tokens)
        {
            if (tokens.Count < 2)
                return null;
            
            var elements = new List<ASTNode>();
            
            foreach (var token in tokens)
            {
                // Check if token is an atomic literal
                if (!LRSAtomicParser.CanBeImplicitVectorElement(token.Type))
                    return null; // Not all atomic - can't be implicit vector
                
                // Parse the token and add to elements
                var node = LRSAtomicParser.ParseAtomicToken(token);
                if (node == null)
                    return null;
                
                elements.Add(node);
            }
            
            // Create vector for all implicit collections
            // K semantics: space-separated literals create vectors (homogeneous or mixed)
            // The evaluator will determine the proper K3Value type based on element types
            return ASTNode.MakeVector(elements);
        }
        
        /// <summary>
        /// Check if tokens represent a simple assignment (e.g., 'a: 42')
        /// Pattern: IDENTIFIER COLON expression
        /// </summary>
        private bool IsSimpleAssignment(List<Token> tokens)
        {
            // Must have at least: identifier, colon, value
            if (tokens.Count < 3)
                return false;
            
            // First token must be an identifier (variable name)
            if (tokens[0].Type != TokenType.IDENTIFIER)
                return false;
            
            // Second token must be colon
            if (tokens[1].Type != TokenType.COLON)
                return false;
            
            // Must not contain any operators before the colon (simple variable name only)
            // e.g., 'a: 42' is simple, but '1 + a: 42' is not
            return true;
        }
        
        /// <summary>
        /// Create AST node for dyadic operation
        /// </summary>
        private ASTNode CreateDyadicNode(Token opToken, ASTNode left, ASTNode right)
        {
            return ASTNode.MakeDyadicOp(opToken.Type, left, right);
        }
        
        /// <summary>
        /// Create AST node for monadic operation
        /// </summary>
        private ASTNode CreateMonadicNode(Token opToken, ASTNode operand)
        {
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue(VerbRegistry.TokenTypeToVerbName(opToken.Type));
            node.Children.Add(operand);
            return node;
        }
        
        /// <summary>
        /// Create AST node from atomic token using LRSAtomicParser
        /// </summary>
        private ASTNode? CreateNodeFromToken(Token token)
        {
            if (LRSAtomicParser.CanBeParsedByAtomicParser(token.Type))
            {
                return LRSAtomicParser.ParseAtomicToken(token);
            }
            
            // Handle operator symbols for parse trees
            return LRSAtomicParser.CreateOperatorNode(token.Type);
        }
        
        /// <summary>
        /// Check if token could be a dyadic operator
        /// </summary>
        public static bool CouldBeDyadicOperator(TokenType tokenType)
        {
            return IsDyadicOperatorDirect(tokenType);
        }
        
        /// <summary>
        /// Check if token is an adverb
        /// </summary>
        /// <param name="tokenType">Token type to check</param>
        /// <returns>True if token is an adverb</returns>
        private bool IsAdverbToken(TokenType tokenType)
        {
            return VerbRegistry.IsAdverbToken(tokenType);
        }
    }
}
