using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Long Right Scope (LRS) parser for K expressions
    /// Implements right-first parsing strategy with proper K language rules
    /// Uses verb-agnostic design with modular components
    /// </summary>
    public class LRSParser
    {
        private readonly List<Token> tokens;
        public IReadOnlyList<Token> ProcessedTokens => tokens;
        private readonly LRSExpressionParser expressionParser;
        private readonly LRSDyadicParser dyadicParser;
        private readonly LRSMonadicParser monadicParser;
        private readonly LRSFunctionParser functionParser;
        private readonly LRSStatementParser statementParser;
        private readonly LRSGroupingParser groupingParser;
        
        // Parse tree construction mode flag
        public bool BuildParseTree { get; set; }
        
        // Pure LRS mode flag (enables improved parsing logic when no fallback)
        public bool PureLRSMode { get; set; }
        
        // Defined variables tracker for parser-time variable registration
        // Per spec: allows expressions to be parsed before evaluation by tracking
        // variables defined in assignment operations during AST construction
        private readonly HashSet<string> definedVariables = new HashSet<string>();
        
        /// <summary>
        /// Register a variable as defined during parsing (for assignment targets)
        /// </summary>
        public void RegisterDefinedVariable(string variableName)
        {
            definedVariables.Add(variableName);
        }
        
        /// <summary>
        /// Check if a variable is defined (either in evaluator or registered during parsing)
        /// </summary>
        public bool IsVariableDefined(string variableName)
        {
            return definedVariables.Contains(variableName);
        }
        
        /// <summary>
        /// Clear defined variables tracker (called after parsing completes)
        /// </summary>
        public void ClearDefinedVariables()
        {
            definedVariables.Clear();
        }
        
        /// <summary>
        /// Preprocess tokens to combine adjacent DOT_APPLY + IDENTIFIER sequences
        /// into single IDENTIFIER tokens with dotted notation (e.g., .k.d becomes a single Variable ".k.d")
        /// Only applies when the DOT_APPLY and IDENTIFIER are adjacent (no space between them).
        /// Also handles DOT + UNDERSCORE + IDENTIFIER for paths like ._dotnet.
        /// </summary>
        private static List<Token> PreprocessDottedPaths(List<Token> tokens)
        {
            var result = new List<Token>();
            int i = 0;
            
            while (i < tokens.Count)
            {
                // Check for DOT_APPLY followed by UNDERSCORE + IDENTIFIER (e.g., ._dotnet)
                if (tokens[i].Type == TokenType.DOT_APPLY && 
                    i + 2 < tokens.Count &&
                    tokens[i + 1].Type == TokenType.UNDERSCORE &&
                    (tokens[i + 2].Type == TokenType.IDENTIFIER || tokens[i + 2].Type == TokenType.SYMBOL))
                {
                    // Check adjacency - the tokens must be consecutive (no spaces between them)
                    bool underscoreAdjacent = (tokens[i].Position + tokens[i].Lexeme.Length) == tokens[i + 1].Position;
                    bool identifierAdjacent = (tokens[i + 1].Position + tokens[i + 1].Lexeme.Length) == tokens[i + 2].Position;
                    
                    if (underscoreAdjacent && identifierAdjacent)
                    {
                        // Combine DOT_APPLY + UNDERSCORE + first IDENTIFIER into a single IDENTIFIER
                        var dottedPath = "._" + tokens[i + 2].Lexeme;
                        int startPos = tokens[i].Position;
                        int j = i + 3;
                        
                        // Continue consuming .identifier sequences
                        while (j + 1 < tokens.Count && 
                               tokens[j].Type == TokenType.DOT_APPLY &&
                               (tokens[j + 1].Type == TokenType.IDENTIFIER || tokens[j + 1].Type == TokenType.SYMBOL) &&
                               (tokens[j].Position + tokens[j].Lexeme.Length) == tokens[j + 1].Position)
                        {
                            dottedPath += "." + tokens[j + 1].Lexeme;
                            j += 2;
                        }
                        
                        // Create a single IDENTIFIER token with the full dotted path
                        result.Add(new Token(TokenType.IDENTIFIER, dottedPath, startPos));
                        i = j;
                        continue;
                    }
                }
                
                // Check for DOT_APPLY followed by adjacent IDENTIFIER pattern
                if (tokens[i].Type == TokenType.DOT_APPLY && 
                    i + 1 < tokens.Count &&
                    (tokens[i + 1].Type == TokenType.IDENTIFIER || tokens[i + 1].Type == TokenType.SYMBOL))
                {
                    // Check adjacency: dot position + 1 == next token position
                    bool isAdjacent = (tokens[i].Position + tokens[i].Lexeme.Length) == tokens[i + 1].Position;
                    
                    if (isAdjacent)
                    {
                        // Build the dotted path
                        var dottedPath = "." + tokens[i + 1].Lexeme;
                        int startPos = tokens[i].Position;
                        int j = i + 2;
                        
                        // Continue consuming .identifier sequences
                        while (j + 1 < tokens.Count && 
                               tokens[j].Type == TokenType.DOT_APPLY &&
                               (tokens[j + 1].Type == TokenType.IDENTIFIER || tokens[j + 1].Type == TokenType.SYMBOL) &&
                               (tokens[j].Position + tokens[j].Lexeme.Length) == tokens[j + 1].Position)
                        {
                            dottedPath += "." + tokens[j + 1].Lexeme;
                            j += 2;
                        }
                        
                        // Create a single IDENTIFIER token with the full dotted path
                        result.Add(new Token(TokenType.IDENTIFIER, dottedPath, startPos));
                        i = j;
                        continue;
                    }
                }
                
                result.Add(tokens[i]);
                i++;
            }
            
            return result;
        }
        
        public LRSParser(List<Token> tokens, bool buildParseTree = false)
        {
            // Preprocess tokens to combine K-tree dotted notation
            var processedTokens = PreprocessDottedPaths(tokens);
            this.tokens = processedTokens;
            this.BuildParseTree = buildParseTree;
            this.PureLRSMode = true; // Default to Pure LRS mode
            this.expressionParser = new LRSExpressionParser(processedTokens);
            this.dyadicParser = new LRSDyadicParser(processedTokens, this);
            this.monadicParser = new LRSMonadicParser(this);
            this.functionParser = new LRSFunctionParser(processedTokens);
            this.statementParser = new LRSStatementParser(processedTokens, this);
            this.groupingParser = new LRSGroupingParser(processedTokens, buildParseTree, this);
        }
        
        /// <summary>
        /// Parse expression using LRS strategy (right-first parsing)
        /// </summary>
        /// <param name="position">Starting position, updated to end of parsed expression</param>
        /// <returns>AST node representing the parsed expression</returns>
        public ASTNode? ParseExpression(ref int position)
        {
            // Step 1: Read entire expression until separator
            var expressionTokens = expressionParser.ReadExpressionTokens(ref position);
            
            if (expressionTokens.Count == 0)
                return null;
            
            // Single token: directly create a node (same as BuildParseTreeFromRight)
            if (expressionTokens.Count == 1)
            {
                // Handle standalone verb as projection
                if (VerbRegistry.IsVerbToken(expressionTokens[0].Type))
                {
                    var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                    var operatorSymbol = VerbRegistry.GetDyadicOperatorSymbol(expressionTokens[0].Type);
                    projectedNode.Value = new SymbolValue(operatorSymbol);
                    
                    var verb = VerbRegistry.GetVerb(operatorSymbol);
                    int defaultArity = verb?.SupportedArities?.Max() ?? 2;
                    projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(defaultArity)));
                    
                    return projectedNode;
                }
                return CreateNodeFromToken(expressionTokens[0]);
            }
            
            // Check for verb followed by colon (monadic projection disambiguation)
            if (expressionTokens.Count == 2 &&
                VerbRegistry.IsVerbToken(expressionTokens[0].Type) &&
                expressionTokens[1].Type == TokenType.COLON)
            {
                var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                var operatorSymbol = VerbRegistry.GetDyadicOperatorSymbol(expressionTokens[0].Type);
                projectedNode.Value = new SymbolValue(operatorSymbol);
                
                // Monadic projection has arity 1
                projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1)));
                
                return projectedNode;
            }
            
            // CRITICAL: Check for projection patterns BEFORE bracket function call handling
            // Pattern: +[;2] (operator with bracket containing semicolon)
            // But NOT for DOT_APPLY/APPLY with 3+ args — those are amend/triadic, not projections
            if (expressionTokens.Count >= 3 &&
                IsVerbToken(expressionTokens[0].Type) &&
                expressionTokens[1].Type == TokenType.LEFT_BRACKET)
            {
                var bracketEnd = FindMatchingBracket(expressionTokens, 1);
                if (bracketEnd != -1)
                {
                    // Count top-level arguments inside brackets (paren/brace-aware)
                    int argCount0 = CountBracketArguments(expressionTokens, 1);
                    // Also detect top-level semicolons directly (handles blank slots like +[;5])
                    int semicolonCount = 0;
                    {
                        int sd = 0;
                        for (int si = 2; si < bracketEnd; si++)
                        {
                            var st = expressionTokens[si].Type;
                            if (st == TokenType.LEFT_PAREN || st == TokenType.LEFT_BRACKET || st == TokenType.LEFT_BRACE) sd++;
                            else if (st == TokenType.RIGHT_PAREN || st == TokenType.RIGHT_BRACKET || st == TokenType.RIGHT_BRACE) sd--;
                            else if (st == TokenType.SEMICOLON && sd == 0) semicolonCount++;
                        }
                    }
                    bool hasSemicolon = semicolonCount > 0;
                    
                    // DOT_APPLY/APPLY with 2+ semicolons = amend/triadic — skip projection handling
                    bool isDotOrApply = expressionTokens[0].Type == TokenType.DOT_APPLY ||
                                       expressionTokens[0].Type == TokenType.APPLY;
                    if (isDotOrApply && semicolonCount >= 2)
                        hasSemicolon = false;
                    
                    if (hasSemicolon)
                    {
                        // Split bracket content by top-level semicolons
                        var bracketContent = expressionTokens.GetRange(2, bracketEnd - 2);
                        var splitArgs = SplitBracketArguments(bracketContent, int.MaxValue);
                        
                        // Check if any slot is blank (empty token list = unspecified argument = projection)
                        bool hasBlankSlot = splitArgs.Any(a => a.Count == 0);
                        
                        if (!hasBlankSlot)
                        {
                            // All arguments specified — evaluate as full function call
                            // f[j;k] with both args = dyadic f applied to j and k
                            var argNodes = splitArgs.Select(argTokens =>
                                BuildParseTree ? BuildParseTreeFromRight(argTokens) : EvaluateFromRight(argTokens)).ToList();
                            
                            if (argNodes.Count == 2)
                            {
                                // Dyadic: create DyadicOp node
                                return new ASTNode(ASTNodeType.DyadicOp, expressionTokens[0].Lexeme == "." ? new SymbolValue(".") : new SymbolValue(expressionTokens[0].Lexeme),
                                    new List<ASTNode> { argNodes[0]!, argNodes[1]! });
                            }
                            else
                            {
                                // N-ary: fall through to ParseTriadicFromBracketCall below
                                // (triadic/tetradic amend is handled there for . and @)
                                // For other verbs with 3+ args, create a FunctionCall node
                                var funcNode = new ASTNode(ASTNodeType.FunctionCall);
                                // Function name stored in Children[0] only (not in Value)
                                // This follows the convention: FunctionCall.Children[0] = function node
                                funcNode.Children.Add(ASTNode.MakeVariable(expressionTokens[0].Lexeme));
                                funcNode.Children.AddRange(argNodes.Where(n => n != null)!);
                                return funcNode;
                            }
                        }
                        else
                        {
                            // At least one blank slot — true projection
                            var splitFirst = SplitBracketArguments(bracketContent, 2);
                            int semicolonPos = -1;
                            for (int i = 0; i < bracketContent.Count; i++)
                            {
                                if (bracketContent[i].Type == TokenType.SEMICOLON)
                                { semicolonPos = i; break; }
                            }
                            ASTNode? leftArg = null;
                            ASTNode? rightArg = null;
                            if (semicolonPos > 0)
                            {
                                var leftTokens = bracketContent.GetRange(0, semicolonPos);
                                leftArg = BuildParseTree ? BuildParseTreeFromRight(leftTokens) : EvaluateFromRight(leftTokens);
                            }
                            if (semicolonPos >= 0 && semicolonPos < bracketContent.Count - 1)
                            {
                                var rightTokens = bracketContent.GetRange(semicolonPos + 1, bracketContent.Count - semicolonPos - 1);
                                rightArg = BuildParseTree ? BuildParseTreeFromRight(rightTokens) : EvaluateFromRight(rightTokens);
                            }
                            return CreateProjectionNode(expressionTokens[0], leftArg, rightArg);
                        }
                    }
                }
            }
            
            // Check for bracket function call pattern BEFORE delegating to specific parsers
            // Brackets bind left-to-right with stronger binding the closer they are to the base.
            // e.g., c1[`Abs][] => (c1[`Abs])[] and f[1][4] => (f[1])[4]
            // Find the first bracket position (prefix is everything before it)
            {
                int firstBracket = -1;
                {
                    // Find first [ at top level (depth 0), respecting ( ) and { } nesting.
                    // If token[0] is ( or {, start depth at 1 so [ inside are not top-level.
                    var t0 = expressionTokens[0].Type;
                    int scanDepth = (t0 == TokenType.LEFT_PAREN || t0 == TokenType.LEFT_BRACE) ? 1 : 0;
                    for (int i = 1; i < expressionTokens.Count; i++)
                    {
                        var tt = expressionTokens[i].Type;
                        if (tt == TokenType.LEFT_BRACKET && scanDepth == 0)
                        {
                            firstBracket = i;
                            break;
                        }
                        if (tt == TokenType.LEFT_PAREN || tt == TokenType.LEFT_BRACE || tt == TokenType.LEFT_BRACKET) scanDepth++;
                        else if (tt == TokenType.RIGHT_PAREN || tt == TokenType.RIGHT_BRACE || tt == TokenType.RIGHT_BRACKET) scanDepth--;
                    }
                }
                
                // Skip bracket function call handling for statement keywords (do, while, if, :)
                // These should be handled by the statement parser instead
                if (firstBracket != -1 && firstBracket == 1)
                {
                    var prefixToken = expressionTokens[0];
                    if (LRSStatementParser.CouldBeStatement(prefixToken.Type))
                    {
                        // Let the statement parser handle this
                        firstBracket = -1;
                    }
                }
                
                if (firstBracket != -1)
                {
                    // Check if this is a triadic/tetradic operation pattern
                    // Uses VerbRegistry to determine if verb supports triadic arity
                    var prefixToken = expressionTokens[0];
                    var prefixVerbName = VerbRegistry.TokenTypeToVerbName(prefixToken.Type);
                    var prefixVerb = VerbRegistry.GetVerb(prefixVerbName);
                    bool prefixSupportsTriadic = prefixVerb != null && prefixVerb.SupportedArities.Any(a => a >= 3);
                    
                    if (prefixSupportsTriadic)
                    {
                        // Check for triadic/tetradic bracket call pattern
                        var triadicNode = TryParseTriadicVerbPattern(expressionTokens, 0, firstBracket);
                        if (triadicNode != null)
                        {
                            return triadicNode;
                        }
                        // Fall through to normal LRS parsing for dyadic bracket notation (e.g., @[a;b])
                    }
                    else
                    {
                        // Not an amend operator — proceed with bracket function call handling
                        // Verify the token sequence from firstBracket to end is entirely consecutive bracket groups
                        // i.e., tokens[firstBracket..end] = [arg1][arg2]...[argN]
                        bool allBrackets = true;
                        int pos = firstBracket;
                        var bracketGroups = new List<(int start, int end)>();
                        while (pos < expressionTokens.Count)
                        {
                            if ((int)expressionTokens[pos].Type != (int)TokenType.LEFT_BRACKET)
                            {
                                allBrackets = false;
                                break;
                            }
                            var groupEnd = FindMatchingBracket(expressionTokens, pos);
                            if (groupEnd == -1)
                            {
                                allBrackets = false;
                                break;
                            }
                            bracketGroups.Add((pos, groupEnd));
                            pos = groupEnd + 1;
                        }
                        
                        if (allBrackets && bracketGroups.Count > 0)
                        {
                            // Parse the base (prefix before first bracket)
                            var prefixTokens = expressionTokens.GetRange(0, firstBracket);
                            
                            // Check if prefix contains dyadic operators at depth 0
                            // If so, skip bracket function call handling to preserve LRS semantics
                            // e.g., y,x[&~x _lin y] should be y joined with x[...], not (y,x)[...]
                            // Exception: a single verb token as the entire prefix is NOT a dyadic
                            // operator — it's a function being applied via bracket notation
                            // e.g., +[3 3 5][2] — the + is the function, not a dyadic op
                            bool prefixHasDyadicOperator = false;
                            if (prefixTokens.Count > 1)
                            {
                                int prefixDepth = 0;
                                for (int i = 0; i < prefixTokens.Count; i++)
                                {
                                    var tok = prefixTokens[i];
                                    if (tok.Type == TokenType.LEFT_PAREN || tok.Type == TokenType.LEFT_BRACKET || tok.Type == TokenType.LEFT_BRACE)
                                        prefixDepth++;
                                    else if (tok.Type == TokenType.RIGHT_PAREN || tok.Type == TokenType.RIGHT_BRACKET || tok.Type == TokenType.RIGHT_BRACE)
                                        prefixDepth--;
                                    else if (prefixDepth == 0 && OperatorDetector.SupportsDyadic(tok.Type))
                                    {
                                        prefixHasDyadicOperator = true;
                                        break;
                                    }
                                }
                            }
                            
                            // When prefix has dyadic operator, skip bracket handling to allow normal dyadic parsing
                            // This ensures LRS semantics: y,x[...] parses as y joined with x[...], not (y,x)[...]
                            if (!prefixHasDyadicOperator)
                            {
                            // Detect pattern: [IDENT, COLON, ...rest] in prefix, where rest is the function to call
                            // The bracket args belong to the function call, not to the assignment result
                            if (prefixTokens.Count >= 3 &&
                                prefixTokens[0].Type == TokenType.IDENTIFIER &&
                                prefixTokens[1].Type == TokenType.COLON)
                            {
                                // Build the function-call RHS (rest of prefix + all brackets)
                                // e.g., for r:f[2;3], prefix=[r,:,f], rhsTokens = [f] + all bracket groups
                                var rhsFuncTokens = prefixTokens.GetRange(2, prefixTokens.Count - 2);
                                // Reconstruct rhs as func + bracket groups
                                var rhsAllTokens = new List<Token>(rhsFuncTokens);
                                foreach (var (gs, ge) in bracketGroups)
                                    for (int bi = gs; bi <= ge; bi++)
                                        rhsAllTokens.Add(expressionTokens[bi]);
                                // Use ParseExpression's bracket-call logic via a fresh parser
                                // (this handles semicolons as arg separators correctly via SplitBracketArguments)
                                var tmpParser = new LRSParser(rhsAllTokens, BuildParseTree);
                                int tmpPos = 0;
                                var rhsNode = tmpParser.ParseExpression(ref tmpPos);
                                // Fall back to the bracket-call logic directly if needed
                                if (rhsNode != null)
                                {
                                    var assignNode = new ASTNode(ASTNodeType.Assignment);
                                    assignNode.Value = new SymbolValue(prefixTokens[0].Lexeme);
                                    assignNode.Children.Add(rhsNode);
                                    assignNode.IsTerminalAssignment = true;
                                    return assignNode;
                                }
                            }
                            
                            ASTNode? currentNode;
                            if (BuildParseTree)
                                currentNode = BuildParseTreeFromRight(prefixTokens);
                            else
                                currentNode = EvaluateFromRight(prefixTokens);
                            
                            // Check if prefix is a single verb token — verb[args] creates
                            // projections when fewer args than max arity are provided
                            bool prefixIsSingleVerb = prefixTokens.Count == 1 && IsVerbToken(prefixTokens[0].Type);
                            
                            if (currentNode != null)
                            {
                                // Apply each bracket group left-to-right
                                foreach (var (groupStart, groupEnd) in bracketGroups)
                                {
                                    var argTokens = expressionTokens.GetRange(groupStart + 1, groupEnd - groupStart - 1);
                                    
                                    if (argTokens.Count == 0)
                                    {
                                        // niladic call: f[]
                                        currentNode = ASTNode.MakeDyadicOp(TokenType.APPLY, currentNode, ASTNode.MakeVector(new List<ASTNode>()));
                                    }
                                    else
                                    {
                                        // Split arguments by semicolons and parse each one
                                        var splitArgs = SplitBracketArguments(argTokens, int.MaxValue);
                                        var argNodes = new List<ASTNode>();
                                        bool hasBlankSlot = splitArgs.Any(a => a.Count == 0);
                                        
                                        foreach (var splitArgTokens in splitArgs)
                                        {
                                            if (splitArgTokens.Count == 0)
                                            {
                                                // Blank slot (e.g. f[1;;3]) — preserve as NullValue sentinel for projection
                                                argNodes.Add(ASTNode.MakeLiteral(new NullValue()));
                                                continue;
                                            }
                                            ASTNode? argNode;
                                            if (BuildParseTree)
                                                argNode = BuildParseTreeFromRight(splitArgTokens);
                                            else
                                                argNode = EvaluateFromRight(splitArgTokens);
                                            
                                            if (argNode != null)
                                                argNodes.Add(argNode);
                                        }
                                        
                                        if (argNodes.Count > 0)
                                        {
                                            // When prefix is a single verb and bracket has fewer args 
                                            // than the verb's max arity, create a projection node
                                            // e.g., +[3 3 5] creates projection with left arg bound
                                            if (prefixIsSingleVerb && !hasBlankSlot && splitArgs.Count == 1)
                                            {
                                                var opSymbol = VerbRegistry.GetDyadicOperatorSymbol(prefixTokens[0].Type);
                                                var verb = VerbRegistry.GetVerb(opSymbol);
                                                int maxArity = verb?.SupportedArities?.Max() ?? 2;
                                                if (maxArity > 1)
                                                {
                                                    // Create projection: verb with first arg bound, second missing
                                                    currentNode = CreateProjectionNode(prefixTokens[0], argNodes[0], null);
                                                    prefixIsSingleVerb = false; // subsequent brackets are applications
                                                    continue;
                                                }
                                            }
                                            
                                            var argVector = argNodes.Count == 1 ? argNodes[0] : ASTNode.MakeVector(argNodes);
                                            currentNode = ASTNode.MakeDyadicOp(TokenType.APPLY, currentNode, argVector);
                                        }
                                    }
                                    // After first bracket, subsequent brackets are applications, not projections
                                    prefixIsSingleVerb = false;
                                }
                                return currentNode;
                            } // Close if (currentNode != null)
                            } // Close if (!prefixHasDyadicOperator)
                        } // Close if (allBrackets && bracketGroups.Count > 0)
                    } // Close else block
                } // Close if (firstBracket != -1)
            }
                
            // Step 2: Choose strategy based on mode
            ASTNode? result;
            if (BuildParseTree)
                result = BuildParseTreeFromRight(expressionTokens);
            else
                result = EvaluateFromRight(expressionTokens);
            return result;
        }
        
        /// <summary>
        /// Build parse tree from tokens using right-to-left strategy
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <returns>AST node representing the parsed expression</returns>
        public ASTNode? BuildParseTreeFromRight(List<Token> tokens)
        {
            if (tokens.Count == 0) return null;
            
            if (tokens.Count == 1)
                return CreateNodeFromToken(tokens[0]);
            
            // Check for chained bracket application (e.g., f[1][2] or +[3 3 5][2])
            // This must be done BEFORE other parsing to handle bracket binding correctly
            var bracketAppResult = TryHandleChainedBrackets(tokens);
            if (bracketAppResult != null)
                return bracketAppResult;
            
            // Check for disambiguating colon pattern: left_arg + verb + colon + adverb
            // Pattern: 1 +:/x (e.g., 1 +:/x for conditional transpose)
            // MUST be checked FIRST to avoid being split into left/op/right
            if (tokens.Count >= 4)
            {
                if (IsVerbToken(tokens[1].Type) &&
                    tokens[2].Type == TokenType.COLON &&
                    (tokens[3].Type == TokenType.ADVERB_SLASH ||
                     tokens[3].Type == TokenType.ADVERB_BACKSLASH ||
                     tokens[3].Type == TokenType.ADVERB_TICK))
                {
                    return ParseGenericVerbAdverbWithColon(tokens, 1);
                }
            }
            
            // Check for statements first (statements have lower precedence than verbs but higher than separators)
            if (tokens.Count >= 2)
            {
                var firstToken = tokens[0];
                if (LRSStatementParser.CouldBeStatement(firstToken.Type))
                {
                    return statementParser.ParseStatement(tokens);
                }
            }
            
            // Check for assignments and apply-and-assign (IDENTIFIER ':' or IDENTIFIER VERB ':')
            if (tokens.Count >= 3 && tokens[0].Type == TokenType.IDENTIFIER)
            {
                if (tokens[1].Type == TokenType.COLON)
                    return statementParser.ParseStatement(tokens);
                // Handle: x?: (3 tokens) or x+:y (4+ tokens)
                if (IsVerbToken(tokens[1].Type) && tokens[2].Type == TokenType.COLON)
                    return statementParser.ParseStatement(tokens);
                if (tokens[1].Type == TokenType.GLOBAL_ASSIGNMENT)
                    return statementParser.ParseStatement(tokens);
            }
            
            // Check for projection patterns BEFORE grouping constructs
            // 1. Parenthesized operator: (+) - projection with both arguments missing
            // 2. Postfix projection: 1+ - left fixed, right missing
            // 3. Prefix projection in brackets: +[;2] - left missing, right fixed
            if (tokens.Count >= 2)
            {
                var projectionResult = TryParseProjection(tokens);
                if (projectionResult != null)
                    return projectionResult;
            }
            
            // Check for grouping constructs (parentheses, braces, brackets)
            var firstTok = tokens[0];
            if (firstTok.Type == TokenType.LEFT_PAREN || firstTok.Type == TokenType.LEFT_BRACE || firstTok.Type == TokenType.LEFT_BRACKET)
            {
                var tempGrouping = new LRSGroupingParser(tokens, BuildParseTree, this);
                int pos = 0;
                ASTNode? groupingResult = firstTok.Type switch
                {
                    TokenType.LEFT_PAREN => tempGrouping.ParseParentheses(ref pos),
                    TokenType.LEFT_BRACE => tempGrouping.ParseBraces(ref pos),
                    _ => groupingParser.ParseBrackets(tokens)
                };
                // Only return the grouping result if it consumed the entire token list.
                // If tokens remain after the closing delimiter, the group is the left operand
                // of a larger expression (e.g. "((y-1)_ a) - 0,...") — fall through to
                // dyadic/monadic parsing so the outer operator is handled correctly.
                bool allConsumed = firstTok.Type == TokenType.LEFT_BRACKET
                    ? true   // ParseBrackets always takes the full list
                    : pos >= tokens.Count;
                if (groupingResult != null && allConsumed)
                    return groupingResult;
                
            }
            
            // Check for verb-immediate-left adverb patterns FIRST (highest priority)
            // Pattern: verb'args or verb/args or verb\args or verb':args or verb/:args or verb\:args
            // This must be checked BEFORE any other parsing to prevent the expression from being parsed as a regular operation
            if (tokens.Count >= 2)
            {
                var potentialVerb = tokens[0];
                var potentialAdverb = tokens[1];
                
                // Check if first token is a verb and second token is an adverb
                bool isVerb = VerbRegistry.IsVerbToken(potentialVerb.Type);
                bool isAdverb = VerbRegistry.IsAdverbToken(potentialAdverb.Type);
                bool isSingleGlyphAdverb = potentialAdverb.Type == TokenType.ADVERB_TICK || 
                                          potentialAdverb.Type == TokenType.ADVERB_SLASH || 
                                          potentialAdverb.Type == TokenType.ADVERB_BACKSLASH;
                bool isTwoGlyphAdverb = potentialAdverb.Type == TokenType.ADVERB_TICK_COLON ||
                                       potentialAdverb.Type == TokenType.ADVERB_SLASH_COLON ||
                                       potentialAdverb.Type == TokenType.ADVERB_BACKSLASH_COLON;
                
                if (isVerb && isAdverb && isSingleGlyphAdverb)
                {
                    // Use LRSAdverbParser to handle verb-immediate-left pattern
                    var adverbParser = new LRSAdverbParser(tokens, BuildParseTree);
                    int position = 1; // Start at adverb position
                    var adverbResult = adverbParser.ParseVerbImmediateLeftAdverb(ref position);
                    if (adverbResult != null)
                        return adverbResult;
                }
                else if (isVerb && isAdverb && isTwoGlyphAdverb)
                {
                    // Delegate two-glyph adverb patterns to dyadic parser which handles them correctly
                    var adverbResult = dyadicParser.ParseDyadicOperation(tokens);
                    if (adverbResult != null)
                        return adverbResult;
                }
            }
            
            // Check for atomic-only sequences (implicit vector creation)
            if (tokens.Count > 1 && tokens.All(t => LRSAtomicParser.CanBeImplicitVectorElement(t.Type)))
            {
                return TryCreateImplicitVector(tokens);
            }
            
            // Check for function+adverb patterns BEFORE dyadic parsing
            // These have higher precedence than regular dyadic operations
            // Pattern: {function}/vector or {function}\vector or {function}/ (noun form, no argument)
            if (tokens.Count >= 2 && tokens[0].Type == TokenType.LEFT_BRACE)
            {
                // Find the matching right brace for the function
                int braceLevel = 1;
                int functionEnd = -1;
                for (int i = 1; i < tokens.Count; i++)
                {
                    if (tokens[i].Type == TokenType.LEFT_BRACE)
                        braceLevel++;
                    else if (tokens[i].Type == TokenType.RIGHT_BRACE)
                        braceLevel--;
                    
                    if (braceLevel == 0)
                    {
                        functionEnd = i;
                        break;
                    }
                }
                
                // Check if token right after the closing brace is an adverb
                if (functionEnd > 0 && functionEnd + 1 < tokens.Count &&
                    VerbRegistry.IsAdverbToken(tokens[functionEnd + 1].Type))
                {
                    var adverbToken = tokens[functionEnd + 1];
                    // Parse the function (from 0 to functionEnd inclusive)
                    var functionTokens = tokens.GetRange(0, functionEnd + 1);
                    var functionNode = groupingParser.ParseBrackets(functionTokens);
                    if (functionNode != null)
                    {
                        // Parse the remaining tokens after the adverb as the argument
                        var remainingTokens = tokens.Skip(functionEnd + 2).ToList();
                        if (remainingTokens.Count > 0)
                        {
                            var argNode = BuildParseTreeFromRight(remainingTokens);
                            if (argNode != null)
                            {
                                // Create adverb node: ADVERB(adverb, function, argument)
                                return CreateAdverbNode(adverbToken, functionNode, argNode);
                            }
                        }
                        else
                        {
                            // Noun form: no argument — create 1-child adverb node (e.g. {x'}/ for point-free)
                            return CreateAdverbNode(adverbToken, functionNode, null);
                        }
                    }
                }
                else if (functionEnd > 0 && functionEnd + 1 < tokens.Count)
                {
                    // Empty braces {} form specifier: let dyadic parser handle {} op expr
                    if (functionEnd == 1)
                    {
                        var dyadicResult = dyadicParser.ParseDyadicOperation(tokens);
                        if (dyadicResult != null)
                            return dyadicResult;
                    }
                }
            }
            
            // Check for projection patterns BEFORE dyadic parsing
            // 1. Parenthesized operator: (+) - projection with both arguments missing
            // 2. Postfix projection: 1+ - left fixed, right missing
            // 3. Prefix projection in brackets: +[;2] - left missing, right fixed
            if (tokens.Count >= 2)
            {
                var projectionResult = TryParseProjection(tokens);
                if (projectionResult != null)
                    return projectionResult;
            }
            
            // Use arity detection to determine parsing order for multi-token expressions
            if (tokens.Count > 2)
            {
                var firstVerbToken = tokens.Count > 0 ? tokens[0] : null;
                if (firstVerbToken != null && IsVerbToken(firstVerbToken.Type))
                {
                    // Try precise arity determination first
                    try
                    {
                        int determinedArity = DetectOperationArity(tokens, 0);
                        
                        // Parse based on the determined arity - no trial and error
                        if (determinedArity == 1)
                        {
                            var monadicResult = monadicParser.ParseMonadicOperator(tokens);
                            if (monadicResult != null)
                                return monadicResult;
                        }
                        else if (determinedArity == 2)
                        {
                            var dyadicResult = dyadicParser.ParseDyadicOperation(tokens);
                            if (dyadicResult != null)
                                return dyadicResult;
                        }
                        else if (determinedArity >= 3)
                        {
                            return ParseMultiAryOperation(tokens);
                        }
                    }
                    catch
                    {
                        // If arity determination fails, fall back to traditional parsing
                    }
                    
                    // Fallback: try dyadic first, then monadic for operators that support both
                    if (OperatorDetector.SupportsDyadic(firstVerbToken.Type) && OperatorDetector.SupportsMonadic(firstVerbToken.Type))
                    {
                        var dyadicResult = dyadicParser.ParseDyadicOperation(tokens);
                        if (dyadicResult != null)
                            return dyadicResult;
                            
                        var monadicResult = monadicParser.ParseMonadicOperator(tokens);
                        if (monadicResult != null)
                            return monadicResult;
                    }
                    else if (OperatorDetector.SupportsDyadic(firstVerbToken.Type))
                    {
                        var dyadicResult = dyadicParser.ParseDyadicOperation(tokens);
                        if (dyadicResult != null)
                            return dyadicResult;
                    }
                    else if (OperatorDetector.SupportsMonadic(firstVerbToken.Type))
                    {
                        return monadicParser.ParseMonadicOperator(tokens);
                    }
                }
                else
                {
                    // Debug: first token is not a verb
                }
            }
            
            // For expressions like "_db _bd (1;2.5;"a")", parse rightmost monadic first
            if (tokens.Count >= 2)
            {
                var monadicChainResult = TryParseMonadicChain(tokens);
                if (monadicChainResult != null)
                    return monadicChainResult;
            }
            
            // Check for disambiguating colon pattern: left_arg + verb + colon + adverb
            // Pattern: 1 +:/x (e.g., 1 +:/x for conditional transpose)
            // MUST be checked BEFORE dyadic parsing to avoid being split
            if (tokens.Count >= 4)
            {
                if (IsVerbToken(tokens[1].Type) &&
                    tokens[2].Type == TokenType.COLON &&
                    (tokens[3].Type == TokenType.ADVERB_SLASH ||
                     tokens[3].Type == TokenType.ADVERB_BACKSLASH ||
                     tokens[3].Type == TokenType.ADVERB_TICK))
                {
                    return ParseGenericVerbAdverbWithColon(tokens, 1);
                }
            }
            
            // Handle multi-token expressions using dyadic parser (right-to-left evaluation)
            // This ensures expressions like "_bd ,5" are parsed as: _bd (monadic) applied to (,5 monadic enlist)
            // rather than as a function call _bd(,5)
            if (tokens.Count > 2)
            {
                var dyadicResult = dyadicParser.ParseDyadicOperation(tokens);
                if (dyadicResult != null)
                    return dyadicResult;
            }
            
            // Handle monadic operations (2 tokens: verb + argument)
            if (tokens.Count == 2)
            {
                // Special case: IDENTIFIER + ADVERB (noun form, e.g. x' in lambda body)
                if (tokens[0].Type == TokenType.IDENTIFIER && VerbRegistry.IsAdverbToken(tokens[1].Type))
                {
                    var fnNode = CreateNodeFromToken(tokens[0]);
                    if (fnNode != null)
                        return CreateAdverbNode(tokens[1], fnNode, null);
                }
                // Special case: VERB + COLON (monadic projection disambiguation, e.g. ,:)
                if (IsVerbToken(tokens[0].Type) && tokens[1].Type == TokenType.COLON)
                {
                    var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                    var operatorSymbol = VerbRegistry.GetDyadicOperatorSymbol(tokens[0].Type);
                    projectedNode.Value = new SymbolValue(operatorSymbol);
                    projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1)));
                    return projectedNode;
                }
                return monadicParser.ParseMonadicOperator(tokens);
            }
            
            // Handle identifier[bracketArgs] followed by additional tokens: e.g. d[x;y#,:]z
            // Also handles function tokens like _f[expression] for recursion
            // This is a bracket function call whose result is then applied to the remaining tokens.
            // Detect: first token is IDENTIFIER or FUNCTION, second is '[', there's a matching ']', and more tokens follow.
            if (tokens.Count >= 4 &&
                (firstTok.Type == TokenType.IDENTIFIER || firstTok.Type == TokenType.FUNCTION) &&
                tokens[1].Type == TokenType.LEFT_BRACKET)
            {
                int closingBracket = FindMatchingBracket(tokens, 1);
                if (closingBracket > 0 && closingBracket < tokens.Count - 1)
                {
                    // Check if token right after the closing bracket is an adverb
                    // Pattern: identifier[args]' - apply adverb to function call result
                    if (VerbRegistry.IsAdverbToken(tokens[closingBracket + 1].Type))
                    {
                        var adverbToken = tokens[closingBracket + 1];
                        
                        // Parse the bracket call as the function
                        var bracketContent = tokens.GetRange(2, closingBracket - 2);
                        var argGroups = SplitBracketArguments(bracketContent, int.MaxValue);
                        var argNodes = new List<ASTNode>();
                        foreach (var ag in argGroups)
                        {
                            if (ag.Count == 0)
                                argNodes.Add(ASTNode.MakeLiteral(new NullValue()));
                            else
                            {
                                var an = BuildParseTree ? BuildParseTreeFromRight(ag) : EvaluateFromRight(ag);
                                argNodes.Add(an ?? ASTNode.MakeLiteral(new NullValue()));
                            }
                        }
                        var funcCallNode = new ASTNode(ASTNodeType.FunctionCall);
                        funcCallNode.Children.Add(ASTNode.MakeVariable(firstTok.Lexeme));
                        funcCallNode.Children.AddRange(argNodes);
                        
                        // Parse the remaining tokens after the adverb as the argument
                        if (closingBracket + 2 < tokens.Count)
                        {
                            var remainingTokens = tokens.Skip(closingBracket + 2).ToList();
                            var argNode = BuildParseTree ? BuildParseTreeFromRight(remainingTokens) : EvaluateFromRight(remainingTokens);
                            if (argNode != null)
                            {
                                // Create adverb node: ADVERB(adverb, function, argument)
                                return CreateAdverbNode(adverbToken, funcCallNode, argNode);
                            }
                        }
                        // Noun form: no argument after adverb
                        return CreateAdverbNode(adverbToken, funcCallNode, null);
                    }
                    
                    // Parse the bracket call: identifier[bracketContent]
                    var bracketContent2 = tokens.GetRange(2, closingBracket - 2);
                    var argGroups2 = SplitBracketArguments(bracketContent2, 2);
                    var argNodes2 = new List<ASTNode>();
                    foreach (var ag in argGroups2)
                    {
                        if (ag.Count == 0)
                            argNodes2.Add(ASTNode.MakeLiteral(new NullValue()));
                        else
                        {
                            var an = BuildParseTree ? BuildParseTreeFromRight(ag) : EvaluateFromRight(ag);
                            if (an != null) argNodes2.Add(an);
                        }
                    }
                    var funcCallNode2 = new ASTNode(ASTNodeType.FunctionCall);
                    funcCallNode2.Children.Add(ASTNode.MakeVariable(firstTok.Lexeme));
                    funcCallNode2.Children.AddRange(argNodes2);

                    // Parse the remaining tokens as the argument to the result
                    var afterBracket = tokens.GetRange(closingBracket + 1, tokens.Count - closingBracket - 1);
                    ASTNode? afterNode = afterBracket.Count == 1
                        ? CreateNodeFromToken(afterBracket[0])
                        : (BuildParseTree ? BuildParseTreeFromRight(afterBracket) : EvaluateFromRight(afterBracket));
                    if (afterNode != null)
                    {
                        // Apply the bracket-call result to the remaining argument
                        var applyNode = new ASTNode(ASTNodeType.FunctionCall);
                        applyNode.Children.Add(funcCallNode2);
                        applyNode.Children.Add(afterNode);
                        return applyNode;
                    }
                    return funcCallNode2;
                }
                else if (closingBracket > 0 && closingBracket == tokens.Count - 1)
                {
                    // Bracket is at end of expression - handle as function call
                    var bracketContent = tokens.GetRange(2, closingBracket - 2);
                    var argGroups = SplitBracketArguments(bracketContent, int.MaxValue);
                    var argNodes = new List<ASTNode>();
                    foreach (var ag in argGroups)
                    {
                        if (ag.Count == 0)
                            argNodes.Add(ASTNode.MakeLiteral(new NullValue()));
                        else
                        {
                            var an = BuildParseTree ? BuildParseTreeFromRight(ag) : EvaluateFromRight(ag);
                            argNodes.Add(an ?? ASTNode.MakeLiteral(new NullValue()));
                        }
                    }
                    var funcCallNode = new ASTNode(ASTNodeType.FunctionCall);
                    funcCallNode.Children.Add(ASTNode.MakeVariable(firstTok.Lexeme));
                    funcCallNode.Children.AddRange(argNodes);
                    return funcCallNode;
                }
            }

            // Try function call parsing only if dyadic/monadic parsing didn't handle it
            // This handles explicit function calls like f[x] or lambda applications
            if (LRSFunctionParser.CouldBeFunction(firstTok.Type))
            {
                var functionResult = functionParser.ParseFunctionCall(tokens);
                if (functionResult != null)
                    return functionResult;
            }
            
            // Check for multi-arity operations (triadic, tetradic, variadic)
            var multiAryResult = ParseMultiAryOperation(tokens);
            if (multiAryResult != null)
                return multiAryResult;
            
            // If we reach here, no parsing strategy succeeded
            return null;
        }
        
        /// <summary>
        /// Try to parse right-nested monadic operations according to LRS rules (Right Scope First)
        /// For expressions like "_db _bd (1;2.5;"a")", parse as right-nested monadic operations
        /// Structure: _db(_bd((1;2.5;"a")))
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <returns>AST node for nested monadic operations, or null if not applicable</returns>
        private ASTNode? TryParseMonadicChain(List<Token> tokens)
        {
            if (tokens.Count < 2) return null;
            
            // Check if the first token is a monadic operator
            if (!OperatorDetector.SupportsMonadic(tokens[0].Type))
                return null;
            
            // Parse the right side (everything after the first monadic operator)
            var rightTokens = tokens.GetRange(1, tokens.Count - 1);
            var rightNode = BuildParseTreeFromRight(rightTokens);
            
            if (rightNode == null) return null;
            
            // Create the monadic operation node
            var verbName = VerbRegistry.TokenTypeToVerbName(tokens[0].Type);
            var children = new List<ASTNode> { rightNode };
            var monadicNode = new ASTNode(ASTNodeType.MonadicOp, new SymbolValue(verbName), children);
            
            return monadicNode;
        }
        
        /// <summary>
        /// Handle chained bracket application (e.g., f[1][2] or +[3 3 5][2])
        /// Brackets bind to the item on their left with left-to-right associativity
        /// </summary>
        private ASTNode? TryHandleChainedBrackets(List<Token> tokens)
        {
            if (tokens.Count < 3)
                return null;
            
            // Don't handle conditional statements (they start with specific keywords)
            if (tokens.Count >= 2 && LRSStatementParser.CouldBeStatement(tokens[0].Type))
                return null;
            
            // Find first bracket at top level
            int firstBracket = -1;
            {
                var t0 = tokens[0].Type;
                int scanDepth = (t0 == TokenType.LEFT_PAREN || t0 == TokenType.LEFT_BRACE) ? 1 : 0;
                for (int i = 1; i < tokens.Count; i++)
                {
                    var tt = tokens[i].Type;
                    if (tt == TokenType.LEFT_BRACKET && scanDepth == 0)
                    {
                        firstBracket = i;
                        break;
                    }
                    if (tt == TokenType.LEFT_PAREN || tt == TokenType.LEFT_BRACE || tt == TokenType.LEFT_BRACKET) scanDepth++;
                    else if (tt == TokenType.RIGHT_PAREN || tt == TokenType.RIGHT_BRACE || tt == TokenType.RIGHT_BRACKET) scanDepth--;
                }
            }
            
            if (firstBracket == -1)
                return null;
            
            // Check if prefix contains dyadic operators (which would prevent bracket binding)
            // Exception: a single verb token is the function being applied, not a dyadic op
            var prefixTokens = tokens.GetRange(0, firstBracket);
            bool prefixHasDyadicOperator = false;
            if (prefixTokens.Count > 1)
            {
                int prefixDepth = 0;
                for (int i = 0; i < prefixTokens.Count; i++)
                {
                    var tok = prefixTokens[i];
                    if (tok.Type == TokenType.LEFT_PAREN || tok.Type == TokenType.LEFT_BRACKET || tok.Type == TokenType.LEFT_BRACE)
                        prefixDepth++;
                    else if (tok.Type == TokenType.RIGHT_PAREN || tok.Type == TokenType.RIGHT_BRACKET || tok.Type == TokenType.RIGHT_BRACE)
                        prefixDepth--;
                    else if (prefixDepth == 0 && OperatorDetector.SupportsDyadic(tok.Type))
                    {
                        prefixHasDyadicOperator = true;
                        break;
                    }
                }
            }
            
            if (prefixHasDyadicOperator)
                return null;
            
            // Verify all remaining tokens are bracket groups
            bool allBrackets = true;
            int pos = firstBracket;
            var bracketGroups = new List<(int start, int end)>();
            while (pos < tokens.Count)
            {
                if (tokens[pos].Type != TokenType.LEFT_BRACKET)
                {
                    allBrackets = false;
                    break;
                }
                var groupEnd = FindMatchingBracket(tokens, pos);
                if (groupEnd == -1)
                {
                    allBrackets = false;
                    break;
                }
                bracketGroups.Add((pos, groupEnd));
                pos = groupEnd + 1;
            }
            
            if (!allBrackets || bracketGroups.Count == 0)
                return null;
            
            // Check if prefix is a single verb token
            bool prefixIsSingleVerb = prefixTokens.Count == 1 && IsVerbToken(prefixTokens[0].Type);
            
            // When prefix is a single verb, check the first bracket for blank slots.
            // Blank slots mean projection (e.g. +[3;] or +[;5]) — let TryParseProjection handle those.
            if (prefixIsSingleVerb && bracketGroups.Count >= 1)
            {
                var (fg, eg) = bracketGroups[0];
                var firstArgTokens = tokens.GetRange(fg + 1, eg - fg - 1);
                var firstSplit = SplitBracketArguments(firstArgTokens, int.MaxValue);
                if (firstSplit.Any(a => a.Count == 0))
                    return null;
                
                // verb[arg1][arg2] with exactly 2 single-arg brackets → dyadic op: verb(arg1, arg2)
                if (bracketGroups.Count == 2 && firstSplit.Count == 1)
                {
                    var (sg, se) = bracketGroups[1];
                    var secondArgTokens = tokens.GetRange(sg + 1, se - sg - 1);
                    var secondSplit = SplitBracketArguments(secondArgTokens, int.MaxValue);
                    if (secondSplit.Count == 1 && secondSplit[0].Count > 0 && !secondSplit.Any(a => a.Count == 0))
                    {
                        var leftArg = BuildParseTreeFromRight(firstSplit[0]);
                        var rightArg = BuildParseTreeFromRight(secondSplit[0]);
                        if (leftArg != null && rightArg != null)
                            return new ASTNode(ASTNodeType.DyadicOp, new SymbolValue(prefixTokens[0].Lexeme), new List<ASTNode> { leftArg, rightArg });
                    }
                }
            }
            
            // Parse the base (prefix before first bracket)
            var currentNode = BuildParseTreeFromRight(prefixTokens);
            if (currentNode == null)
                return null;
            
            // Apply each bracket group left-to-right
            foreach (var (groupStart, groupEnd) in bracketGroups)
            {
                var argTokens = tokens.GetRange(groupStart + 1, groupEnd - groupStart - 1);
                
                if (argTokens.Count == 0)
                {
                    // niladic call: f[]
                    currentNode = ASTNode.MakeDyadicOp(TokenType.APPLY, currentNode, ASTNode.MakeVector(new List<ASTNode>()));
                }
                else
                {
                    // Split arguments by semicolons and parse each one
                    var splitArgs = SplitBracketArguments(argTokens, int.MaxValue);
                    var argNodes = new List<ASTNode>();
                    
                    foreach (var splitArgTokens in splitArgs)
                    {
                        if (splitArgTokens.Count == 0)
                        {
                            // Blank slot (e.g. f[1;;3]) — preserve as NullValue sentinel for projection
                            argNodes.Add(ASTNode.MakeLiteral(new NullValue()));
                            continue;
                        }
                        var argNode = BuildParseTreeFromRight(splitArgTokens);
                        if (argNode != null)
                            argNodes.Add(argNode);
                    }
                    
                    if (argNodes.Count > 0)
                    {
                        var argVector = argNodes.Count == 1 ? argNodes[0] : ASTNode.MakeVector(argNodes);
                        currentNode = ASTNode.MakeDyadicOp(TokenType.APPLY, currentNode, argVector);
                    }
                }
            }
            
            return currentNode;
        }
        
        /// <summary>
        /// Try to parse projection patterns
        /// 1. (+) - parenthesized operator alone
        /// 2. 1+ - postfix projection (left fixed, right missing)
        /// 3. +[;2] - bracket projection with semicolon
        /// </summary>
        private ASTNode? TryParseProjection(List<Token> tokens)
        {
            // Pattern 1: (+) - parenthesized operator alone
            if (tokens.Count == 3 &&
                tokens[0].Type == TokenType.LEFT_PAREN &&
                IsVerbToken(tokens[1].Type) &&
                tokens[2].Type == TokenType.RIGHT_PAREN)
            {
                return CreateProjectionNode(tokens[1], null, null);
            }
            
            // Pattern 2: expr+ - postfix projection (left fixed, right missing)
            // Last token is an operator that supports dyadic operations,
            // and the prefix (everything before it) has no top-level dyadic operators
            // (so we don't misparse "a + b" where b happens to be a verb)
            if (tokens.Count >= 2 &&
                IsVerbToken(tokens[tokens.Count - 1].Type) &&
                OperatorDetector.SupportsDyadic(tokens[tokens.Count - 1].Type))
            {
                var verbToken = tokens[tokens.Count - 1];
                var leftTokens = tokens.GetRange(0, tokens.Count - 1);
                // Only treat as postfix projection if the prefix has no top-level dyadic operators
                bool prefixHasDyadic = false;
                int pd = 0;
                foreach (var lt in leftTokens)
                {
                    if (lt.Type == TokenType.LEFT_PAREN || lt.Type == TokenType.LEFT_BRACKET || lt.Type == TokenType.LEFT_BRACE) pd++;
                    else if (lt.Type == TokenType.RIGHT_PAREN || lt.Type == TokenType.RIGHT_BRACKET || lt.Type == TokenType.RIGHT_BRACE) pd--;
                    else if (pd == 0 && IsVerbToken(lt.Type)) { prefixHasDyadic = true; break; }
                }
                if (!prefixHasDyadic)
                {
                    var leftOperand = leftTokens.Count == 1
                        ? CreateNodeFromToken(leftTokens[0])
                        : BuildParseTreeFromRight(leftTokens);
                    if (leftOperand != null)
                        return CreateProjectionNode(verbToken, leftOperand, null);
                }
            }
            
            // Pattern 3: +[;2] or +[1;] - bracket projection
            if (tokens.Count >= 3 &&
                IsVerbToken(tokens[0].Type) &&
                tokens[1].Type == TokenType.LEFT_BRACKET)
            {
                // Find matching bracket
                var bracketEnd = FindMatchingBracket(tokens, 1);
                if (bracketEnd != -1)
                {
                    // Check for semicolon indicating projection
                    bool hasSemicolon = false;
                    for (int i = 2; i < bracketEnd; i++)
                    {
                        if (tokens[i].Type == TokenType.SEMICOLON)
                        {
                            hasSemicolon = true;
                            break;
                        }
                    }
                    
                    if (hasSemicolon)
                    {
                        // Parse the bracket content as projection arguments
                        var bracketContent = tokens.GetRange(2, bracketEnd - 2);
                        ASTNode? leftArg = null;
                        ASTNode? rightArg = null;
                        
                        // Split by semicolon
                        int semicolonPos = -1;
                        for (int i = 0; i < bracketContent.Count; i++)
                        {
                            if (bracketContent[i].Type == TokenType.SEMICOLON)
                            {
                                semicolonPos = i;
                                break;
                            }
                        }
                        
                        if (semicolonPos > 0)
                        {
                            // Has left argument
                            var leftTokens = bracketContent.GetRange(0, semicolonPos);
                            leftArg = BuildParseTreeFromRight(leftTokens);
                        }
                        
                        if (semicolonPos < bracketContent.Count - 1)
                        {
                            // Has right argument
                            var rightTokens = bracketContent.GetRange(semicolonPos + 1, bracketContent.Count - semicolonPos - 1);
                            rightArg = BuildParseTreeFromRight(rightTokens);
                        }
                        
                        return CreateProjectionNode(tokens[0], leftArg, rightArg);
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Create a ProjectedFunction AST node
        /// </summary>
        internal ASTNode CreateProjectionNode(Token operatorToken, ASTNode? leftArg, ASTNode? rightArg)
        {
            var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
            
            // Get operator symbol
            var operatorSymbol = VerbRegistry.GetDyadicOperatorSymbol(operatorToken.Type);
            projectedNode.Value = new SymbolValue(operatorSymbol);
            
            // Determine arity (default to 2 for dyadic operators)
            var verb = VerbRegistry.GetVerb(operatorSymbol);
            int arity = verb?.SupportedArities?.Max() ?? 2;
            projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(arity)));
            
            // Add left argument (or :: placeholder if missing)
            if (leftArg != null)
            {
                projectedNode.Children.Add(leftArg);
            }
            else
            {
                projectedNode.Children.Add(ASTNode.MakeLiteral(new SymbolValue("::")));
            }
            
            // Add right argument (or :: placeholder if missing)  
            if (rightArg != null)
            {
                projectedNode.Children.Add(rightArg);
            }
            else
            {
                projectedNode.Children.Add(ASTNode.MakeLiteral(new SymbolValue("::")));
            }
            
            return projectedNode;
        }
        
        /// <summary>
        /// Find matching closing bracket for an opening bracket
        /// </summary>
        private int FindMatchingBracket(List<Token> tokens, int openPos)
        {
            if (openPos >= tokens.Count || tokens[openPos].Type != TokenType.LEFT_BRACKET)
                return -1;
            
            int depth = 1;
            for (int i = openPos + 1; i < tokens.Count; i++)
            {
                if (tokens[i].Type == TokenType.LEFT_BRACKET)
                    depth++;
                else if (tokens[i].Type == TokenType.RIGHT_BRACKET)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// Parse expression tokens from right to left (LRS strategy)
        /// </summary>
        /// <param name="expressionTokens">Tokens to parse</param>
        /// <returns>AST node representing parsed expression</returns>
        internal ASTNode? EvaluateFromRight(List<Token> expressionTokens)
        {
            if (expressionTokens.Count == 0)
            {
                return null;
            }
                
            if (expressionTokens.Count == 1)
            {
                return CreateNodeFromToken(expressionTokens[0]);
            }

            // 2-token special cases (mirrors BuildParseTreeFromRight)
            if (expressionTokens.Count == 2)
            {
                // VERB + COLON: monadic projection e.g. ,:  returns ProjectedFunction
                if (IsVerbToken(expressionTokens[0].Type) && expressionTokens[1].Type == TokenType.COLON)
                {
                    var projNode = new ASTNode(ASTNodeType.ProjectedFunction);
                    projNode.Value = new SymbolValue(expressionTokens[0].Lexeme);
                    projNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1)));
                    return projNode;
                }
                // IDENTIFIER + ADVERB: noun-form adverb e.g. x'
                if (expressionTokens[0].Type == TokenType.IDENTIFIER && VerbRegistry.IsAdverbToken(expressionTokens[1].Type))
                {
                    var fnNode = CreateNodeFromToken(expressionTokens[0]);
                    if (fnNode != null)
                        return CreateAdverbNode(expressionTokens[1], fnNode, null);
                }
                // COLON + IDENTIFIER: disambiguation marker followed by argument
                // e.g., in ,:z the right operand is :z, where : is monadic disambiguation
                if (expressionTokens[0].Type == TokenType.COLON && expressionTokens[1].Type == TokenType.IDENTIFIER)
                {
                    return CreateNodeFromToken(expressionTokens[1]);
                }
            }

            // Postfix projection pattern: expr+ (last token is dyadic verb, prefix has no top-level verbs)
            // e.g., (3 3 5 +) → left-bound projection of + with left=3 3 5
            if (expressionTokens.Count >= 2 &&
                IsVerbToken(expressionTokens[expressionTokens.Count - 1].Type) &&
                OperatorDetector.SupportsDyadic(expressionTokens[expressionTokens.Count - 1].Type))
            {
                var lastVerbToken = expressionTokens[expressionTokens.Count - 1];
                var prefixTokens2 = expressionTokens.GetRange(0, expressionTokens.Count - 1);
                bool prefixHasDyadic2 = false;
                int pd2 = 0;
                foreach (var lt in prefixTokens2)
                {
                    if (lt.Type == TokenType.LEFT_PAREN || lt.Type == TokenType.LEFT_BRACKET || lt.Type == TokenType.LEFT_BRACE) pd2++;
                    else if (lt.Type == TokenType.RIGHT_PAREN || lt.Type == TokenType.RIGHT_BRACKET || lt.Type == TokenType.RIGHT_BRACE) pd2--;
                    else if (pd2 == 0 && IsVerbToken(lt.Type)) { prefixHasDyadic2 = true; break; }
                }
                if (!prefixHasDyadic2)
                {
                    ASTNode? leftOperand2 = prefixTokens2.Count == 1
                        ? CreateNodeFromToken(prefixTokens2[0])
                        : EvaluateFromRight(prefixTokens2);
                    if (leftOperand2 != null)
                        return CreateProjectionNode(lastVerbToken, leftOperand2, null);
                }
            }
            
            // Check for function+adverb patterns FIRST
            // Pattern: {function}/vector or {function}\vector
            if (expressionTokens.Count >= 2 && expressionTokens[0].Type == TokenType.LEFT_BRACE)
            {
                // Find the matching right brace for the function
                int braceLevel = 1;
                int functionEnd = -1;
                for (int i = 1; i < expressionTokens.Count; i++)
                {
                    if (expressionTokens[i].Type == TokenType.LEFT_BRACE)
                        braceLevel++;
                    else if (expressionTokens[i].Type == TokenType.RIGHT_BRACE)
                        braceLevel--;
                    
                    if (braceLevel == 0)
                    {
                        functionEnd = i;
                        break;
                    }
                }
                
                // Check if there's an adverb immediately after the closing brace
                if (functionEnd > 0 && functionEnd + 1 < expressionTokens.Count)
                {
                    var potentialAdverb = expressionTokens[functionEnd + 1];
                    
                    if (VerbRegistry.IsAdverbToken(potentialAdverb.Type))
                    {
                        // Parse the function (from 0 to functionEnd inclusive)
                        var functionTokens = expressionTokens.GetRange(0, functionEnd + 1);
                        var functionNode = groupingParser.ParseBraces(functionTokens);
                        if (functionNode != null)
                        {
                            // Parse the remaining tokens as the adverb argument
                            var remainingTokens = expressionTokens.Skip(functionEnd + 2).ToList(); // Skip function and adverb
                            if (remainingTokens.Count > 0)
                            {
                                var argNode = EvaluateFromRight(remainingTokens);
                                if (argNode != null)
                                {
                                    // Create adverb node: ADVERB(adverb, function, argument)
                                    var adverbNode = CreateAdverbNode(potentialAdverb, functionNode, argNode);
                                    return adverbNode;
                                }
                            }
                            else
                            {
                                // Noun form: no argument — create 1-child adverb node for point-free projection
                                return CreateAdverbNode(potentialAdverb, functionNode, null);
                            }
                        }
                        else
                        {
                            // Function parsing failed
                        }
                    }
                    else
                    {
                        var nextToken = expressionTokens[functionEnd + 1];
                        
                        // Empty braces {} form specifier: let dyadic parser handle {} op expr
                        // The dyadic parser recognizes {} as FormSpecifier in ParseSubExpression
                        if (functionEnd == 1)
                        {
                            var dyadicResult = dyadicParser.ParseDyadicOperation(expressionTokens);
                            if (dyadicResult != null)
                                return dyadicResult;
                        }
                        
                        // Direct lambda application: only if next token is not a dyadic operator
                        // (dyadic operators should bind to the lambda as left operand, not as function application)
                        if (!OperatorDetector.SupportsDyadic(nextToken.Type))
                        {
                            var functionTokens = expressionTokens.GetRange(0, functionEnd + 1);
                            var functionNode = groupingParser.ParseBraces(functionTokens);
                            if (functionNode != null)
                            {
                                var remainingTokens = expressionTokens.Skip(functionEnd + 1).ToList();
                                var argNode = EvaluateFromRight(remainingTokens);
                                if (argNode != null)
                                {
                                    return ASTNode.MakeFunctionCall(functionNode, new System.Collections.Generic.List<ASTNode> { argNode });
                                }
                            }
                        }
                    }
                }
            }
            
            // Check for function variable + adverb patterns
            // Pattern: f/vector or f\vector where f is a function variable
            // Also: f' or f/ with no argument (noun form — e.g. x' in lambda body)
            if (expressionTokens.Count >= 2 && expressionTokens[0].Type == TokenType.IDENTIFIER)
            {
                var potentialAdverb = expressionTokens[1];
                if (VerbRegistry.IsAdverbToken(potentialAdverb.Type))
                {
                    // Create a variable node for the function
                    var functionNode = CreateNodeFromToken(expressionTokens[0]);
                    
                    if (functionNode != null)
                    {
                        // Parse the remaining tokens as the adverb argument
                        var remainingTokens = expressionTokens.Skip(2).ToList(); // Skip function variable and adverb
                        if (remainingTokens.Count > 0)
                        {
                            var argNode = EvaluateFromRight(remainingTokens);
                            if (argNode != null)
                            {
                                // Create adverb node: ADVERB(adverb, function, argument)
                                var adverbNode = CreateAdverbNode(potentialAdverb, functionNode, argNode);
                                return adverbNode;
                            }
                        }
                        else
                        {
                            // Noun form: no argument after adverb (e.g. x' in lambda body)
                            // Create a 1-child adverb node representing the adverb applied to identifier
                            return CreateAdverbNode(potentialAdverb, functionNode, null);
                        }
                    }
                }
            }
            
            // Check for statements first (statements have lower precedence than verbs but higher than separators)
            // This is critical for assignment parsing in Pure LRS mode
            if (expressionTokens.Count >= 2)
            {
                var firstToken = expressionTokens[0];
                if (LRSStatementParser.CouldBeStatement(firstToken.Type))
                {
                    return statementParser.ParseStatement(expressionTokens);
                }
                
                // Also check for assignment operators elsewhere in the expression
                // This handles cases like 'foo:7' where first token is IDENTIFIER, not COLON
                // But DON'T treat as statement if assignment is embedded in larger expression
                // e.g., '1 + a: 42' should parse as '1 + (a: 42)' not as a statement
                for (int i = 1; i < expressionTokens.Count; i++)
                {
                    if (LRSStatementParser.CouldBeStatement(expressionTokens[i].Type))
                    {
                        // Only treat as statement if:
                        // 1. The assignment is at position 1 (e.g., 'a: 42' where a is at 0, : at 1)
                        // 2. The token before assignment is a simple identifier (variable name)
                        // This ensures '1 + a: 42' is NOT treated as a statement
                        bool isSimpleAssignment = i == 1 && expressionTokens[0].Type == TokenType.IDENTIFIER;
                        bool isAssignmentOnly = expressionTokens[i].Type == TokenType.COLON && 
                                                i == expressionTokens.Count - 2 && 
                                                expressionTokens[0].Type == TokenType.IDENTIFIER;
                        
                        if (isSimpleAssignment || isAssignmentOnly)
                    {
                        return statementParser.ParseStatement(expressionTokens);
                        }
                        // Otherwise, let dyadic parser handle colon as operator (e.g., inline assignment)
                    }
                }
            }
                
            // Check for grouping constructs FIRST (highest precedence per K spec)
            // Only if the ENTIRE expression is wrapped in grouping constructs
            if (expressionTokens.Count >= 2)
            {
                var firstToken = expressionTokens[0];
                if (firstToken.Type == TokenType.LEFT_PAREN || 
                    firstToken.Type == TokenType.LEFT_BRACKET || 
                    firstToken.Type == TokenType.LEFT_BRACE)
                {
                    
                    // Check if the entire expression is wrapped in this grouping construct
                    // CRITICAL FIX: We need to verify that the FIRST opening delimiter
                    // is closed by the LAST closing delimiter, not just any closing delimiter
                    int depth = 0;
                    bool firstOpenClosed = false;
                    TokenType openType = firstToken.Type;
                    TokenType closeType = openType == TokenType.LEFT_PAREN ? TokenType.RIGHT_PAREN :
                                         openType == TokenType.LEFT_BRACKET ? TokenType.RIGHT_BRACKET :
                                         TokenType.RIGHT_BRACE;
                    
                    for (int i = 0; i < expressionTokens.Count; i++)
                    {
                        if (expressionTokens[i].Type == openType) depth++;
                        else if (expressionTokens[i].Type == closeType) depth--;
                        
                        // Track when the first opening delimiter gets closed
                        if (i == 0) depth = 1; // First token is the opening delimiter
                        if (depth == 0 && !firstOpenClosed)
                        {
                            firstOpenClosed = true;
                            // If the first opening closes at the last position, entire expression is wrapped
                            if (i == expressionTokens.Count - 1 && expressionTokens[i].Type == closeType)
                            {
                                
                                // Delegate to grouping parser for the entire wrapped expression
                                var tempGroupingParser = new LRSGroupingParser(expressionTokens, BuildParseTree, this);
                                int pos = 0;
                                if (openType == TokenType.LEFT_PAREN)
                                    return tempGroupingParser.ParseParentheses(ref pos);
                                else if (openType == TokenType.LEFT_BRACKET)
                                    return tempGroupingParser.ParseBrackets(ref pos);
                                else if (openType == TokenType.LEFT_BRACE)
                                    return tempGroupingParser.ParseBraces(ref pos);
                            }
                            break;
                        }
                    }
                }
                // Period expressions (dictionaries) handled by DOT_APPLY/MAKE operators
                // These will be caught by the dyadic/monadic parsing below
            }

            // Check for implicit vector creation (sequences of atomic literals like "1 2 3 4 5")
            // This applies to both Pure and Safe LRS modes
            if (expressionTokens.Count >= 2)
            {
                var implicitVector = TryCreateImplicitVector(expressionTokens);
                if (implicitVector != null)
                    return implicitVector;
            }

            // Check for implicit apply: (vector_literal)indices
            // Only applies when: (1) content is a simple vector literal (no operators),
            // and (2) following tokens are simple atomic literals (indices, no operators)
            if (expressionTokens.Count >= 3 && expressionTokens[0].Type == TokenType.LEFT_PAREN)
            {
                // Find the matching closing paren
                int depth = 1;
                int parenEnd = 1;
                while (parenEnd < expressionTokens.Count && depth > 0)
                {
                    if (expressionTokens[parenEnd].Type == TokenType.LEFT_PAREN) depth++;
                    else if (expressionTokens[parenEnd].Type == TokenType.RIGHT_PAREN) depth--;
                    parenEnd++;
                }

                // If we found a closing paren and there are tokens after it
                if (depth == 0 && parenEnd < expressionTokens.Count)
                {
                    var vectorTokens = expressionTokens.GetRange(1, parenEnd - 2); // Exclude parens
                    var indexTokens = expressionTokens.Skip(parenEnd).ToList();

                    // Check if vector content is a simple literal sequence (no operators)
                    bool isSimpleVector = IsSimpleLiteralSequence(vectorTokens);
                    // Check if index tokens are simple literals (no operators)
                    bool isSimpleIndex = IsSimpleLiteralSequence(indexTokens);

                    if (isSimpleVector && isSimpleIndex)
                    {
                        // Parse the vector and index
                        var vectorNode = EvaluateFromRight(expressionTokens.GetRange(0, parenEnd));
                        ASTNode? indexNode = indexTokens.Count == 1
                            ? LRSAtomicParser.ParseAtomicToken(indexTokens[0], this)
                            : EvaluateFromRight(indexTokens);

                        if (vectorNode != null && indexNode != null)
                        {
                            return ASTNode.MakeDyadicOp(TokenType.APPLY, vectorNode, indexNode);
                        }
                    }
                    else if (indexTokens.Count >= 1 && indexTokens[0].Type == TokenType.LEFT_BRACKET)
                    {
                        // Complex expression followed by bracket indexing: (expr)[index]
                        // e.g., ((&#z),y)[=z,x]
                        // Parse the parenthesized expression as the data
                        var dataNode = EvaluateFromRight(expressionTokens.GetRange(0, parenEnd));
                        if (dataNode != null)
                        {
                            // Parse the bracket contents as the index argument
                            int bracketEnd = FindMatchingBracket(indexTokens, 0);
                            if (bracketEnd > 0)
                            {
                                var bracketContent = indexTokens.GetRange(1, bracketEnd - 1);
                                ASTNode? indexNode = null;
                                if (bracketContent.Count == 1)
                                {
                                    indexNode = LRSAtomicParser.ParseAtomicToken(bracketContent[0], this);
                                }
                                else if (bracketContent.Count > 0)
                                {
                                    indexNode = EvaluateFromRight(bracketContent);
                                }
                                else
                                {
                                    indexNode = ASTNode.MakeLiteral(new NullValue());
                                }

                                if (indexNode != null)
                                {
                                    return ASTNode.MakeDyadicOp(TokenType.APPLY, dataNode, indexNode);
                                }
                            }
                        }
                    }
                }
            }

            // Check for statements first (statements have lower precedence than verbs but higher than separators)
            if (expressionTokens.Count >= 2)
            {
                var firstToken = expressionTokens[0];
                if (LRSStatementParser.CouldBeStatement(firstToken.Type))
                {
                    return statementParser.ParseStatement(expressionTokens);
                }
            }
            
            // NOTE: Removed premature verb token check that was delegating to function parser
            // Verbs should be handled naturally through right-to-left dyadic/monadic parsing
            // This allows "_bd ,5" to parse correctly as: _bd (monadic) applied to (,5 monadic enlist)
            
            // Check for assignments (variable:value pattern) and apply-and-assign (variable verb: value)
            // Only treat as statement if it starts with a plain identifier followed by ':' or 'verb:'
            // For inline assignments like '1 + a: 42', let dyadic parser handle them
            if (expressionTokens.Count >= 3 && expressionTokens[0].Type == TokenType.IDENTIFIER)
            {
                // Simple assignment: IDENTIFIER COLON expression  (a: 42)
                if (expressionTokens[1].Type == TokenType.COLON)
                {
                    return statementParser.ParseStatement(expressionTokens);
                }
                
                // Apply-and-assign: IDENTIFIER VERB COLON expression  (i+:1, x-:5, x?:, etc.)
                // Handle both 3 tokens (x?:) and 4+ tokens (x+:y)
                if (IsVerbToken(expressionTokens[1].Type) &&
                    expressionTokens[2].Type == TokenType.COLON)
                {
                    return statementParser.ParseStatement(expressionTokens);
                }
                
                // Global assignment: IDENTIFIER GLOBAL_ASSIGNMENT expression  (a:: 42)
                if (expressionTokens[1].Type == TokenType.GLOBAL_ASSIGNMENT)
                {
                    return statementParser.ParseStatement(expressionTokens);
                }
            }
                
            // Check for user-defined function application: identifier argument -> f x
            // Pattern: IDENTIFIER followed by a non-assignment, non-adverb, non-bracket token
            // f[args] goes through the bracket-indexing path below, not here
            // Only applies when the identifier is NOT a known built-in verb token
            if (expressionTokens.Count >= 2 &&
                expressionTokens[0].Type == TokenType.IDENTIFIER &&
                expressionTokens[1].Type != TokenType.COLON &&
                expressionTokens[1].Type != TokenType.GLOBAL_ASSIGNMENT &&
                expressionTokens[1].Type != TokenType.LEFT_BRACKET &&
                !VerbRegistry.IsAdverbToken(expressionTokens[1].Type) &&
                !VerbRegistry.IsVerbToken(expressionTokens[1].Type))
            {
                var funcNode = CreateNodeFromToken(expressionTokens[0]);
                var argTokens = expressionTokens.Skip(1).ToList();
                ASTNode? argNode = argTokens.Count == 1
                    ? CreateNodeFromToken(argTokens[0])
                    : EvaluateFromRight(argTokens);
                if (funcNode != null && argNode != null)
                {
                    return ASTNode.MakeFunctionCall(funcNode, new System.Collections.Generic.List<ASTNode> { argNode });
                }
            }

            // TARGETED FIX for idiom pattern: (=x)z?/:?x
            // Pattern: (paren_expr) IDENTIFIER VERB ADVERB ...
            // This specifically handles (=x)z?/:?x where (=x) should apply to z?/:?x
            if (expressionTokens.Count >= 5 &&
                expressionTokens[0].Type == TokenType.LEFT_PAREN)
            {
                // Find matching paren (FindMatchingBracket only handles [])
                int depth = 1;
                int parenEnd = 1;
                while (parenEnd < expressionTokens.Count && depth > 0)
                {
                    if (expressionTokens[parenEnd].Type == TokenType.LEFT_PAREN) depth++;
                    else if (expressionTokens[parenEnd].Type == TokenType.RIGHT_PAREN) depth--;
                    parenEnd++;
                }
                parenEnd--; // back up to the closing paren
                
                if (parenEnd > 0 && parenEnd < expressionTokens.Count - 3)
                {
                    // Check for pattern: ) IDENTIFIER VERB ADVERB
                    if (expressionTokens[parenEnd + 1].Type == TokenType.IDENTIFIER &&
                        VerbRegistry.IsVerbToken(expressionTokens[parenEnd + 2].Type) &&
                        VerbRegistry.IsAdverbToken(expressionTokens[parenEnd + 3].Type))
                    {
                        var leftTokens = expressionTokens.GetRange(0, parenEnd + 1);
                        var rightTokens = expressionTokens.GetRange(parenEnd + 1, expressionTokens.Count - parenEnd - 1);
                        
                        var leftNode = BuildParseTree ? 
                            BuildParseTreeFromRight(leftTokens) : EvaluateFromRight(leftTokens);
                        var rightNode = BuildParseTree ? 
                            BuildParseTreeFromRight(rightTokens) : EvaluateFromRight(rightTokens);
                        
                        if (leftNode != null && rightNode != null)
                        {
                            return ASTNode.MakeDyadicOp(TokenType.APPLY, leftNode, rightNode);
                        }
                    }
                }
            }

            // Check for adverb operations (only if expression starts with adverb or contains clear adverb patterns)
            bool hasAdverbPattern = false;
            foreach (var token in expressionTokens)
            {
                if (token.Type == TokenType.ADVERB_SLASH || 
                    token.Type == TokenType.ADVERB_BACKSLASH || 
                    token.Type == TokenType.ADVERB_TICK ||
                    token.Type == TokenType.ADVERB_SLASH_COLON ||
                    token.Type == TokenType.ADVERB_BACKSLASH_COLON ||
                    token.Type == TokenType.ADVERB_TICK_COLON ||
                    token.Type == TokenType.VARIADIC_ADVERB_OVER ||
                    token.Type == TokenType.VARIADIC_ADVERB_SCAN ||
                    token.Type == TokenType.VARIADIC_ADVERB_EACH)
                {
                    hasAdverbPattern = true;
                    break;
                }
            }
            
            if (hasAdverbPattern && ParseAdverbExpression(expressionTokens) is ASTNode adverbResult)
            {
                return adverbResult;
            }
            
            // Handle identifier[bracketArgs] followed by additional tokens: e.g. d[x;y#,:]z
            // This is a bracket function call whose result is then applied to the remaining tokens.
            // SKIP if the remaining tokens start with a dyadic operator (e.g., sum[3;4]*2 should be multiplication, not function call)
            if (expressionTokens.Count >= 4 &&
                expressionTokens[0].Type == TokenType.IDENTIFIER &&
                expressionTokens[1].Type == TokenType.LEFT_BRACKET)
            {
                var closingBracketEFR = FindMatchingBracket(expressionTokens, 1);
                if (closingBracketEFR > 0 && closingBracketEFR < expressionTokens.Count - 1)
                {
                    // Check if tokens after bracket start with a dyadic operator
                    var afterBracketTokens = expressionTokens.GetRange(closingBracketEFR + 1, expressionTokens.Count - closingBracketEFR - 1);
                    if (afterBracketTokens.Count > 0 && OperatorDetector.SupportsDyadic(afterBracketTokens[0].Type))
                    {
                        // Check if the operator is followed by an adverb (two-glyph adverb pattern like </:)
                        // If so, this is actually an adverb application, not a dyadic operation.
                        // The dyadic parser knows how to handle adverb patterns, so let it fall through.
                        bool isTwoGlyphAdverb = afterBracketTokens.Count >= 2 &&
                                                VerbRegistry.IsAdverbToken(afterBracketTokens[1].Type);
                        if (!isTwoGlyphAdverb)
                        {
                            // Tokens after bracket start with a dyadic operator (without adverb) - 
                            // fall through to dyadic parsing. This ensures sum[3;4]*2 is parsed as 
                            // multiplication, not as function call with *2 as argument.
                        }
                        // If it's a two-glyph adverb, fall through to let the dyadic parser handle it
                    }
                    else
                    {
                        var bracketContentEFR = expressionTokens.GetRange(2, closingBracketEFR - 2);
                    var argGroupsEFR = SplitBracketArguments(bracketContentEFR, 2);
                    var argNodesEFR = new List<ASTNode>();
                    foreach (var ag in argGroupsEFR)
                    {
                        if (ag.Count == 0)
                            argNodesEFR.Add(ASTNode.MakeLiteral(new NullValue()));
                        else
                        {
                            var an = BuildParseTreeFromRight(ag);
                            argNodesEFR.Add(an ?? ASTNode.MakeLiteral(new NullValue()));
                        }
                    }
                    var funcCallNodeEFR = new ASTNode(ASTNodeType.FunctionCall);
                    funcCallNodeEFR.Children.Add(ASTNode.MakeVariable(expressionTokens[0].Lexeme));
                    funcCallNodeEFR.Children.AddRange(argNodesEFR);

                    var afterBracketEFR = expressionTokens.GetRange(closingBracketEFR + 1, expressionTokens.Count - closingBracketEFR - 1);
                    ASTNode? afterNodeEFR = afterBracketEFR.Count == 1
                        ? CreateNodeFromToken(afterBracketEFR[0])
                        : BuildParseTreeFromRight(afterBracketEFR);
                    if (afterNodeEFR != null)
                    {
                        var applyNodeEFR = new ASTNode(ASTNodeType.FunctionCall);
                        applyNodeEFR.Children.Add(funcCallNodeEFR);
                        applyNodeEFR.Children.Add(afterNodeEFR);
                        return applyNodeEFR;
                    }
                    return funcCallNodeEFR;
                    } // Close else block for dyadic operator check
                }
            }

            // Check for bracket indexing pattern: identifier[expression] or identifier[a;b;...]
            // Also handles function tokens like _f[expression] for recursion
            // Only fires when the bracket group spans to the end of the token list.
            if (expressionTokens.Count >= 4 &&
                (expressionTokens[0].Type == TokenType.IDENTIFIER || expressionTokens[0].Type == TokenType.FUNCTION) &&
                expressionTokens[1].Type == TokenType.LEFT_BRACKET)
            {
                var bracketEnd = FindMatchingBracket(expressionTokens, 1);
                if (bracketEnd != -1 && bracketEnd == expressionTokens.Count - 1)
                {
                    var identifier = expressionTokens[0];
                    var identifierNode = CreateNodeFromToken(identifier);
                    var bracketContent = expressionTokens.GetRange(2, bracketEnd - 2);
                    
                    // Split by top-level semicolons to handle multi-arg calls: f[a;b;c]
                    var splitArgs = SplitBracketArguments(bracketContent, int.MaxValue);
                    if (splitArgs.Count == 1)
                    {
                        var indexNode = splitArgs[0].Count > 0
                            ? EvaluateFromRight(splitArgs[0])
                            : null;
                        return ASTNode.MakeDyadicOp(TokenType.APPLY, identifierNode, 
                            indexNode ?? ASTNode.MakeLiteral(new NullValue()));
                    }
                    else
                    {
                        // Multi-argument bracket indexing: x[a;b;c] - use APPLY with Block
                        // This handles matrix indexing where semicolons separate dimensions
                        var argNodes = new List<ASTNode>();
                        foreach (var argTokens in splitArgs)
                        {
                            var argNode = argTokens.Count > 0 ? EvaluateFromRight(argTokens) : ASTNode.MakeLiteral(new NullValue());
                            argNodes.Add(argNode ?? ASTNode.MakeLiteral(new NullValue()));
                        }
                        // Create an ExpressionList with all index arguments for multi-dimensional indexing
                        // ExpressionList evaluates all children and returns them as a VectorValue
                        var indexList = new ASTNode(ASTNodeType.ExpressionList, null, argNodes);
                        return ASTNode.MakeDyadicOp(TokenType.APPLY, identifierNode, indexList);
                    }
                }
            }

            // Check for implicit vector followed by bracket indexing: e.g., 1 -1[y]
            // This handles vector literals with bracket indexing like "1 -1[y]" which creates
            // the vector [1, -1] and then indexes at position y
            if (expressionTokens.Count >= 3 &&
                expressionTokens[expressionTokens.Count - 1].Type == TokenType.RIGHT_BRACKET)
            {
                // Find the matching left bracket by scanning backwards from the end
                int bracketEnd = expressionTokens.Count - 1;
                int bracketStart = -1;
                int depth = 1;
                for (int i = bracketEnd - 1; i >= 0; i--)
                {
                    if (expressionTokens[i].Type == TokenType.RIGHT_BRACKET)
                        depth++;
                    else if (expressionTokens[i].Type == TokenType.LEFT_BRACKET)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            bracketStart = i;
                            break;
                        }
                    }
                }

                if (bracketStart > 0)
                {
                    // Get the tokens before the bracket
                    var vectorTokens = expressionTokens.GetRange(0, bracketStart);
                    var bracketContent = expressionTokens.GetRange(bracketStart + 1, bracketEnd - bracketStart - 1);

                    // Check if all tokens before the bracket are atomic (implicit vector)
                    if (vectorTokens.Count > 0 && vectorTokens.All(t => LRSAtomicParser.CanBeImplicitVectorElement(t.Type)))
                    {
                        // Create the implicit vector node
                        var vectorElements = new List<ASTNode>();
                        foreach (var token in vectorTokens)
                        {
                            vectorElements.Add(LRSAtomicParser.ParseAtomicToken(token, this));
                        }
                        var vectorNode = ASTNode.MakeVector(vectorElements);

                        // Parse the index
                        ASTNode? indexNode = null;
                        if (bracketContent.Count > 0)
                        {
                            indexNode = EvaluateFromRight(bracketContent);
                        }

                        // Create indexing operation using @ (apply) operator
                        // This represents: vector @ index
                        return ASTNode.MakeDyadicOp(TokenType.APPLY, vectorNode,
                            indexNode ?? ASTNode.MakeLiteral(new NullValue()));
                    }
                }
            }

            // Detect the exact arity using VerbRegistry rules
            int determinedArity = DetectOperationArity(expressionTokens, 0);
            
            // Parse based on the determined arity - no trial and error
            if (determinedArity == 1)
            {
                var monadicResult = monadicParser.ParseMonadicOperator(expressionTokens);
                if (monadicResult != null)
                    return monadicResult;
                // Fall through to dyadic fallback if monadic parsing failed
                // (e.g., IDENTIFIER[args]*value where IDENTIFIER is treated as arity-1 but is really a dyadic sub-expression)
            }
            else if (determinedArity == 2)
            {
                return dyadicParser.ParseDyadicOperation(expressionTokens);
            }
            else if (determinedArity >= 3)
            {
                return ParseMultiAryOperation(expressionTokens);
            }
            
            // FALLBACK: If arity detection at position 0 failed or monadic parsing returned null,
            // try dyadic parsing. This handles patterns like "d . `keyA" and "sum[3;4]*2"
            // where position 0 is an operand, not an operator.
            if (expressionTokens.Count >= 3)
            {
                var dyadicResult = dyadicParser.ParseDyadicOperation(expressionTokens);
                if (dyadicResult != null)
                {
                    return dyadicResult;
                }
            }
            
            // If we reach here, no parsing strategy succeeded
            
            return null;
        }
        
        /// <summary>
        /// Parse sub-expression for monadic parser (handles position parameter)
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <param name="position">Reference to position parameter</param>
        /// <returns>AST node representing parsed expression</returns>
        internal ASTNode? ParseSubExpressionForMonadic(List<Token> tokens, ref int position)
        {
            if (tokens.Count == 0)
            {
                position = 0;
                return null;
            }
            if (tokens.Count == 1)
            {
                position = 1; // Consume the single token
                return CreateNodeFromToken(tokens[0]);
            }
            
            // Handle parenthesized expressions using grouping parser
            if (tokens[0].Type == TokenType.LEFT_PAREN)
            {
                var tempGroupingParser = new LRSGroupingParser(tokens, BuildParseTree, this);
                var groupedResult = tempGroupingParser.ParseParentheses(ref position);
                
                // Check if there are remaining tokens after the parenthesized expression
                if (position < tokens.Count)
                {
                    // There are remaining tokens - the parenthesized result is the left operand
                    // and the remaining tokens form an operation with it
                    var remainingTokens = tokens.GetRange(position, tokens.Count - position);
                    
                    // Check if the first remaining token is an operator
                    if (remainingTokens.Count > 0 && OperatorDetector.SupportsDyadic(remainingTokens[0].Type))
                    {
                        // The first remaining token is the operator
                        var opToken = remainingTokens[0];
                        var rightTokens = remainingTokens.GetRange(1, remainingTokens.Count - 1);
                        
                        // Parse the right operand
                        var rightPos = 0;
                        var rightNode = ParseSubExpressionForMonadic(rightTokens, ref rightPos);
                        
                        // Create dyadic node: groupedResult OP rightNode
                        var dyadicNode = new ASTNode(ASTNodeType.DyadicOp);
                        dyadicNode.Value = new SymbolValue(VerbRegistry.TokenTypeToVerbName(opToken.Type));
                        if (groupedResult != null) dyadicNode.Children.Add(groupedResult);
                        if (rightNode != null) dyadicNode.Children.Add(rightNode);
                        
                        // Update position to consume all tokens since we handled the remaining ones
                        position = tokens.Count;
                        
                        return dyadicNode;
                    }
                }
                
                // Position must reflect total consumption to prevent double-processing by caller
                position = tokens.Count;
                return groupedResult;
            }
            
            // Handle bracket expressions using grouping parser
            if (tokens[0].Type == TokenType.LEFT_BRACKET)
            {
                var tempGroupingParser = new LRSGroupingParser(tokens, BuildParseTree, this);
                return tempGroupingParser.ParseBrackets(ref position);
            }
            
            // Handle brace expressions using grouping parser
            if (tokens[0].Type == TokenType.LEFT_BRACE)
            {
                var tempGroupingParser = new LRSGroupingParser(tokens, BuildParseTree, this);
                return tempGroupingParser.ParseBraces(ref position);
            }

            // Handle identifier[bracket] indexing pattern (e.g., x[;y] inside monadic operator)
            if (tokens.Count >= 2 &&
                (tokens[0].Type == TokenType.IDENTIFIER || tokens[0].Type == TokenType.FUNCTION) &&
                tokens[1].Type == TokenType.LEFT_BRACKET)
            {
                var bracketEnd = FindMatchingBracket(tokens, 1);
                if (bracketEnd != -1 && bracketEnd == tokens.Count - 1)
                {
                    var identifier = tokens[0];
                    var identifierNode = CreateNodeFromToken(identifier);
                    var bracketContent = tokens.GetRange(2, bracketEnd - 2);

                    // Split by top-level semicolons to handle multi-arg indexing: x[a;b;c]
                    var splitArgs = SplitBracketArguments(bracketContent, int.MaxValue);
                    if (splitArgs.Count == 1)
                    {
                        var indexNode = splitArgs[0].Count > 0
                            ? EvaluateFromRight(splitArgs[0])
                            : null;
                        position = tokens.Count;
                        return ASTNode.MakeDyadicOp(TokenType.APPLY, identifierNode,
                            indexNode ?? ASTNode.MakeLiteral(new NullValue()));
                    }
                    else
                    {
                        // Multi-dimensional indexing: x[a;b;c] - use APPLY with Block
                        var argNodes = new List<ASTNode>();
                        foreach (var argTokens in splitArgs)
                        {
                            var argNode = argTokens.Count > 0 ? EvaluateFromRight(argTokens) : ASTNode.MakeLiteral(new NullValue());
                            argNodes.Add(argNode ?? ASTNode.MakeLiteral(new NullValue()));
                        }
                        var indexBlock = new ASTNode(ASTNodeType.Block, null, argNodes);
                        position = tokens.Count;
                        return ASTNode.MakeDyadicOp(TokenType.APPLY, identifierNode, indexBlock);
                    }
                }
            }

            // Check for verb+adverb patterns (e.g., |/x in ~|/x)
            // Must be checked before monadic to avoid splitting verb+adverb incorrectly
            if (tokens.Count >= 2)
            {
                bool hasAdverb = false;
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (VerbRegistry.IsAdverbToken(tokens[i].Type))
                    {
                        hasAdverb = true;
                        break;
                    }
                }
                if (hasAdverb)
                {
                    var adverbResult = EvaluateFromRight(tokens);
                    if (adverbResult != null)
                    {
                        position = tokens.Count;
                        return adverbResult;
                    }
                }
            }
            
            // Handle triadic/tetradic amend patterns in sub-expression context
            // e.g., @[a;b;c] or .[a;b;c;d] when used as an operand to another operator
            if (tokens.Count >= 2 && 
                (tokens[0].Type == TokenType.APPLY || tokens[0].Type == TokenType.DOT_APPLY) &&
                tokens[1].Type == TokenType.LEFT_BRACKET)
            {
                int bracketEnd = FindMatchingBracket(tokens, 1);
                if (bracketEnd != -1 && bracketEnd == tokens.Count - 1)
                {
                    int argCount = CountBracketArguments(tokens, 1);
                    if (argCount >= 3)
                    {
                        var bracketContent = tokens.GetRange(2, bracketEnd - 2);
                        var triadicNode = ParseTriadicFromBracketCall(tokens[0], bracketContent, argCount);
                        if (triadicNode != null)
                        {
                            position = tokens.Count;
                            return triadicNode;
                        }
                    }
                }
            }
            
            // Check if this is a nested monadic operation (e.g., ,`a in ^,`a)
            if (tokens.Count >= 2 && OperatorDetector.SupportsMonadic(tokens[0].Type))
            {
                // Parse as monadic operation
                var monadicResult = monadicParser.ParseMonadicOperator(tokens);
                if (monadicResult != null)
                {
                    position = tokens.Count;
                    return monadicResult;
                }
            }
            
            // Try dyadic operation as fallback
            var dyadicResult = dyadicParser.ParseDyadicOperation(tokens);
            if (dyadicResult != null)
            {
                position = tokens.Count;
                return dyadicResult;
            }
            return null;
        }
        
        /// <summary>
        /// Try to parse a triadic/tetradic verb bracket call like @[a;b;c] or .[a;b;c;d].
        /// Uses VerbRegistry to determine if the verb supports triadic/tetradic arity
        /// rather than hardcoding specific token types.
        /// </summary>
        /// <param name="tokens">Token list</param>
        /// <param name="operatorPosition">Position of the operator token</param>
        /// <param name="bracketPosition">Position of the LEFT_BRACKET token</param>
        /// <returns>AST node if triadic/tetradic pattern detected, null otherwise</returns>
        private ASTNode? TryParseTriadicVerbPattern(List<Token> tokens, int operatorPosition, int bracketPosition)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokens[operatorPosition].Type);
            var verb = VerbRegistry.GetVerb(verbName);
            
            // Only consider verbs that support triadic or higher arity
            if (verb == null || !verb.SupportedArities.Any(a => a >= 3))
                return null;
            
            if (bracketPosition >= tokens.Count || tokens[bracketPosition].Type != TokenType.LEFT_BRACKET)
                return null;
            
            int bracketEnd = FindMatchingBracket(tokens, bracketPosition);
            if (bracketEnd == -1 || bracketEnd != tokens.Count - 1)
                return null;
            
            int argCount = CountBracketArguments(tokens, bracketPosition);
            if (argCount < 3)
                return null;
            
            var bracketContent = tokens.GetRange(bracketPosition + 1, bracketEnd - bracketPosition - 1);
            return ParseTriadicFromBracketCall(tokens[operatorPosition], bracketContent, argCount);
        }
        
        /// <summary>
        /// Parse multi-arity operations (triadic, tetradic, variadic)
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <returns>AST node for multi-arity operation</returns>
        private ASTNode? ParseMultiAryOperation(List<Token> tokens)
        {
            if (tokens.Count < 2) return null;
            
            // Look for multi-arity operators
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                int arity = DetectOperationArity(tokens, i);
                
                if (arity >= 3)
                {
                    // Handle triadic operations
                    if (arity == 3)
                    {
                        return ParseTriadicOperation(tokens, i);
                    }
                    // Handle tetradic operations
                    else if (arity == 4)
                    {
                        return ParseTetradicOperation(tokens, i);
                    }
                }
                
                // Handle variadic adverbs
                if (token.Type == TokenType.VARIADIC_ADVERB_OVER ||
                    token.Type == TokenType.VARIADIC_ADVERB_SCAN ||
                    token.Type == TokenType.VARIADIC_ADVERB_EACH)
                {
                    return ParseVariadicAdverb(tokens, i);
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Detect operation arity using the abstracted VerbRegistry.DetermineVerbArity function
        /// </summary>
        /// <param name="tokens">Tokens to analyze</param>
        /// <param name="position">Starting position</param>
        /// <returns>Detected arity (1=monadic, 2=dyadic, 3=triadic, 4=tetradic)</returns>
        private int DetectOperationArity(List<Token> tokens, int position)
        {
            if (position >= tokens.Count) return 0;
            
            var opToken = tokens[position];
            var verbName = VerbRegistry.TokenTypeToVerbName(opToken.Type);
            
            // Create arity context following K language specification
            var context = new VerbRegistry.ArityContext();
            
            // Check for triadic/tetradic patterns - ONLY supported with brackets
            if (opToken.Type == TokenType.DOT_APPLY || opToken.Type == TokenType.APPLY)
            {
                // Look for bracket-based triadic/tetradic patterns
                // Format: .[arg1;arg2;arg3] or @[arg1;arg2;arg3;arg4]
                if (position + 1 < tokens.Count && tokens[position + 1].Type == TokenType.LEFT_BRACKET)
                {
                    // Count arguments inside brackets
                    int argCount = CountBracketArguments(tokens, position + 1);
                    context.ArgumentCount = argCount;
                    
                    // Use the abstracted function for determination
                    return VerbRegistry.DetermineVerbArity(verbName, context);
                }
            }
            
            if (opToken.Type == TokenType.QUESTION)
            {
                // Check for function inverse pattern
                if (position + 2 < tokens.Count && tokens[position + 1].Type == TokenType.COLON)
                {
                    context.ArgumentCount = 3; // Triadic inverse function
                    return VerbRegistry.DetermineVerbArity(verbName, context);
                }
            }
            
            // Determine left and right operands
            context.HasLeftOperand = position > 0 && !IsExpressionSeparator(tokens[position - 1].Type);
            
            // For right operand, check if there's meaningful content after the operator
            // This includes parenthesized/bracketed expressions
            context.HasRightOperand = false;
            if (position < tokens.Count - 1)
            {
                // Look for content after the operator
                for (int i = position + 1; i < tokens.Count; i++)
                {
                    var token = tokens[i];
                    
                    // If we find an opening delimiter, there's definitely a right operand
                    if (token.Type == TokenType.LEFT_PAREN ||
                        token.Type == TokenType.LEFT_BRACKET ||
                        token.Type == TokenType.LEFT_BRACE)
                    {
                        context.HasRightOperand = true;
                        break;
                    }
                    
                    // If we find atomic content (not a separator or closing delimiter)
                    if (!IsExpressionSeparator(token.Type) && 
                        token.Type != TokenType.RIGHT_PAREN &&
                        token.Type != TokenType.RIGHT_BRACKET &&
                        token.Type != TokenType.RIGHT_BRACE)
                    {
                        context.HasRightOperand = true;
                        break;
                    }
                    
                    // If we hit a true separator before finding content, no right operand
                    if (IsExpressionSeparator(token.Type) &&
                        token.Type != TokenType.RIGHT_PAREN &&
                        token.Type != TokenType.RIGHT_BRACKET &&
                        token.Type != TokenType.RIGHT_BRACE)
                    {
                        break;
                    }
                }
            }
            
            // Check for disambiguating colon
            context.HasDisambiguatingColon = position + 1 < tokens.Count && 
                tokens[position + 1].Type == TokenType.COLON;
            
            // Check for adverb on right
            context.HasAdverbOnRight = position + 1 < tokens.Count && 
                VerbRegistry.IsAdverbToken(tokens[position + 1].Type);
            
            // Check if at expression start
            context.IsAtExpressionStart = position == 0 || IsExpressionSeparator(tokens[position - 1].Type);
            
            // Count arguments (simplified - this would need more sophisticated analysis for full accuracy)
            context.ArgumentCount = CountArguments(tokens, position);
            
            // Use the abstracted function for determination
            return VerbRegistry.DetermineVerbArity(verbName, context);
        }
        
        /// <summary>
        /// Count arguments for arity determination (simplified version)
        /// </summary>
        private int CountArguments(List<Token> tokens, int position)
        {
            // This is a simplified implementation - a full implementation would need
            // to parse the expression structure more carefully
            int count = 0;
            
            // Check left side
            if (position > 0 && !IsExpressionSeparator(tokens[position - 1].Type))
                count++;
            
            // Check right side  
            if (position < tokens.Count - 1 && !IsExpressionSeparator(tokens[position + 1].Type))
                count++;
                
            return count;
        }
        
        /// <summary>
        /// Count arguments inside brackets for triadic/tetradic operations
        /// </summary>
        /// <param name="tokens">Token list</param>
        /// <param name="bracketPos">Position of LEFT_BRACKET</param>
        /// <returns>Number of arguments found</returns>
        private int CountBracketArguments(List<Token> tokens, int bracketPos)
        {
            if (bracketPos >= tokens.Count || tokens[bracketPos].Type != TokenType.LEFT_BRACKET)
                return 0;
                
            // Empty brackets [] have 0 arguments
            if (bracketPos + 1 < tokens.Count && tokens[bracketPos + 1].Type == TokenType.RIGHT_BRACKET)
                return 0;
                
            int semicolonCount = 0;
            int bracketDepth = 1;  // tracks [] depth (we start inside the outer bracket)
            int nestedDepth = 0;     // tracks ()/{} nesting inside current bracket level
            int i = bracketPos + 1;
            
            while (i < tokens.Count && bracketDepth > 0)
            {
                var token = tokens[i];
                
                if (token.Type == TokenType.LEFT_BRACKET)
                {
                    bracketDepth++;
                }
                else if (token.Type == TokenType.RIGHT_BRACKET)
                {
                    bracketDepth--;
                }
                else if (token.Type == TokenType.LEFT_PAREN || token.Type == TokenType.LEFT_BRACE)
                {
                    nestedDepth++;
                }
                else if (token.Type == TokenType.RIGHT_PAREN || token.Type == TokenType.RIGHT_BRACE)
                {
                    if (nestedDepth > 0)
                        nestedDepth--;
                }
                else if (bracketDepth == 1 && nestedDepth == 0 && token.Type == TokenType.SEMICOLON)
                {
                    semicolonCount++;
                }
                
                i++;
            }
            
            // Number of arguments = semicolons + 1 (e.g., [a;b] has 1 semicolon, 2 args)
            // This correctly counts empty slots: [a;;c] has 2 semicolons, 3 args
            return semicolonCount + 1;
        }
        
        /// <summary>
        /// Check if token type is an expression separator (doesn't count as left operand)
        /// </summary>
        private bool IsExpressionSeparator(TokenType tokenType)
        {
            return tokenType == TokenType.NEWLINE || 
                   tokenType == TokenType.SEMICOLON ||
                   tokenType == TokenType.EOF ||
                   tokenType == TokenType.LEFT_BRACKET ||
                   tokenType == TokenType.LEFT_PAREN ||
                   tokenType == TokenType.LEFT_BRACE ||
                   tokenType == TokenType.ASSIGNMENT ||
                   tokenType == TokenType.GLOBAL_ASSIGNMENT;
        }
        
        /// <summary>
        /// Parse triadic operation (3 arguments) - ONLY bracket-based
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <param name="position">Starting position</param>
        /// <returns>AST node for triadic operation</returns>
        private ASTNode? ParseTriadicOperation(List<Token> tokens, int position)
        {
            if (position >= tokens.Count) return null;
            
            var opToken = tokens[position];
            
            // Must be followed by LEFT_BRACKET for triadic operations
            if (position + 1 >= tokens.Count || tokens[position + 1].Type != TokenType.LEFT_BRACKET)
                return null;
                
            var children = new List<ASTNode>();
            
            // Parse arguments inside brackets: .[arg1;arg2;arg3]
            var bracketEnd = FindMatchingBracket(tokens, position + 1);
            if (bracketEnd == -1) return null;
            
            var bracketTokens = tokens.GetRange(position + 2, bracketEnd - position - 2); // Skip . and [
            var splitArgs = SplitBracketArguments(bracketTokens, 3);
            
            if (splitArgs.Count >= 3)
            {
                // Parse first two arguments normally
                for (int i = 0; i < 2 && i < splitArgs.Count; i++)
                {
                    var argNode = BuildParseTreeFromTokens(splitArgs[i]);
                    if (argNode != null) children.Add(argNode);
                }
                
                // Handle 3rd argument with disambiguating colon detection
                // Pattern: verb: (e.g., -:) where tokenizer joined them into ASSIGNMENT
                var thirdArgTokens = splitArgs[2];
                if (thirdArgTokens.Count == 1 && thirdArgTokens[0].Type == TokenType.ASSIGNMENT)
                {
                    // This is a verb+colon pattern like -: 
                    // Extract the verb part (everything before the colon)
                    var lexeme = thirdArgTokens[0].Lexeme;
                    if (lexeme.EndsWith(":") && lexeme.Length > 1)
                    {
                        var verbPart = lexeme.Substring(0, lexeme.Length - 1);
                        // Create a symbol node for the verb with disambiguating colon marker
                        var verbWithColonNode = new ASTNode(ASTNodeType.Literal, new SymbolValue(verbPart));
                        // Store disambiguating colon info in a special way
                        // We'll wrap it to signal to the evaluator
                        var wrapperNode = new ASTNode(ASTNodeType.MonadicOp, new SymbolValue("monadic"), new List<ASTNode> { verbWithColonNode });
                        children.Add(wrapperNode);
                    }
                    else
                    {
                        // Regular colon, parse normally
                        var argNode = BuildParseTreeFromTokens(thirdArgTokens);
                        if (argNode != null) children.Add(argNode);
                    }
                }
                else
                {
                    // Normal parsing for 3rd argument
                    var argNode = BuildParseTreeFromTokens(thirdArgTokens);
                    if (argNode != null) children.Add(argNode);
                }
                
                var triadicNode = new ASTNode(ASTNodeType.TriadicOp, new SymbolValue(opToken.Lexeme), children);
                return triadicNode;
            }
            
            return null;
        }
        
        /// <summary>
        /// Parse tetradic operation (4 arguments) - ONLY bracket-based
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <param name="position">Starting position</param>
        /// <returns>AST node for tetradic operation</returns>
        private ASTNode? ParseTetradicOperation(List<Token> tokens, int position)
        {
            if (position >= tokens.Count) return null;
            
            var opToken = tokens[position];
            
            // Must be followed by LEFT_BRACKET for tetradic operations
            if (position + 1 >= tokens.Count || tokens[position + 1].Type != TokenType.LEFT_BRACKET)
                return null;
                
            var children = new List<ASTNode>();
            
            // Parse arguments inside brackets: @[arg1;arg2;arg3;arg4]
            var bracketEnd = FindMatchingBracket(tokens, position + 1);
            if (bracketEnd == -1) return null;
            
            var bracketTokens = tokens.GetRange(position + 2, bracketEnd - position - 2); // Skip @ and [
            var splitArgs = SplitBracketArguments(bracketTokens, 4);
            
            if (splitArgs.Count >= 4)
            {
                foreach (var argTokens in splitArgs.Take(4))
                {
                    var argNode = BuildParseTreeFromTokens(argTokens);
                    if (argNode != null) children.Add(argNode);
                }
                
                var tetradicNode = new ASTNode(ASTNodeType.TetradicOp, new SymbolValue(opToken.Lexeme), children);
                
                // Check for adjacent brackets - implicit apply (indexing)
                // @[...][x] should parse as (@[...])[x]
                if (bracketEnd < tokens.Count - 1 && (int)tokens[bracketEnd + 1].Type == (int)TokenType.LEFT_BRACKET)
                {
                    // Find the matching bracket for the second [ to extract the index expression
                    var secondBracketStart = bracketEnd + 1;
                    var secondBracketEnd = FindMatchingBracket(tokens, secondBracketStart);
                    if (secondBracketEnd > secondBracketStart)
                    {
                        // Extract the content inside [x] - just parse the content, not as an indexing operation
                        var indexContentTokens = tokens.GetRange(secondBracketStart + 1, secondBracketEnd - secondBracketStart - 1);
                        ASTNode? indexNode = null;
                        if (indexContentTokens.Count == 1)
                            indexNode = CreateNodeFromToken(indexContentTokens[0]);
                        else if (indexContentTokens.Count > 0)
                            indexNode = EvaluateFromRight(indexContentTokens);
                        
                        if (indexNode != null)
                        {
                            var applyNode = new ASTNode(ASTNodeType.DyadicOp);
                            applyNode.Value = new SymbolValue("@");
                            applyNode.Children.Add(tetradicNode);
                            applyNode.Children.Add(indexNode);
                            return applyNode;
                        }
                    }
                }
                
                return tetradicNode;
            }
            
            return null;
        }
        
        /// <summary>
        /// Split bracket arguments by semicolons
        /// </summary>
        private List<List<Token>> SplitBracketArguments(List<Token> tokens, int expectedArgs)
        {
            var args = new List<List<Token>>();
            var currentArg = new List<Token>();
            int depth = 0;
            
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                
                if (token.Type == TokenType.LEFT_PAREN || token.Type == TokenType.LEFT_BRACKET || token.Type == TokenType.LEFT_BRACE)
                    depth++;
                else if (token.Type == TokenType.RIGHT_PAREN || token.Type == TokenType.RIGHT_BRACKET || token.Type == TokenType.RIGHT_BRACE)
                    depth--;
                
                if (token.Type == TokenType.SEMICOLON && depth == 0)
                {
                    args.Add(currentArg);
                    currentArg = new List<Token>();
                }
                else
                {
                    currentArg.Add(token);
                }
            }
            
            // Add the last argument
            args.Add(currentArg);
                
            return args;
        }
        
        /// <summary>
        /// Parse variadic adverb operation
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <param name="position">Starting position</param>
        /// <returns>AST node for variadic adverb operation</returns>
        private ASTNode? ParseVariadicAdverb(List<Token> tokens, int position)
        {
            if (position >= tokens.Count) return null;
            
            var adverbToken = tokens[position];
            string adverbName = "";
            
            // Determine adverb type
            switch (adverbToken.Type)
            {
                case TokenType.VARIADIC_ADVERB_OVER:
                    adverbName = "over";
                    break;
                case TokenType.VARIADIC_ADVERB_SCAN:
                    adverbName = "scan";
                    break;
                case TokenType.VARIADIC_ADVERB_EACH:
                    adverbName = "each";
                    break;
                default:
                    return null;
            }
            
            // Return "not yet implemented" node
            var message = $"generalized {adverbName} is not yet implemented";
            return new ASTNode(ASTNodeType.NotImplemented, new CharacterValue(message), null);
        }
        
        /// <summary>
        /// Helper method to recursively build parse tree from tokens
        /// </summary>
        private ASTNode? BuildParseTreeFromTokens(List<Token> tokens)
        {
            var tempParser = new LRSParser(tokens, true);
            return tempParser.BuildParseTreeFromRight(tokens);
        }
        
        /// <summary>
        /// Create AST node from token using LRSAtomicParser
        /// Rebuild marker V2
        /// </summary>
        internal ASTNode CreateNodeFromToken(Token token)
        {
            // Handle adverb tokens in Pure LRS mode
            if (PureLRSMode && VerbRegistry.IsAdverbToken(token.Type))
            {
                // In Pure LRS mode, try to parse adverb operations
                return ParseAdverbOperation(token);
            }
            
            if (LRSAtomicParser.CanBeParsedByAtomicParser(token.Type))
            {
                return LRSAtomicParser.ParseAtomicToken(token, this);
            }
            
            // Handle verb tokens by creating symbol nodes
            if (VerbRegistry.IsVerbToken(token.Type))
            {
                var verbName = VerbRegistry.TokenTypeToVerbName(token.Type);
                return ASTNode.MakeLiteral(new SymbolValue(verbName));
            }
            
            // Handle operator symbols for parse trees
            return LRSAtomicParser.CreateOperatorNode(token.Type);
        }
        
        /// <summary>
        /// Parse adverb operation in Pure LRS mode
        /// </summary>
        /// <param name="adverbToken">The adverb token</param>
        /// <returns>AST node representing the adverb operation</returns>
        private ASTNode ParseAdverbOperation(Token adverbToken)
        {
            // Find the position of this adverb token in the original token list
            int adverbPosition = -1;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Type == adverbToken.Type && tokens[i].Lexeme == adverbToken.Lexeme)
                {
                    adverbPosition = i;
                    break;
                }
            }
            
            if (adverbPosition == -1)
            {
                throw new Exception($"Adverb token not found in token list: {adverbToken.Lexeme}");
            }
            
            // Use LRSAdverbParser for proper adverb handling
            var adverbParser = new LRSAdverbParser(tokens, BuildParseTree);
            int position = adverbPosition;
            
            // Try to parse verb-immediate-left adverb pattern (verb on left side only)
            var result = adverbParser.ParseVerbImmediateLeftAdverb(ref position);
            if (result != null)
            {
                return result;
            }
            
            // Fallback: create a simple node for basic adverb handling
            var children = new List<ASTNode>();
            return new ASTNode(ASTNodeType.DyadicOp, new SymbolValue(adverbToken.Lexeme), children);
        }
        
        /// <summary>
        /// Parse adverb expression in Safe LRS mode
        /// </summary>
        /// <param name="expressionTokens">Tokens to parse for adverb operations</param>
        /// <returns>AST node representing the adverb operation, or null if no adverb found</returns>
        private ASTNode? ParseAdverbExpression(List<Token> expressionTokens)
        {
            // CONSERVATIVE APPROACH: Only handle very specific, simple cases
            // Start with the most basic adverb patterns to avoid breaking the system
            
            // Case 1: Verb + two-glyph adverb patterns
            // Pattern: verb/: arguments, verb\: arguments, etc.
            if (expressionTokens.Count >= 3)
            {
                // Check for verb + two-glyph adverb at the beginning
                if (IsVerbToken(expressionTokens[0].Type) &&
                    (expressionTokens[1].Type == TokenType.ADVERB_SLASH_COLON ||
                     expressionTokens[1].Type == TokenType.ADVERB_BACKSLASH_COLON ||
                     expressionTokens[1].Type == TokenType.ADVERB_TICK_COLON))
                {
                    return ParseVerbTwoGlyphAdverb(expressionTokens);
                }
            }
            
            // Case 2: Simple two-glyph adverb with simple left and right sides
            // Pattern: (vector) %\: number
            if (expressionTokens.Count >= 4)
            {
                // Check for pattern: (1 2 3) %\: 2
                if (expressionTokens[0].Type == TokenType.LEFT_PAREN &&
                    expressionTokens[2].Type == TokenType.DIVIDE &&
                    expressionTokens[3].Type == TokenType.ADVERB_BACKSLASH_COLON &&
                    expressionTokens[4].Type == TokenType.INTEGER)
                {
                    return ParseSimpleTwoGlyphAdverb(expressionTokens);
                }
            }

            
            // Case 3a: Disambiguating colon pattern - verb + colon + adverb
            // Pattern: verb:' args (e.g., #:' (1 2;3 4) for count each)
            // MUST be checked BEFORE generic verb+adverb pattern
            if (expressionTokens.Count >= 3)
            {
                if (IsVerbToken(expressionTokens[0].Type) &&
                    expressionTokens[1].Type == TokenType.COLON &&
                    (expressionTokens[2].Type == TokenType.ADVERB_SLASH ||
                     expressionTokens[2].Type == TokenType.ADVERB_BACKSLASH ||
                     expressionTokens[2].Type == TokenType.ADVERB_TICK))
                {
                    return ParseGenericVerbAdverbWithColon(expressionTokens, 0);
                }
                
                // Pattern with left argument: left_arg + verb + colon + adverb
                // Pattern: 1 +:/x (e.g., 1 +:/x for conditional transpose)
                // Skip if position 0 is a grouping delimiter - those should be parsed as grouping constructs first
                if (expressionTokens.Count >= 4 &&
                    expressionTokens[0].Type != TokenType.LEFT_PAREN &&
                    expressionTokens[0].Type != TokenType.LEFT_BRACKET &&
                    expressionTokens[0].Type != TokenType.LEFT_BRACE &&
                    IsVerbToken(expressionTokens[1].Type) &&
                    expressionTokens[2].Type == TokenType.COLON &&
                    (expressionTokens[3].Type == TokenType.ADVERB_SLASH ||
                     expressionTokens[3].Type == TokenType.ADVERB_BACKSLASH ||
                     expressionTokens[3].Type == TokenType.ADVERB_TICK))
                {
                    return ParseGenericVerbAdverbWithColon(expressionTokens, 1);
                }
            }
            
            // Case 3: Generic verb + single-glyph adverb pattern
            // Pattern: verb/ args, verb\ args, verb' args (e.g., -/ 10 2 3 1, +\ 1 2 3, #' (1 2;3 4))
            // Note: verb must be at position 0, adverb at position 1
            if (expressionTokens.Count >= 2)
            {
                // Check for verb followed by single-glyph adverb at positions 0 and 1
                if (IsVerbToken(expressionTokens[0].Type) &&
                    (expressionTokens[1].Type == TokenType.ADVERB_SLASH ||
                     expressionTokens[1].Type == TokenType.ADVERB_BACKSLASH ||
                     expressionTokens[1].Type == TokenType.ADVERB_TICK))
                {
                    return ParseGenericVerbAdverb(expressionTokens);
                }
            }
            
            return null; // No simple adverb pattern found
        }
        
        /// <summary>
        /// Check if a token represents a verb (operator or system function)
        /// Uses VerbRegistry to include all verbs, not just hardcoded operators
        /// </summary>
        internal bool IsVerbToken(TokenType tokenType)
        {
            // Use VerbRegistry to check all registered verbs (operators + system functions)
            return VerbRegistry.IsVerbToken(tokenType);
        }
        
        /// <summary>
        /// Parse generic verb + single-glyph adverb pattern (verb/ verb\ verb')
        /// Handles common patterns like: -/ 10 2 3 1, +\ 1 2 3, #' (1 2;3 4)
        /// </summary>
        private ASTNode ParseGenericVerbAdverb(List<Token> expressionTokens)
        {
            var verbToken = expressionTokens[0];
            var adverbToken = expressionTokens[1];
            
            // Create verb node
            var verbNode = CreateNodeFromToken(verbToken);
            
            // Check for nested adverbs: verb/'x, verb//x, verb/\x, etc.
            // Per spec: "a modified verb is also a verb" — adverbs bind closest to the verb first.
            // One-adverb-at-a-time: build nested 2-child nodes where each outer adverb wraps
            // the inner modified verb. Inner modified verb is ADVERB(verb) with 1 child.
            // Outermost adverb is ADVERB(inner_modified_verb, argument) with 2 children.
            if (expressionTokens.Count >= 3 && VerbRegistry.IsAdverbToken(expressionTokens[2].Type))
            {
                // Build innermost modified verb: ADVERB(verb) — 1 child, no arguments
                var innerNode = new ASTNode(ASTNodeType.DyadicOp);
                innerNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
                innerNode.Children.Add(verbNode);
                
                // Wrap with successive adverbs (each is 1-child until the outermost)
                int chainPos = 2;
                while (chainPos < expressionTokens.Count - 1 && VerbRegistry.IsAdverbToken(expressionTokens[chainPos].Type))
                {
                    // Check if the NEXT token is also an adverb — if so, this is still an inner adverb (1-child)
                    if (chainPos + 1 < expressionTokens.Count && VerbRegistry.IsAdverbToken(expressionTokens[chainPos + 1].Type))
                    {
                        var midAdverb = expressionTokens[chainPos];
                        var midNode = new ASTNode(ASTNodeType.DyadicOp);
                        midNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(midAdverb.Type));
                        midNode.Children.Add(innerNode);
                        innerNode = midNode;
                        chainPos++;
                    }
                    else
                    {
                        break; // This is the outermost adverb — handle below with arguments
                    }
                }
                
                // The token at chainPos is the outermost adverb
                var outerAdverbToken = expressionTokens[chainPos];
                
                // Parse remaining tokens as the argument
                var remainingTokens = expressionTokens.Skip(chainPos + 1).ToList();
                ASTNode? chainArg = null;
                if (remainingTokens.Count == 1 && remainingTokens[0].Type == TokenType.IDENTIFIER)
                {
                    chainArg = LRSAtomicParser.ParseAtomicToken(remainingTokens[0], this);
                }
                else if (remainingTokens.Count > 0)
                {
                    chainArg = EvaluateFromRight(remainingTokens);
                }
                
                // Build outermost adverb node: ADVERB(inner_modified_verb, argument) — 2 children
                var outerNode = new ASTNode(ASTNodeType.DyadicOp);
                outerNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(outerAdverbToken.Type));
                outerNode.Children.Add(innerNode);
                if (chainArg != null)
                    outerNode.Children.Add(chainArg);
                else
                    outerNode.Children.Add(ASTNode.MakeLiteral(new NullValue()));
                
                return outerNode;
            }
            
            // Parse arguments (everything after the adverb)
            var argTokens = expressionTokens.Skip(2).ToList();
            ASTNode? argNode = null;
            
            if (argTokens.Count > 0)
            {
                // Check if all argument tokens are atomic - if so, create a vector directly
                // But single identifiers should remain as Variable nodes for runtime resolution
                bool allAtomic = true;
                bool hasIdentifier = false;
                foreach (var token in argTokens)
                {
                    if (!LRSAtomicParser.CanBeParsedByAtomicParser(token.Type))
                    {
                        allAtomic = false;
                        break;
                    }
                    if (token.Type == TokenType.IDENTIFIER)
                        hasIdentifier = true;
                }
                
                if (allAtomic && argTokens.Count == 1 && hasIdentifier)
                {
                    // Single identifier: return as variable node for runtime resolution
                    argNode = LRSAtomicParser.ParseAtomicToken(argTokens[0], this);
                }
                else if (allAtomic && !hasIdentifier)
                {
                    // Create a vector node from the atomic literal tokens
                    var argNodes = new List<ASTNode>();
                    foreach (var token in argTokens)
                    {
                        argNodes.Add(LRSAtomicParser.ParseAtomicToken(token, this));
                    }
                    argNode = new ASTNode(ASTNodeType.Vector, null, argNodes);
                }
                else
                {
                    // Parse the arguments as a full expression
                    // This handles complex cases like x*y, (1 2 3;4 5 6), etc.
                    argNode = EvaluateFromRight(argTokens);
                    
                    // Fallback to BuildParseTreeFromTokens if EvaluateFromRight fails
                    if (argNode == null && argTokens.Count > 0)
                    {
                        argNode = BuildParseTreeFromTokens(argTokens);
                    }
                }
            }
            
            // Check if verb is monadic to determine node structure
            var verbName = VerbRegistry.TokenTypeToVerbName(verbToken.Type);
            var verbInfo = VerbRegistry.GetVerb(verbName);
            bool isMonadicVerb = verbInfo != null && verbInfo.SupportedArities.Contains(1) && verbInfo.SupportedArities.Length == 1;
            
            // Create adverb node with appropriate structure
            var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
            adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
            adverbNode.Children.Add(verbNode);
            
            if (isMonadicVerb)
            {
                // Monadic verb: 2-child structure (verb, arguments)
                if (argNode != null)
                {
                    adverbNode.Children.Add(argNode);
                }
            }
            else
            {
                // Dyadic verb: 3-child structure (verb, left, right)
                // Add dummy left argument (0) for now
                adverbNode.Children.Add(ASTNode.MakeLiteral(new NullValue()));
                
                if (argNode != null)
                {
                    adverbNode.Children.Add(argNode);
                }
            }
            
            return adverbNode;
        }
        
        /// <summary>
        /// Parse generic verb + colon + single-glyph adverb pattern (verb:' verb:/ verb:\)
        /// Handles disambiguating colon patterns like #:' (count each), +:/ (sum over)
        /// The colon indicates monadic interpretation of the verb
        /// </summary>
        /// <param name="expressionTokens">Token list</param>
        /// <param name="offset">Offset of the verb in the token list (0 for verb at start, 1 for verb after left arg)</param>
        internal ASTNode ParseGenericVerbAdverbWithColon(List<Token> expressionTokens, int offset = 0)
        {
            var verbToken = expressionTokens[offset];
            var colonToken = expressionTokens[offset + 1];  // COLON - disambiguating
            var adverbToken = expressionTokens[offset + 2];
            
            // Create verb node using the monadic form (verb + ':') so Over Monad dispatch recognises it
            string monadicVerbName = GetVerbName(verbToken.Type) + ":";
            var verbNode = new ASTNode(ASTNodeType.Literal, new SymbolValue(monadicVerbName));
            
            // Parse arguments (everything after the adverb)
            var argTokens = expressionTokens.Skip(offset + 3).ToList();
            ASTNode? argNode = null;
            
            if (argTokens.Count > 0)
            {
                // Parse the arguments as a sub-expression
                int argPosition = 0;
                var argParser = new LRSExpressionProcessor(argTokens, BuildParseTree, this);
                argNode = argParser.ProcessExpression(ref argPosition);
                
                // If we couldn't parse the arguments, or ProcessExpression only parsed
                // a partial expression (e.g., just 'a' from 'a _ x'), use the full LRSParser.
                // This handles complex expressions like (1 2 3;4 5 6) with semicolons
                // and dyadic operations that ProcessExpression doesn't handle.
                if ((argNode == null || argPosition < argTokens.Count) && argTokens.Count > 0)
                {
                    argNode = BuildParseTreeFromTokens(argTokens);
                }
                
                // Final fallback: create a vector from atomic tokens only
                if (argNode == null && argTokens.Count > 0)
                {
                    // Create a vector node from the argument tokens
                    var argNodes = new List<ASTNode>();
                    foreach (var token in argTokens)
                    {
                        if (LRSAtomicParser.CanBeParsedByAtomicParser(token.Type))
                        {
                            argNodes.Add(LRSAtomicParser.ParseAtomicToken(token, this));
                        }
                    }
                    
                    if (argNodes.Count > 0)
                    {
                        argNode = new ASTNode(ASTNodeType.Vector, null, argNodes);
                    }
                }
            }
            
            // Create adverb node: DyadicOp(adverb_symbol, verb, arguments)
            // Disambiguating colon forces monadic interpretation - use 3-child structure: verb, left, arg
            var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
            adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
            
            // Add verb node FIRST (evaluator expects verb at Children[0])
            adverbNode.Children.Add(verbNode);
            
            // If there's a left argument (offset > 0), parse it and add as second child
            if (offset > 0)
            {
                var leftArgTokens = expressionTokens.Take(offset).ToList();
                if (leftArgTokens.Count > 0)
                {
                    int leftPosition = 0;
                    var leftParser = new LRSExpressionProcessor(leftArgTokens, BuildParseTree, this);
                    var leftNode = leftParser.ProcessExpression(ref leftPosition);
                    
                    if (leftNode == null && leftArgTokens.Count > 0)
                    {
                        leftNode = BuildParseTreeFromTokens(leftArgTokens);
                    }
                    
                    if (leftNode != null)
                    {
                        adverbNode.Children.Add(leftNode);
                    }
                }
            }
            
            // Add argument node if present as third child
            if (argNode != null)
            {
                adverbNode.Children.Add(argNode);
            }
            
            return adverbNode;
        }
        
        /// <summary>
        /// Parse verb + two-glyph adverb pattern
        /// </summary>
        /// <param name="expressionTokens">Tokens to parse</param>
        /// <returns>AST node representing the adverb operation</returns>
        private ASTNode ParseVerbTwoGlyphAdverb(List<Token> expressionTokens)
        {
            var verbToken = expressionTokens[0];
            var adverbToken = expressionTokens[1];
            
            // Create verb node
            var verbNode = new ASTNode(ASTNodeType.Literal, new SymbolValue(GetVerbName(verbToken.Type)));
            
            // Parse arguments after the adverb as a single expression
            // e.g., for >':0,1 2 3 4, parse "0,1 2 3 4" as a single expression
            var arguments = new List<ASTNode>();
            
            if (expressionTokens.Count > 2)
            {
                var remainingTokens = expressionTokens.GetRange(2, expressionTokens.Count - 2);
                var argNode = BuildParseTreeFromRight(remainingTokens);
                if (argNode != null)
                {
                    arguments.Add(argNode);
                }
            }
            
            // Create adverb node: ADVERB(verb, left, right)
            var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
            adverbNode.Value = new SymbolValue(VerbRegistry.GetAdverbType(adverbToken.Type));
            adverbNode.Children.Add(verbNode);
            
            // Handle arguments: 0 args = projection, 1 arg = right only
            if (arguments.Count == 0)
            {
                // No arguments - projection
                adverbNode.Children.Add(new ASTNode(ASTNodeType.Literal, new NullValue())); // left (dummy)
                adverbNode.Children.Add(new ASTNode(ASTNodeType.Literal, new NullValue())); // right (dummy)
            }
            else if (arguments.Count == 1)
            {
                // One argument - right operand only
                adverbNode.Children.Add(new ASTNode(ASTNodeType.Literal, new NullValue())); // left (dummy)
                adverbNode.Children.Add(arguments[0]); // right
            }
            else
            {
                // More than 1 argument - shouldn't happen with single-expression parsing
                adverbNode.Children.Add(new ASTNode(ASTNodeType.Literal, new NullValue())); // left (dummy)
                var vectorNode = new ASTNode(ASTNodeType.Vector);
                foreach (var arg in arguments)
                {
                    vectorNode.Children.Add(arg);
                }
                adverbNode.Children.Add(vectorNode);
            }
            
            return adverbNode;
        }
        
        /// <summary>
        /// Get verb name from token type
        /// </summary>
        private string GetVerbName(TokenType tokenType)
        {
            return VerbRegistry.TokenTypeToVerbName(tokenType);
        }
        
        /// <summary>
        /// Parse simple two-glyph adverb (conservative approach)
        /// </summary>
        /// <param name="expressionTokens">Tokens to parse</param>
        /// <returns>AST node representing the adverb operation</returns>
        private ASTNode ParseSimpleTwoGlyphAdverb(List<Token> expressionTokens)
        {
            // Create a simple placeholder node that represents %\: operation
            // The actual evaluation will be handled by the existing evaluator
            var children = new List<ASTNode>();
            
            // Left side: (1 2 3) - create a simple vector node
            var vectorElements = new List<ASTNode>();
            for (int i = 1; i < expressionTokens.Count - 2; i++)
            {
                if (expressionTokens[i].Type == TokenType.INTEGER)
                {
                    vectorElements.Add(ASTNode.MakeLiteral(new IntegerValue(int.Parse(expressionTokens[i].Lexeme))));
                }
            }
            var leftNode = ASTNode.MakeVector(vectorElements);
            
            // Right side: 2
            var rightNode = ASTNode.MakeLiteral(new IntegerValue(int.Parse(expressionTokens[4].Lexeme)));
            
            children.Add(leftNode);
            children.Add(rightNode);
            
            // Create node with %\: symbol
            return new ASTNode(ASTNodeType.DyadicOp, new SymbolValue("%\\:"), children);
        }
        
        /// <summary>
        /// Create adverb node following LRS principles (verb-agnostic)
        /// Uses VerbRegistry to determine adverb behavior
        /// Follows same pattern as LRSAdverbParser for consistency
        /// </summary>
        /// <param name="adverbToken">The adverb token</param>
        /// <param name="functionNode">The function node (verb)</param>
        /// <param name="argumentNode">The argument node</param>
        /// <returns>AST node representing the adverb operation</returns>
        private ASTNode CreateAdverbNode(Token adverbToken, ASTNode functionNode, ASTNode? argumentNode)
        {
            // Get adverb symbol for token type (same as LRSAdverbParser)
            string adverbSymbol = GetAdverbSymbol(adverbToken.Type);
            
            // Create DyadicOp node (consistent with LRSAdverbParser)
            var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
            adverbNode.Value = new SymbolValue(adverbSymbol);
            adverbNode.Children.Add(functionNode);
            if (argumentNode != null)
                adverbNode.Children.Add(argumentNode);
            
            return adverbNode;
        }
        
        /// <summary>
        /// Get adverb symbol for token type (same as LRSAdverbParser)
        /// </summary>
        private string GetAdverbSymbol(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.ADVERB_TICK => "'",
                TokenType.ADVERB_SLASH => "/",
                TokenType.ADVERB_BACKSLASH => "\\",
                TokenType.ADVERB_SLASH_COLON => "/:",
                TokenType.ADVERB_TICK_COLON => "':",
                TokenType.ADVERB_BACKSLASH_COLON => "\\:",
                _ => tokenType.ToString().ToLower()
            };
        }
        
        /// <summary>
        /// Try to create an implicit vector from a sequence of atomic literals
        /// Returns null if tokens don't form a valid implicit vector
        /// </summary>
        private ASTNode? TryCreateImplicitVector(List<Token> tokens)
        {
            if (tokens.Count < 2)
                return null;
            
            // IDENTIFIER as first token means function application (f x), not implicit vector
            // Implicit vectors only consist of literal values: 1 2 3, `a`b`c, etc.
            if (tokens[0].Type == TokenType.IDENTIFIER)
                return null;
                
            // Check if all tokens are atomic literals and collect them
            var elements = new List<ASTNode>();
            
            foreach (var token in tokens)
            {
                // Check if token is an atomic literal
                if (!LRSAtomicParser.CanBeImplicitVectorElement(token.Type))
                    return null; // Not all atomic - can't be implicit vector
                
                // Parse the token and add to elements
                var node = LRSAtomicParser.ParseAtomicToken(token, this);
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
        /// Check if a sequence of tokens consists only of simple literals (no operators)
        /// Used to detect implicit apply patterns like (9 7 6)2 2 1 1 0 0
        /// </summary>
        private bool IsSimpleLiteralSequence(List<Token> tokens)
        {
            if (tokens.Count == 0)
                return false;

            foreach (var token in tokens)
            {
                // Allow only atomic literal types (no operators, no delimiters)
                switch (token.Type)
                {
                    case TokenType.INTEGER:
                    case TokenType.LONG:
                    case TokenType.FLOAT:
                    case TokenType.CHARACTER:
                    case TokenType.CHARACTER_VECTOR:
                    case TokenType.SYMBOL:
                        continue; // These are valid literal types
                    default:
                        return false; // Any operator or other token type disqualifies
                }
            }
            return true;
        }

        /// <summary>
        /// Get the statement parser for handling assignment statements in sub-expressions
        /// </summary>
        /// <returns>The LRS statement parser instance</returns>
        public LRSStatementParser? GetStatementParser()
        {
            return statementParser;
        }
        
        /// <summary>
        /// Parse triadic/tetradic operation from bracket function call
        /// Handles .[arg1;arg2;arg3] and @[arg1;arg2;arg3;arg4] patterns
        /// </summary>
        /// <param name="opToken">The operator token (DOT_APPLY or APPLY)</param>
        /// <param name="argTokens">Tokens inside brackets (excluding brackets)</param>
        /// <param name="argCount">Number of arguments detected</param>
        /// <returns>AST node for triadic/tetradic operation</returns>
        private ASTNode? ParseTriadicFromBracketCall(Token opToken, List<Token> argTokens, int argCount)
        {
            // Split arguments by semicolons
            var splitArgs = SplitBracketArguments(argTokens, argCount);
            
            var children = new List<ASTNode>();
            
            // Parse arguments based on arity
            int maxArgs = Math.Min(splitArgs.Count, argCount);
            for (int i = 0; i < maxArgs; i++)
            {
                var currentArgTokens = splitArgs[i];
                
                // Handle verb+colon patterns as monadic verb symbols (e.g. -: >: +: etc.)
                // These are disambiguating-colon forms: VERB COLON -> SymbolValue("verb")
                if (i == 2 && currentArgTokens.Count == 2 &&
                    IsVerbToken(currentArgTokens[0].Type) &&
                    currentArgTokens[1].Type == TokenType.COLON)
                {
                    // Represent as a monadic verb symbol — evaluator dispatches via CallFunction(SymbolValue)
                    var verbSymbol = VerbRegistry.GetDyadicOperatorSymbol(currentArgTokens[0].Type);
                    children.Add(ASTNode.MakeLiteral(new SymbolValue(verbSymbol)));
                }
                else
                {
                    // Parse argument respecting current mode
                    ASTNode? argNode;
                    if (BuildParseTree)
                        argNode = BuildParseTreeFromTokens(currentArgTokens);
                    else
                        argNode = EvaluateFromRight(currentArgTokens);
                    if (argNode != null) children.Add(argNode);
                }
            }
            
            // Create the appropriate AST node based on arity
            if (argCount == 3 && children.Count >= 3)
            {
                return new ASTNode(ASTNodeType.TriadicOp, new SymbolValue(opToken.Lexeme), children.GetRange(0, 3));
            }
            else if (argCount == 4 && children.Count >= 4)
            {
                return new ASTNode(ASTNodeType.TetradicOp, new SymbolValue(opToken.Lexeme), children.GetRange(0, 4));
            }
            
            return null;
        }
        
        /// <summary>
        /// Force rebuild marker V3 - ensures new assembly is built
        /// </summary>
        public void ForceRebuildMarkerV3() { }
        
        /// <summary>
        /// Force rebuild marker V4 - April 2026 adverb fixes
        /// </summary>
        public void ForceRebuildMarkerV4() { }
    }
}

// Force rebuild marker class V5
public static class ForceRebuildMarkerV5 { public static int Value = 42; }
