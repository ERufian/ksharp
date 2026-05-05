using System;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Verb-agnostic operator detection using VerbRegistry
    /// Provides operator classification without hardcoded verb knowledge
    /// </summary>
    public class OperatorDetector
    {
        /// <summary>
        /// Check if token type is a dyadic operator using VerbRegistry
        /// </summary>
        public static bool IsDyadicOperator(TokenType tokenType)
        {
            return SupportsDyadic(tokenType);
        }
        
        /// <summary>
        /// Check if token type supports monadic operations using VerbRegistry
        /// </summary>
        public static bool SupportsMonadic(TokenType tokenType)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            return verb?.SupportedArities?.Contains(1) ?? false;
        }
        
        /// <summary>
        /// Check if token type supports dyadic operations using VerbRegistry
        /// </summary>
        public static bool SupportsDyadic(TokenType tokenType)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            return verb?.SupportedArities?.Contains(2) ?? false;
        }
        
        /// <summary>
        /// Get verb information for a token type
        /// </summary>
        public static VerbInfo? GetVerbInfo(TokenType tokenType)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            return VerbRegistry.GetVerb(verbName);
        }
        
        /// <summary>
        /// Check if token type represents a function
        /// </summary>
        public static bool IsFunction(TokenType tokenType)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            return verb?.Type == VerbType.Function || verb?.Type == VerbType.SystemFunction || verb?.Type == VerbType.SystemVariable;
        }
        
        /// <summary>
        /// Check if token type represents a system variable
        /// </summary>
        public static bool IsSystemVariable(TokenType tokenType)
        {
            var verbName = VerbRegistry.TokenTypeToVerbName(tokenType);
            var verb = VerbRegistry.GetVerb(verbName);
            return verb?.Type == VerbType.SystemVariable;
        }
    }
}
