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
        /// Find the leftmost dyadic operator at the lowest nesting depth
        /// For LRS: the leftmost operator splits the expression (left is atomic, right is recursively parsed)
        /// </summary>
        /// <param name="tokens">Tokens to search</param>
        /// <returns>Index of dyadic operator, or -1 if none found</returns>
        public int FindRightmostOperator(List<Token> tokens)
        {
            // The leftmost operator splits: left side is atomic, right side is recursively parsed
            
            int depth = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                var currentToken = tokens[i];
                
                // Track grouping depth
                if (currentToken.Type == TokenType.LEFT_PAREN || 
                    currentToken.Type == TokenType.LEFT_BRACKET || 
                    currentToken.Type == TokenType.LEFT_BRACE)
                {
                    depth++;
                    continue;
                }
                else if (currentToken.Type == TokenType.RIGHT_PAREN || 
                         currentToken.Type == TokenType.RIGHT_BRACKET || 
                         currentToken.Type == TokenType.RIGHT_BRACE)
                {
                    depth--;
                    continue;
                }
                
                // Only consider operators at depth 0 (not inside grouping constructs)
                if (depth == 0 && IsDyadicOperatorDirect(currentToken.Type))
                {
                    // Skip verb+adverb patterns (VERB ADVERB or VERB COLON ADVERB)
                    if (i + 1 < tokens.Count && IsAdverbToken(tokens[i + 1].Type))
                    {
                        i++; // Skip the adverb too
                        continue;
                    }
                    // Skip VERB COLON ADVERB (disambiguating colon before adverb: e.g. +:/)
                    if (i + 2 < tokens.Count && tokens[i + 1].Type == TokenType.COLON && IsAdverbToken(tokens[i + 2].Type))
                    {
                        i += 2; // Skip the colon and adverb
                        continue;
                    }
                    
                    // Return leftmost operator for LRS right-to-left evaluation
                    return i;
                }
            }
            
            return -1;
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
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <returns>AST node representing dyadic operation</returns>
        public ASTNode? ParseDyadicOperation(List<Token> tokens)
        {
            if (tokens.Count < 3) return null; // Need at least: left op right
            
            // Find the leftmost dyadic operator at the lowest nesting depth in the token list at depth 0
            // This handles cases like (1 2 3) +' (4 5 6) where the verb+adverb is in the middle
            // Must respect grouping depth to avoid matching inside parenthesized sub-expressions
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
                    // E.g., in ~=':x, the ~ before = is monadic, not the left arg of =':   
                    if (i > 0 && IsDyadicOperatorDirect(tokens[i - 1].Type) && 
                        OperatorDetector.SupportsMonadic(tokens[i - 1].Type))
                    {
                        continue;
                    }
                    // Skip if the token immediately before this verb is a colon (assignment).
                    // The verb is then the start of an inline assignment's RHS, not a dyadic
                    // operator with the entire left side as its left argument.
                    // E.g., in `((y-1)_ a)-0,(-y)_ a:+\x`, `+\` is the RHS of `a:`, not a
                    // dyadic adverb application of `+\` to `((y-1)_ a)-0,(-y)_ a:`.
                    if (i > 0 && (tokens[i - 1].Type == TokenType.COLON || 
                                  tokens[i - 1].Type == TokenType.GLOBAL_ASSIGNMENT))
                    {
                        continue;
                    }
                    var verbToken = tokens[i];
                    // Determine if this is a VERB COLON ADVERB pattern
                    bool hasDisambiguatingColon = hasColonAdverb;
                    // Consume all consecutive adverbs after the verb (skipping over colon if present)
                    int adverbStart = hasDisambiguatingColon ? i + 2 : i + 1;
                    int adverbEnd = adverbStart;
                    while (adverbEnd < tokens.Count && IsAdverbToken(tokens[adverbEnd].Type))
                        adverbEnd++;
                    // adverbEnd is now the index of the first non-adverb token after the verb
                    var adverbTokens = tokens.GetRange(adverbStart, adverbEnd - adverbStart); // all adverb tokens
                    var adverbToken = adverbTokens[adverbTokens.Count - 1]; // outermost adverb
                    // Extract the tokens for the adverb operation
                    // Everything before the verb is the left operand
                    var adverbLeftTokens = tokens.GetRange(0, i);
                    // Everything after ALL adverbs is the right operand
                    var adverbRightTokens = tokens.GetRange(adverbEnd, tokens.Count - adverbEnd);
                    
                    // Build parse tree for left and right operands
                    var leftNode = adverbLeftTokens.Count > 0 ? BuildParseTreeFromTokens(adverbLeftTokens) : null;
                    var rightNode = adverbRightTokens.Count > 0 ? BuildParseTreeFromTokens(adverbRightTokens) : null;
                    
                    // Create base verb node — if disambiguating colon was present, use monadic verb name (e.g. "+:")
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
                    
                    // Build nested verb node for multiple adverbs (one-adverb-at-a-time per spec).
                    // For verb/:\: the innermost adverb (/:) wraps the base verb, producing a
                    // modified verb node; the outermost adverb (\:) then wraps that.
                    // innerVerbNode starts as the base verb and is wrapped left-to-right through
                    // the adverb list (adverbTokens[0] is closest to verb, last is outermost).
                    ASTNode innerVerbNode = verbNode;
                    for (int a = 0; a < adverbTokens.Count - 1; a++)
                    {
                        // Wrap innerVerbNode with adverbTokens[a] to produce a modified verb
                        var innerAdverbNode = new ASTNode(ASTNodeType.DyadicOp);
                        innerAdverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbTokens[a].Type));
                        innerAdverbNode.Children.Add(innerVerbNode);
                        innerVerbNode = innerAdverbNode;
                    }
                    
                    // Create top-level adverb node with the structure expected by the evaluator
                    var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
                    
                    if (leftNode != null && rightNode != null)
                    {
                        // Both operands: e.g., (1 2 3) ,/:\: (4 5 6)
                        // Create a 3-child structure: (outerAdverb, modifiedVerb, leftArg, rightArg)
                        adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
                        adverbNode.Children.Add(innerVerbNode);
                        adverbNode.Children.Add(leftNode);
                        adverbNode.Children.Add(rightNode);
                    }
                    else if (rightNode != null)
                    {
                        adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
                        adverbNode.Children.Add(innerVerbNode);
                        adverbNode.Children.Add(rightNode);
                    }
                    else if (leftNode != null)
                    {
                        adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
                        adverbNode.Children.Add(innerVerbNode);
                        adverbNode.Children.Add(leftNode);
                    }
                    else
                    {
                        adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
                        adverbNode.Children.Add(innerVerbNode);
                        adverbNode.Children.Add(ASTNode.MakeLiteral(new NullValue()));
                    }
                    
                    return adverbNode;
                }
            }
            
            var rightmostOpIndex = FindRightmostOperator(tokens);
            if (rightmostOpIndex == -1) return null;

            // Split at rightmost operator
            var leftTokens = tokens.GetRange(0, rightmostOpIndex);
            var rightTokens = tokens.GetRange(rightmostOpIndex + 1, tokens.Count - rightmostOpIndex - 1);
            var opToken = tokens[rightmostOpIndex];

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
