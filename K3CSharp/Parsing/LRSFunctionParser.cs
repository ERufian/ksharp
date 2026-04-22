using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Function call parsing for LRS parser
    /// Handles function calls, special verbs, and anonymous functions without PrimaryParser dependency
    /// Supports verb-agnostic function parsing using VerbRegistry categories
    /// </summary>
    public class LRSFunctionParser
    {
        private readonly List<Token> tokens;
        private readonly bool buildParseTree;
        
        public LRSFunctionParser(List<Token> tokens, bool buildParseTree = false)
        {
            this.tokens = tokens;
            this.buildParseTree = buildParseTree;
        }
        
        /// <summary>
        /// Parse function call from tokens
        /// </summary>
        /// <param name="tokens">Tokens to parse</param>
        /// <returns>AST node representing function call</returns>
        public ASTNode ParseFunctionCall(List<Token> tokens)
        {
            if (tokens.Count == 0)
                throw new ArgumentException("Cannot parse empty function call");
                
            var funcToken = tokens[0];
            
            // Lambda expressions need legacy parser (LRS doesn't have lambda parsing yet)
            if (funcToken.Type == TokenType.LEFT_BRACE)
            {
                var result = DelegateToLegacyParser(tokens);
                if (result == null)
                    throw new Exception("Failed to parse lambda expression");
                return result;
            }
            
            // Check if this is a special function (_parse, _eval, etc.)
            if (IsSpecialFunction(funcToken.Type))
            {
                var argTokens = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : new List<Token>();
                return HandleSpecialFunction(funcToken, argTokens);
            }
            
            // Check if this is a system function using VerbRegistry
            if (IsSystemFunction(funcToken.Type))
            {
                var argTokens = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : new List<Token>();
                return HandleSystemFunction(funcToken, argTokens);
            }
            
            // Check if this is a system operator (like _gtime, _ltime, _getenv, _db, _bd, etc.)
            if (IsSystemOperator(funcToken.Type))
            {
                var argTokens = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : new List<Token>();
                return HandleSystemOperator(funcToken, argTokens);
            }
            
            // Parse as regular function call
            var regularArgTokens = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : new List<Token>();
            return ParseRegularFunction(funcToken, regularArgTokens);
        }
        
        /// <summary>
        /// Handle special functions like _parse and _eval
        /// </summary>
        private ASTNode HandleSpecialFunction(Token funcToken, List<Token> argTokens)
        {
            // Parse argument using LRS parser
            var argument = ParseArgumentWithLRS(argTokens);
            
            var funcCall = new ASTNode(ASTNodeType.FunctionCall);
            funcCall.Children.Add(ASTNode.MakeVariable(funcToken.Lexeme));
            if (argument != null)
                funcCall.Children.Add(argument);
                
            return funcCall;
        }
        
        /// <summary>
        /// Handle system functions using VerbRegistry
        /// </summary>
        private ASTNode HandleSystemFunction(Token funcToken, List<Token> argTokens)
        {
            // Get function info from VerbRegistry
            var verbInfo = OperatorDetector.GetVerbInfo(funcToken.Type);
            if (verbInfo == null)
                throw new Exception($"Unknown system function: {funcToken.Lexeme}");
            
            // Parse arguments based on supported arities
            var arguments = ParseFunctionArguments(argTokens, verbInfo.SupportedArities);
            
            var funcCall = new ASTNode(ASTNodeType.FunctionCall);
            funcCall.Children.Add(ASTNode.MakeVariable(funcToken.Lexeme));
            funcCall.Children.AddRange(arguments);
                
            return funcCall;
        }
        
        /// <summary>
        /// Parse regular function call
        /// </summary>
        private ASTNode ParseRegularFunction(Token funcToken, List<Token> argTokens)
        {
            // Parse argument using LRS parser
            var argument = ParseArgumentWithLRS(argTokens);
            
            var funcCall = new ASTNode(ASTNodeType.FunctionCall);
            funcCall.Children.Add(ASTNode.MakeVariable(funcToken.Lexeme));
            if (argument != null)
                funcCall.Children.Add(argument);
                
            return funcCall;
        }
        
        /// <summary>
        /// Parse function arguments based on supported arities
        /// </summary>
        private List<ASTNode> ParseFunctionArguments(List<Token> argTokens, int[] supportedArities)
        {
            var arguments = new List<ASTNode>();
            
            if (argTokens.Count == 0)
                return arguments;
            
            // For simplicity, parse all arguments as a single expression
            // In a more complex implementation, we'd parse based on arity requirements
            var argument = ParseArgumentWithLRS(argTokens);
            if (argument != null)
                arguments.Add(argument);
            
            return arguments;
        }
        
        /// <summary>
        /// Parse anonymous function from braces
        /// </summary>
        public ASTNode ParseAnonymousFunction(List<Token> tokens)
        {
            if (tokens.Count < 2 || tokens[0].Type != TokenType.LEFT_BRACE)
                throw new Exception("Expected '{' for anonymous function");
            
            // Use grouping parser to parse function body
            var groupingParser = new LRSGroupingParser(tokens, buildParseTree);
            var position = 0;
            return groupingParser.ParseBraces(ref position);
        }
        
        /// <summary>
        /// Parse function parameters from bracket list
        /// </summary>
        public List<string> ParseParameterList(List<Token> paramTokens)
        {
            var parameters = new List<string>();
            
            for (int i = 0; i < paramTokens.Count; i++)
            {
                var token = paramTokens[i];
                
                if (token.Type == TokenType.IDENTIFIER)
                {
                    parameters.Add(token.Lexeme);
                }
                else if (token.Type == TokenType.SEMICOLON)
                {
                    // Parameter separator - continue
                    continue;
                }
                else
                {
                    throw new Exception($"Invalid parameter token: {token.Type}({token.Lexeme})");
                }
            }
            
            return parameters;
        }
        
        /// <summary>
        /// Parse argument tokens using LRS parser
        /// </summary>
        private ASTNode? ParseArgumentWithLRS(List<Token> argTokens)
        {
            if (argTokens.Count == 0) return null;
            if (argTokens.Count == 1) return CreateNodeFromToken(argTokens[0]);
            
            // Use LRS parser for complex arguments
            var lrsParser = new LRSParser(argTokens);
            var position = 0;
            return lrsParser.ParseExpression(ref position);
        }
        
        /// <summary>
        /// Check if token type represents a special function (_parse, _eval, etc.)
        /// These are registered as SystemFunction in the VerbRegistry
        /// </summary>
        private bool IsSpecialFunction(TokenType tokenType)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            return verb?.Type == VerbType.SystemFunction;
        }
        
        /// <summary>
        /// Check if token type represents a system function using VerbRegistry
        /// </summary>
        private bool IsSystemFunction(TokenType tokenType)
        {
            return OperatorDetector.IsFunction(tokenType);
        }
        
        /// <summary>
        /// Check if token type represents a system operator (like _gtime, _ltime, etc.)
        /// System operators are registered as VerbType.SystemFunction with names starting with "_"
        /// AND must support monadic arity (function-style calls like _gtime 0)
        /// </summary>
        private bool IsSystemOperator(TokenType tokenType)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            
            // System operators are system functions 
            // AND must support monadic arity (function-style calls)
            return verb?.Type == VerbType.SystemFunction && 
                   verb.SupportedArities.Contains(1);
        }
        
        /// <summary>
        /// Handle system operators (like _gtime, _ltime, _getenv, etc.)
        /// These are registered as operators but function like system calls
        /// </summary>
        private ASTNode HandleSystemOperator(Token funcToken, List<Token> argTokens)
        {
            // Parse argument using LRS parser
            var argument = ParseArgumentWithLRS(argTokens);
            
            var funcCall = new ASTNode(ASTNodeType.FunctionCall);
            funcCall.Children.Add(ASTNode.MakeVariable(funcToken.Lexeme));
            if (argument != null)
                funcCall.Children.Add(argument);
                
            return funcCall;
        }
        
        /// <summary>
        /// Create AST node from atomic token using LRSAtomicParser
        /// </summary>
        private ASTNode CreateNodeFromToken(Token token)
        {
            return LRSAtomicParser.ParseAtomicToken(token);
        }
        
        /// <summary>
        /// Delegate to legacy parser for lambda expressions
        /// LRS doesn't have lambda parsing yet, so we use the legacy FunctionParser
        /// </summary>
        private ASTNode? DelegateToLegacyParser(List<Token> tokens)
        {
            // Create a parse context for the legacy parser
            // We need to reconstruct the source text from tokens for ParseContext
            var sourceText = string.Join(" ", tokens.Select(t => t.Lexeme));
            var context = new ParseContext(tokens, sourceText);
            var parser = new FunctionParser();
            return parser.Parse(context);
        }
        
        /// <summary>
        /// Check if token could be a function using VerbRegistry
        /// </summary>
        public static bool CouldBeFunction(TokenType tokenType)
        {
            // Lambda expressions are functions
            if (tokenType == TokenType.LEFT_BRACE)
                return true;
            
            // Check if it's a registered verb
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            
            // System functions and system variables are always treated as functions
            // System variables like _f need to be recognized as functions for recursion
            if (verb != null && (verb.Type == VerbType.SystemFunction || verb.Type == VerbType.SystemVariable))
                return true;
            
            // Only treat as function if it's NOT a monadic operator
            // Monadic operators should be handled by the monadic parser
            if (verb != null && OperatorDetector.SupportsMonadic(tokenType))
                return false;
            
            return verb != null;
        }
    }
}
