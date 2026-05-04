using System.Collections.Generic;
using System.Linq;
using K3CSharp.Parsing;

namespace K3CSharp
{
    public partial class Evaluator
    {
        
        private K3Value ApplySymbolVerb(string verbName, K3Value left, K3Value right)
        {
            // Handle SymbolValue verbs by name
            
            // Check if it's a built-in operator (single character)
            if (verbName.Length == 1)
            {
                return ApplyBuiltInOperator(verbName[0], left, right);
            }
            
            // For all other verbs, use AST approach with preserved verb name
            var verbNode = new ASTNode(ASTNodeType.DyadicOp, new SymbolValue(verbName));
            verbNode.Children.Add(ASTNode.MakeLiteral(left));
            verbNode.Children.Add(ASTNode.MakeLiteral(right));
            
            return Evaluate(verbNode);
        }
        
        private K3Value ApplyBuiltInOperator(char op, K3Value left, K3Value right)
        {
            return op switch
            {
                '+' => Plus(left, right),
                '-' => Minus(left, right),
                '*' => Times(left, right),
                '%' => Divide(left, right),
                '&' => Min(left, right),
                '|' => Max(left, right),
                '<' => Less(left, right),
                '>' => More(left, right),
                '=' => Match(left, right),
                '^' => Power(left, right),
                '!' => ModRotate(left, right),
                ',' => Join(left, right),
                '#' => Take(left, right),
                '_' => FloorBinary(left, right),
                '@' => AtIndex(left, right),
                '.' => DotApply(left, right),
                '$' => Format(left, right),
                '~' => Match(left, right),
                '?' => Find(left, right),
                _ => throw new Exception($"Unknown operator: {op}")
            };
        }
        
        private K3Value ExecuteFunction(FunctionValue func, List<K3Value> arguments)
        {
            // Execute the function using the full function call machinery
            var tempNode = new ASTNode(ASTNodeType.Function);
            tempNode.Value = func;
            return CallDirectFunction(tempNode, arguments);
        }
        

        private K3Value ApplyMonadicVerb(string verbName, K3Value operand)
        {
            // Use VerbRegistry to handle all monadic verbs dynamically
            if (VerbRegistry.HasVerb(verbName))
            {
                var verbInfo = VerbRegistry.GetVerb(verbName);
                if (verbInfo != null && verbInfo.SupportedArities.Contains(1))
                {
                    // Handle system verbs with dedicated methods
                    return verbName switch
                    {
                        "_ci" => Ci(operand),
                        "_ic" => Ic(operand),
                        // Basic operators
                        "+" => Transpose(operand),  
                        "+:" => Transpose(operand),  // Monadic transpose
                        "-" => ArithmeticNegate(operand),
                        "-:" => ArithmeticNegate(operand),  // Monadic negate
                        "*" => First(operand),
                        "*:" => First(operand),  // Monadic first
                        "%" => Reciprocal(operand),
                        "%:" => Reciprocal(operand),  // Monadic reciprocal
                        "&" => Where(operand),
                        "&:" => Where(operand),  // Monadic where
                        "|" => Reverse(operand),
                        "|:" => Reverse(operand),  // Monadic reverse
                        "^" => Shape(operand),
                        "^:" => Shape(operand),  // Monadic shape
                        "!" => Enumerate(operand),
                        "!:" => Enumerate(operand),  // Monadic enumerate
                        "," => Enlist(operand),
                        ",:" => Enlist(operand),  // Monadic enlist
                        "#" => Count(operand),
                        "#:" => Count(operand),  // Monadic count
                        "_" => Floor(operand),
                        "_:" => Floor(operand),  // Monadic floor
                        "?" => Unique(operand),
                        "?:" => Unique(operand),  // Monadic unique
                        "=" => Group(operand),
                        "=:" => Group(operand),  // Monadic group
                        "." => MakeFunction(operand),  // Monadic make/execute
                        ".:" => MakeFunction(operand),  // Monadic make/execute
                        "~" => Negate(operand),
                        "~:" => Negate(operand),  // Monadic negate
                        "<" => GradeUp(operand),
                        "<:" => GradeUp(operand),  // Monadic grade up
                        ">" => GradeDown(operand),
                        ">:" => GradeDown(operand),  // Monadic grade down
                        "$" => Format(operand),
                        "$:" => Format(operand),  // Monadic format
                        "@" => Atom(operand),
                        "@:" => Atom(operand),  // Monadic atom
                        _ => throw new Exception($"Verb '{verbName}' is registered as monadic but not implemented in ApplyMonadicVerb")
                    };
                }
            }
            
            throw new Exception($"Unknown monadic verb: {verbName}");
        }

        private K3Value ApplySymbolVerbWithOperator(K3Value verb, K3Value left, K3Value right)
        {
            // Handle case where verb is a value (like 2 +/ 1 2 3)
            // This means we should use verb as left operand with operator
            if (verb is SymbolValue verbSymbol)
            {
                return ApplySymbolVerb(verbSymbol.Value, verb, right);
            }
            else if (verb is FunctionValue function)
            {
                // For functions, execute the function with left and right as arguments
                return ExecuteFunction(function, new List<K3Value> { left, right });
            }
            else
            {
                // For numeric verbs, assume addition by default
                // But check if this is actually a glyph verb stored as a different type
                if (verb.Type == ValueType.Symbol || 
                    (verb.Type == ValueType.Integer && verb.ToString().Length == 1 && "+-*/%^!&|<>=^,_?#~".Contains(verb.ToString())))
                {
                    return ApplySymbolVerb(verb.ToString(), verb, right);
                }
                else
                {
                    return Plus(verb, right);
                }
            }
        }

        /// <summary>
        /// Returns true if the verb string denotes a monadic-only verb (ends with ':' disambiguating colon,
        /// or is a verb that only supports arity 1 in the registry).
        /// </summary>
        private bool IsMonadicOnlyVerb(string verbName)
        {
            if (verbName.EndsWith(":"))
                return true;
            var info = VerbRegistry.GetVerb(verbName);
            return info != null && info.SupportedArities.Length == 1 && info.SupportedArities[0] == 1;
        }

        /// <summary>
        /// Over Monad: apply monadic verb f iteratively to x.
        ///   f/ x        — iterate until result matches previous or initial (fixed-point)
        ///   n f/ x      — apply exactly n times (Adverbial Do)
        ///   b f/ x      — apply while b[x] != 0 (Adverbial While)
        /// </summary>
        internal K3Value OverMonad(string verbName, K3Value left, K3Value x)
        {
            bool leftSentinel = left is NullValue;

            // n f/ x — Adverbial Do: apply n times
            if (!leftSentinel && left is IntegerValue nVal)
            {
                var current = x;
                for (int i = 0; i < nVal.Value; i++)
                    current = ApplyMonadicVerb(verbName, current);
                return current;
            }

            // b f/ x — Adverbial While: apply while b[current] != 0
            if (!leftSentinel && (left is FunctionValue || left is SymbolValue || left is ProjectedFunctionValue || left is AdverbProjectedFunctionValue))
            {
                var current = x;
                const int maxIter = 1000000;
                for (int i = 0; i < maxIter; i++)
                {
                    var condition = left is FunctionValue bf
                        ? ExecuteFunction(bf, new List<K3Value> { current })
                        : ApplyMonadicVerb((left as SymbolValue)!.Value, current);
                    if (condition is IntegerValue cv && cv.Value == 0) break;
                    if (condition is LongValue lv && lv.Value == 0L) break;
                    current = ApplyMonadicVerb(verbName, current);
                }
                return current;
            }

            // f/ x — fixed-point: iterate until result matches previous or initial
            {
                var initial = x;
                var prev = x;
                var current = ApplyMonadicVerb(verbName, x);
                const int maxIter = 1000000;
                for (int i = 0; i < maxIter; i++)
                {
                    // Stop if result matches previous or initial
                    bool matchesPrev = ValuesMatch(current, prev);
                    bool matchesInitial = ValuesMatch(current, initial);
                    if (matchesPrev || matchesInitial)
                        return prev; // return next-to-last
                    prev = current;
                    current = ApplyMonadicVerb(verbName, current);
                }
                return prev;
            }
        }

        /// <summary>Deep structural equality check used by Over Monad fixed-point detection.</summary>
        private bool ValuesMatch(K3Value a, K3Value b)
        {
            if (a.Type != b.Type) return false;
            if (a is IntegerValue ia && b is IntegerValue ib) return ia.Value == ib.Value;
            if (a is LongValue la && b is LongValue lb) return la.Value == lb.Value;
            if (a is FloatValue fa && b is FloatValue fb) return fa.Value == fb.Value;
            if (a is CharacterValue ca && b is CharacterValue cb) return ca.Value == cb.Value;
            if (a is SymbolValue sa && b is SymbolValue sb) return sa.Value == sb.Value;
            if (a is VectorValue va && b is VectorValue vb)
            {
                if (va.Elements.Count != vb.Elements.Count) return false;
                for (int i = 0; i < va.Elements.Count; i++)
                    if (!ValuesMatch(va.Elements[i], vb.Elements[i])) return false;
                return true;
            }
            return a.ToString() == b.ToString();
        }

        private K3Value ApplyAdverbSlash(K3Value verb, K3Value left, K3Value right)
        {
            // For adverb slash /:
            // If left is dummy 0 and right is vector, use Over (e.g., +/ 1 2 3 4 5)
            // If left is vector and right is scalar, use Each (e.g., (1 2 3) %/ 2)
            // If left is vector and right is vector, use Each (e.g., (1 2 3) %/ (4 5 6))
            // If only right argument, use Over (e.g., %/ 1 2 3)

            bool leftSentinel = left is NullValue;
            bool rightSentinel = right is NullValue;

            string verbName = verb is SymbolValue vs1 ? vs1.Value : verb.ToString() ?? "";

            // FunctionValue verb: n {lambda}/ x — adverbial do with lambda
            // Applies the lambda function n times starting from x.
            // n=0 means apply 0 times (return x unchanged).
            // This must be checked before sentinel disambiguation since n=0 is valid (not a sentinel here).
            if (verb is FunctionValue funcVerb)
            {
                // Noun form: both sentinels — return projected function placeholder
                if (leftSentinel && rightSentinel)
                    return new AdverbProjectedFunctionValue("over", funcVerb.BodyText, 2);
                
                // n {lambda}/ x — apply lambda n times to x (n != 0, since 0 is sentinel for 'no left arg')
                if (left is IntegerValue nInt && !leftSentinel)
                {
                    var current = right;
                    for (int i = 0; i < nInt.Value; i++)
                        current = ExecuteFunction(funcVerb, new List<K3Value> { current });
                    return current;
                }
                
                // {lambda}/ x — fixed-point iteration with lambda (monadic only)
                // For dyadic functions, fall through to standard Over handling
                if (leftSentinel && funcVerb.Valence == 1)
                {
                    var prev = right;
                    var curr2 = ExecuteFunction(funcVerb, new List<K3Value> { prev });
                    const int maxIter2 = 1000000;
                    for (int i = 0; i < maxIter2; i++)
                    {
                        if (ValuesMatch(curr2, prev)) return prev;
                        prev = curr2;
                        curr2 = ExecuteFunction(funcVerb, new List<K3Value> { prev });
                    }
                    return prev;
                }
            }

            // Over Monad dispatch: verb is monadic (ends with ':' or registry says monadic-only)
            // Handles: f:/ x (fixed-point), n f:/ x (adverbial do), b f:/ x (adverbial while)
            if (IsMonadicOnlyVerb(verbName))
            {
                // Noun form — return projected function
                if (leftSentinel && rightSentinel)
                    return new AdverbProjectedFunctionValue("over", verbName, 1);
                return OverMonad(verbName, left, right);
            }

            // Noun form: both args are sentinel 0 — return projected function (e.g. +/ used as a value)
            if (leftSentinel && rightSentinel)
            {
                return new AdverbProjectedFunctionValue("over", verbName, 1);
            }
            
            // Check for "over" case: left is dummy 0 and right is vector
            if (leftSentinel && right is VectorValue)
            {
                return Over(verb, left, right);
            }
            
            // Check for "each" case: left is vector and right is scalar
            if (left is VectorValue && IsScalar(right))
            {
                return Each(verb, left, right);
            }
            
            // Check for vector-vector case
            if (left is VectorValue && right is VectorValue)
            {
                return Each(verb, left, right);
            }
            
            // Default case: use Over
            return Over(verb, left ?? new NullValue(), right ?? new NullValue());
        }

        /// <summary>
        /// Scan Monad: apply monadic verb f iteratively to x, collecting all intermediate results.
        ///   f:\ x       — iterate until fixed-point, return all values including x (excluding repeated last)
        ///   n f:\ x     — apply exactly n times, return [x, f[x], ..., f^n[x]]
        ///   b f:\ x     — apply while b[current]!=0, return all collected values
        /// </summary>
        internal K3Value ScanMonad(string verbName, K3Value left, K3Value x)
        {
            bool leftSentinel = left is NullValue;

            // n f:\ x — Adverbial Do: collect x, f[x], ..., f^n[x]
            if (!leftSentinel && left is IntegerValue nVal)
            {
                var results = new List<K3Value> { x };
                var current = x;
                for (int i = 0; i < nVal.Value; i++)
                {
                    current = ApplyMonadicVerb(verbName, current);
                    results.Add(current);
                }
                return new VectorValue(results);
            }

            // b f:\ x — Adverbial While: collect values while b[current] != 0
            if (!leftSentinel && (left is FunctionValue || left is SymbolValue || left is ProjectedFunctionValue || left is AdverbProjectedFunctionValue))
            {
                var results = new List<K3Value> { x };
                var current = x;
                const int maxIter = 1000000;
                for (int i = 0; i < maxIter; i++)
                {
                    var condition = left is FunctionValue bf
                        ? ExecuteFunction(bf, new List<K3Value> { current })
                        : ApplyMonadicVerb((left as SymbolValue)!.Value, current);
                    if (condition is IntegerValue cv && cv.Value == 0) break;
                    if (condition is LongValue lv && lv.Value == 0L) break;
                    current = ApplyMonadicVerb(verbName, current);
                    results.Add(current);
                }
                return new VectorValue(results);
            }

            // f:\ x — fixed-point scan: collect x, f[x], f[f[x]], ... until fixed-point
            {
                var results = new List<K3Value> { x };
                var prev = x;
                var current = ApplyMonadicVerb(verbName, x);
                const int maxIter = 1000000;
                for (int i = 0; i < maxIter; i++)
                {
                    bool matchesPrev = ValuesMatch(current, prev);
                    bool matchesInitial = ValuesMatch(current, x);
                    if (matchesPrev || matchesInitial)
                        break; // stop — don't add the repeated value
                    results.Add(current);
                    prev = current;
                    current = ApplyMonadicVerb(verbName, current);
                }
                return new VectorValue(results);
            }
        }

        private K3Value ApplyAdverbBackslash(K3Value verb, K3Value left, K3Value right)
        {
            // Noun form: both args are sentinel 0 — return projected function (e.g. +\ used as a value)
            bool leftSentinel = left is NullValue;
            bool rightSentinel = right is NullValue;

            string verbName = verb is SymbolValue vs1 ? vs1.Value : verb.ToString() ?? "";

            // Scan Monad dispatch: verb is monadic (ends with ':' or registry says monadic-only)
            if (IsMonadicOnlyVerb(verbName))
            {
                if (leftSentinel && rightSentinel)
                    return new AdverbProjectedFunctionValue("scan", verbName, 1);
                return ScanMonad(verbName, left, right);
            }

            if (leftSentinel && rightSentinel)
            {
                return new AdverbProjectedFunctionValue("scan", verbName, 1);
            }
            // Natural nested evaluation: call Scan with the verb and arguments
            return Scan(verb, left ?? new NullValue(), right ?? new NullValue());
        }

        public K3Value HandleAdverbTick(K3Value verb, K3Value left, K3Value right)
        {
            // Handle AdverbProjectedFunctionValue (e.g. ,' as inner verb of ,'')
            // Apply the projected adverb recursively to each corresponding element pair
            if (verb is AdverbProjectedFunctionValue apfv)
            {
                var innerVerbSymbol = new SymbolValue(apfv.Verb);
                bool isMonadicContext = left is NullValue;
                if (!isMonadicContext && left is VectorValue leftRows && right is VectorValue rightRows)
                {
                    if (leftRows.Elements.Count != rightRows.Elements.Count)
                        throw new Exception($"length error: {leftRows.Elements.Count} != {rightRows.Elements.Count}");
                    var result = new List<K3Value>();
                    for (int i = 0; i < leftRows.Elements.Count; i++)
                        result.Add(HandleAdverbTick(innerVerbSymbol, leftRows.Elements[i], rightRows.Elements[i]));
                    return new VectorValue(result, DetermineVectorType(result));
                }
                if (isMonadicContext && right is VectorValue dataVec)
                {
                    var result = new List<K3Value>();
                    foreach (var element in dataVec.Elements)
                        result.Add(HandleAdverbTick(innerVerbSymbol, new NullValue(), element));
                    return new VectorValue(result, DetermineVectorType(result));
                }
                return HandleAdverbTick(innerVerbSymbol, left, right);
            }

            // Handle FunctionValue verb: apply function to each element (monadic each)
            if (verb is FunctionValue fvVerb)
            {
                bool isMonadicCtx = left is NullValue;
                bool rightIsSentinel = right is NullValue;
                // right=data, left=sentinel: monadic each over right
                if (isMonadicCtx && right is VectorValue dataVecFv)
                {
                    var result = new List<K3Value>();
                    foreach (var element in dataVecFv.Elements)
                        result.Add(ExecuteFunction(fvVerb, new List<K3Value> { element }));
                    return new VectorValue(result, DetermineVectorType(result));
                }
                // left=data, right=sentinel: monadic each over left
                if (rightIsSentinel && left is VectorValue leftVecFv2)
                {
                    var result = new List<K3Value>();
                    foreach (var element in leftVecFv2.Elements)
                        result.Add(ExecuteFunction(fvVerb, new List<K3Value> { element }));
                    return new VectorValue(result, DetermineVectorType(result));
                }
                // Dyadic each: left and right are parallel vectors
                if (left is VectorValue leftVecFv && right is VectorValue rightVecFv)
                {
                    if (leftVecFv.Elements.Count != rightVecFv.Elements.Count)
                        throw new Exception($"length error");
                    var result = new List<K3Value>();
                    for (int i = 0; i < leftVecFv.Elements.Count; i++)
                        result.Add(ExecuteFunction(fvVerb, new List<K3Value> { leftVecFv.Elements[i], rightVecFv.Elements[i] }));
                    return new VectorValue(result, DetermineVectorType(result));
                }
            }

            // Handle DeferredTakeProjection verb: apply DTP to each element
            if (verb is DeferredTakeProjection dtpVerb)
            {
                bool isMonadicCtx = left is NullValue;
                K3Value dataToIter = isMonadicCtx ? right : left;
                if (dataToIter is VectorValue dataVecDtp)
                {
                    var result = new List<K3Value>();
                    foreach (var element in dataVecDtp.Elements)
                    {
                        var args = new List<K3Value> { element };
                        // Apply DTP: n#(f element)
                        K3Value funcRes;
                        if (dtpVerb.Func is ProjectedFunctionValue dtpPfvI)
                            funcRes = CallProjectedFunction(dtpPfvI, args);
                        else if (dtpVerb.Func is FunctionValue dtpFvI)
                        {
                            var tmpN = new ASTNode(ASTNodeType.Function); tmpN.Value = dtpFvI;
                            funcRes = CallDirectFunction(tmpN, args);
                        }
                        else
                            funcRes = element;
                        result.Add(Take(dtpVerb.Count, funcRes));
                    }
                    return new VectorValue(result, DetermineVectorType(result));
                }
                else
                {
                    // Scalar data: apply DTP directly to the single element
                    var args = new List<K3Value> { dataToIter };
                    K3Value funcRes;
                    if (dtpVerb.Func is ProjectedFunctionValue dtpPfvS)
                        funcRes = CallProjectedFunction(dtpPfvS, args);
                    else if (dtpVerb.Func is FunctionValue dtpFvS)
                    {
                        var tmpN = new ASTNode(ASTNodeType.Function); tmpN.Value = dtpFvS;
                        funcRes = CallDirectFunction(tmpN, args);
                    }
                    else
                        funcRes = dataToIter;
                    return Take(dtpVerb.Count, funcRes);
                }
            }

            // Handle IntegerValue verb: depth-based each (0', 1', 2', etc.)
            // 0' = identity (depth 0), 1' = each at depth 1, 2' = each at depth 2, etc.
            if (verb is IntegerValue intVerb)
            {
                int depth = intVerb.Value;
                bool leftIsDummy = left is NullValue;
                bool rightIsDummy = right is NullValue;
                
                // Noun form: both args are dummy - return projected function
                if (leftIsDummy && rightIsDummy)
                {
                    // Return an encoded function value for depth-based each
                    // Format: EACH_DEPTH:n where n is the depth (0, 1, 2, etc.)
                    return new FunctionValue($"EACH_DEPTH:{depth}", new List<string> { "x", "y" });
                }
                
                // Monadic form: left is dummy, right is data
                if (leftIsDummy && !rightIsDummy)
                {
                    if (depth == 0)
                    {
                        // 0' is identity - return data unchanged
                        return right;
                    }
                    return ApplyEachAtDepth(right, depth);
                }
                
                // Dyadic form: both are data (or left is data, right is dummy)
                // For integer each, treat as monadic with left as data
                if (depth == 0)
                {
                    return left ?? new NullValue();
                }
                return ApplyEachAtDepth(left, depth);
            }

            // Determine verb arity using VerbRegistry
            int arity = 2; // Default to dyadic
            if (verb is SymbolValue vs)
            {
                var verbName = vs.Value;
                
                // Special handling for system verbs with each adverb
                // These are monadic system verbs - use monadic Each
                if (verbName == "_ci" || verbName == "_ic")
                {
                    return Each(verb, right ?? new NullValue());
                }
                
                var verbInfo = VerbRegistry.GetVerb(verbName);
                if (verbInfo != null)
                {
                    // Check if verb supports monadic and if we're in monadic context
                    bool isMonadicContext = left is NullValue;
                    if (isMonadicContext && verbInfo.SupportedArities.Contains(1))
                    {
                        arity = 1;
                    }
                    else if (verbInfo.SupportedArities.Length == 1)
                    {
                        // Fixed arity verb
                        arity = verbInfo.SupportedArities[0];
                    }
                }
            }
            
            // Handle based on arity
            if (arity == 1)
            {
                // Monadic verb with each
                return Each(verb, right ?? new NullValue());
            }
            else
            {
                // Dyadic verb with each (or higher arity treated as dyadic for now)
                return Each(verb, left, right ?? new NullValue());
            }
        }

        private K3Value ApplyAdverbTick(K3Value verb, K3Value left, K3Value right)
        {
            // Check if this is a monadic verb with each (left and right are dummy values)
            if (left is NullValue && right is NullValue && verb is SymbolValue vs)
            {
                // Noun form: return projected function (e.g. ,' used as a value / inner adverb)
                return new AdverbProjectedFunctionValue("each", vs.Value, 2);
            }
            
            // For dyadic verbs, call 3-argument Each
            return Each(verb, left, right);
        }

        private K3Value ApplyAdverbSlashColon(K3Value verb, K3Value left, K3Value right)
        {
            // One-adverb-at-a-time: consume just the outer adverb (/:), preserve inner verb for next step
            // Check if this is a nested adverb call (has verb, left, right arguments)
            // Use sentinel values to distinguish between "no arguments" and "actual arguments"
            bool hasLeftArg = left is not NullValue;
            bool hasRightArg = right is not NullValue;
            
            if (hasLeftArg && hasRightArg)
            {
                // One-adverb-at-a-time: apply just the outer adverb (/:) with preserved inner verb
                // This creates natural nested evaluation without complex chaining
                return EachRight(verb, left, right);
            }
            
            // For simple cases (nominalized adverb), return a function that represents "each-right of verb"
            // Store the verb in the function's BodyText for later use - this preserves the inner verb
            string verbStr = verb is SymbolValue sym ? sym.Value : verb.ToString();
            var lambda = new FunctionValue($"EACH_RIGHT:{verbStr}", new List<string> { "x", "y" });
            return lambda;
        }

        private K3Value ApplyAdverbBackslashColon(K3Value verb, K3Value left, K3Value right)
        {
            // One-adverb-at-a-time: consume just the outer adverb (\:), preserve inner verb for next step
            // Natural nested evaluation: call EachLeft with the verb and arguments
            // This creates natural nested evaluation without complex chaining
            return EachLeft(verb, left ?? new NullValue(), right ?? new NullValue());
        }
        
        private K3Value ApplyAdverbTickColon(K3Value verb, K3Value left, K3Value right)
        {
            // Tacit composition: when right is a ProjectedFunctionValue, create a composition function
            // e.g., >':0, means "prepend 0, then apply each-prior greater-than"
            if (right is ProjectedFunctionValue projectedRight && left is NullValue)
            {
                string verbStr = verb is SymbolValue vs ? vs.Value : verb.ToString();
                string? leftArgStr = projectedRight.BoundArguments?[0]?.ToString();
                if (leftArgStr != null)
                {
                    string bodyText = $"{verbStr}':{leftArgStr},x";
                    string originalSourceText = $"{verbStr}':{leftArgStr},";
                    return new FunctionValue(bodyText, new List<string> { "x" }, originalSourceText: originalSourceText);
                }
            }
            // Natural nested evaluation: call EachPrior with the verb and arguments
            return EachPrior(verb, left, right);
        }
        

        private K3Value Over(K3Value verb, K3Value initialization, K3Value data)
        {
            // Handle vector case (over)
            if (data is VectorValue dataVec)
            {
                return OverVector(verb, initialization, dataVec);
            }
            
            // Handle matrix case (VectorValue of VectorValues)
            if (data is VectorValue matrixData && matrixData.Elements.Count > 0 && matrixData.Elements[0] is VectorValue)
            {
                return OverMatrix(verb, initialization, matrixData);
            }
            
            // Handle scalar case
            if (IsScalar(data))
            {
                return data;
            }
            
            throw new Exception($"Over not implemented for types: {verb.Type}, {data.Type}");
        }
        
        private K3Value OverVector(K3Value verb, K3Value initialization, VectorValue dataVec)
        {
            // Special case: empty vector
            if (dataVec.Elements.Count == 0)
            {
                return HandleEmptyVectorOver(verb, initialization);
            }
            
            // If initialization is :: (null/sentinel), use first element as initialization (K behavior for / without explicit init)
            if (initialization is NullValue && dataVec.Elements.Count > 0)
            {
                return OverVectorWithFirstElementInit(verb, dataVec);
            }
            else
            {
                return OverVectorWithProvidedInit(verb, initialization, dataVec);
            }
        }
        
        private K3Value HandleEmptyVectorOver(K3Value verb, K3Value initialization)
        {
            // For +/!0 and +/!0L, return 0 (identity element for addition)
            if (verb is SymbolValue verbSymbol && verbSymbol.Value == "+")
            {
                return new IntegerValue(0);
            }
            // For */!0 and */!0L, return 1 (identity element for multiplication)
            else if (verb is SymbolValue verbSymbolMul && verbSymbolMul.Value == "*")
            {
                return new IntegerValue(1);
            }
            // For other verbs with empty vectors, return initialization value
            else
            {
                return initialization;
            }
        }
        
        private K3Value OverVectorWithFirstElementInit(K3Value verb, VectorValue dataVec)
        {
            var result = dataVec.Elements[0]; // Use first element as starting point
            var startIndex = 1; // Start from second element
            
            if (verb is SymbolValue verbSymbol)
            {
                // Apply verb to remaining elements
                for (int i = startIndex; i < dataVec.Elements.Count; i++)
                {
                    result = ApplySymbolVerb(verbSymbol.Value, result, dataVec.Elements[i]);
                }
            }
            else
            {
                // If verb is not a symbol, treat it as a value to apply with operator
                for (int i = startIndex; i < dataVec.Elements.Count; i++)
                {
                    result = ApplySymbolVerbWithOperator(verb, result, dataVec.Elements[i]);
                }
            }
            
            return result;
        }
        
        private K3Value OverVectorWithProvidedInit(K3Value verb, K3Value initialization, VectorValue dataVec)
        {
            var result = initialization;
            
            if (verb is SymbolValue verbSym)
            {
                // Apply verb to each element of vector, accumulating result
                for (int i = 0; i < dataVec.Elements.Count; i++)
                {
                    result = ApplySymbolVerb(verbSym.Value, result, dataVec.Elements[i]);
                }
            }
            else
            {
                // If verb is not a symbol, treat it as a value to apply with operator
                for (int i = 0; i < dataVec.Elements.Count; i++)
                {
                    result = ApplySymbolVerbWithOperator(verb, result, dataVec.Elements[i]);
                }
            }
            
            return result;
        }
        
        private K3Value OverMatrix(K3Value verb, K3Value initialization, VectorValue matrixData)
        {
            var result = new List<K3Value>();
            
            if (verb is SymbolValue verbSymbol)
            {
                return OverMatrixWithSymbolVerb(verbSymbol, initialization, matrixData);
            }
            else
            {
                return OverMatrixWithValueVerb(verb, initialization, matrixData);
            }
        }
        
        private K3Value OverMatrixWithSymbolVerb(SymbolValue verbSymbol, K3Value initialization, VectorValue matrixData)
        {
            var result = new List<K3Value>();
            
            // For each row in the matrix, apply the verb over that row
            for (int i = 0; i < matrixData.Elements.Count; i++)
            {
                var row = (VectorValue)matrixData.Elements[i];
                var rowResult = OverVector(verbSymbol, initialization, row);
                result.Add(rowResult);
            }
            
            return new VectorValue(result);
        }
        
        private K3Value OverMatrixWithValueVerb(K3Value verb, K3Value initialization, VectorValue matrixData)
        {
            var result = new List<K3Value>();
            
            for (int i = 0; i < matrixData.Elements.Count; i++)
            {
                var row = (VectorValue)matrixData.Elements[i];
                var rowResult = OverVector(verb, initialization, row);
                result.Add(rowResult);
            }
            
            return new VectorValue(result);
        }

        private K3Value Scan(K3Value verb, K3Value initialization, K3Value data)
        {
            // Handle vector case with initialization
            if (data is VectorValue dataVec && dataVec.Elements.Count > 0)
            {
                return ScanVector(verb, initialization, dataVec);
            }
            
            return data;
        }
        
        private K3Value ScanVector(K3Value verb, K3Value initialization, VectorValue dataVec)
        {
            // If initialization is :: (null/sentinel), use first element as initialization (K behavior for \ without explicit init)
            if (initialization is NullValue && dataVec.Elements.Count > 0)
            {
                return ScanVectorWithFirstElementInit(verb, dataVec);
            }
            else
            {
                return ScanVectorWithProvidedInit(verb, initialization, dataVec);
            }
        }
        
        private K3Value ScanVectorWithFirstElementInit(K3Value verb, VectorValue dataVec)
        {
            var result = new List<K3Value>();
            var current = dataVec.Elements[0]; // Use first element as starting point
            result.Add(current); // Add first element to result
            
            var startIndex = 1; // Start from second element
            
            if (verb is SymbolValue verbSymbol)
            {
                // Apply verb to remaining elements
                for (int i = startIndex; i < dataVec.Elements.Count; i++)
                {
                    current = ApplySymbolVerb(verbSymbol.Value, current, dataVec.Elements[i]);
                    result.Add(current);
                }
            }
            else
            {
                // If verb is not a symbol, treat it as a value to apply with operator
                for (int i = startIndex; i < dataVec.Elements.Count; i++)
                {
                    current = ApplySymbolVerbWithOperator(verb, current, dataVec.Elements[i]);
                    result.Add(current);
                }
            }
            
            return new VectorValue(result, DetermineVectorType(result));
        }
        
        private K3Value ScanVectorWithProvidedInit(K3Value verb, K3Value initialization, VectorValue dataVec)
        {
            var result = new List<K3Value>();
            var current = initialization;
            
            // Add initialization value as first element
            result.Add(current);
            
            if (verb is SymbolValue verbSymbol)
            {
                // Apply verb to each element, accumulating result
                for (int i = 0; i < dataVec.Elements.Count; i++)
                {
                    current = ApplySymbolVerb(verbSymbol.Value, current, dataVec.Elements[i]);
                    result.Add(current);
                }
            }
            else
            {
                // If verb is not a symbol, treat it as a value to apply with operator
                for (int i = 0; i < dataVec.Elements.Count; i++)
                {
                    current = ApplySymbolVerbWithOperator(verb, current, dataVec.Elements[i]);
                    result.Add(current);
                }
            }
            
            return new VectorValue(result, DetermineVectorType(result));
        }

        private K3Value Each(K3Value verb, K3Value left, K3Value right)
        {
            // New structure: Each(verbSymbol, leftVector, rightVector)
            
            // Handle FunctionValue verbs first (e.g., from {x'}/)
            if (verb is FunctionValue func)
            {
                // This is a function-based adverb (like {x'}/) - call it for each element
                
                // Monadic case: left is dummy value, right is the data vector
                if (left is IntegerValue leftInt && leftInt.Value == 0 && right is VectorValue dataVec)
                {
                    var result = new List<K3Value>();
                    foreach (var element in dataVec.Elements)
                    {
                        // Execute the function with the element as argument
                        var funcResult = ExecuteFunction(func, new List<K3Value> { element });
                        result.Add(funcResult);
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                
                // Dyadic case: left is scalar, right is vector
                if (IsScalar(left) && right is VectorValue rightVecFunc)
                {
                    var result = new List<K3Value>();
                    foreach (var rightElement in rightVecFunc.Elements)
                    {
                        var funcResult = ExecuteFunction(func, new List<K3Value> { left, rightElement });
                        result.Add(funcResult);
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                
                // Dyadic case: left is vector, right is scalar
                if (left is VectorValue leftVecFunc && IsScalar(right))
                {
                    var result = new List<K3Value>();
                    foreach (var leftElement in leftVecFunc.Elements)
                    {
                        var funcResult = ExecuteFunction(func, new List<K3Value> { leftElement, right });
                        result.Add(funcResult);
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                
                // Dyadic case: both are vectors
                if (left is VectorValue leftVec2Func && right is VectorValue rightVec2Func)
                {
                    // Check if vectors have different lengths - should throw length error
                    if (leftVec2Func.Elements.Count != rightVec2Func.Elements.Count)
                    {
                        throw new Exception($"length error: {leftVec2Func.Elements.Count} != {rightVec2Func.Elements.Count}");
                    }
                    
                    var result = new List<K3Value>();
                    for (int i = 0; i < leftVec2Func.Elements.Count; i++)
                    {
                        var funcResult = ExecuteFunction(func, new List<K3Value> { leftVec2Func.Elements[i], rightVec2Func.Elements[i] });
                        result.Add(funcResult);
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
            }
            
            if (verb is SymbolValue verbSymbol)
            {
                // Check if this is a monadic verb with each (left is dummy value)
                if (left is IntegerValue leftInt && leftInt.Value == 0 && right is VectorValue dataVec)
                {
                    // This is a monadic verb with each - apply verb to each element of dataVec
                    var result = new List<K3Value>();
                    foreach (var element in dataVec.Elements)
                    {
                        result.Add(ApplyMonadicVerb(verbSymbol.Value, element));
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                
                // Handle scalar + vector case (e.g., 1!'x, y _' x)
                // Atom left is broadcast: apply verb(left, right[i]) for each element of right
                if (IsScalar(left) && right is VectorValue rightVecScalar)
                {
                    var result = new List<K3Value>();
                    foreach (var rightElement in rightVecScalar.Elements)
                    {
                        result.Add(ApplySymbolVerb(verbSymbol.Value, left, rightElement));
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                
                // Handle vector + scalar case (e.g., (1 2 3) %/ 2)
                if (left is VectorValue leftVec && IsScalar(right))
                {
                    // Apply dyadic operation element-wise with scalar right
                    var result = new List<K3Value>();
                    foreach (var leftElement in leftVec.Elements)
                    {
                        result.Add(ApplySymbolVerb(verbSymbol.Value, leftElement, right));
                    }
                    
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                
                // Handle vector + vector case (same length) - should behave like default operator
                if (left is VectorValue leftVec2 && right is VectorValue rightVec)
                {
                    // Check if vectors have different lengths - should throw length error
                    if (leftVec2.Elements.Count != rightVec.Elements.Count)
                    {
                        throw new Exception($"length error: {leftVec2.Elements.Count} != {rightVec.Elements.Count}");
                    }
                    
                    // Apply dyadic operation element-wise (same as default operator behavior)
                    var result = new List<K3Value>();
                    for (int i = 0; i < leftVec2.Elements.Count; i++)
                    {
                        var leftElement = leftVec2.Elements[i];
                        var rightElement = rightVec.Elements[i];
                        
                        result.Add(ApplySymbolVerb(verbSymbol.Value, leftElement, rightElement));
                    }
                    
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
            }
            
            throw new Exception($"Each not implemented for types: {verb.Type}, {left.Type}, {right.Type}");
        }

        private K3Value EachRight(K3Value verb, K3Value left, K3Value right)
        {
            // Each-Right (/:): Apply verb to each element of right with entire left
            // One-adverb-at-a-time: consume just the outer adverb, preserve inner verb
            
            if (verb is FunctionValue func)
            {
                // This is a nested adverb function - call it with left and each right element
                if (right is VectorValue rightVec)
                {
                    var result = new List<K3Value>();
                    foreach (var rightElement in rightVec.Elements)
                    {
                        // Call the function with left argument and right element
                        var funcResult = ExecuteFunction(func, new List<K3Value> { left, rightElement });
                        result.Add(funcResult);
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                else if (IsScalar(right))
                {
                    return ExecuteFunction(func, new List<K3Value> { left, right });
                }
            }
            else if (verb is SymbolValue verbSymbol)
            {
                // This is a base verb - apply it directly using one-adverb-at-a-time
                if (right is VectorValue rightVec)
                {
                    var result = new List<K3Value>();
                    foreach (var element in rightVec.Elements)
                    {
                        // Apply the base verb with preserved verb name
                        result.Add(ApplySymbolVerb(verbSymbol.Value, left, element));
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                else if (left is VectorValue leftVec)
                {
                    var result = new List<K3Value>();
                    foreach (var element in leftVec.Elements)
                    {
                        result.Add(ApplySymbolVerb(verbSymbol.Value, element, right));
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                else if (IsScalar(left))
                {
                    return ApplySymbolVerb(verbSymbol.Value, left, right);
                }
            }
            
            throw new Exception($"EachRight not implemented for types: {verb.Type}, {left.Type}, {right.Type}");
        }

        private K3Value EachLeft(K3Value verb, K3Value left, K3Value right)
        {
            // Each-Left (\:): Apply verb to entire right with each element of left
            // One-adverb-at-a-time: consume just the outer adverb, preserve inner verb
            
            if (verb is FunctionValue func)
            {
                // Check if this is a nested each-right function
                if (func.BodyText.StartsWith("EACH_RIGHT:"))
                {
                    // Extract the original verb from the function body
                    var verbStr = func.BodyText.Substring("EACH_RIGHT:".Length);
                    
                    // Parse the verb back to a K3Value
                    K3Value originalVerb;
                    if (verbStr.StartsWith("`") && verbStr.EndsWith("`"))
                    {
                        originalVerb = new SymbolValue(verbStr.Substring(1, verbStr.Length - 2));
                    }
                    else
                    {
                        // For now, assume it's a symbol (without backticks)
                        originalVerb = new SymbolValue(verbStr);
                    }
                    
                    // Apply each-right behavior with each left element
                    if (left is VectorValue leftVec)
                    {
                        var result = new List<K3Value>();
                        foreach (var leftElement in leftVec.Elements)
                        {
                            var eachRightResult = EachRight(originalVerb, leftElement, right);
                            result.Add(eachRightResult);
                        }
                        int vectorType = DetermineVectorType(result);
                        return new VectorValue(result, vectorType);
                    }
                    else if (IsScalar(left))
                    {
                        return EachRight(originalVerb, left, right);
                    }
                }
                else
                {
                    // This is a regular function - call it with each left element and right
                    if (left is VectorValue leftVec)
                    {
                        var result = new List<K3Value>();
                        foreach (var leftElement in leftVec.Elements)
                        {
                            var funcResult = ExecuteFunction(func, new List<K3Value> { leftElement, right });
                            result.Add(funcResult);
                        }
                        int vectorType = DetermineVectorType(result);
                        return new VectorValue(result, vectorType);
                    }
                    else if (IsScalar(left))
                    {
                        return ExecuteFunction(func, new List<K3Value> { left, right });
                    }
                }
            }
            else if (verb is SymbolValue verbSymbol)
            {
                // This is a base verb - apply it directly using one-adverb-at-a-time
                if (left is VectorValue leftVec)
                {
                    var result = new List<K3Value>();
                    foreach (var element in leftVec.Elements)
                    {
                        // Apply the base verb with preserved verb name
                        result.Add(ApplySymbolVerb(verbSymbol.Value, element, right));
                    }
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                else if (IsScalar(left))
                {
                    return ApplySymbolVerb(verbSymbol.Value, left, right);
                }
            }
            
            throw new Exception($"EachLeft not implemented for types: {verb.Type}, {left.Type}, {right.Type}");
        }

        private K3Value EachPrior(K3Value verb, K3Value left, K3Value right)
        {
            // Each-Prior (':): Apply verb to each element with previous element
            // y f': x returns: y as first element, then x[i] f x[i-1] for i >= 1
            if (verb is SymbolValue verbSymbol)
            {
                if (right is VectorValue rightVec)
                {
                    var result = new List<K3Value>();
                    
                    // Check if left is a real argument (not a dummy null for monadic form)
                    bool hasLeftArg = left is not NullValue;
                    
                    // If left argument is provided, include it as the first element
                    if (hasLeftArg)
                    {
                        result.Add(left);
                    }
                    
                    // For each element at index i >= 1, compute x[i] f x[i-1]
                    for (int i = 1; i < rightVec.Elements.Count; i++)
                    {
                        var current = rightVec.Elements[i];
                        var prior = rightVec.Elements[i - 1];
                        result.Add(ApplySymbolVerb(verbSymbol.Value, current, prior));
                    }
                    
                    int vectorType = DetermineVectorType(result);
                    return new VectorValue(result, vectorType);
                }
                else if (IsScalar(right))
                {
                    // Scalar right: return the seed (left argument) as the result
                    return left is not NullValue ? left : right;
                }
            }
            
            throw new Exception($"EachPrior not implemented for types: {verb.Type}, {left.Type}, {right.Type}");
        }

        private K3Value Each(K3Value verb, K3Value data)
        {
            // Handle monadic verb with vector data (e.g., #:' 1 2 3)
            if (IsScalar(verb) && data is VectorValue vec)
            {
                // Special case: monadic dot (execute) with each should preserve list structure
                // This handles .:'x where x is a character matrix - results should be a general list
                bool isMonadicDot = verb is SymbolValue vs && vs.Value == ".";
                
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    if (verb is SymbolValue verbSymbol)
                    {
                        // For each operations, apply verb as a monadic operation to each element
                        result.Add(ApplyMonadicVerb(verbSymbol.Value, element));
                    }
                    else
                    {
                        // Check if verb is a glyph stored as non-vector type
                        string verbStr = verb.ToString();
                        if (verbStr.Length == 1 && "+-*/%^!&|<>=^,_?#~".Contains(verbStr))
                        {
                            result.Add(ApplyMonadicVerb(verbStr, element));
                        }
                        else
                        {
                            result.Add(ApplySymbolVerbWithOperator(verb, element, new NullValue()));
                        }
                    }
                }
                
                // For monadic dot, return as general list (type 0) to preserve structure
                if (isMonadicDot)
                {
                    return new VectorValue(result, 0, null);
                }
                
                int vectorType = DetermineVectorType(result);
                return new VectorValue(result, vectorType, null);
            }
            
            // Legacy 2-argument call for backward compatibility
            if (verb is VectorValue verbVec && data is VectorValue dataVec)
            {
                // Check if vectors have different lengths - should throw length error
                if (verbVec.Elements.Count != dataVec.Elements.Count)
                {
                    throw new Exception($"length error: {verbVec.Elements.Count} != {dataVec.Elements.Count}");
                }
                
                // Apply dyadic operation element-wise
                var result = new List<K3Value>();
                for (int i = 0; i < verbVec.Elements.Count; i++)
                {
                    var left = verbVec.Elements[i];
                    var right = dataVec.Elements[i];
                    
                    // Determine the operation based on the verb type
                    if (verb is SymbolValue verbSymbol)
                    {
                        result.Add(ApplySymbolVerb(verbSymbol.Value, left, right));
                    }
                    else
                    {
                        // Handle case where verb is a scalar value (for mixed operations)
                        result.Add(ApplySymbolVerbWithOperator(verb, left, right));
                    }
                }
                int vectorType = DetermineVectorType(result);
                return new VectorValue(result, vectorType);
            }
            
            // Handle scalar + vector case (legacy)
            if (IsScalar(verb) && data is VectorValue dataVector)
            {
                var result = new List<K3Value>();
                foreach (var element in dataVector.Elements)
                {
                    if (verb is SymbolValue verbSymbol)
                    {
                        // For each operations, apply the verb as a monadic operation to each element
                        result.Add(ApplyMonadicVerb(verbSymbol.Value, element));
                    }
                    else
                    {
                        // Check if verb is a glyph stored as non-vector type
                        string verbStr = verb.ToString();
                        if (verbStr.Length == 1 && "+-*/%^!&|<>=^,_?#~".Contains(verbStr))
                        {
                            result.Add(ApplyMonadicVerb(verbStr, element));
                        }
                        else
                        {
                            result.Add(ApplySymbolVerbWithOperator(verb, element, new NullValue()));
                        }
                    }
                }
                int vectorType = DetermineVectorType(result);
                return new VectorValue(result, vectorType);
            }
            
            // Handle scalar + scalar case (legacy)
            if (IsScalar(verb) && IsScalar(data))
            {
                if (verb is SymbolValue verbSymbol)
                {
                    return ApplySymbolVerb(verbSymbol.Value, verb, data);
                }
                else
                {
                    return ApplySymbolVerbWithOperator(verb, verb, data);
                }
            }
            
            throw new Exception($"Each not implemented for types: {verb.Type}, {data.Type}");
        }

        /// <summary>
        /// Apply "each" at a specified depth level.
        /// depth=1: apply to each element at the top level
        /// depth=2: apply to each element at level 2 (nested vectors)
        /// etc.
        /// </summary>
        private K3Value ApplyEachAtDepth(K3Value data, int depth)
        {
            if (depth <= 0)
            {
                return data;
            }
            
            if (depth == 1)
            {
                // Apply each at the top level - return the elements as-is
                if (data is VectorValue vecDepth1)
                {
                    // Each at depth 1 preserves the vector structure
                    return vecDepth1;
                }
                return data;
            }
            
            // depth >= 2: descend into nested vectors
            if (data is VectorValue vecDeep)
            {
                var result = new List<K3Value>();
                foreach (var element in vecDeep.Elements)
                {
                    // Recursively apply at depth-1 to each element
                    result.Add(ApplyEachAtDepth(element, depth - 1));
                }
                return new VectorValue(result, DetermineVectorType(result));
            }
            
            // Scalar data - return as-is
            return data;
        }

        
        private int DetermineVectorType(List<K3Value> elements)
        {
            if (elements.Count == 0) return 0; // Empty list
            
            // Check if all elements are the same type
            var firstType = elements[0].Type;
            var allSameType = elements.All(e => e.Type == firstType);
            
            if (!allSameType) return 0; // Mixed types = generic list
            
            return firstType switch
            {
                ValueType.Integer => -1,   // Integer vector
                ValueType.Long => -64,     // Long vector  
                ValueType.Float => -2,     // Float vector
                ValueType.Character => -3, // Character vector
                ValueType.Symbol => -4,    // Symbol vector
                _ => 0                      // Default to generic list
            };
        }
    }
}
