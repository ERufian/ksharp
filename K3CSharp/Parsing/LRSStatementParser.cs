using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Statement parsing for LRS parser
    /// Handles assignment and control flow statements with proper precedence
    /// Statements have lower precedence than verbs but higher precedence than separators
    /// </summary>
    public class LRSStatementParser
    {
        private readonly List<Token> tokens;
        private readonly LRSParser parentParser;
        
        public LRSStatementParser(List<Token> tokens, LRSParser parentParser)
        {
            this.tokens = tokens;
            this.parentParser = parentParser;
        }
        
        /// <summary>
        /// Check if the first token could be a statement
        /// </summary>
        public static bool CouldBeStatement(TokenType tokenType)
        {
            return tokenType == TokenType.COLON ||           // Assignment / conditional evaluation
                   tokenType == TokenType.ASSIGNMENT ||     // Assignment operator
                   tokenType == TokenType.GLOBAL_ASSIGNMENT || // Global assignment
                   tokenType == TokenType.DO ||              // do statement
                   tokenType == TokenType.WHILE ||           // while statement
                   tokenType == TokenType.IF_FUNC;           // if statement
        }
        
        /// <summary>
        /// Parse statement from tokens as an inline (non-terminal) assignment.
        /// Used when the assignment is a sub-expression within a larger expression (e.g., right side of dyadic op).
        /// Inline assignments return the assigned value, not Null.
        /// </summary>
        public ASTNode? ParseInlineStatement(List<Token> statementTokens)
        {
            var node = ParseStatement(statementTokens);
            if (node is { Type: ASTNodeType.Assignment })
                node.IsTerminalAssignment = false;
            return node;
        }
        
        /// <summary>
        /// Parse statement from tokens
        /// </summary>
        /// <param name="statementTokens">Tokens representing the statement</param>
        /// <returns>AST node representing the statement</returns>
        public ASTNode? ParseStatement(List<Token> statementTokens)
        {
            if (statementTokens.Count == 0)
                return null;
                
            var firstToken = statementTokens[0];
            
            // Check for statements that start with specific keywords
            switch (firstToken.Type)
            {
                case TokenType.COLON:
                    return ParseColonStatement(statementTokens);
                case TokenType.ASSIGNMENT:
                case TokenType.GLOBAL_ASSIGNMENT:
                    return ParseAssignmentStatement(statementTokens);
                case TokenType.DO:
                    return ParseDoStatement(statementTokens);
                case TokenType.WHILE:
                    return ParseWhileStatement(statementTokens);
                case TokenType.IF_FUNC:
                    return ParseIfStatement(statementTokens);
            }
            
            // Check for assignments anywhere in the token list (e.g., a:5, x:10)
            // This handles the common case where variable name comes before the colon
            for (int i = 1; i < statementTokens.Count; i++)
            {
                if (statementTokens[i].Type == TokenType.COLON ||
                    statementTokens[i].Type == TokenType.ASSIGNMENT ||
                    statementTokens[i].Type == TokenType.GLOBAL_ASSIGNMENT)
                {
                    return ParseAssignmentStatement(statementTokens);
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Parse colon statement (either assignment, conditional evaluation, or monadic return)
        /// </summary>
        private ASTNode? ParseColonStatement(List<Token> tokens)
        {
            // Check if this is conditional evaluation :[...]
            if (tokens.Count >= 2 && tokens[1].Type == TokenType.LEFT_BRACKET)
            {
                return ParseConditionalEvaluation(tokens);
            }
            
            // Check if this is monadic colon (return/identity) - no left operand
            // Format: : value  (e.g., : 42)
            var assignmentIndex = FindAssignmentOperator(tokens);
            if (assignmentIndex == 0)
            {
                // Colon is the first token - this is monadic return
                // Return the operand as-is
                if (tokens.Count >= 2)
                {
                    var operandTokens = tokens.GetRange(1, tokens.Count - 1);
                    return ParseRightSideExpression(operandTokens);
                }
                return null;
            }
            
            // Otherwise, treat as assignment
            return ParseAssignmentStatement(tokens);
        }
        
        /// <summary>
        /// Parse assignment statement with pure vs inline behavior
        /// </summary>
        private ASTNode? ParseAssignmentStatement(List<Token> tokens)
        {
            // Find the assignment operator
            int assignmentIndex = FindAssignmentOperator(tokens);
            if (assignmentIndex == -1)
                return null;
                
            var assignmentToken = tokens[assignmentIndex];
            
            // Split into left and right parts
            var leftTokens = tokens.GetRange(0, assignmentIndex);
            var rightTokens = tokens.GetRange(assignmentIndex + 1, tokens.Count - assignmentIndex - 1);
            
            // Check for apply-and-assign pattern: IDENTIFIER VERB COLON (e.g., x?:)
            // This pattern allows empty right side for monadic operators
            bool isApplyAndAssignPattern = assignmentToken.Type == TokenType.COLON &&
                                            leftTokens.Count == 2 &&
                                            leftTokens[0].Type == TokenType.IDENTIFIER &&
                                            VerbRegistry.IsVerbToken(leftTokens[1].Type);
            
            if (leftTokens.Count == 0)
                return null;
            
            // Empty right side is only allowed for apply-and-assign patterns (e.g., x?:)
            if (rightTokens.Count == 0 && !isApplyAndAssignPattern)
                return null;
            
            // Check if this is a modified assignment operator (apply and assign)
            // Case 1: compound token like "+:" (lexeme ends with ":")
            // Exclude GLOBAL_ASSIGNMENT (::) which also ends with ":" but is not apply-and-assign
            if (assignmentToken.Type != TokenType.GLOBAL_ASSIGNMENT &&
                assignmentToken.Lexeme.Length > 1 && assignmentToken.Lexeme.EndsWith(":"))
            {
                return ParseApplyAndAssignStatement(leftTokens, assignmentToken, rightTokens);
            }
            
            // Case 2: apply-and-assign with separate tokens: IDENTIFIER VERB COLON [expression]
            // e.g., i+:1 tokenized as [IDENTIFIER(i), PLUS(+), COLON(:), INTEGER(1)] - dyadic
            // e.g., x?: tokenized as [IDENTIFIER(x), QUESTION(?), COLON(:)] - monadic
            // Here leftTokens=[IDENTIFIER,VERB] and assignmentToken=COLON
            if (assignmentToken.Type == TokenType.COLON &&
                leftTokens.Count == 2 &&
                leftTokens[0].Type == TokenType.IDENTIFIER &&
                VerbRegistry.IsVerbToken(leftTokens[1].Type))
            {
                // Reconstruct as apply-and-assign: operator is leftTokens[1]
                var applyAndAssignNode = new ASTNode(ASTNodeType.ApplyAndAssign);
                applyAndAssignNode.Value = new SymbolValue(leftTokens[0].Lexeme);
                var opSymbol = VerbRegistry.TokenTypeToVerbName(leftTokens[1].Type);
                applyAndAssignNode.Children.Add(ASTNode.MakeLiteral(new SymbolValue(opSymbol)));
                
                // Parse right side expression (may be null for monadic case)
                var applyRightNode = ParseRightSideExpression(rightTokens);
                if (applyRightNode == null)
                {
                    // Monadic case: no right argument provided (e.g., x?:)
                    // Use NullValue as placeholder - evaluator will handle monadic dispatch
                    applyRightNode = ASTNode.MakeLiteral(new NullValue());
                }
                applyAndAssignNode.Children.Add(applyRightNode);
                return applyAndAssignNode;
            }
            
            // Case 3: indexed apply-and-assign: IDENTIFIER [indices...] VERB COLON expression
            // e.g., a[1;0 2]+:100 — amend a at index path [1; 0 2] by applying + with 100
            if (assignmentToken.Type == TokenType.COLON &&
                leftTokens.Count >= 4 &&
                leftTokens[0].Type == TokenType.IDENTIFIER &&
                leftTokens[1].Type == TokenType.LEFT_BRACKET &&
                VerbRegistry.IsVerbToken(leftTokens[leftTokens.Count - 1].Type))
            {
                // Find the matching right bracket
                int depth = 0;
                int rightBracket = -1;
                for (int i = 1; i < leftTokens.Count; i++)
                {
                    if (leftTokens[i].Type == TokenType.LEFT_BRACKET) depth++;
                    else if (leftTokens[i].Type == TokenType.RIGHT_BRACKET)
                    {
                        depth--;
                        if (depth == 0) { rightBracket = i; break; }
                    }
                }
                // The verb should be right after the closing bracket
                if (rightBracket != -1 && rightBracket == leftTokens.Count - 2 &&
                    VerbRegistry.IsVerbToken(leftTokens[rightBracket + 1].Type))
                {
                    var varName = leftTokens[0].Lexeme;
                    var indexTokens = leftTokens.GetRange(2, rightBracket - 2);
                    var verbToken = leftTokens[rightBracket + 1];
                    var opSymbol = VerbRegistry.TokenTypeToVerbName(verbToken.Type);
                    
                    // Split index tokens by semicolons to support multi-level paths like a[1;0 2]
                    var indexParts = SplitByTopLevelSemicolons(indexTokens);
                    ASTNode indexNode;
                    if (indexParts.Count == 1)
                    {
                        var parsed = ParseRightSideExpression(indexParts[0]);
                        if (parsed == null) return null;
                        indexNode = parsed;
                    }
                    else
                    {
                        var indexLevels = new List<ASTNode>();
                        foreach (var part in indexParts)
                        {
                            var parsed = ParseRightSideExpression(part);
                            if (parsed != null) indexLevels.Add(parsed);
                        }
                        indexNode = new ASTNode(ASTNodeType.Block, null, indexLevels);
                    }
                    
                    var applyRightNode = ParseRightSideExpression(rightTokens);
                    if (applyRightNode == null)
                        return null;
                    
                    var indexedAmendNode = new ASTNode(ASTNodeType.ApplyAndAssign);
                    indexedAmendNode.Value = new SymbolValue(varName);
                    indexedAmendNode.Children.Add(ASTNode.MakeLiteral(new SymbolValue(opSymbol)));
                    indexedAmendNode.Children.Add(applyRightNode);
                    indexedAmendNode.Children.Add(indexNode); // index as 3rd child
                    return indexedAmendNode;
                }
            }
            
            // Case 4: simple indexed assignment: IDENTIFIER [indices...] COLON expression
            // e.g., x[]:0 — amend all elements of x with 0
            // e.g., x[1]:0 — amend element at index 1 with 0
            // e.g., x[1;2]:0 — amend nested element at path [1;2] with 0
            if (assignmentToken.Type == TokenType.COLON &&
                leftTokens.Count >= 3 &&
                leftTokens[0].Type == TokenType.IDENTIFIER &&
                leftTokens[1].Type == TokenType.LEFT_BRACKET)
            {
                // Find the matching right bracket
                int depth = 0;
                int rightBracket = -1;
                for (int i = 1; i < leftTokens.Count; i++)
                {
                    if (leftTokens[i].Type == TokenType.LEFT_BRACKET) depth++;
                    else if (leftTokens[i].Type == TokenType.RIGHT_BRACKET)
                    {
                        depth--;
                        if (depth == 0) { rightBracket = i; break; }
                    }
                }
                // The right bracket should be the last token in leftTokens
                if (rightBracket != -1 && rightBracket == leftTokens.Count - 1)
                {
                    var varName = leftTokens[0].Lexeme;
                    var indexTokens = leftTokens.GetRange(2, rightBracket - 2);
                    
                    // Parse the index (empty tokens = null index = all elements)
                    ASTNode? indexNode = null;
                    if (indexTokens.Count > 0)
                    {
                        // Split index tokens by semicolons to support multi-level paths
                        var indexParts = SplitByTopLevelSemicolons(indexTokens);
                        
                        // Check if any part is non-empty (has actual index values)
                        bool hasNonEmptyPart = indexParts.Any(p => p.Count > 0);
                        
                        if (indexParts.Count == 1 && hasNonEmptyPart)
                        {
                            // Single index value
                            var parsed = ParseRightSideExpression(indexParts[0]);
                            if (parsed == null) return null;
                            indexNode = parsed;
                        }
                        else if (indexParts.Count > 1 || hasNonEmptyPart)
                        {
                            // Multi-level path (e.g., [1;2] or [;0] or [0;])
                            // Empty parts represent "all elements" at that dimension (null index)
                            var indexLevels = new List<ASTNode>();
                            foreach (var part in indexParts)
                            {
                                if (part.Count > 0)
                                {
                                    var parsed = ParseRightSideExpression(part);
                                    if (parsed != null) indexLevels.Add(parsed);
                                }
                                else
                                {
                                    // Empty part means "all elements" at this dimension - add NullValue marker
                                    indexLevels.Add(ASTNode.MakeLiteral(new NullValue()));
                                }
                            }
                            // Create Block with all index levels (including NullValue markers for empty parts)
                            indexNode = new ASTNode(ASTNodeType.Block, null, indexLevels);
                        }
                        // else: single empty part (e.g., []), indexNode stays null meaning "all elements"
                    }
                    else
                    {
                        // Empty brackets [] means "all elements" - create a NullValue index node
                        indexNode = ASTNode.MakeLiteral(new NullValue());
                    }
                    // else: indexNode stays null, meaning "all elements"
                    
                    var assignRightNode = ParseRightSideExpression(rightTokens);
                    if (assignRightNode == null)
                        return null;
                    
                    // Create indexed assignment as ApplyAndAssign with colon (direct assignment marker)
                    var indexedAssignNode = new ASTNode(ASTNodeType.ApplyAndAssign);
                    indexedAssignNode.Value = new SymbolValue(varName);
                    indexedAssignNode.Children.Add(ASTNode.MakeLiteral(new SymbolValue(":"))); // direct assignment marker
                    indexedAssignNode.Children.Add(assignRightNode);
                    if (indexNode != null)
                        indexedAssignNode.Children.Add(indexNode); // index as 3rd child
                    return indexedAssignNode;
                }
            }
            
            // Parse left side (variable name)
            var variableNode = ParseVariableName(leftTokens);
            if (variableNode == null)
                return null;
            
            // Register variable as defined during parsing (per spec)
            // This allows subsequent expressions to reference this variable before evaluation
            if (variableNode.Value is SymbolValue symbolValue)
            {
                parentParser.RegisterDefinedVariable(symbolValue.Value);
            }
            
            // Parse right side (expression)
            var rightNode = ParseRightSideExpression(rightTokens);
            if (rightNode == null)
                return null;
            
            // Create assignment node
            if (assignmentToken.Type == TokenType.GLOBAL_ASSIGNMENT)
            {
                var globalAssignNode = new ASTNode(ASTNodeType.GlobalAssignment);
                globalAssignNode.Value = variableNode.Value; // Variable name
                globalAssignNode.Children.Add(rightNode);
                return globalAssignNode;
            }
            
            var assignmentNode = new ASTNode(ASTNodeType.Assignment);
            assignmentNode.Value = variableNode.Value; // Variable name
            assignmentNode.Children.Add(rightNode);
            
            // Mark whether this is a terminal (pure) or inline assignment
            bool isTerminalAssignment = IsTerminalAssignment(tokens);
            assignmentNode.IsTerminalAssignment = isTerminalAssignment;
            
            return assignmentNode;
        }
        
        /// <summary>
        /// Parse apply and assign statement (e.g., i+:1, x:*2, etc.)
        /// </summary>
        private ASTNode? ParseApplyAndAssignStatement(List<Token> leftTokens, Token assignmentToken, List<Token> rightTokens)
        {
            // Parse left side (variable name)
            var variableNode = ParseVariableName(leftTokens);
            if (variableNode == null)
                return null;
            
            // Note: Do NOT register variable for apply-and-assign operations
            // Apply-and-assign (i+:1) needs to access the existing variable first,
            // so the variable must already be defined. Only regular assignments (i:1)
            // should register new variables in the tracker.
            
            // Parse right side (expression)
            var rightNode = ParseRightSideExpression(rightTokens);
            if (rightNode == null)
                return null;
            
            // Extract the operator (everything except the colon)
            string operatorSymbol = assignmentToken.Lexeme.Substring(0, assignmentToken.Lexeme.Length - 1);
            
            // Create apply and assign node
            var applyAndAssignNode = new ASTNode(ASTNodeType.ApplyAndAssign);
            applyAndAssignNode.Value = variableNode.Value; // Variable name
            applyAndAssignNode.Children.Add(ASTNode.MakeLiteral(new SymbolValue(operatorSymbol)));
            applyAndAssignNode.Children.Add(rightNode);
            
            return applyAndAssignNode;
        }
        
        /// <summary>
        /// Parse conditional evaluation :[...]
        /// </summary>
        private ASTNode? ParseConditionalEvaluation(List<Token> tokens)
        {
            if (tokens.Count < 3 || tokens[1].Type != TokenType.LEFT_BRACKET)
                return null;
            
            // Find the matching right bracket
            int bracketDepth = 1;
            int rightBracketIndex = -1;
            
            for (int i = 2; i < tokens.Count && bracketDepth > 0; i++)
            {
                if (tokens[i].Type == TokenType.LEFT_BRACKET)
                    bracketDepth++;
                else if (tokens[i].Type == TokenType.RIGHT_BRACKET)
                    bracketDepth--;
                    
                if (bracketDepth == 0)
                {
                    rightBracketIndex = i;
                    break;
                }
            }
            
            if (rightBracketIndex == -1)
                return null;
            
            // Extract the content inside brackets
            var contentTokens = tokens.GetRange(2, rightBracketIndex - 2);
            var arguments = ParseBracketArguments(contentTokens);
            
            // Create conditional evaluation node
            var conditionalNode = new ASTNode(ASTNodeType.ConditionalStatement);
            conditionalNode.Value = new SymbolValue(":");
            
            // Add parsed arguments as children
            foreach (var arg in arguments)
            {
                conditionalNode.Children.Add(arg);
            }
            
            return conditionalNode;
        }
        
        /// <summary>
        /// Parse do[...] statement
        /// </summary>
        private ASTNode? ParseDoStatement(List<Token> tokens)
        {
            return ParseControlFlowStatement(tokens, "do");
        }
        
        /// <summary>
        /// Parse if[...] statement
        /// </summary>
        private ASTNode? ParseIfStatement(List<Token> tokens)
        {
            return ParseControlFlowStatement(tokens, "if");
        }
        
        /// <summary>
        /// Parse while[...] statement
        /// </summary>
        private ASTNode? ParseWhileStatement(List<Token> tokens)
        {
            return ParseControlFlowStatement(tokens, "while");
        }
        
        /// <summary>
        /// Parse control flow statement (do, if, while) with bracket arguments
        /// </summary>
        private ASTNode? ParseControlFlowStatement(List<Token> tokens, string statementType)
        {
            if (tokens.Count < 3 || tokens[1].Type != TokenType.LEFT_BRACKET)
                return null;
            
            // Find the matching right bracket
            int bracketDepth = 1;
            int rightBracketIndex = -1;
            
            for (int i = 2; i < tokens.Count && bracketDepth > 0; i++)
            {
                if (tokens[i].Type == TokenType.LEFT_BRACKET)
                    bracketDepth++;
                else if (tokens[i].Type == TokenType.RIGHT_BRACKET)
                    bracketDepth--;
                    
                if (bracketDepth == 0)
                {
                    rightBracketIndex = i;
                    break;
                }
            }
            
            if (rightBracketIndex == -1)
                return null;
            
            // Extract the content inside brackets
            var contentTokens = tokens.GetRange(2, rightBracketIndex - 2);
            var arguments = ParseBracketArguments(contentTokens);
            
            // Create control flow node
            var controlFlowNode = new ASTNode(ASTNodeType.ConditionalStatement);
            controlFlowNode.Value = new SymbolValue(statementType);
            
            // Add parsed arguments as children
            foreach (var arg in arguments)
            {
                controlFlowNode.Children.Add(arg);
            }
            
            return controlFlowNode;
        }
        
        /// <summary>
        /// Parse arguments inside brackets (separated by semicolons at top level only)
        /// Respects nested brackets, braces, and parentheses
        /// </summary>
        private List<ASTNode> ParseBracketArguments(List<Token> contentTokens)
        {
            var arguments = new List<ASTNode>();
            var currentArgTokens = new List<Token>();
            int depth = 0; // Track nesting depth of brackets/braces/parens
            
            foreach (var token in contentTokens)
            {
                // Track nesting depth
                if (token.Type == TokenType.LEFT_BRACKET || 
                    token.Type == TokenType.LEFT_BRACE || 
                    token.Type == TokenType.LEFT_PAREN)
                {
                    depth++;
                }
                else if (token.Type == TokenType.RIGHT_BRACKET || 
                         token.Type == TokenType.RIGHT_BRACE || 
                         token.Type == TokenType.RIGHT_PAREN)
                {
                    depth--;
                }
                
                // Only split on semicolons at depth 0 (top level)
                if (token.Type == TokenType.SEMICOLON && depth == 0)
                {
                    // End of current argument
                    if (currentArgTokens.Count > 0)
                    {
                        var argNode = ParseArgument(currentArgTokens);
                        if (argNode != null)
                            arguments.Add(argNode);
                        currentArgTokens.Clear();
                    }
                }
                else
                {
                    currentArgTokens.Add(token);
                }
            }
            
            // Add the last argument
            if (currentArgTokens.Count > 0)
            {
                var argNode = ParseArgument(currentArgTokens);
                if (argNode != null)
                    arguments.Add(argNode);
            }
            
            return arguments;
        }
        
        /// <summary>
        /// Parse a single argument expression
        /// </summary>
        private ASTNode? ParseArgument(List<Token> argTokens)
        {
            if (argTokens.Count == 0)
                return null;
                
            if (argTokens.Count == 1)
                return CreateNodeFromToken(argTokens[0]);
            
            // Use the parent parser to handle complex expressions
            return parentParser.BuildParseTreeFromRight(argTokens);
        }
        
        /// <summary>
        /// Split token list by top-level semicolons (respecting bracket/paren/brace depth)
        /// </summary>
        private List<List<Token>> SplitByTopLevelSemicolons(List<Token> tokens)
        {
            var parts = new List<List<Token>>();
            var current = new List<Token>();
            int depth = 0;
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.LEFT_PAREN || token.Type == TokenType.LEFT_BRACKET || token.Type == TokenType.LEFT_BRACE) depth++;
                else if (token.Type == TokenType.RIGHT_PAREN || token.Type == TokenType.RIGHT_BRACKET || token.Type == TokenType.RIGHT_BRACE) depth--;
                else if (token.Type == TokenType.SEMICOLON && depth == 0)
                {
                    parts.Add(current);
                    current = new List<Token>();
                    continue;
                }
                current.Add(token);
            }
            parts.Add(current);
            return parts;
        }

        /// <summary>
        /// Find the assignment operator in the token list
        /// </summary>
        private int FindAssignmentOperator(List<Token> tokens)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Type == TokenType.COLON ||
                    tokens[i].Type == TokenType.ASSIGNMENT ||
                    tokens[i].Type == TokenType.GLOBAL_ASSIGNMENT)
                {
                    return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// Parse variable name from left side tokens
        /// </summary>
        private ASTNode? ParseVariableName(List<Token> leftTokens)
        {
            // Variable name should be a single identifier or symbol
            if (leftTokens.Count == 1)
            {
                var token = leftTokens[0];
                if (token.Type == TokenType.IDENTIFIER || token.Type == TokenType.SYMBOL)
                {
                    return new ASTNode(ASTNodeType.Variable)
                    {
                        Value = new SymbolValue(token.Lexeme)
                    };
                }
            }
            return null;
        }
        
        /// <summary>
        /// Parse right side expression of assignment
        /// </summary>
        private ASTNode? ParseRightSideExpression(List<Token> rightTokens)
        {
            if (rightTokens.Count == 1)
                return CreateNodeFromToken(rightTokens[0]);
            
            // Use the parent parser to handle complex expressions
            // Choose the right method based on parser mode
            if (parentParser.BuildParseTree)
                return parentParser.BuildParseTreeFromRight(rightTokens);
            else
                return parentParser.EvaluateFromRight(rightTokens);
        }
        
        /// <summary>
        /// Check if this is a terminal (pure) assignment
        /// Terminal assignment returns null, inline assignment returns the assigned value
        /// </summary>
        private bool IsTerminalAssignment(List<Token> tokens)
        {
            // For a proper implementation, we need to analyze the context
            // to determine if there are verbs to the left of this assignment.
            // 
            // Terminal (pure) assignment: no verbs to the left between assignment and separator
            // Inline assignment: one or more verbs to the left between assignment and separator
            //
            // Since we don't have the full context here, we'll use a heuristic:
            // If the assignment starts at the beginning of the token list or is preceded by a separator,
            // it's likely terminal. Otherwise, it's likely inline.
            
            // For statement-level assignments (like 'a: 42' standing alone), treat as terminal
            // For inline assignments (like 'b + a: 42'), treat as inline
            
            // Simple heuristic: if the assignment is the first token or has a simple pattern, it's terminal
            if (tokens.Count >= 3 && 
                tokens[0].Type == TokenType.IDENTIFIER && 
                tokens[1].Type == TokenType.COLON)
            {
                // This is a simple assignment like 'a: 42'
                // If it's exactly 3 tokens (var: value), it's likely a terminal statement
                // If it's more complex, it might be inline
                return tokens.Count == 3;
            }
            
            // Default to terminal for safety
            return true;
        }
        
        /// <summary>
        /// Create AST node from atomic token
        /// </summary>
        private ASTNode CreateNodeFromToken(Token token)
        {
            return LRSAtomicParser.ParseAtomicToken(token, parentParser);
        }
    }
}
