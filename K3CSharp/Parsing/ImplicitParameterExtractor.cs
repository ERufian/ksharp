using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    /// <summary>
    /// Specialized parser for extracting implicit parameters from function body tokens
    /// In K, implicit parameters are single lowercase letters that appear in the function body
    /// The parameters are extracted in alphabetical order of first appearance
    /// </summary>
    public class ImplicitParameterExtractor : IParserModule
    {
        public bool CanHandle(TokenType currentToken)
        {
            // This parser is used specifically for parameter extraction
            // It's not directly called by token type, but by the parser when needed
            return false;
        }

        public ASTNode? Parse(ParseContext context)
        {
            // This method is not used directly - ExtractImplicitParameters is the main entry point
            throw new NotImplementedException("Use ExtractImplicitParameters method instead");
        }

        /// <summary>
        /// Extract implicit parameters from function body tokens
        /// In K, implicit parameters are single lowercase letters that appear in the function body
        /// The parameters are extracted in alphabetical order of first appearance
        /// </summary>
        public static List<string> ExtractImplicitParameters(List<Token> bodyTokens)
        {
            var parameters = new List<string>();
            var seenParameters = new HashSet<string>();
            
            foreach (var token in bodyTokens)
            {
                // Look for identifier tokens that are single lowercase letters (a-z)
                if (token.Type == TokenType.IDENTIFIER && 
                    token.Lexeme.Length == 1 && 
                    char.IsLower(token.Lexeme[0]) &&
                    !seenParameters.Contains(token.Lexeme))
                {
                    parameters.Add(token.Lexeme);
                    seenParameters.Add(token.Lexeme);
                }
            }
            
            // Sort parameters alphabetically (x, y, z order)
            parameters.Sort();
            
            return parameters;
        }
    }
}
