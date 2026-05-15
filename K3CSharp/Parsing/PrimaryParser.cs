// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    /// <summary>
    /// Primary parser module for handling literals, identifiers, and monadic operations
    /// </summary>
    public class PrimaryParser : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // Primary parser handles literals, identifiers, and monadic operators
            return currentToken switch
            {
                TokenType.INTEGER or TokenType.LONG or TokenType.FLOAT or TokenType.CHARACTER or 
                TokenType.CHARACTER_VECTOR or TokenType.SYMBOL or TokenType.IDENTIFIER or
                TokenType.LEFT_PAREN or TokenType.LEFT_BRACE or TokenType.LEFT_BRACKET or
                TokenType.PLUS or TokenType.MINUS or TokenType.MULTIPLY or TokenType.DIVIDE or
                TokenType.MODULUS or TokenType.POWER or TokenType.MIN or TokenType.MAX or
                TokenType.MATCH or TokenType.NOT or TokenType.HASH or TokenType.UNDERSCORE or
                TokenType.QUESTION or TokenType.DOLLAR or TokenType.APPLY or
                TokenType.JOIN or // Add JOIN for monadic ENLIST operations
                TokenType.NULL => true,
                _ => false
            };
        }

        public ASTNode? Parse(ParseContext context)
        {
            var token = context.CurrentToken();
            
            var result = token.Type switch
            {
                TokenType.INTEGER => ParseInteger(context),
                TokenType.LONG => ParseLong(context),
                TokenType.FLOAT => ParseFloat(context),
                TokenType.CHARACTER => ParseCharacter(context),
                TokenType.CHARACTER_VECTOR => ParseString(context),
                TokenType.SYMBOL => ParseSymbol(context),
                TokenType.IDENTIFIER => ParseIdentifier(context),
                TokenType.LEFT_PAREN => ParseParentheses(context),
                TokenType.LEFT_BRACE => ParseBraces(context),
                TokenType.LEFT_BRACKET => ParseBrackets(context),
                TokenType.NULL => ParseNull(context),
                TokenType.PLUS => ParseMonadicPlus(context),
                TokenType.MINUS => ParseMonadicMinus(context),
                TokenType.MULTIPLY => ParseMonadicFirst(context),
                TokenType.DIVIDE => ParseMonadicReciprocal(context),
                TokenType.MODULUS => ParseMonadicReciprocal(context),
                TokenType.POWER => ParseMonadicPower(context),
                TokenType.JOIN => ParseMonadicJoin(context), // Add monadic ENLIST
                TokenType.MIN => ParseMonadicMin(context),
                TokenType.MAX => ParseMonadicMax(context),
                TokenType.MATCH => ParseMonadicMatch(context),
                TokenType.NOT => ParseMonadicNot(context),
                TokenType.HASH => ParseMonadicCount(context),
                TokenType.APPLY => ParseMonadicApply(context),
                _ => throw new Exception($"Unexpected token: {token.Type}({token.Lexeme})")
            };

            return result;
        }

        private ASTNode ParseInteger(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            
            // Handle special integer values per K specification
            if (lexeme == "0I" || lexeme == "-0I")
                return ASTNode.MakeLiteral(new IntegerValue(lexeme));
            
            if (int.TryParse(lexeme, out int intValue))
            {
                // Convert extreme values to special values per spec
                // Values >= int32 max become 0I (positive infinity)
                if (intValue >= 2147483647)
                    return ASTNode.MakeLiteral(new IntegerValue("0I"));
                // Values <= -2147483647 become -0I (negative infinity)
                if (intValue <= -2147483647)
                    return ASTNode.MakeLiteral(new IntegerValue("-0I"));
                return ASTNode.MakeLiteral(new IntegerValue(intValue));
            }
            
            throw new ArgumentException($"Invalid integer literal: {lexeme}");
        }

        private ASTNode ParseLong(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            
            // Handle special long values per K specification
            if (lexeme == "0Ij")
                return ASTNode.MakeLiteral(new LongValue(long.MaxValue));
            if (lexeme == "-0Ij")
                return ASTNode.MakeLiteral(new LongValue(-long.MaxValue));
            if (lexeme == "0Nj")
                return ASTNode.MakeLiteral(new LongValue(long.MinValue));
            
            // Parse with bounds checking
            var numberPart = lexeme.Substring(0, lexeme.Length - 1); // Remove 'j'
            double parsedValue = double.Parse(numberPart);
            
            if (parsedValue >= long.MaxValue)
                return ASTNode.MakeLiteral(new LongValue(long.MaxValue));
            else if (parsedValue <= -long.MaxValue)
                return ASTNode.MakeLiteral(new LongValue(-long.MaxValue));
            else
                return ASTNode.MakeLiteral(new LongValue(long.Parse(numberPart)));
        }

        private ASTNode ParseFloat(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            
            // Handle special float values
            if (lexeme == "0i" || lexeme == "0n" || lexeme == "-0i")
                return ASTNode.MakeLiteral(new FloatValue(lexeme));
            
            return ASTNode.MakeLiteral(new FloatValue(double.Parse(lexeme)));
        }

        private ASTNode ParseCharacter(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            
            // Handle escape sequences in quoted format (e.g., '\n', '\t')
            if (lexeme.Length == 3 && lexeme[0] == '\'' && lexeme[2] == '\'')
            {
                char c = lexeme[1];
                var charValue = c switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    '\'' => '\'',
                    _ => c
                };
                return ASTNode.MakeLiteral(new CharacterValue(charValue.ToString()));
            }
            
            // Handle unquoted single character (e.g., "f", "a" from double-quoted strings)
            if (lexeme.Length == 1)
            {
                return ASTNode.MakeLiteral(new CharacterValue(lexeme));
            }
            
            throw new ArgumentException($"Invalid character literal: {lexeme}");
        }

        private ASTNode ParseString(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            
            // Remove quotes and handle escape sequences
            if (lexeme.Length >= 2 && lexeme[0] == '"' && lexeme[^1] == '"')
            {
                var stringValue = lexeme.Substring(1, lexeme.Length - 2);
                
                // According to spec:
                // - If length is 1, produce a character
                // - If length is other than 1 (including 0 or >1), produce a character vector
                if (stringValue.Length == 0)
                    return ASTNode.MakeLiteral(new VectorValue(new List<K3Value>(), -3)); // Empty character vector
                else if (stringValue.Length == 1)
                    return ASTNode.MakeLiteral(new CharacterValue(stringValue)); // Single character
                else
                {
                    var charValues = stringValue.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
                    return ASTNode.MakeLiteral(new VectorValue(charValues)); // Character vector
                }
            }
            
            throw new ArgumentException($"Invalid string literal: {lexeme}");
        }

        private ASTNode ParseSymbol(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            var lexeme = token.Lexeme;
            
            // Remove backticks for symbol value
            var symbolValue = lexeme.Trim('`');
            return ASTNode.MakeLiteral(new SymbolValue(symbolValue));
        }

        private ASTNode ParseIdentifier(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            return ASTNode.MakeVariable(token.Lexeme);
        }

        private ASTNode ParseParentheses(ParseContext context)
        {
            context.Advance(); // Consume '('

            // Parse expressions inside parentheses according to K specification
            // Semicolons and newlines are expression separators
            // Commas are OPERATORS (join/enlist), not separators
            var elements = new List<ASTNode>();
            
            // Handle empty parentheses () - should be treated as 1 expression of value null
            if (context.Check(TokenType.RIGHT_PAREN))
            {
                context.Advance(); // Consume ')'
                return ASTNode.MakeLiteral(new NullValue());
            }
            
            // Parse expressions sequentially from left to right
            bool lastWasSeparator = false;
            while (!context.IsAtEnd() && !context.Check(TokenType.RIGHT_PAREN))
            {
                // Skip newlines as they are also expression separators
                while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                {
                    context.Advance();
                }
                
                // Check for empty expressions (consecutive separators)
                // Multiple consecutive semicolons each represent an empty position
                while (context.CurrentToken().Type == TokenType.SEMICOLON || 
                    context.CurrentToken().Type == TokenType.NEWLINE)
                {
                    elements.Add(ASTNode.MakeLiteral(new NullValue()));
                    context.Advance(); // Consume the separator
                    lastWasSeparator = true;
                }
                
                // Parse the expression (commas are operators, so they're handled as part of expressions)
                var expr = ParseExpressionInGrouping(context);
                if (expr != null)
                {
                    elements.Add(expr);
                }
                else
                {
                    // Failed to parse expression - this means empty position
                    // Only add null if we're not at RIGHT_PAREN (trailing empty handled separately)
                    if (!context.Check(TokenType.RIGHT_PAREN))
                    {
                        elements.Add(ASTNode.MakeLiteral(new NullValue()));
                    }
                }
                
                // Check for separator (semicolon or newline only - commas are operators!)
                if (context.Match(TokenType.SEMICOLON))
                {
                    lastWasSeparator = true;
                    continue; // Next expression
                }
                else if (context.Match(TokenType.NEWLINE))
                {
                    lastWasSeparator = true;
                    continue; // Next expression
                }
                else if (context.Check(TokenType.RIGHT_PAREN))
                {
                    break; // End of parentheses
                }
                else
                {
                    // No separator and not at end - this should not happen in valid K
                    break;
                }
            }
            
            context.Advance(); // Consume ')'
            
            // If the last token before ')' was a separator, add a null for the trailing empty expression
            // This handles cases like (a;) and (;;)
            if (lastWasSeparator)
            {
                elements.Add(ASTNode.MakeLiteral(new NullValue()));
            }
            
            // Apply K specification rules for the result
            if (elements.Count == 1)
            {
                // If the content has 1 expression, return the value of the expression
                return elements[0];
            }
            else if (elements.Count > 1)
            {
                // If the content has more than 1 expression, generate a list
                // The evaluator will handle vector collapsing if all elements have the same type
                return ASTNode.MakeVector(elements);
            }
            else
            {
                // Should not reach here due to empty parentheses handling above
                return ASTNode.MakeLiteral(new NullValue());
            }
        }

        private ASTNode? ParseBracketArgument(ParseContext context)
        {
            // Check if we have a bracket expression
            if (context.CurrentToken().Type == TokenType.LEFT_BRACKET)
            {
                // Parse bracket contents directly to avoid recursion
                context.Advance(); // Consume '['
                
                // Try LRS-based parsing first (simplified integration)
                try
                {
                    var lrsResult = TestLRSIntegration(context);
                    if (lrsResult != null)
                    {
                        if (!context.Match(TokenType.RIGHT_BRACKET))
                        {
                            throw new Exception("Expected ']' after bracket expression");
                        }
                        return lrsResult;
                    }
                }
                catch
                {
                    // Fall back to original parsing if LRS fails
                }
                
                // Simple case: just parse the contents as a single expression
                var expressionParser = new ExpressionParser();
                var result = expressionParser.Parse(context);
                
                if (!context.Match(TokenType.RIGHT_BRACKET))
                {
                    throw new Exception("Expected ']' after bracket expression");
                }
                
                return result;
            }
            
            // Use expression parser for other cases
            var expressionParser2 = new ExpressionParser();
            if (expressionParser2.CanHandle(context.CurrentToken().Type))
            {
                return expressionParser2.Parse(context);
            }
            
            throw new Exception("Expected expression in brackets");
        }

        /// <summary>
        /// Test LRS Integration using simplified approach without circular dependencies
        /// </summary>
        private ASTNode? TestLRSIntegration(ParseContext context)
        {
            // Extract tokens from current position to matching ]
            var tokens = new List<Token>();
            var depth = 1;
            var startPos = context.Current;
            
            while (context.Current < context.Tokens.Count && depth > 0)
            {
                var token = context.Tokens[context.Current];
                tokens.Add(token);
                
                if (token.Type == TokenType.LEFT_BRACKET) depth++;
                else if (token.Type == TokenType.RIGHT_BRACKET) depth--;
                
                context.Current++;
            }
            
            // Remove the final ] from tokens
            if (tokens.Count > 0 && tokens.Last().Type == TokenType.RIGHT_BRACKET)
            {
                tokens.RemoveAt(tokens.Count - 1);
            }
            // Reset position for LRS processing
            context.Current = startPos;
            
            // Use simplified parsing that demonstrates LRS concept
            if (tokens.Count == 0)
                return null;
                
            if (tokens.Count == 1)
            {
                // Single token - create atomic node
                return CreateAtomicNode(tokens[0]);
            }
            
            // Multiple tokens - try simple monadic operator detection
            if (CouldBeMonadicOperator(tokens[0].Type))
            {
                var monadicTokens = tokens.Skip(1).ToList();
                if (monadicTokens.Count == 1)
                {
                    var operand = CreateAtomicNode(monadicTokens[0]);
                    if (operand != null)
                    {
                        return CreateMonadicOperatorNode(tokens[0].Type, operand);
                    }
                }
            }
            
            // Fall back to expression parsing for complex cases
            var expressionParser = new ExpressionParser();
            var tempContext = new ParseContext(tokens, "");
            return expressionParser.Parse(tempContext);
        }
        
        /// <summary>
        /// Create atomic node from token (simplified LRS approach)
        /// </summary>
        private ASTNode? CreateAtomicNode(Token token)
        {
            switch (token.Type)
            {
                case TokenType.INTEGER:
                    return ASTNode.MakeLiteral(new IntegerValue(int.Parse(token.Lexeme)));
                case TokenType.FLOAT:
                    return ASTNode.MakeLiteral(new FloatValue(double.Parse(token.Lexeme)));
                case TokenType.CHARACTER:
                    return ASTNode.MakeLiteral(new CharacterValue(token.Lexeme[0].ToString()));
                case TokenType.SYMBOL:
                    return ASTNode.MakeLiteral(new SymbolValue(token.Lexeme));
                case TokenType.NULL:
                    return ASTNode.MakeLiteral(new NullValue());
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Check if token could be monadic operator
        /// </summary>
        private bool CouldBeMonadicOperator(TokenType tokenType)
        {
            return tokenType == TokenType.PLUS || tokenType == TokenType.MINUS ||
                   tokenType == TokenType.MULTIPLY || tokenType == TokenType.DIVIDE ||
                   tokenType == TokenType.MODULUS || tokenType == TokenType.POWER;
        }
        
        /// <summary>
        /// Create monadic operator node (simplified LRS approach)
        /// </summary>
        private ASTNode CreateMonadicOperatorNode(TokenType operatorType, ASTNode operand)
        {
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue(operatorType.ToString().ToLower());
            node.Children.Add(operand);
            return node;
        }

        private ASTNode? ParseExpressionInGrouping(ParseContext context)
        {
            // Parse a single expression within grouping constructs (parentheses, brackets, braces)
            // This method respects semicolons and newlines as expression separators
            // Commas are OPERATORS (join/enlist), not separators
            // but doesn't consume them unless they're part of current expression
            
            if (context.IsAtEnd())
                return null;
                
            var token = context.CurrentToken();
            
            // Handle primary expressions
            switch (token.Type)
            {
                case TokenType.INTEGER:
                case TokenType.LONG:
                case TokenType.FLOAT:
                case TokenType.CHARACTER:
                case TokenType.CHARACTER_VECTOR:
                case TokenType.SYMBOL:
                case TokenType.IDENTIFIER:
                case TokenType.NULL:
                    return ParsePrimaryToken(context);
                    
                case TokenType.LEFT_PAREN:
                    return ParseParentheses(context);
                    
                case TokenType.LEFT_BRACKET:
                    return ParseBrackets(context);
                    
                case TokenType.LEFT_BRACE:
                    return ParseBraces(context);
                    
                case TokenType.PLUS:
                case TokenType.MINUS:
                case TokenType.MULTIPLY:
                case TokenType.DIVIDE:
                case TokenType.MODULUS:
                case TokenType.POWER:
                case TokenType.MIN:
                case TokenType.MAX:
                case TokenType.MATCH:
                case TokenType.NOT:
                case TokenType.HASH:
                case TokenType.UNDERSCORE:
                case TokenType.QUESTION:
                case TokenType.DOLLAR:
                case TokenType.APPLY:
                case TokenType.JOIN: // Comma is an operator, not a separator
                    return ParseMonadicOperator(context);
                    
                default:
                    // Stop at expression separators (semicolons and newlines only)
                    if (token.Type == TokenType.SEMICOLON || 
                        token.Type == TokenType.NEWLINE ||
                        token.Type == TokenType.RIGHT_PAREN ||
                        token.Type == TokenType.RIGHT_BRACKET ||
                        token.Type == TokenType.RIGHT_BRACE)
                    {
                        return null; // End of current expression
                    }
                    
                    throw new Exception($"Unexpected token in expression: {token.Type}({token.Lexeme})");
            }
        }
        
        private ASTNode ParsePrimaryToken(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            if (token.Type == TokenType.LONG)
            {
                var lexeme = token.Lexeme;
                // Handle special long values per K specification
                if (lexeme == "0Ij")
                    return ASTNode.MakeLiteral(new LongValue(long.MaxValue));
                if (lexeme == "-0Ij")
                    return ASTNode.MakeLiteral(new LongValue(-long.MaxValue));
                if (lexeme == "0Nj")
                    return ASTNode.MakeLiteral(new LongValue(long.MinValue));
                return ASTNode.MakeLiteral(new LongValue(long.Parse(lexeme.Substring(0, lexeme.Length - 1))));
            }
            
            return token.Type switch
            {
                TokenType.INTEGER => ASTNode.MakeLiteral(new IntegerValue(int.Parse(token.Lexeme))),
                TokenType.FLOAT => ASTNode.MakeLiteral(new FloatValue(double.Parse(token.Lexeme))),
                TokenType.CHARACTER => ASTNode.MakeLiteral(new CharacterValue(token.Lexeme)),
                TokenType.CHARACTER_VECTOR => CreateCharacterVector(token),
                TokenType.SYMBOL => ASTNode.MakeLiteral(new SymbolValue(token.Lexeme.Trim('`'))),
                TokenType.IDENTIFIER => ASTNode.MakeVariable(token.Lexeme),
                TokenType.NULL => ASTNode.MakeLiteral(new NullValue()),
                _ => throw new Exception($"Unexpected primary token: {token.Type}")
            };
        }
        
        private ASTNode CreateCharacterVector(Token token)
        {
            // Create a character vector from the string content
            var content = token.Lexeme;
            // Remove surrounding quotes if present
            if (content.Length >= 2 && content[0] == '"' && content[^1] == '"')
            {
                content = content.Substring(1, content.Length - 2);
            }
            var charValues = content.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
            
            // Preserve character vector type for empty strings
            if (charValues.Count == 0)
                return ASTNode.MakeLiteral(new VectorValue(charValues, -3)); // Empty character vector
            else if (charValues.Count == 1)
                return ASTNode.MakeLiteral(charValues[0]); // Single character
            else
                return ASTNode.MakeLiteral(new VectorValue(charValues)); // Character vector
        }
        
        private ASTNode ParseMonadicOperator(ParseContext context)
        {
            var token = context.CurrentToken();
            context.Advance();
            
            // Check if this is a projection (no operand before closing parenthesis/separator)
            // Only treat as projection if we're at end or before a delimiter, NOT if there are operands
            var isProjection = context.IsAtEnd() || 
                context.CurrentToken().Type == TokenType.RIGHT_PAREN ||
                context.CurrentToken().Type == TokenType.RIGHT_BRACKET ||
                context.CurrentToken().Type == TokenType.RIGHT_BRACE ||
                context.CurrentToken().Type == TokenType.SEMICOLON ||
                context.CurrentToken().Type == TokenType.NEWLINE;
                // Removed: || context.CurrentToken().Type == TokenType.JOIN; // Comma is operator, not separator
                
            if (isProjection)
            {
                // This is a projection - create a ProjectedFunction node
                var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                
                // Convert token type to operator symbol
                var operatorSymbol = TokenFactory.GetSymbol(token.Type);
                
                projectedNode.Value = new SymbolValue(operatorSymbol);
                
                // Determine arity from VerbRegistry - use highest supported arity as default
                var verb = VerbRegistry.GetVerb(operatorSymbol);
                int defaultArity = verb?.SupportedArities.Max() ?? 2;
                projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(defaultArity)));
                
                return projectedNode;
            }
            
            var expressionParser = new ExpressionParser();
            var operand = expressionParser.Parse(context);
            if (operand == null)
            {
                throw new Exception($"Expected expression after monadic operator {token.Lexeme}");
            }
            
            return ASTNode.MakeDyadicOp(token.Type, operand, ASTNode.MakeLiteral(new NullValue()));
        }

        private ASTNode ParseBraces(ParseContext context)
        {
            context.Advance(); // Consume '{'
            
            // Parse function body
            var body = ParseFunctionBody(context);
            
            context.Advance(); // Consume '}'
            
            return body;
        }

        private ASTNode ParseBrackets(ParseContext context)
        {
            context.Advance(); // Consume '['
            
            // Parse bracket contents with proper semicolon separation
            var elements = ParseBracketContentsAsCommaEnlisted(context);
            
            return elements;
        }

        private ASTNode ParseBracketContentsAsCommaEnlisted(ParseContext context)
        {
            var elements = new List<ASTNode>();
            var projectionSlots = new List<ASTNode?>();
            var hasSemicolon = false;
            
            if (context.Check(TokenType.RIGHT_BRACKET))
            {
                context.Advance(); // Consume ']'
                return ASTNode.MakeVector(elements);
            }
            
            // Check if the first token is a semicolon (indicating a projection)
            if (context.Match(TokenType.SEMICOLON))
            {
                hasSemicolon = true;
                projectionSlots.Add(null); // Missing first argument
                
                // Skip empty lines
                while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                {
                    context.Match(TokenType.NEWLINE);
                }
            }
            
            // Parse first element if not at end
            if (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
            {
                var firstElement = ParseBracketArgument(context);
                if (firstElement == null)
                {
                    throw new Exception("Expected expression in brackets");
                }
                
                if (hasSemicolon)
                {
                    projectionSlots.Add(firstElement);
                }
                else
                {
                    elements.Add(firstElement);
                }
            }
            
            // Parse additional elements separated by semicolons
            while (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
            {
                if (context.Match(TokenType.SEMICOLON))
                {
                    hasSemicolon = true;
                    
                    // Add null for missing argument
                    projectionSlots.Add(null);
                    
                    // Skip empty lines
                    while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                    {
                        context.Match(TokenType.NEWLINE);
                    }
                    
                    if (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
                    {
                        var element = ParseBracketArgument(context);
                        if (element != null)
                        {
                            projectionSlots[projectionSlots.Count - 1] = element; // Replace the null
                        }
                    }
                }
                else
                {
                    var element = ParseBracketArgument(context);
                    if (element != null)
                    {
                        if (hasSemicolon)
                        {
                            projectionSlots.Add(element);
                        }
                        else
                        {
                            elements.Add(element);
                        }
                    }
                }
            }
            
            context.Advance(); // Consume ']'

            
            // If we detected semicolons (projection syntax), create a projection node
            if (hasSemicolon)
            {
                // For projections, we need to create a vector with the projection arguments
                var projectionElements = new List<ASTNode>();
                
                // Add all non-null projection arguments
                var nonNullElements = projectionSlots.Where(x => x != null).Cast<ASTNode>().ToList();
                projectionElements.AddRange(nonNullElements);
                
                return ASTNode.MakeVector(projectionElements);
            }
            
            return ASTNode.MakeVector(elements);
        }

        private ASTNode ParseFunctionBody(ParseContext context)
        {
            var parameters = new List<string>();
            
            // Parse parameter list if present
            if (context.Check(TokenType.LEFT_BRACKET))
            {
                context.Advance(); // Consume '['
                
                while (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
                {
                    if (context.CurrentToken().Type == TokenType.IDENTIFIER)
                    {
                        parameters.Add(context.CurrentToken().Lexeme);
                        context.Advance();
                        
                        if (context.Check(TokenType.SEMICOLON))
                        {
                            context.Advance(); // Consume ';'
                        }
                    }
                    else
                    {
                        throw new Exception("Expected identifier in parameter list");
                    }
                }
                
                context.Advance(); // Consume ']'
            }
            
            // Parse function body
            var bodyParser = new ExpressionParser();
            var body = bodyParser.Parse(context) ?? ASTNode.MakeLiteral(new NullValue());
            
            return ASTNode.MakeFunction(parameters, body);
        }

        // Monadic operator parsing methods
        private ASTNode ParseMonadicPlus(ParseContext context)
        {
            context.Advance();
            
            // Check if we have a bracket expression for projection
            if (context.CurrentToken().Type == TokenType.LEFT_BRACKET)
            {
                context.Advance(); // Consume '['
                
                // Parse projection arguments manually
                var projectionArgs = new List<ASTNode?>();
                var hasSemicolon = false;
                
                // Check if first token is semicolon (projection)
                if (context.Match(TokenType.SEMICOLON))
                {
                    hasSemicolon = true;
                    projectionArgs.Add(null); // Missing first argument placeholder
                    
                    // Skip whitespace/newlines
                    while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                    {
                        context.Match(TokenType.NEWLINE);
                    }
                }
                
                // Parse arguments until closing bracket
                while (!context.IsAtEnd() && context.CurrentToken().Type != TokenType.RIGHT_BRACKET)
                {
                    var token = context.CurrentToken();
                    
                    if (token.Type == TokenType.SEMICOLON)
                    {
                        context.Advance(); // Consume semicolon
                        hasSemicolon = true;
                        projectionArgs.Add(null); // Missing argument placeholder
                        
                        // Skip whitespace/newlines
                        while (!context.IsAtEnd() && context.CurrentToken().Type == TokenType.NEWLINE)
                        {
                            context.Match(TokenType.NEWLINE);
                        }
                    }
                    else if (token.Type == TokenType.INTEGER)
                    {
                        context.Advance();
                        var value = int.Parse(token.Lexeme);
                        projectionArgs.Add(ASTNode.MakeLiteral(new IntegerValue(value)));
                    }
                    else if (token.Type == TokenType.FLOAT)
                    {
                        context.Advance();
                        var value = double.Parse(token.Lexeme);
                        projectionArgs.Add(ASTNode.MakeLiteral(new FloatValue(value)));
                    }
                    else if (token.Type == TokenType.JOIN) // Comma operator
                    {
                        context.Advance();
                        // Comma should be represented as a SymbolValue for projections
                        projectionArgs.Add(ASTNode.MakeLiteral(new SymbolValue(",")));
                    }
                    else
                    {
                        // For now, just advance to avoid infinite loops
                        context.Advance();
                    }
                }
                
                if (!context.Match(TokenType.RIGHT_BRACKET))
                {
                    throw new Exception("Expected ']' after bracket expression");
                }
                
                // If we detected a projection, create the correct vector structure directly
                if (hasSemicolon)
                {
                    // Create the projection structure: (operator, ::, args...)
                    var projectionVector = new List<ASTNode>();
                    projectionVector.Add(ASTNode.MakeLiteral(new SymbolValue("+"))); // Add operator
                    projectionVector.Add(ASTNode.MakeLiteral(new SymbolValue("::"))); // Add projection symbol
                    
                    // Add non-null projection arguments
                    foreach (var arg in projectionArgs)
                    {
                        if (arg != null)
                        {
                            // Convert to enlisted vector to get comma formatting
                            var k3Value = arg.Value;
                            if (k3Value != null && (k3Value is IntegerValue || k3Value is FloatValue || k3Value is SymbolValue || k3Value is CharacterValue) &&
                                !(k3Value is SymbolValue sym && sym.Value == "::"))
                            {
                                // Create enlisted vector for atomic values to get comma formatting
                                var elements = new List<K3Value> { k3Value };
                                var enlistedVector = new VectorValue(elements);
                                projectionVector.Add(ASTNode.MakeLiteral(enlistedVector));
                            }
                            else
                            {
                                projectionVector.Add(arg);
                            }
                        }
                    }
                    
                    return ASTNode.MakeVector(projectionVector);
                }
                else
                {
                    // Regular bracket expression - create a vector with non-null args
                    var nonNullArgs = projectionArgs.Where(x => x != null).Cast<ASTNode>().ToList();
                    return ASTNode.MakeVector(nonNullArgs);
                }
            }
            
            // Regular monadic plus without brackets
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("+");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicMinus(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("-");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicReciprocal(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            if (operand == null)
            {
                // Create a projected function instead of an empty dyadic op
                var projectedNode = new ASTNode(ASTNodeType.ProjectedFunction);
                projectedNode.Value = new SymbolValue("%");
                projectedNode.Children.Add(ASTNode.MakeLiteral(new IntegerValue(1))); // Needs 1 more argument
                return projectedNode;
            }
            
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("%");
            node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicPower(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("^");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicJoin(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue(",");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseNull(ParseContext context)
        {
            context.Advance();
            return ASTNode.MakeLiteral(new NullValue());
        }

        // Missing monadic operator methods
        private ASTNode ParseMonadicFirst(ParseContext context)
        {
            context.Advance();
            
            // For monadic operators, try to parse a vector if multiple elements are present
            var elements = new List<ASTNode>();
            
            while (!context.IsAtEnd() && !IsExpressionTerminator(context.CurrentToken().Type))
            {
                var element = ParsePrimaryToken(context);
                if (element != null)
                {
                    elements.Add(element);
                }
                else
                {
                    break;
                }
            }
            
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("*");
            
            if (elements.Count == 1)
            {
                // Single element
                node.Children.Add(elements[0]);
            }
            else if (elements.Count > 1)
            {
                // Multiple elements - create a vector
                var vectorNode = ASTNode.MakeVector(elements);
                node.Children.Add(vectorNode);
            }
            else
            {
                // No elements - this shouldn't happen
                throw new Exception("Monadic * operator requires an operand");
            }
            
            return node;
        }
        
        private bool IsExpressionTerminator(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.SEMICOLON or TokenType.NEWLINE or TokenType.EOF or
                TokenType.RIGHT_PAREN or TokenType.RIGHT_BRACKET or TokenType.RIGHT_BRACE => true,
                _ => false
            };
        }

        private ASTNode ParseMonadicMin(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("&");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicMax(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("|");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicMatch(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("~");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicNot(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("~");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicCount(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("#");
            if (operand != null) node.Children.Add(operand);
            return node;
        }

        private ASTNode ParseMonadicApply(ParseContext context)
        {
            context.Advance();
            var operand = ParseBracketArgument(context);
            var node = new ASTNode(ASTNodeType.MonadicOp);
            node.Value = new SymbolValue("@");
            if (operand != null) node.Children.Add(operand);
            return node;
        }
    }
}