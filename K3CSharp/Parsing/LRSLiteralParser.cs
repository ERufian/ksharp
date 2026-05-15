// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// LRS Literal Parser for atomic values (int, float, char, symbol, etc.)
    /// Verb-agnostic design using VerbRegistry for type detection
    /// </summary>
    public class LRSLiteralParser
    {
        private readonly List<Token> tokens;
        
        public LRSLiteralParser(List<Token> tokens)
        {
            this.tokens = tokens;
        }
        
        /// <summary>
        /// Check if token can be parsed as literal
        /// </summary>
        public static bool IsLiteralToken(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.INTEGER or TokenType.LONG or TokenType.FLOAT or 
                TokenType.CHARACTER or TokenType.CHARACTER_VECTOR or
                TokenType.SYMBOL or TokenType.NULL => true,
                _ => false
            };
        }
        
        /// <summary>
        /// Parse atomic token into AST node
        /// </summary>
        public static ASTNode? ParseAtomicToken(Token token)
        {
            return token.Type switch
            {
                TokenType.INTEGER => ParseInteger(token),
                TokenType.LONG => ParseLong(token),
                TokenType.FLOAT => ParseFloat(token),
                TokenType.CHARACTER => ParseCharacter(token),
                TokenType.CHARACTER_VECTOR => ParseCharacterVector(token),
                TokenType.SYMBOL => ParseSymbol(token),
                TokenType.NULL => ParseNull(token),
                _ => null
            };
        }
        
        /// <summary>
        /// Parse integer token
        /// </summary>
        private static ASTNode ParseInteger(Token token)
        {
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
            else if (long.TryParse(token.Lexeme, out long longValue))
            {
                return ASTNode.MakeLiteral(new LongValue(longValue));
            }
            throw new Exception($"Invalid integer literal: {token.Lexeme}");
        }
        
        /// <summary>
        /// Parse long token
        /// </summary>
        private static ASTNode ParseLong(Token token)
        {
            var lexeme = token.Lexeme;
            
            // Handle special long values per K specification
            if (lexeme == "0Ij")
                return ASTNode.MakeLiteral(new LongValue(long.MaxValue));
            if (lexeme == "-0Ij")
                return ASTNode.MakeLiteral(new LongValue(-long.MaxValue));
            if (lexeme == "0Nj")
                return ASTNode.MakeLiteral(new LongValue(long.MinValue));
            
            // Strip the 'j' suffix and parse
            var numberPart = lexeme.Substring(0, lexeme.Length - 1);
            if (long.TryParse(numberPart, out long longValue))
            {
                return ASTNode.MakeLiteral(new LongValue(longValue));
            }
            throw new Exception($"Invalid long literal: {token.Lexeme}");
        }
        
        /// <summary>
        /// Parse float token
        /// </summary>
        private static ASTNode ParseFloat(Token token)
        {
            if (double.TryParse(token.Lexeme, out double floatValue))
            {
                return ASTNode.MakeLiteral(new FloatValue(floatValue));
            }
            throw new Exception($"Invalid float literal: {token.Lexeme}");
        }
        
        /// <summary>
        /// Parse character token
        /// </summary>
        private static ASTNode ParseCharacter(Token token)
        {
            if (token.Lexeme.Length >= 1)
            {
                return ASTNode.MakeLiteral(new CharacterValue(token.Lexeme));
            }
            throw new Exception($"Invalid character literal: {token.Lexeme}");
        }
        
        /// <summary>
        /// Parse character vector token
        /// </summary>
        private static ASTNode ParseCharacterVector(Token token)
        {
            return ASTNode.MakeLiteral(new CharacterValue(token.Lexeme));
        }
        
        /// <summary>
        /// Parse symbol token
        /// </summary>
        private static ASTNode ParseSymbol(Token token)
        {
            return ASTNode.MakeLiteral(new SymbolValue(token.Lexeme));
        }
        
        /// <summary>
        /// Parse string token (removed - handled as character vector)
        /// </summary>
        // private static ASTNode ParseString(Token token)
        // {
        //     return ASTNode.MakeLiteral(new CharacterValue(token.Lexeme));
        // }
        
        /// <summary>
        /// Parse null token
        /// </summary>
        private static ASTNode ParseNull(Token token)
        {
            return ASTNode.MakeLiteral(new NullValue());
        }
        
        /// <summary>
        /// Get literal type from token (for verb-agnostic processing)
        /// </summary>
        public static string GetLiteralType(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.INTEGER => "integer",
                TokenType.LONG => "long",
                TokenType.FLOAT => "float",
                TokenType.CHARACTER => "character",
                TokenType.CHARACTER_VECTOR => "string",
                TokenType.SYMBOL => "symbol",
                TokenType.NULL => "null",
                _ => "unknown"
            };
        }
    }
}