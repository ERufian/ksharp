// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;

namespace K3CSharp
{
    /// <summary>
    /// Interface for parser modules
    /// </summary>
    public interface IParserModule
    {
        ASTNode? Parse(ParseContext context);
        bool CanHandle(TokenType currentToken);
    }

    /// <summary>
    /// Context object for parser operations
    /// </summary>
    public class ParseContext
    {
        public List<Token> Tokens { get; set; }
        public int Current { get; set; }
        public string SourceText { get; set; }
        public bool ParsingDotApplyArguments { get; set; }
        public int DelimiterDepth { get; set; }

        public ParseContext(List<Token> tokens, string sourceText)
        {
            Tokens = tokens;
            SourceText = sourceText;
            Current = 0;
            ParsingDotApplyArguments = false;
            DelimiterDepth = 0;
        }

        public Token CurrentToken() => Current < Tokens.Count ? Tokens[Current] : new Token(TokenType.EOF, "", Current);

        public Token PreviousToken() => Current > 0 ? Tokens[Current - 1] : new Token(TokenType.EOF, "", Current);

        public bool IsAtEnd() => Current >= Tokens.Count || (Current < Tokens.Count && Tokens[Current].Type == TokenType.EOF);

        public bool Match(TokenType tokenType)
        {
            if (Check(tokenType))
            {
                Current++;
                return true;
            }
            return false;
        }

        public bool Check(TokenType tokenType)
        {
            return !IsAtEnd() && CurrentToken().Type == tokenType;
        }

        public void Advance()
        {
            if (!IsAtEnd()) Current++;
        }
    }

    /// <summary>
    /// Expression parser module for handling ParseExpression, ParseTerm, and expression-level operations
    /// </summary>
    public class ExpressionParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // Expression parser handles most tokens except delimiters that start primary expressions
            return currentToken != TokenType.LEFT_PAREN && 
                   currentToken != TokenType.LEFT_BRACE && 
                   currentToken != TokenType.LEFT_BRACKET &&
                   currentToken != TokenType.RIGHT_PAREN &&
                   currentToken != TokenType.RIGHT_BRACE &&
                   currentToken != TokenType.RIGHT_BRACKET &&
                   currentToken != TokenType.EOF &&
                   currentToken != TokenType.NEWLINE;
        }

        public ASTNode? Parse(ParseContext context)
        {
            return ParseExpression(context);
        }

        private ASTNode? ParseExpression(ParseContext context)
        {
            var left = ParseTerm(context);

            // Handle case where ParseTerm returns null (e.g., due to NEWLINE at start)
            if (left == null)
            {
                return null;
            }

            // Handle dyadic operators with Long Right Scope (right-associative, equal precedence)
            while (!context.IsAtEnd() && IsDyadicOperator(context.CurrentToken().Type))
            {
                var opToken = context.CurrentToken();
                context.Advance();
                
                var right = ParseTerm(context);
                if (right == null)
                {
                    // This is a projection - create a ProjectedFunction node
                    var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                    
                    // Convert token type to operator symbol
                    var operatorSymbol = opToken.Type switch
                    {
                        TokenType.PLUS => "+",
                        TokenType.MINUS => "-",
                        TokenType.MULTIPLY => "*",
                        TokenType.DIVIDE => "%",
                        TokenType.MIN => "&",
                        TokenType.MAX => "|",
                        TokenType.LESS => "<",
                        TokenType.GREATER => ">",
                        TokenType.EQUAL => "=",
                        TokenType.IN => "_in",
                        TokenType.BIN => "_bin",
                        TokenType.BINL => "_binl",
                        TokenType.LIN => "_lin",
                        TokenType.LSQ => "_lsq",
                        TokenType.DV => "_dv",
                        TokenType.DVL => "_dvl",
                        TokenType.DI => "_di",
                        TokenType.VS => "_vs",
                        TokenType.SV => "_sv",
                        TokenType.SS => "_ss",
                        TokenType.SM => "_sm",
                        TokenType.CI => "_ci",
                        TokenType.IC => "_ic",
                        TokenType.POWER => "^",
                        TokenType.MODULUS => "!",
                        TokenType.JOIN => ",",
                        TokenType.COLON => ":",
                        TokenType.HASH => "#",
                        TokenType.UNDERSCORE => "_",
                        TokenType.QUESTION => "?",
                        TokenType.DOLLAR => "$",
                        TokenType.TYPE => "@",
                        TokenType.STRING_REPRESENTATION => "$",
                        TokenType.APPLY => "@",
                        TokenType.DOT_APPLY => "_dot",
                        _ => opToken.Lexeme
                    };
                    
                    projectedNode.Value = new SymbolValue(operatorSymbol);
                    
                    // Determine arity from VerbRegistry - use highest supported arity as default
                    var verb = VerbRegistry.GetVerb(operatorSymbol);
                    int defaultArity = verb?.SupportedArities.Max() ?? 2;
                    projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(defaultArity)));
                    
                    // Add the left operand if we have one
                    if (left != null)
                    {
                        projectedNode.Children.Add(left);
                    }
                    
                    return projectedNode;
                }

                // Create dyadic operation node
                left = ASTNode.MakeDyadicOp(opToken.Type, left, right);
            }

            return left;
        }

        private ASTNode? ParseTerm(ParseContext context, bool parseUntilEnd = false)
        {
            // Only return null for EOF, not for NEWLINE when parsing expressions
            // NEWLINE should be handled at higher levels as statement separators
            if (context.IsAtEnd())
            {
                return null;
            }

            ASTNode? result;
            
            // Check if current token is an adverb - if so, we're in a nesting context
            if (!context.IsAtEnd() && VerbRegistry.IsAdverbToken(context.CurrentToken().Type))
            {
                // Parse the primary expression normally - adverb will be handled in ParseTerm
                result = ParsePrimary(context);
            }
            else
            {
                result = ParsePrimary(context);
            }

            // Handle case where ParsePrimary returned null
            if (result == null)
            {
                return null;
            }

            // Handle adverbs
            if (!context.IsAtEnd() && VerbRegistry.IsAdverbToken(context.CurrentToken().Type))
            {
                var adverbType = context.CurrentToken().Type;
                context.Advance(); // Consume the adverb token
                
                // Parse the right argument for the adverb
                var rightArg = ParseTerm(context, parseUntilEnd);
                
                // Create adverb node: ADVERB(adverbType, verb, rightArg)
                var adverbNode = new ASTNode(ASTNodeType.DyadicOp);
                adverbNode.Value = new SymbolValue(adverbType.ToString());
                adverbNode.Children.Add(result!); // verb
                adverbNode.Children.Add(rightArg!); // right argument
                
                result = adverbNode;
            }

            return result;
        }

        private ASTNode? ParsePrimary(ParseContext context)
        {
            // Delegate to PrimaryParser for primary expression parsing
            var primaryParser = new PrimaryParser();
            if (primaryParser.CanHandle(context.CurrentToken().Type))
            {
                return primaryParser.Parse(context);
            }
            
            throw new Exception($"Unexpected token: {context.CurrentToken().Type}({context.CurrentToken().Lexeme})");
        }

        private bool IsDyadicOperator(TokenType tokenType)
        {
            return VerbRegistry.IsDyadicOperator(tokenType);
        }

        }
}