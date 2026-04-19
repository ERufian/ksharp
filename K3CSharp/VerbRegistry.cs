using System;
using System.Collections.Generic;
using System.Linq;
using K3CSharp.Verbs;

namespace K3CSharp
{
    /// <summary>
    /// Context for verb arity determination separate from parser ParseContext
    /// </summary>
    public class VerbArityContext
    {
        public bool HasLeftOperand { get; set; }
        public bool HasRightOperand { get; set; }
        public bool HasBracketNotation { get; set; }
        public int BracketArgumentCount { get; set; }
        public bool HasDisambiguatingColon { get; set; }
        public bool IsAtExpressionStart { get; set; }
        public TokenType NextToken { get; set; }
        public bool IsInVectorContext { get; set; }
        public bool IsInAssignmentContext { get; set; }
    }

    public enum VerbArityType
    {
        MonadicOnly,     // Verbs like _ic, _ci that are always monadic
        DyadicOnly,      // Verbs like _sv, _vs that are always dyadic  
        ContextSensitive, // Most glyphs - need parser analysis
        Variadic         // Verbs like ., @ with 3+ arities
    }
    public enum VerbType
    {
        Operator, // Native operators
        SystemFunction, // Native functions with names that start with underscore. Names are longer than 1 char
        SystemVariable, // Native variables with names that start with underscore. Names are 1 char long
        Function, // Anonymous functions (value)
        FunctionVariable, // Named functions (variables of type 7 that have an anonymous function assigned to them)
        ProjectedFunction // Value that results from calls to other functions with fewer arguments than their valence (-arity)
    }

    public class VerbInfo
    {
        public string Name { get; set; } = "";
        public int[] SupportedArities { get; set; } = Array.Empty<int>();
        public Func<K3Value[], K3Value>?[]? Implementations { get; set; }
        public VerbType Type { get; set; }
        public bool IsSystemVariable { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// Builder class for creating VerbArityContext from parser state
    /// </summary>
    public static class VerbArityContextBuilder
    {
        /// <summary>
        /// Build context from parser position and tokens
        /// </summary>
        public static VerbArityContext BuildContext(Token[] tokens, int currentPosition, bool hasLeftOperand = false)
        {
            var context = new VerbArityContext();
            
            // Determine if we're at expression start
            context.IsAtExpressionStart = currentPosition == 0 || 
                tokens.Length > currentPosition - 1 && 
                IsExpressionSeparator(tokens[currentPosition - 1].Type);
            
            context.HasLeftOperand = hasLeftOperand;
            
            // Look ahead for bracket notation
            if (currentPosition < tokens.Length - 1)
            {
                context.NextToken = tokens[currentPosition + 1].Type;
                context.HasBracketNotation = context.NextToken == TokenType.LEFT_BRACKET;
                
                // Count bracket arguments if bracket notation detected
                if (context.HasBracketNotation)
                {
                    context.BracketArgumentCount = CountBracketArguments(tokens, currentPosition + 1);
                }
            }
            
            // Check for disambiguating colon
            context.HasDisambiguatingColon = currentPosition < tokens.Length - 1 && 
                                           tokens[currentPosition + 1].Type == TokenType.COLON;
            
            return context;
        }
        
        /// <summary>
        /// Build context for vector parsing
        /// </summary>
        public static VerbArityContext BuildVectorContext(Token[] tokens, int currentPosition)
        {
            var context = BuildContext(tokens, currentPosition);
            context.IsInVectorContext = true;
            return context;
        }
        
        /// <summary>
        /// Build context for assignment parsing
        /// </summary>
        public static VerbArityContext BuildAssignmentContext(Token[] tokens, int currentPosition)
        {
            var context = BuildContext(tokens, currentPosition);
            context.IsInAssignmentContext = true;
            return context;
        }
        
        private static bool IsExpressionSeparator(TokenType tokenType)
        {
            return tokenType == TokenType.SEMICOLON || 
                   tokenType == TokenType.NEWLINE || 
                   tokenType == TokenType.EOF ||
                   tokenType == TokenType.LEFT_PAREN ||
                   tokenType == TokenType.LEFT_BRACE;
        }
        
        private static int CountBracketArguments(Token[] tokens, int bracketStart)
        {
            int count = 0;
            int depth = 1; // Start with 1 for the opening bracket
            int i = bracketStart + 1; // Skip opening bracket
            
            while (i < tokens.Length && depth > 0)
            {
                var token = tokens[i];
                
                if (token.Type == TokenType.LEFT_BRACKET)
                {
                    depth++;
                }
                else if (token.Type == TokenType.RIGHT_BRACKET)
                {
                    depth--;
                    if (depth == 0) break; // Reached closing bracket
                }
                else if (depth == 1 && token.Type == TokenType.SEMICOLON)
                {
                    count++;
                }
                
                i++;
            }
            
            return count + 1; // Number of arguments = semicolons + 1
        }
    }

    /// <summary>
    /// Centralized registry for all verb metadata and implementations
    /// </summary>
    public static class VerbRegistry
    {
        private static readonly Dictionary<string, VerbInfo> verbs = new Dictionary<string, VerbInfo>();
        
        // Performance optimization: caching for frequently accessed operations
        private static readonly Dictionary<string, VerbType> verbTypeCache = new Dictionary<string, VerbType>();
        private static readonly Dictionary<string, bool> systemVariableCache = new Dictionary<string, bool>();
        private static readonly Dictionary<string, int[]> supportedAritiesCache = new Dictionary<string, int[]>();
        
        // TokenType-level caching for performance
        private static readonly Dictionary<TokenType, string> tokenTypeToVerbNameCache = new Dictionary<TokenType, string>();
        private static readonly Dictionary<TokenType, bool> monadicOperatorCache = new Dictionary<TokenType, bool>();
        private static readonly Dictionary<TokenType, bool> dyadicOperatorCache = new Dictionary<TokenType, bool>();
        private static readonly Dictionary<TokenType, bool> verbTokenCache = new Dictionary<TokenType, bool>();
        
        // Performance monitoring
        private static int lookupCount = 0;
        private static int cacheHits = 0;

        static VerbRegistry()
        {
            InitializeBasicOperators();
            PopulateImplementationDelegates(); // This is a hack for _eval. We need to fix _eval and remove it
        }

        public static void RegisterVerb(string name, VerbType type, int[] arities, Func<K3Value[], K3Value>?[]? implementations, bool isSystemVariable = false, string? description = null)
        {
            verbs[name] = new VerbInfo
            {
                Name = name,
                SupportedArities = arities,
                Implementations = implementations,
                Type = type,
                IsSystemVariable = isSystemVariable,
                Description = description
            };
        }

        public static VerbInfo? GetVerb(string name)
        {
            lookupCount++;
            return verbs.TryGetValue(name, out var verbInfo) ? verbInfo : null;
        }

        /// <summary>
        /// Get verb type with caching optimization
        /// </summary>
        public static VerbType GetVerbType(string verbName)
        {
            if (verbTypeCache.TryGetValue(verbName, out var cachedType))
            {
                cacheHits++;
                return cachedType;
            }
            
            var verb = GetVerb(verbName);
            var type = verb?.Type ?? VerbType.Function;
            verbTypeCache[verbName] = type;
            return type;
        }

        /// <summary>
        /// Check if verb is system variable with caching optimization
        /// </summary>
        public static bool IsSystemVariable(string verbName)
        {
            if (systemVariableCache.TryGetValue(verbName, out var cachedResult))
            {
                cacheHits++;
                return cachedResult;
            }
            
            var verb = GetVerb(verbName);
            var isSystemVar = verb?.Type == VerbType.SystemVariable;
            systemVariableCache[verbName] = isSystemVar;
            return isSystemVar;
        }

        /// <summary>
        /// Get supported arities with caching optimization
        /// </summary>
        public static int[] GetSupportedArities(string verbName)
        {
            if (supportedAritiesCache.TryGetValue(verbName, out var cachedArities))
            {
                cacheHits++;
                return cachedArities;
            }
            
            var verb = GetVerb(verbName);
            var arities = verb?.SupportedArities ?? new int[0];
            supportedAritiesCache[verbName] = arities;
            return arities;
        }

        /// <summary>
        /// Clear all caches (useful for testing or memory management)
        /// </summary>
        public static void ClearCaches()
        {
            verbTypeCache.Clear();
            systemVariableCache.Clear();
            supportedAritiesCache.Clear();
            tokenTypeToVerbNameCache.Clear();
            monadicOperatorCache.Clear();
            dyadicOperatorCache.Clear();
            verbTokenCache.Clear();
            lookupCount = 0;
            cacheHits = 0;
        }

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        public static Dictionary<string, object> GetCacheStats()
        {
            var hitRate = lookupCount > 0 ? (double)cacheHits / lookupCount * 100 : 0;
            return new Dictionary<string, object>
            {
                ["LookupCount"] = lookupCount,
                ["CacheHits"] = cacheHits,
                ["HitRate"] = $"{hitRate:F2}%",
                ["VerbTypeCacheSize"] = verbTypeCache.Count,
                ["SystemVariableCacheSize"] = systemVariableCache.Count,
                ["SupportedAritiesCacheSize"] = supportedAritiesCache.Count,
                ["TokenTypeCacheSize"] = tokenTypeToVerbNameCache.Count,
                ["MonadicCacheSize"] = monadicOperatorCache.Count,
                ["DyadicCacheSize"] = dyadicOperatorCache.Count,
                ["VerbTokenCacheSize"] = verbTokenCache.Count
            };
        }

        /// <summary>
        /// Convert TokenType to verb name with caching
        /// </summary>
        public static string TokenTypeToVerbName(TokenType tokenType)
        {
            if (tokenTypeToVerbNameCache.TryGetValue(tokenType, out var cachedName))
            {
                cacheHits++;
                return cachedName;
            }
            
            lookupCount++;
            string verbName = tokenType switch
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
                TokenType.POWER => "^",
                TokenType.MODULUS => "!",
                TokenType.MATCH => "~",
                TokenType.JOIN => ",",
                TokenType.DOT_PRODUCT => "_dot",
                TokenType.DOT_APPLY => ".",
                TokenType.APPLY => "@",
                TokenType.MUL => "_mul",
                TokenType.COLON => ":",
                TokenType.HASH => "#",
                TokenType.UNDERSCORE => "_",
                TokenType.QUESTION => "?",
                TokenType.NEGATE => "~",
                TokenType.DOLLAR => "$",
                TokenType.AND => "_and",
                TokenType.OR => "_or",
                TokenType.XOR => "_xor",
                TokenType.ROT => "_rot",
                TokenType.SHIFT => "_shift",
                TokenType.LSQ => "_lsq",
                TokenType.DIV => "_div",
                TokenType.DIRECTORY => "_d",
                TokenType.IN => "_in",
                TokenType.BIN => "_bin",
                TokenType.BINL => "_binl",
                TokenType.LIN => "_lin",
                TokenType.DV => "_dv",
                TokenType.DVL => "_dvl",
                TokenType.DI => "_di",
                TokenType.VS => "_vs",
                TokenType.SV => "_sv",
                TokenType.SS => "_ss",
                TokenType.SM => "_sm",
                TokenType.CI => "_ci",
                TokenType.IC => "_ic",
                TokenType.DRAW => "_draw",
                TokenType.GETENV => "_getenv",
                TokenType.SETENV => "_setenv",
                TokenType.SIZE => "_size",
                TokenType.BD => "_bd",
                TokenType.DB => "_db",
                TokenType.EXIT => "_exit",
                TokenType.LT => "_lt",
                TokenType.JD => "_jd",
                TokenType.DJ => "_dj",
                TokenType.GTIME => "_gtime",
                TokenType.LTIME => "_ltime",
                TokenType.CEIL => "_ceil",
                TokenType.FLOOR_MATH => "_floor",
                TokenType.LOG => "_log",
                TokenType.EXP => "_exp",
                TokenType.ABS => "_abs",
                TokenType.SQR => "_sqr",
                TokenType.SQRT => "_sqrt",
                TokenType.SIN => "_sin",
                TokenType.COS => "_cos",
                TokenType.TAN => "_tan",
                TokenType.ASIN => "_asin",
                TokenType.ACOS => "_acos",
                TokenType.ATAN => "_atan",
                TokenType.SINH => "_sinh",
                TokenType.COSH => "_cosh",
                TokenType.TANH => "_tanh",
                TokenType.NOT => "_not",
                TokenType.DISPOSE => "_dispose",
                TokenType.SETHINT => "_sethint",
                TokenType.GETHINT => "_gethint",
                TokenType.PARSE => "_parse",
                TokenType.EVAL => "_eval",
                TokenType.INV => "_inv",
                _ => tokenType.ToString()
            };
            
            tokenTypeToVerbNameCache[tokenType] = verbName;
            return verbName;
        }

        /// <summary>
        /// Check if token type is a monadic operator with caching and context awareness
        /// </summary>
        public static bool IsMonadicOperator(TokenType tokenType, VerbArityContext? context = null)
        {
            if (monadicOperatorCache.TryGetValue(tokenType, out var cachedResult))
            {
                cacheHits++;
                return cachedResult;
            }
            
            lookupCount++;
            var arity = DetermineVerbArity(tokenType, context);
            var result = arity == 1;
            monadicOperatorCache[tokenType] = result;
            return result;
        }

        /// <summary>
        /// Check if token type is a dyadic operator with caching and context awareness
        /// </summary>
        public static bool IsDyadicOperator(TokenType tokenType, VerbArityContext? context = null)
        {
            if (dyadicOperatorCache.TryGetValue(tokenType, out var cachedResult))
            {
                cacheHits++;
                return cachedResult;
            }
            
            lookupCount++;
            var arity = DetermineVerbArity(tokenType, context);
            var result = arity == 2;
            dyadicOperatorCache[tokenType] = result;
            return result;
        }

        /// <summary>
        /// Check if token type is a verb token with caching
        /// </summary>
        public static bool IsVerbToken(TokenType tokenType, VerbArityContext? context = null)
        {
            if (verbTokenCache.TryGetValue(tokenType, out var cachedResult))
            {
                cacheHits++;
                return cachedResult;
            }
            
            lookupCount++;
            var verbName = TokenTypeToVerbName(tokenType);
            var verb = GetVerb(verbName);
            // Include all verb types that can act as operators: Operator, SystemFunction, and Function
            var result = verb != null && (verb.Type == VerbType.Operator || 
                                          verb.Type == VerbType.SystemFunction || 
                                          verb.Type == VerbType.Function);
            verbTokenCache[tokenType] = result;
            return result;
        }

        /// <summary>
        /// Check if a verb supports a specific arity
        /// </summary>
        public static bool SupportsArity(TokenType tokenType, int arity, VerbArityContext? context = null)
        {
            var verbName = TokenTypeToVerbName(tokenType);
            var verb = GetVerb(verbName);
            return verb != null && verb.SupportedArities.Contains(arity);
        }

        /// <summary>
        /// Get supported arities for a token type
        /// </summary>
        public static int[] GetSupportedArities(TokenType tokenType, VerbArityContext? context = null)
        {
            var verbName = TokenTypeToVerbName(tokenType);
            return GetSupportedArities(verbName);
        }

        /// <summary>
        /// Determine verb arity based on context analysis
        /// </summary>
        public static int DetermineVerbArity(TokenType tokenType, VerbArityContext? context)
        {
            context ??= new VerbArityContext();
            
            // Check VerbRegistry for fixed-arity verbs first
            var verbName = TokenTypeToVerbName(tokenType);
            var verbInfo = GetVerb(verbName);
            if (verbInfo != null)
            {
                // System verbs with fixed arity
                if (verbInfo.SupportedArities.Length == 1)
                    return verbInfo.SupportedArities[0];
                    
                // Context-sensitive verbs - use parser analysis
                return AnalyzeContextForArity(tokenType, context, verbInfo.SupportedArities);
            }
            
            // Fallback to context analysis for unregistered verbs
            return AnalyzeContextForArity(tokenType, context, new[] { 1, 2 });
        }

        /// <summary>
        /// Analyze context to determine verb arity
        /// </summary>
        private static int AnalyzeContextForArity(TokenType tokenType, VerbArityContext context, int[] supportedArities)
        {
            // 1. Check bracket notation for explicit argument count
            if (context.HasBracketNotation)
                return context.BracketArgumentCount;
                
            // 2. Check for left operand (dyadic indication)
            if (context.HasLeftOperand && supportedArities.Contains(2))
                return 2;
                
            // 3. Check for expression start (monadic indication)
            if (context.IsAtExpressionStart && supportedArities.Contains(1))
                return 1;
                
            // 4. Special disambiguation rules
            return tokenType switch
            {
                // Colon: assignment vs return
                TokenType.COLON when context.HasLeftOperand => 2, // Assignment
                TokenType.COLON => 1, // Return
                
                // Hash: count vs take  
                TokenType.HASH when context.HasLeftOperand => 2, // Take
                TokenType.HASH => 1, // Count
                
                // Default: prefer dyadic if left operand present
                _ when context.HasLeftOperand && supportedArities.Contains(2) => 2,
                _ when supportedArities.Contains(1) => 1,
                _ => supportedArities.Max() // Fallback to highest supported arity
            };
        }

        public static IEnumerable<VerbInfo> GetAllVerbs()
        {
            return verbs.Values;
        }

        public static bool IsVerb(string tokenName)
        {
            return verbs.ContainsKey(tokenName);
        }

        private static void InitializeBasicOperators()
        {
            // Clear all caches to ensure clean state before registration
            ClearCaches();
            
            // Primitive verbs
            // + - * % | & ^ < > = ! # _ ~ $ ? @ . ,
            RegisterVerb("+", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("PLUS", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("-", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("MINUS", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("*", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("MULTIPLY", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("%", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("DIVIDE", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("|", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("MAX", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("&", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("MIN", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("^", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("POWER", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("<", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("LESS", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb(">", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("GREATER", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("=", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("EQUAL", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("!", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("MODULUS", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("#", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("HASH", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("_", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("UNDERSCORE", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("~", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("MATCH", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("$", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("DOLLAR", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("?", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("QUESTION", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("@", VerbType.Operator, new[] { 1, 2, 3, 4 }, null);
            RegisterVerb("APPLY", VerbType.Operator, new[] { 1, 2, 3, 4 }, null);
            RegisterVerb(".", VerbType.Operator, new[] { 1, 2, 3, 4 }, null);
            RegisterVerb("DOT_APPLY", VerbType.Operator, new[] { 1, 2, 3, 4 }, null);
            RegisterVerb(",", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("JOIN", VerbType.Operator, new[] { 1, 2 }, null);

            // Monadic (incl forced monadic, with disambiguating colon)
            RegisterVerb("+:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("FLIP", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("-:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("ARITHMETICNEGATE", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("*:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("MULTIPLY", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("%:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("INVERT", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("|:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("REVERSE", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("&:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("WHERE", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("^:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("SHAPE", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("<:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("GRADEUP", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb(">:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("GRADEDOWN", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("=:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("GROUP", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("!:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("ENUMERATE", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("#:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("COUNT", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("_:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("TOLERANTFLOOR", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("~:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("LOGICALNEGATE", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("$:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("FORMAT", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("?:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("UNIQUES", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("@:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("ATOM", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb(".:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("MAKE", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb(",:", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("ENLIST", VerbType.Operator, new[] { 1 }, null);
            RegisterVerb("COLON", VerbType.Operator, new[] { 1 }, null); // Return

            // Mathematical functions - register both symbol and token type names
            // _log _exp _abs _sqr _sqrt _floor _ceil _dot _mul _inv
            // _sin _cos _tan _asin _acos _atan _sinh _cosh _tanh
            // _div (integer) _and _or _xor _not _rot _shift (bitwise)
            // y _lsq A is least squares x for y~+/A*x (i.e. Ax=y)
            RegisterVerb("_log", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("LOG", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_exp", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("EXP", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_abs", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("ABS", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_sqr", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("SQR", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_sqrt", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("SQRT", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_floor", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("FLOOR_MATH", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_ceil", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("CEIL", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_dot", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("DOT_MULTIPLY", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_mul", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("MUL", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_inv", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("INV", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_sin", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("SIN", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_cos", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("COS", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_tan", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("TAN", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_asin", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("ASIN", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_acos", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("ACOS", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_atan", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("ATAN", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_sinh", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("SINH", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_cosh", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("COSH", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_tanh", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("TANH", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_div", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("DIV", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_and", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("AND", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_or", VerbType.SystemFunction, new[] { 2 }, null);            
            RegisterVerb("OR", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_xor", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("XOR", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_not", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("NOT", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_rot", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("ROT", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_shift", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("SHIFT", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_lsq", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("LSQ", VerbType.SystemFunction, new[] { 2 }, null);

            // Rand Functions
            // x _draw y (from !y); x _draw -y (deal from !y); x _draw 0 (from (0,1))
            RegisterVerb("_draw", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("DRAW", VerbType.SystemFunction, new[] { 2 }, null);

            // Time Functions
            // _t is gmt seconds. _lt is local from gmt, e.g. _gtime _lt _t
            // _jd yyyymmdd (and _dj) for to and from julian day number (0 is monday)
            RegisterVerb("_T", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("DAYS", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_t", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("SECONDS", VerbType.SystemVariable, new[] { 0 }, null);
            //RegisterVerb("TIME", VerbType.Operator, new[] { 0 }, null);
            RegisterVerb("_lt", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("LT", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_gtime", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("GTIME", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_ltime", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("LTIME", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_jd", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("JD", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_dj", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("DJ", VerbType.SystemFunction, new[] { 1 }, null);

            // List
            // x _in y is 1 if x is an item of y; 0 otherwise (list: _lin)
            // x _bin y is binary search for y in ascending x (list: _binl)
            // x _dv y and x _di y to delete by value and index (list: _dvl)
            // x _sv v (scalar from vector) and x _vs s (vector from scalar)
            // _ci i (character from integer) and _ic c (integer from character)
            // x _sm y is string match. y can have *?[^-], e.g. files _sm "*.[kK]"
            // x _ss y is string/symbol search for start indices. y can have ?[^-].
            // _ssr[x;y;z] is string/symbol search and replace. z can be a function.
            RegisterVerb("_in", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("IN", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_lin", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("LIN", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_bin", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("BIN", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_binl", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("BINL", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_dv", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("DV", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_dvl", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("DVL", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_di", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("DI", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_sv", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("SV", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_vs", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("VS", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_ci", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("CI", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_ic", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("IC", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_sm", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("SM", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_ss", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("SS", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_ssr", VerbType.SystemFunction, new[] { 3 }, null);
            RegisterVerb("SSR", VerbType.SystemFunction, new[] { 3 }, null);

            // Data verbs
            // _bd d (bytes from data) and _db b (data from bytes).
            RegisterVerb("_bd", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("BD", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_db", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("DB", VerbType.SystemFunction, new[] { 1 }, null);

            // System verbs
            // _getenv v (v _setenv s) gets(sets) environment variable v.
            // _host addr; _host name; _size file; _del file; old _rename new
            // _exit code; _kill PIDS
            RegisterVerb("_getenv", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("GETENV", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_setenv", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("SETENV", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("_size", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("SIZE", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_host", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("HOST", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_exit", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("EXIT", VerbType.SystemFunction, new[] { 1 }, null);

            // System variables (0-arity)
            // _d(dir) _v(var) _i(index) _t(second) _f(function) _n(null) _s(space)
            // _h(host) _p(port) _P(PID) _w(who) _u(user) _a(address) _k(version) _T(time)
            // _o(os) _c(cores) _r(RAM) _m(mach id) _y(stack) _b(backtrace if enabled)
            RegisterVerb("_d", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_v", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_i", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_f", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_n", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_s", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_h", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_p", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_P", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_w", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_u", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_a", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_k", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_o", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_c", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_r", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_m", VerbType.SystemVariable, new[] { 0 }, null);
            RegisterVerb("_y", VerbType.SystemVariable, new[] { 0 }, null);            
            
            // Also register NULL for TokenType.NULL
            RegisterVerb("NULL", VerbType.SystemVariable, new[] { 0 }, null);

            // I/O verbs (0:, 1:, 2:, 3:, 4:, 5:, 6:, 7:, 8:, 9:)
            for (int i = 0; i <= 9; i++)
            {
                var ioNum = i.ToString();
                RegisterVerb(ioNum+":", VerbType.Operator, new[] { 1, 2 }, null); // Dyadic
                RegisterVerb(ioNum+"::", VerbType.Operator, new[] { 1 }, null); // Forced Monadic with disambiguating colon
            }
            
            // Also register with the TokenType names that the evaluator expects
            RegisterVerb("IO_VERB_0", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_1", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_2", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_3", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_4", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_5", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_6", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_7", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_8", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("IO_VERB_9", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("TYPE", VerbType.Operator, new[] { 1, 2 }, null);
            RegisterVerb("STRING_REPRESENTATION", VerbType.Operator, new[] { 1, 2 }, null);

            // ksharp new verbs: parser and FFI
            RegisterVerb("_parse", VerbType.SystemFunction, new[] { 1 }, new Func<K3Value[], K3Value>?[] { ParseVerbHandler.Parse, null });
            RegisterVerb("_eval", VerbType.SystemFunction, new[] { 1 }, new Func<K3Value[], K3Value>?[] { EvalVerbHandler.Evaluate, null });
            RegisterVerb("_dispose", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_gethint", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("GETHINT", VerbType.SystemFunction, new[] { 1 }, null);
            RegisterVerb("_sethint", VerbType.SystemFunction, new[] { 2 }, null);
            RegisterVerb("SETHINT", VerbType.SystemFunction, new[] { 2 }, null);
        }

        // This is a hack for _eval, we should fix _eval and remove it
        private static void PopulateImplementationDelegates()
        {
            // Only implement basic arithmetic operators that have K3Value methods
            // All other operators will be handled by the evaluator
            UpdateVerbImplementations("+", new Func<K3Value[], K3Value>?[] { 
                null, // No monadic implementation for +
                args => args[0].Add(args[1])
            });
            
            UpdateVerbImplementations("-", new Func<K3Value[], K3Value>?[] { 
                null, // Monadic - will be handled by evaluator
                args => args[0].Subtract(args[1])
            });
            
            UpdateVerbImplementations("*", new Func<K3Value[], K3Value>?[] { 
                null, // No monadic implementation for *
                args => args[0].Multiply(args[1])
            });
            
            UpdateVerbImplementations("%", new Func<K3Value[], K3Value>?[] { 
                null, // Monadic % will be handled by evaluator
                args => args[0].Divide(args[1])
            });
            
            UpdateVerbImplementations(",", new Func<K3Value[], K3Value>?[] { 
                null, // No monadic implementation for ,
                args => {
                    // Implement join logic manually since it's commonly used
                    var elements = new List<K3Value>();
                    
                    if (args[0] is VectorValue vecA)
                    {
                        elements.AddRange(vecA.Elements);
                    }
                    else
                    {
                        elements.Add(args[0]);
                    }
                    
                    if (args[1] is VectorValue vecB)
                    {
                        elements.AddRange(vecB.Elements);
                    }
                    else
                    {
                        elements.Add(args[1]);
                    }
                    
                    return new VectorValue(elements);
                }
            });
            
            // Common modified verbs (operators with colons)
            UpdateVerbImplementations("*:", new Func<K3Value[], K3Value>?[] { 
                args => {
                    // Monadic *: is "first" - return first element
                    if (args[0] is VectorValue vec && vec.Elements.Count > 0)
                    {
                        return vec.Elements[0];
                    }
                    else
                    {
                        return args[0]; // First on scalar returns scalar
                    }
                },
                args => {
                    // Dyadic *: is "each" - apply to each element
                    if (args[0] is VectorValue vec)
                    {
                        var elements = new List<K3Value>();
                        foreach (var elem in vec.Elements)
                        {
                            elements.Add(elem.Multiply(args[1]));
                        }
                        return new VectorValue(elements);
                    }
                    else
                    {
                        return args[0].Multiply(args[1]);
                    }
                }
            });
                        
            // System operators - need to access Evaluator methods
            // These will be handled by the evaluator's method dispatch
            UpdateVerbImplementations("GTIME", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_gtime", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("LTIME", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_ltime", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("GETENV", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_getenv", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("SETENV", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_setenv", new Func<K3Value[], K3Value>?[] { null, null });
            
            // Mathematical functions - delegate to Evaluator_Math.cs implementations via Evaluator switch
            UpdateVerbImplementations("CEIL", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_ceil", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("FLOOR", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_floor", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("SIN", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_sin", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("COS", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_cos", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("TAN", new Func<K3Value[], K3Value>?[] { null, null });
            UpdateVerbImplementations("_tan", new Func<K3Value[], K3Value>?[] { null, null });
        }

        // This is a hack for _eval, we should fix _eval and remove it
        private static void UpdateVerbImplementations(string name, Func<K3Value[], K3Value>?[]? implementations)
        {
            if (verbs.TryGetValue(name, out var verb))
            {
                verb.Implementations = implementations;
            }
        }

        /// <summary>
        /// Get the preferred arity for an operator based on context and position
        /// </summary>
        public static int GetPreferredArity(string verbName, bool hasLeftOperand, bool hasRightOperand = false)
        {
            var verb = GetVerb(verbName);
            if (verb == null) return 0;
            
            // For multi-arity operators, determine preferred arity based on context
            if (verb.SupportedArities.Length > 1)
            {                
                // For other multi-arity operators, prefer dyadic if we have both operands
                if (hasLeftOperand && hasRightOperand && verb.SupportedArities.Contains(2))
                    return 2;
                
                // Prefer monadic if we only have one operand or no left operand
                if (!hasLeftOperand && verb.SupportedArities.Contains(1))
                    return 1;
            }
            
            // Return the highest supported arity as fallback
            return verb.SupportedArities.Length > 0 ? verb.SupportedArities.Max() : 0;
        }

        /// <summary>
        /// Check if an operator supports a specific arity
        /// </summary>
        public static bool SupportsArity(string verbName, int arity)
        {
            var verb = GetVerb(verbName);
            return verb != null && verb.SupportedArities.Contains(arity);
        }

        /// <summary>
        /// Context for arity determination following K language specification
        /// </summary>
        public class ArityContext
        {
            public bool HasLeftOperand { get; set; }
            public bool HasRightOperand { get; set; }
            public int ArgumentCount { get; set; }
            public bool HasDisambiguatingColon { get; set; }
            public bool HasAdverbOnRight { get; set; }
            public bool IsArgumentToAmend { get; set; }
            public bool IsSingleExpressionInParentheses { get; set; }
            public bool IsAtExpressionStart { get; set; }
        }

        /// <summary>
        /// Determine verb arity following K language specification rules
        /// </summary>
        /// <param name="verbName">Name of the verb</param>
        /// <param name="context">Context information for arity determination</param>
        /// <returns>Determined arity (1=monadic, 2=dyadic, 3=triadic, 4=tetradic)</returns>
        public static int DetermineVerbArity(string verbName, ArityContext context)
        {
            var verb = GetVerb(verbName);
            if (verb == null) return 0;
            
            var supportedArities = verb.SupportedArities;
            
            // Rule 1: If the verb only supports one arity return that arity
            if (supportedArities.Length == 1)
            {
                return supportedArities[0];
            }
            
            // Rule 2: If the verb is called with 4 arguments and it supports tetradic use then return tetradic
            if (context.ArgumentCount == 4 && supportedArities.Contains(4))
            {
                return 4;
            }
            
            // Rule 3: If the verb is called with 3 arguments and it supports triadic use then return triadic
            if (context.ArgumentCount == 3 && supportedArities.Contains(3))
            {
                return 3;
            }
            
            // Rule 4: If the verb is called with a right argument and there is no left argument then return monadic
            if (context.HasRightOperand && !context.HasLeftOperand && supportedArities.Contains(1))
            {
                return 1;
            }
            
            // Rule 5: If the verb doesn't have arguments directly and it has a disambiguating colon then return monadic
            if (!context.HasLeftOperand && !context.HasRightOperand && 
                (context.HasAdverbOnRight || context.IsArgumentToAmend || context.IsSingleExpressionInParentheses) &&
                context.HasDisambiguatingColon && supportedArities.Contains(1))
            {
                return 1;
            }
            
            // Rule 6: If none of the previous conditions are met then return dyadic
            if (supportedArities.Contains(2))
            {
                return 2;
            }
            
            // Fallback: return the highest supported arity
            return supportedArities.Max();
        }

        /// <summary>
        /// Validate verb arity and provide helpful error message
        /// </summary>
        public static string ValidateVerbArity(string verbName, int arity)
        {
            var verb = GetVerb(verbName);
            if (verb == null)
                return $"Unknown verb: {verbName}";
            
            if (!verb.SupportedArities.Contains(arity))
            {
                var aritiesStr = string.Join(", ", verb.SupportedArities);
                return $"Verb '{verbName}' does not support {arity} argument{(arity == 1 ? "" : "s")}. Supported arities: [{aritiesStr}]";
            }
            
            return string.Empty; // No error
        }

        /// <summary>
        /// Fast check if verb exists - optimized for performance
        /// </summary>
        public static bool HasVerb(string verbName)
        {
            return verbs.ContainsKey(verbName);
        }

        /// <summary>
        /// Get all verbs of a specific type
        /// </summary>
        public static IEnumerable<string> GetVerbsByType(VerbType type)
        {
            return verbs.Where(kvp => kvp.Value.Type == type).Select(kvp => kvp.Key);
        }

        /// <summary>
        /// Get verb description for debugging/help
        /// </summary>
        public static string GetVerbDescription(string verbName)
        {
            var verb = GetVerb(verbName);
            return verb?.Description ?? $"Unknown verb: {verbName}";
        }

        /// <summary>
        /// Check if a verb is a projected function (adverb-modified)
        /// </summary>
        public static bool IsProjectedFunction(string verbName)
        {
            var verb = GetVerb(verbName);
            return verb?.Type == VerbType.ProjectedFunction;
        }

        /// <summary>
        /// Get the remaining required arguments for a projected function
        /// </summary>
        public static int GetRemainingArity(string verbName)
        {
            var verb = GetVerb(verbName);
            if (verb?.Type == VerbType.ProjectedFunction)
            {
                // For projected functions, return the remaining arity
                // This is a simplified implementation - in a full version, we'd track projection state
                return verb.SupportedArities.Length > 0 ? verb.SupportedArities.Max() - 1 : 0;
            }
            return verb?.SupportedArities.Length > 0 ? verb.SupportedArities.Max() : 0;
        }

        /// <summary>
        /// Register a new projected function dynamically
        /// </summary>
        public static void RegisterProjectedFunction(string name, int[] supportedArities, string description)
        {
            var projectedVerb = new VerbInfo
            {
                Name = name,
                SupportedArities = supportedArities,
                Type = VerbType.ProjectedFunction,
                Description = description
            };
            
            verbs[name] = projectedVerb;
        }

        /// <summary>
        /// Check if a verb can be used with adverbs
        /// </summary>
        public static bool SupportsAdverbs(string verbName, string? adverbType = null)
        {
            var verb = GetVerb(verbName);
            if (verb == null) 
                return false;
            
            // If no specific adverb type, check if verb supports any adverb-compatible operations
            if (string.IsNullOrEmpty(adverbType))
                return (verb.Type == VerbType.Operator || verb.Type == VerbType.Function) &&
                       verb.SupportedArities.Contains(1);
            
            // Check specific adverb requirements
            return adverbType switch
            {
                // Over (/) and Scan (\) - support monadic and dyadic verbs
                "over" or "scan" => verb.SupportedArities.Contains(1) || verb.SupportedArities.Contains(2),
                
                // Each (') - supports monadic verbs only
                "each" => verb.SupportedArities.Contains(1),
                
                // Each-right (/), Each-left (\), Each-prior (') - support dyadic verbs
                "each-right" or "each-left" or "each-prior" => verb.SupportedArities.Contains(2),
                
                _ => (verb.Type == VerbType.Operator || verb.Type == VerbType.Function) &&
                       verb.SupportedArities.Contains(1)
            };
        }

        /// <summary>
        /// Check if a token type is an adverb
        /// </summary>
        public static bool IsAdverbToken(TokenType tokenType)
        {
            return tokenType == TokenType.ADVERB_SLASH || tokenType == TokenType.ADVERB_BACKSLASH || tokenType == TokenType.ADVERB_TICK ||
                   tokenType == TokenType.ADVERB_SLASH_COLON || tokenType == TokenType.ADVERB_BACKSLASH_COLON || tokenType == TokenType.ADVERB_TICK_COLON;
        }

        /// <summary>
        /// Get the string representation of an adverb type
        /// </summary>
        public static string GetAdverbType(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.ADVERB_SLASH => "over",
                TokenType.ADVERB_BACKSLASH => "scan",
                TokenType.ADVERB_TICK => "each",
                TokenType.ADVERB_SLASH_COLON => "each-right",
                TokenType.ADVERB_BACKSLASH_COLON => "each-left",
                TokenType.ADVERB_TICK_COLON => "each-prior",
                _ => tokenType.ToString()
            };
        }

        /// <summary>
        /// Check if a token type is a dyadic operator
        /// </summary>
        public static bool IsDyadicOperatorToken(TokenType tokenType)
        {
            var verb = GetVerb(tokenType.ToString());
            return verb != null && verb.Type == VerbType.Operator;
        }

        /// <summary>
        /// Get tokens that should stop parsing until end of expression
        /// </summary>
        public static readonly TokenType[] ParseUntilEndStopTokens = {
            TokenType.RIGHT_PAREN, TokenType.RIGHT_BRACE, TokenType.SEMICOLON, TokenType.NEWLINE, TokenType.EOF
        };

        /// <summary>
        /// Get default stop tokens for expression parsing
        /// </summary>
        public static readonly TokenType[] DefaultStopTokens = {
            TokenType.PLUS, TokenType.MINUS, TokenType.MULTIPLY, TokenType.DIVIDE, TokenType.DOT_PRODUCT, TokenType.MIN, TokenType.MAX,
            TokenType.LESS, TokenType.GREATER, TokenType.EQUAL, TokenType.IN, TokenType.POWER, TokenType.MODULUS,
            TokenType.COLON, TokenType.HASH, TokenType.UNDERSCORE, TokenType.QUESTION, TokenType.MATCH, TokenType.NEGATE, TokenType.DOLLAR, TokenType.RIGHT_PAREN,
            TokenType.RIGHT_BRACE, TokenType.RIGHT_BRACKET, TokenType.SEMICOLON, TokenType.NEWLINE, TokenType.ASSIGNMENT, TokenType.GLOBAL_ASSIGNMENT,
            TokenType.LEFT_BRACKET, TokenType.APPLY, TokenType.DOT_APPLY, TokenType.TYPE, TokenType.STRING_REPRESENTATION,
            TokenType.ADVERB_SLASH, TokenType.ADVERB_BACKSLASH, TokenType.ADVERB_TICK,
            TokenType.ADVERB_SLASH_COLON, TokenType.ADVERB_BACKSLASH_COLON, TokenType.ADVERB_TICK_COLON,
            TokenType.TIME, TokenType.IN, TokenType.BIN, TokenType.BINL, TokenType.LSQ, TokenType.LIN,
            TokenType.GTIME, TokenType.LTIME, TokenType.VS, TokenType.SV, TokenType.SS, TokenType.CI, TokenType.IC,
            TokenType.AND, TokenType.OR, TokenType.XOR, TokenType.ROT, TokenType.SHIFT,
            TokenType.DIRECTORY, TokenType.BD, TokenType.DB, TokenType.DO, TokenType.WHILE, TokenType.IF_FUNC, TokenType.EXIT, TokenType.EOF
        };

        /// <summary>
        /// Check if parsing should stop for given stop tokens
        /// </summary>
        public static bool ShouldStopParsing(TokenType currentToken, TokenType[] stopTokens)
        {
            return stopTokens.Contains(currentToken);
        }

        /// <summary>
        /// Get all dyadic operator token types
        /// </summary>
        public static readonly TokenType[] DyadicOperatorTokens = {
            TokenType.PLUS, TokenType.MINUS, TokenType.MULTIPLY, TokenType.DIVIDE, TokenType.DOT_PRODUCT, TokenType.MUL, 
            TokenType.MIN, TokenType.MAX, TokenType.LESS, TokenType.GREATER, TokenType.EQUAL, TokenType.IN, 
            TokenType.POWER, TokenType.MODULUS, TokenType.JOIN, TokenType.COLON, TokenType.HASH, 
            TokenType.UNDERSCORE, TokenType.QUESTION, TokenType.MATCH, TokenType.NEGATE, TokenType.DOLLAR, 
            TokenType.LSQ, TokenType.AND, TokenType.OR, TokenType.XOR, TokenType.ROT, TokenType.SHIFT,
            TokenType.APPLY, TokenType.DOT_APPLY
        };

        /// <summary>
        /// Get string symbol for dyadic operator token
        /// </summary>
        public static string GetDyadicOperatorSymbol(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.PLUS => "+",
                TokenType.MINUS => "-",
                TokenType.MULTIPLY => "*",
                TokenType.DIVIDE => "%",
                TokenType.DOT_PRODUCT => "_dot",
                TokenType.MUL => ".*",
                TokenType.MIN => "&",
                TokenType.MAX => "|",
                TokenType.LESS => "<",
                TokenType.GREATER => ">",
                TokenType.EQUAL => "=",
                TokenType.IN => "in",
                TokenType.POWER => "^",
                TokenType.MODULUS => "!",
                TokenType.JOIN => ",",
                TokenType.COLON => ":",
                TokenType.HASH => "#",
                TokenType.UNDERSCORE => "_",
                TokenType.QUESTION => "?",
                TokenType.MATCH => "~",
                TokenType.NEGATE => "~",
                TokenType.DOLLAR => "$",
                TokenType.LSQ => "_lsq",
                TokenType.AND => "&",
                TokenType.OR => "|",
                TokenType.XOR => "^",
                TokenType.ROT => "rot",
                TokenType.SHIFT => "shift",
                TokenType.APPLY => "@",
                TokenType.DOT_APPLY => ".",
                TokenType.PARSE => "_parse",
                TokenType.EVAL => "_eval",
                TokenType.ADVERB_SLASH => "/",
                TokenType.ADVERB_BACKSLASH => "\\",
                TokenType.ADVERB_TICK => "'",
                TokenType.ADVERB_SLASH_COLON => "/:",
                TokenType.ADVERB_BACKSLASH_COLON => "\\:",
                TokenType.ADVERB_TICK_COLON => "':",
                _ => tokenType.ToString()
            };
        }

        /// <summary>
        /// Check if token type is a dyadic operator (comprehensive)
        /// </summary>
        public static bool IsDyadicOperator(TokenType tokenType)
        {
            return DyadicOperatorTokens.Contains(tokenType);
        }

        /// <summary>
        /// Get delimiter tokens (parentheses, brackets, braces)
        /// </summary>
        public static readonly TokenType[] DelimiterTokens = {
            TokenType.LEFT_PAREN, TokenType.RIGHT_PAREN,
            TokenType.LEFT_BRACE, TokenType.RIGHT_BRACE,
            TokenType.LEFT_BRACKET, TokenType.RIGHT_BRACKET
        };

        /// <summary>
        /// Check if a token is a delimiter
        /// </summary>
        public static bool IsDelimiterToken(TokenType tokenType)
        {
            return tokenType == TokenType.LEFT_PAREN || tokenType == TokenType.RIGHT_PAREN ||
                   tokenType == TokenType.LEFT_BRACE || tokenType == TokenType.RIGHT_BRACE ||
                   tokenType == TokenType.LEFT_BRACKET || tokenType == TokenType.RIGHT_BRACKET;
        }

        /// <summary>
        /// Get statement separator tokens
        /// </summary>
        public static readonly TokenType[] StatementSeparatorTokens = {
            TokenType.SEMICOLON, TokenType.NEWLINE
        };

        /// <summary>
        /// Check if a token is a statement separator
        /// </summary>
        public static bool IsStatementSeparatorToken(TokenType tokenType)
        {
            return tokenType == TokenType.SEMICOLON || tokenType == TokenType.NEWLINE;
        }

        /// <summary>
        /// Extract digit from I/O verb token
        /// </summary>
        public static int GetIOVerbDigit(TokenType tokenType)
        {
            return tokenType switch
            {
                TokenType.IO_VERB_0 => 0,
                TokenType.IO_VERB_1 => 1,
                TokenType.IO_VERB_2 => 2,
                TokenType.IO_VERB_3 => 3,
                TokenType.IO_VERB_4 => 4,
                TokenType.IO_VERB_5 => 5,
                TokenType.IO_VERB_6 => 6,
                TokenType.IO_VERB_7 => 7,
                TokenType.IO_VERB_8 => 8,
                TokenType.IO_VERB_9 => 9,
                _ => throw new ArgumentException($"Invalid I/O verb token: {tokenType}")
            };
        }

        /// <summary>
        /// Get performance statistics for the VerbRegistry
        /// </summary>
        public static Dictionary<string, object> GetPerformanceStats()
        {
            var cacheStats = GetCacheStats();
            return new Dictionary<string, object>
            {
                ["TotalVerbs"] = verbs.Count,
                ["Operators"] = verbs.Count(kvp => kvp.Value.Type == VerbType.Operator),
                ["SystemVariables"] = verbs.Count(kvp => kvp.Value.Type == VerbType.SystemVariable),
                ["Functions"] = verbs.Count(kvp => kvp.Value.Type == VerbType.Function),
                ["ProjectedFunctions"] = verbs.Count(kvp => kvp.Value.Type == VerbType.ProjectedFunction),
                ["VerbsWithImplementations"] = verbs.Count(kvp => kvp.Value.Implementations != null),
                ["MultiArities"] = verbs.Count(kvp => kvp.Value.SupportedArities.Length > 1)
            }.Union(cacheStats).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Export verb registry information for debugging/documentation
        /// </summary>
        public static string ExportVerbInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Verb Registry Information ===");
            sb.AppendLine($"Total verbs: {verbs.Count}");
            sb.AppendLine();
            
            foreach (var type in Enum.GetValues<VerbType>())
            {
                var verbsByType = verbs.Where(kvp => kvp.Value.Type == type).OrderBy(kvp => kvp.Key);
                sb.AppendLine($"{type} ({verbsByType.Count()}):");
                
                foreach (var kvp in verbsByType)
                {
                    var verb = kvp.Value;
                    var arities = string.Join(",", verb.SupportedArities);
                    var hasImpl = verb.Implementations != null ? "✓" : "✗";
                    sb.AppendLine($"  {verb.Name} [{arities}] {hasImpl} - {verb.Description}");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Validate the integrity of the VerbRegistry
        /// </summary>
        public static List<string> ValidateRegistry()
        {
            var issues = new List<string>();
            
            foreach (var kvp in verbs)
            {
                var verb = kvp.Value;
                
                // Check for empty name
                if (string.IsNullOrEmpty(verb.Name))
                {
                    issues.Add("Verb with empty name found");
                }
                
                // Check for empty supported arities
                if (verb.SupportedArities.Length == 0)
                {
                    issues.Add($"Verb '{verb.Name}' has no supported arities");
                }
                
                // Check for invalid arities (negative or zero)
                if (verb.SupportedArities.Any(a => a <= 0))
                {
                    issues.Add($"Verb '{verb.Name}' has invalid arities: [{string.Join(",", verb.SupportedArities)}]");
                }
                
                // Check for duplicate arities
                var uniqueArities = verb.SupportedArities.Distinct().ToArray();
                if (uniqueArities.Length != verb.SupportedArities.Length)
                {
                    issues.Add($"Verb '{verb.Name}' has duplicate arities");
                }
                
                // Check if implementations array matches supported arities
                if (verb.Implementations != null)
                {
                    if (verb.Implementations.Length < verb.SupportedArities.Max())
                    {
                        issues.Add($"Verb '{verb.Name}' implementations array is too small for max arity");
                    }
                }
            }
            
            return issues;
        }
    }
}
