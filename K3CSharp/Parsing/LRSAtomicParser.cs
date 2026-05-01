using System;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Atomic token parsing for LRS parser
    /// Handles literals, identifiers, and basic atomic values without PrimaryParser dependency
    /// Provides verb-agnostic atomic token creation
    /// </summary>
    public class LRSAtomicParser
    {
        /// <summary>
        /// Parse atomic token and create appropriate AST node
        /// </summary>
        /// <param name="token">Token to parse</param>
        /// <returns>AST node representing atomic value</returns>
        public static ASTNode ParseAtomicToken(Token token)
        {
            return ParseAtomicToken(token, null);
        }
        
        /// <summary>
        /// Parse atomic token and create appropriate AST node with parser context
        /// </summary>
        /// <param name="token">Token to parse</param>
        /// <param name="parser">Optional parser reference for variable tracking</param>
        /// <returns>AST node representing atomic value</returns>
        public static ASTNode ParseAtomicToken(Token token, LRSParser? parser)
        {
            return token.Type switch
            {
                TokenType.INTEGER => ParseInteger(token),
                TokenType.LONG => ParseLong(token),
                TokenType.FLOAT => ParseFloat(token),
                TokenType.CHARACTER => ParseCharacter(token),
                TokenType.CHARACTER_VECTOR => ParseString(token),
                TokenType.SYMBOL => ParseSymbol(token),
                TokenType.IDENTIFIER => ParseIdentifier(token, parser),
                TokenType.NULL => ParseNull(token),
                TokenType.SS => ParseSystemVerb(token),
                TokenType.SSR => ParseSystemVerb(token),
                TokenType.TIME => ParseSystemVariable(token, "_t"),
                TokenType.DAYS => ParseSystemVariable(token, "_T"),
                TokenType.DIRECTORY => ParseSystemVariable(token, "_d"),
                TokenType.VARIABLE => ParseSystemVariable(token, "_v"),
                TokenType.INDEX => ParseSystemVariable(token, "_i"),
                TokenType.FUNCTION => ParseSystemVariable(token, "_f"),
                TokenType.SPACE => ParseSystemVariable(token, "_s"),
                TokenType.HOST => ParseSystemVariable(token, "_h"),
                TokenType.PORT => ParseSystemVariable(token, "_p"),
                TokenType.PID => ParseSystemVariable(token, "_P"),
                TokenType.WHO => ParseSystemVariable(token, "_w"),
                TokenType.USER => ParseSystemVariable(token, "_u"),
                TokenType.ADDRESS => ParseSystemVariable(token, "_a"),
                TokenType.VERSION => ParseSystemVariable(token, "_k"),
                TokenType.OS => ParseSystemVariable(token, "_o"),
                TokenType.CORES => ParseSystemVariable(token, "_c"),
                TokenType.RAM => ParseSystemVariable(token, "_r"),
                TokenType.MACHID => ParseSystemVariable(token, "_m"),
                TokenType.STACK => ParseSystemVariable(token, "_y"),
                _ => throw new Exception($"Unsupported atomic token type: {token.Type}({token.Lexeme})")
            };
        }

        /// <summary>
        /// Parse integer token with special value handling
        /// </summary>
        private static ASTNode ParseInteger(Token token)
        {
            var lexeme = token.Lexeme;
            
            // Handle special integer values per K specification
            if (lexeme == "0I" || lexeme == "-0I")
                return ASTNode.MakeLiteral(new IntegerValue(lexeme));
            
            // Handle null integer literals (0N, -0N)
            if (lexeme == "0N" || lexeme == "-0N")
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

        /// <summary>
        /// Parse long integer token with bounds checking
        /// </summary>
        private static ASTNode ParseLong(Token token)
        {
            var lexeme = token.Lexeme;
            
            // Handle special long values per K specification
            // 0Ij represents positive infinity (long.MaxValue)
            if (lexeme == "0Ij")
                return ASTNode.MakeLiteral(new LongValue(long.MaxValue));
            
            // -0Ij represents negative infinity (-long.MaxValue)
            if (lexeme == "-0Ij")
                return ASTNode.MakeLiteral(new LongValue(-long.MaxValue));
            
            // 0Nj represents null (long.MinValue)
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

        /// <summary>
        /// Parse float token with special value handling
        /// </summary>
        private static ASTNode ParseFloat(Token token)
        {
            var lexeme = token.Lexeme;
            
            // Handle special float values
            if (lexeme == "0i" || lexeme == "0n" || lexeme == "-0i")
                return ASTNode.MakeLiteral(new FloatValue(lexeme));
            
            return ASTNode.MakeLiteral(new FloatValue(double.Parse(lexeme)));
        }

        /// <summary>
        /// Parse character token with escape sequence handling
        /// </summary>
        private static ASTNode ParseCharacter(Token token)
        {
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

        /// <summary>
        /// Parse string token (character vector)
        /// Note: Escape sequences are already processed by the lexer (ReadString)
        /// </summary>
        public static ASTNode ParseString(Token token)
        {
            var lexeme = token.Lexeme;
            
            // CHARACTER_VECTOR tokens from lexer contain processed string content
            // Escape sequences are already handled by the lexer, so we use the lexeme directly
            var charValues = lexeme.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
            
            // Preserve character vector type for empty strings
            // According to spec: if length != 1 (including 0), produce character vector (type -3)
            if (charValues.Count == 0)
                return ASTNode.MakeLiteral(new VectorValue(charValues, -3)); // Empty character vector
            else if (charValues.Count == 1)
                return ASTNode.MakeLiteral(charValues[0]); // Single character
            else
                return ASTNode.MakeLiteral(new VectorValue(charValues)); // Character vector
        }
        

        /// <summary>
        /// Parse symbol token
        /// </summary>
        private static ASTNode ParseSymbol(Token token)
        {
            var lexeme = token.Lexeme;
            
            // Remove backticks for symbol value
            var symbolValue = lexeme.Trim('`');
            return ASTNode.MakeLiteral(new SymbolValue(symbolValue));
        }
        
        /// <summary>
        /// Parse system verb token
        /// </summary>
        private static ASTNode ParseSystemVerb(Token token)
        {
            // Create a literal node for the system verb
            return ASTNode.MakeLiteral(new SymbolValue(token.Lexeme));
        }

        /// <summary>
        /// Parse niladic system variable token (e.g., _t) as a Variable node
        /// so the evaluator routes it through EvaluateVerb
        /// </summary>
        private static ASTNode ParseSystemVariable(Token token, string verbName)
        {
            var node = new ASTNode(ASTNodeType.Variable);
            node.Value = new SymbolValue(verbName);
            node.Children.Add(ASTNode.MakeLiteral(new SymbolValue("system")));
            return node;
        }

        /// <summary>
        /// Parse identifier token (variable)
        /// Uses verb-agnostic system function detection
        /// </summary>
        private static ASTNode ParseIdentifier(Token token, LRSParser? parser)
        {
            var identifier = token.Lexeme;
            
            // Check if this identifier is a system function using verb-agnostic approach
            if (VerbQueryExtensions.IsSystemFunction(identifier))
            {
                // Create a system function node instead of a regular variable
                return ASTNode.MakeFunctionCall(ASTNode.MakeVariable(identifier), new List<ASTNode>());
            }
            
            // Per spec: Check if variable is defined during parsing (for multi-line scripts)
            // This allows variables assigned in earlier expressions to be referenced in later ones
            // Note: We create the variable node regardless - the evaluator will validate at runtime
            // The parser just needs to know the variable is "expected to exist" when evaluation runs
            if (parser != null && parser.IsVariableDefined(identifier))
            {
                // Variable is registered as defined during parsing - safe to create variable node
                return ASTNode.MakeVariable(identifier);
            }
            
            return ASTNode.MakeVariable(identifier);
        }

        /// <summary>
        /// Parse null token
        /// </summary>
        private static ASTNode ParseNull(Token token)
        {
            return ASTNode.MakeLiteral(new NullValue());
        }

        /// <summary>
        /// Check if token type can be an element in an implicit vector
        /// Implicit vectors only consist of literal values: integers, longs, floats, characters, symbols
        /// System variables, identifiers, and other tokens cannot be implicit vector elements
        /// </summary>
        public static bool CanBeImplicitVectorElement(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.INTEGER or TokenType.LONG or TokenType.FLOAT or 
                TokenType.CHARACTER or TokenType.CHARACTER_VECTOR or 
                TokenType.SYMBOL => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if token type represents an atomic verb (glyphs, identifiers with verb assignments)
        /// This is used to determine if a token can be used as a verb in verb+adverb patterns
        /// </summary>
        public static bool IsAtomicVerb(TokenType tokenType)
        {
            // Glyphs (operator symbols) and identifiers can be verbs
            // Integers, floats, etc are NOT verbs
            return tokenType switch
            {
                TokenType.PLUS or TokenType.MINUS or TokenType.MULTIPLY or TokenType.DIVIDE or
                TokenType.MODULUS or TokenType.POWER or TokenType.LESS or TokenType.GREATER or
                TokenType.EQUAL or TokenType.MATCH or TokenType.DOLLAR or
                TokenType.QUESTION or TokenType.HASH or TokenType.UNDERSCORE or
                TokenType.IDENTIFIER => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if token type can be handled by the atomic parser
        /// This includes literals, identifiers, system variables, and other atomic expressions
        /// </summary>
        public static bool CanBeParsedByAtomicParser(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.INTEGER or TokenType.LONG or TokenType.FLOAT or 
                TokenType.CHARACTER or TokenType.CHARACTER_VECTOR or 
                TokenType.SYMBOL or TokenType.IDENTIFIER or TokenType.NULL or
                TokenType.TIME or TokenType.DAYS or TokenType.DIRECTORY or
                TokenType.VARIABLE or TokenType.INDEX or TokenType.FUNCTION or
                TokenType.SPACE or TokenType.HOST or TokenType.PORT or
                TokenType.PID or TokenType.WHO or TokenType.USER or
                TokenType.ADDRESS or TokenType.VERSION or TokenType.OS or
                TokenType.CORES or TokenType.RAM or TokenType.MACHID or
                TokenType.STACK or TokenType.SS or TokenType.SSR => true,
                _ => false
            };
        }

        /// <summary>
        /// Create AST node for operator symbols in parse trees
        /// </summary>
        public static ASTNode CreateOperatorNode(TokenType tokenType)
        {
            // Delimiters for parse tree representation - not verbs, so handled separately
            if (tokenType == TokenType.LEFT_PAREN) return ASTNode.MakeLiteral(new SymbolValue("("));
            if (tokenType == TokenType.RIGHT_PAREN) return ASTNode.MakeLiteral(new SymbolValue(")"));
            if (tokenType == TokenType.LEFT_BRACKET) return ASTNode.MakeLiteral(new SymbolValue("["));
            if (tokenType == TokenType.RIGHT_BRACKET) return ASTNode.MakeLiteral(new SymbolValue("]"));
            if (tokenType == TokenType.LEFT_BRACE) return ASTNode.MakeLiteral(new SymbolValue("{"));
            if (tokenType == TokenType.RIGHT_BRACE) return ASTNode.MakeLiteral(new SymbolValue("}"));
            if (tokenType == TokenType.SEMICOLON) return ASTNode.MakeLiteral(new SymbolValue(";"));

            // Use VerbRegistry to get verb name - LRS principle: parser is verb-agnostic
            string verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            
            // Check if this is a system variable (niladic)
            var verbInfo = VerbRegistry.GetVerb(verbName);
            if (verbInfo != null && verbInfo.IsSystemVariable)
            {
                return ASTNode.MakeVariable(verbName);
            }
            
            // Return as literal symbol value
            return ASTNode.MakeLiteral(new SymbolValue(verbName));
        }
    }
}
