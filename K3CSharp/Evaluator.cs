using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using K3CSharp.Parsing;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private readonly Dictionary<string, K3Value> globalVariables = new Dictionary<string, K3Value>();
        private readonly Dictionary<string, K3Value> localVariables = new Dictionary<string, K3Value>();
        private readonly Dictionary<string, int> symbolTable = new Dictionary<string, int>();
        public bool isInFunctionCall = false; // Track if we're evaluating a function call
        public static int floatPrecision = 7; // Default precision for floating point display
        
        // K Tree for global namespace management
        private KTree kTree = new KTree();
        
        // Reference to the current function being executed (for AST caching)
        public FunctionValue? currentFunctionValue = null;

        // Reference to parent evaluator for global scope access
        private Evaluator? parentEvaluator = null;

        // Track whether current assignment is intermediate (used by another operator) or terminal
        private bool isIntermediateAssignment = false;

        // Adverb-aware evaluator for enhanced verb/adverb handling
        private readonly AdverbAwareEvaluator adverbAwareEvaluator;

        /// <summary>
        /// Constructor for Evaluator
        /// </summary>
        public Evaluator()
        {
            adverbAwareEvaluator = new AdverbAwareEvaluator(this);
        }

        /// <summary>
        /// Constructor for Evaluator with parent (for nested function calls)
        /// </summary>
        /// <param name="parent">Parent evaluator for global scope access</param>
        public Evaluator(Evaluator parent)
        {
            parentEvaluator = parent;
            // Inherit currentFunctionValue from parent for _f recursion support
            currentFunctionValue = parent?.currentFunctionValue;
            adverbAwareEvaluator = new AdverbAwareEvaluator(this);
            
        }

                
        public void SetCurrentBranch(string branchPath)
        {
            kTree.CurrentBranch = new SymbolValue(branchPath);
        }

        /// <summary>
        /// Returns the variable names in the current K-tree branch.
        /// </summary>
        public List<string> GetCurrentBranchVariableNames()
        {
            return kTree.GetBranchVariableNames(kTree.CurrentBranch?.Value ?? "");
        }

        /// <summary>
        /// Returns the variable names in the specified K-tree branch.
        /// </summary>
        public List<string> GetBranchVariableNames(string branchPath)
        {
            return kTree.GetBranchVariableNames(branchPath);
        }
        
        public void SetParentBranch()
        {
            kTree.CurrentBranch = kTree.GetParentBranch();
        }
        
        /// <summary>
        /// Resets the K tree to its default state (for testing purposes)
        /// Also resets random seed to -314159 for reproducible tests
        /// </summary>
        public void ResetKTree()
        {
            kTree = new KTree();
            Evaluator.RandomSeed = -314159;
            ObjectRegistry.Clear();
        }

        public K3Value Evaluate(ASTNode? node)
        {
            if (node == null)
                return new NullValue();
            return EvaluateNode(node) ?? new NullValue();
        }

        /// <summary>
        /// Evaluate a system variable (niladic getter like _d, _n, _t, etc.)
        /// </summary>
        private K3Value EvaluateSystemVariable(string name)
        {
            return name switch
            {
                "_d" => DirectoryFunction(new NullValue()),
                "_n" => NullFunction(new NullValue()),
                "_t" => TimeFunction(new NullValue()),
                "_T" => TFunction(new NullValue()),
                "_i" => IndexFunction(new NullValue()),
                "_f" => FunctionFunction(new NullValue()),
                "_s" => SpaceFunction(new NullValue()),
                "_h" => HostFunction(new NullValue()),
                "_p" => PortFunction(new NullValue()),
                "_P" => ProcessIdFunction(new NullValue()),
                "_w" => WhoFunction(new NullValue()),
                "_u" => UserFunction(new NullValue()),
                "_a" => AddressFunction(new NullValue()),
                "_k" => VersionFunction(new NullValue()),
                "_o" => OsFunction(new NullValue()),
                "_c" => CoresFunction(new NullValue()),
                "_r" => RamFunction(new NullValue()),
                "_m" => MachineIdFunction(new NullValue()),
                "_y" => StackFunction(new NullValue()),
                _ => throw new Exception($"Unknown system variable: {name}")
            };
        }

        private K3Value? EvaluateNode(ASTNode? node)
        {
            if (node == null)
                return new NullValue();

            switch (node.Type)
            {
                case ASTNodeType.Literal:
                    return node.Value;

                case ASTNodeType.Variable:
                    var name = node.Value is SymbolValue symbol ? symbol.Value : node.Value?.ToString() ?? "";
                    // Strip leading backtick if present (symbol literals like `_d)
                    var cleanName = name.StartsWith("`") ? name.Substring(1) : name;
                    // Special handling for _f (function self-reference)
                    // Must check before general system variable handling
                    if (cleanName == "_f")
                    {
                        return FunctionFunction(new NullValue());
                    }
                    // Check if this is a system variable (like _d, _n, _t, etc.)
                    if (VerbRegistry.IsSystemVariable(cleanName))
                    {
                        return EvaluateSystemVariable(cleanName);
                    }
                    return GetVariable(name);

                case ASTNodeType.Assignment:
                    {
                        var assignName = node.Value is SymbolValue assignmentSym ? assignmentSym.Value : node.Value?.ToString() ?? "";
                        var value = Evaluate(node.Children[0]);
                        SetVariable(assignName, value!); // Use local variables for regular assignments
                        
                        // LRS behavior: Return value for inline assignments, null for terminal (pure) assignments
                        // Terminal assignment: no verbs to the left between assignment and separator
                        // Inline assignment: one or more verbs to the left between assignment and separator
                        return node.IsTerminalAssignment ? new NullValue() : value;
                    }

                case ASTNodeType.ApplyAndAssign:
                    {
                        var variableName = node.Value is SymbolValue varSym ? varSym.Value : node.Value?.ToString() ?? "";
                        var operatorSymbol = node.Children[0].Value as SymbolValue;
                        var rightArgument = Evaluate(node.Children[1]);

                        if (operatorSymbol != null)
                        {
                            var opName = operatorSymbol.Value;

                            // Indexed apply-and-assign: a[i]+:y or a[]:y — amend at index path or all elements
                            if (node.Children.Count >= 3)
                            {
                                var currentData = GetVariable(variableName);
                                // Build path from Children[2]: Block means multi-level, else single level
                                var indexNode = node.Children[2];
                                List<K3Value> path;
                                if (indexNode.Type == ASTNodeType.Block)
                                {
                                    path = indexNode.Children.Select(c => Evaluate(c)).ToList();
                                }
                                else
                                {
                                    path = new List<K3Value> { Evaluate(indexNode) };
                                }
                                // Build function (dyadic verb to apply, or colon for direct assignment)
                                var opFunc = new SymbolValue(opName);
                                var amended = AmendAtPath(currentData, path, 0, opFunc, rightArgument);
                                SetVariable(variableName, amended);
                                return amended;
                            }
                            else if (opName == ":")
                            {
                                // Direct assignment to all elements: x[]:y
                                // No index specified means amend entire value
                                var currentData = GetVariable(variableName);
                                // Use empty path for top-level replacement
                                var amended = AmendAtPath(currentData, new List<K3Value>(), 0, new SymbolValue(":"), rightArgument);
                                SetVariable(variableName, amended);
                                return amended;
                            }

                            // Get current value of variable
                            var currentValue = GetVariable(variableName);

                            // Monadic apply-and-assign: operator is monadic-only OR right argument is null
                            // e.g., x?: has NullValue right argument, triggering monadic unique
                            if (IsMonadicOnlyVerb(opName) || rightArgument is NullValue)
                            {
                                // Apply monadic operator to current value
                                var monadicResult = ApplyMonadicVerb(opName, currentValue);
                                // Assign result to a new variable named {variableName}?
                                // e.g., x?: creates variable x? with unique values
                                var newVariableName = variableName + "?";
                                SetVariable(newVariableName, monadicResult);
                                return monadicResult;
                            }

                            // Apply operator to current value and right argument
                            var opNode = new ASTNode(ASTNodeType.DyadicOp);
                            opNode.Value = new SymbolValue(opName);
                            opNode.Children.Add(ASTNode.MakeLiteral(currentValue));
                            opNode.Children.Add(ASTNode.MakeLiteral(rightArgument));

                            // Evaluate the operation
                            var result = EvaluateDyadicOp(opNode);
                            
                            // Assign result back to variable
                            SetVariable(variableName, result);
                            
                            // Apply and assign operations should always return the result (not null)
                            // This is different from regular assignments which follow LRS behavior
                            return result;
                        }
                        else
                        {
                            throw new Exception("Apply and assign requires a valid operator");
                        }
                    }

                case ASTNodeType.ConditionalStatement:
                    {
                        var statementType = node.Value is SymbolValue sym ? sym.Value : node.Value?.ToString() ?? "";
                        
                        return statementType switch
                        {
                            ":" => EvaluateConditionalExpression(node.Children),
                            "do" => EvaluateDoStatement(node.Children),
                            "if" => EvaluateIfStatement(node.Children),
                            "while" => EvaluateWhileStatement(node.Children),
                            _ => throw new Exception($"Unknown conditional statement type: {statementType}")
                        };
                    }

                case ASTNodeType.GlobalAssignment:
                    {
                        var globalAssignName = node.Value is SymbolValue globalAssignmentSym ? globalAssignmentSym.Value : node.Value?.ToString() ?? "";
                        var globalValue = Evaluate(node.Children[0]);
                        SetGlobalVariable(globalAssignName, globalValue);
                        return globalValue; // Return the assigned value
                    }

                case ASTNodeType.DyadicOp:
                    return EvaluateDyadicOp(node);

                case ASTNodeType.Vector:
                    return EvaluateVector(node);

                case ASTNodeType.Function:
                    return EvaluateFunction(node);

                case ASTNodeType.FunctionCall:
                    // Handle control flow functions specially - they need unevaluated AST nodes
                    if (node.Children.Count >= 2 && node.Children[0].Type == ASTNodeType.Variable)
                    {
                        var cfName = node.Children[0].Value is SymbolValue cfSym ? cfSym.Value : "";
                        if (cfName == "do" || cfName == "while" || cfName == "if")
                        {
                            return EvaluateControlFlow(cfName, node.Children[1]);
                        }
                    }
                    return EvaluateFunctionCall(node);

                case ASTNodeType.Block:
                    return EvaluateBlock(node);

                case ASTNodeType.ExpressionList:
                    return EvaluateExpressionList(node);

                case ASTNodeType.StatementBlock:
                    return EvaluateStatementBlock(node);

                case ASTNodeType.FormSpecifier:
                    // {} form specifier - return a special value that will be handled in dyadic form operations
                    return new SymbolValue("{}");

                case ASTNodeType.ProjectedFunction:
                    return EvaluateProjectedFunction(node);

                case ASTNodeType.TriadicOp:
                    return EvaluateTriadicOp(node);

                case ASTNodeType.MonadicOp:
                    // Evaluate monadic operation using the same pattern as EvaluateDyadicOp
                    if (node.Children.Count == 0)
                        throw new Exception("MonadicOp must have at least one child");
                    
                    var operand = Evaluate(node.Children[0]);
                    var verbSymbol = node.Value as SymbolValue;
                    if (verbSymbol == null)
                        throw new Exception("MonadicOp must have a verb symbol as its value");
                    
                    // Use the same monadic operator evaluation as in EvaluateDyadicOp
                    var monoResult = verbSymbol.Value switch
                    {
                        "-" => MonadicMinus(operand),
                        "+" => Transpose(operand),
                        "*" => First(operand),
                        "%" => Reciprocal(operand),
                        "&" => Where(operand),
                        "|" => Reverse(operand),
                        "TYPE" => IoVerbMonadic(operand, 4),
                        "STRING_REPRESENTATION" => IoVerbMonadic(operand, 5),
                        "IO_VERB_0" => IoVerbMonadic(operand, 0),
                        "IO_VERB_1" => IoVerbMonadic(operand, 1),
                        "IO_VERB_2" => IoVerbMonadic(operand, 2),
                        "IO_VERB_3" => IoVerbMonadic(operand, 3),
                        "IO_VERB_4" => IoVerbMonadic(operand, 4),
                        "IO_VERB_5" => IoVerbMonadic(operand, 5),
                        "IO_VERB_6" => IoVerbMonadic(operand, 6),
                        "IO_VERB_7" => IoVerbMonadic(operand, 7),
                        "IO_VERB_8" => IoVerbMonadic(operand, 8),
                        "IO_VERB_9" => IoVerbMonadic(operand, 9),
                        "<" => GradeUp(operand),
                        ">" => GradeDown(operand),
                        "^" => Shape(operand),
                        "!" => Enumerate(operand),
                        "," => Enlist(operand),
                        "#" => Count(operand),
                        "_" => Floor(operand),
                        "?" => Unique(operand),
                        "=" => Group(operand),
                        "$" => Format(operand),
                        "@" => Atom(operand),
                        "." => MakeFunction(operand),
                        "~" => Negate(operand),
                        "_log" => MathLog(operand),
                        "_exp" => MathExp(operand),
                        "_abs" => MathAbs(operand),
                        "_sqr" => MathSqr(operand),
                        "_sqrt" => MathSqrt(operand),
                        "_floor" => MathFloor(operand),
                        "_sin" => MathSin(operand),
                        "_cos" => MathCos(operand),
                        "_tan" => MathTan(operand),
                        "_asin" => MathAsin(operand),
                        "_acos" => MathAcos(operand),
                        "_atan" => MathAtan(operand),
                        "_sinh" => MathSinh(operand),
                        "_cosh" => MathCosh(operand),
                        "_tanh" => MathTanh(operand),
                        "_inv" => MathInv(operand),
                        "_ceil" => MathCeil(operand),
                        "_lt" => LtFunction(operand),
                        "_jd" => JdFunction(operand),
                        "_dj" => DjFunction(operand),
                        "_T" => TFunction(operand),
                        "_in" => InFunction(operand),
                        "_bin" => BinFunction(operand),
                        "_binl" => BinlFunction(operand),
                        "_lin" => LinFunction(operand),
                        "_gtime" => GtimeFunction(operand),
                        "_ltime" => LtimeFunction(operand),
                        "_bd" => BdFunction(operand),
                        "_db" => DbFunction(operand),
                        "_ci" => Ci(operand),
                        "_ic" => Ic(operand),
                        "_v" => VarFunction(operand),
                        "_i" => IndexFunction(operand),
                        "_f" => FunctionFunction(operand),
                        "_n" => NullFunction(operand),
                        "_s" => SpaceFunction(operand),
                        "_h" => HostFunction(operand),
                        "_p" => PortFunction(operand),
                        "_P" => ProcessIdFunction(operand),
                        "_w" => WhoFunction(operand),
                        "_u" => UserFunction(operand),
                        "_a" => AddressFunction(operand),
                        "_k" => VersionFunction(operand),
                        "_o" => OsFunction(operand),
                        "_c" => CoresFunction(operand),
                        "_r" => RamFunction(operand),
                        "_m" => MachineIdFunction(operand),
                        "_y" => StackFunction(operand),
                        "_while" => WhileFunction(operand),
                        "_if" => IfFunction(operand),
                        "_d" => DirFunction(operand),
                        "_getenv" => GetenvFunction(operand),
                        "_size" => SizeFunction(operand),
                        "_not" => MathNot(operand),
                        "_parse" => Verbs.ParseVerbHandler.Parse(new[] { operand }),
                        "_eval" => EvaluateEvalVerb(operand),
                        "GETHINT" => GetHintFunction(new List<K3Value> { operand }),
                        "_gethint" => GetHintFunction(new List<K3Value> { operand }),
                        "DISPOSE" => DisposeFunction(new List<K3Value> { operand }),
                        "_dispose" => DisposeFunction(new List<K3Value> { operand }),
                        _ => throw new Exception($"Unknown monadic operator: {verbSymbol.Value}")
                    };
                    return monoResult;

                case ASTNodeType.TetradicOp:
                    return EvaluateTetradicOp(node);

                case ASTNodeType.VariadicOp:
                    return EvaluateVariadicOp(node);

                case ASTNodeType.NotImplemented:
                    var message = node.Value is CharacterValue charVal ? charVal.Value : node.Value?.ToString() ?? "Not implemented";
                    throw new Exception($"Not yet implemented: {message}");

                default:
                    throw new Exception($"Unknown AST node type: {node.Type}");
            }
        }

        private static bool IsBuiltInOperator(string operatorName)
        {
            // List of built-in operators that can be used as functions
            return operatorName == "+" || operatorName == "-" || operatorName == "*" || operatorName == "%" ||
                   operatorName == "^" || operatorName == "<" || operatorName == ">" || operatorName == "=" ||
                   operatorName == "," || operatorName == "." || operatorName == "!" || operatorName == "@" ||
                   operatorName == "#" || operatorName == "_" || operatorName == "?" || operatorName == "$" ||
                   operatorName == ":";
        }

        private static bool IsColon(K3Value value)
        {
            // Check if the value represents a colon (:)
            return value is SymbolValue symbol && symbol.Value == ":";
        }
        
        private K3Value? GetVariableValue(string variableName)
        {
            K3Value? kTreeValue;
            // Check if this is an absolute path (starts with dot)
            if (variableName.StartsWith("."))
            {
                // Absolute path - look up directly from root
                kTreeValue = kTree.GetValue(variableName);
                return kTreeValue;
            }
            // Check local variables first
            if (localVariables.TryGetValue(variableName, out var localValue))
            {
                return localValue;
            }
            // Relative path 
            var currentBranch = kTree.CurrentBranch?.Value ?? "";
            var relativePath = currentBranch + "." + variableName;
            kTreeValue = kTree.GetValue(relativePath);
            if (kTreeValue != null)
            {
                return kTreeValue;
            }
                        
            return new NullValue(); // Variable not found
        }

        /// <summary>
        /// Public method for getting variable values (used by MethodInvocation)
        /// </summary>
        public K3Value? GetVariableValuePublic(string variableName)
        {
            return GetVariableValue(variableName);
        }

        private K3Value GetVariable(string variableName)
        {
            // Check local variables first (function parameters and local assignments)
            if (localVariables.TryGetValue(variableName, out var localValue))
            {
                return localValue;
            }
            
            // Check if this is a K tree dotted notation variable
            if (variableName.Contains('.'))
            {
                var kTreeValue = kTree.GetValue(variableName);
                if (kTreeValue != null)
                {
                    return kTreeValue;
                }
            }
            
            // Check if this is a relative path in the current K tree branch
            if (!variableName.Contains('.'))
            {
                var currentBranch = kTree.CurrentBranch?.Value ?? "";
                if (!string.IsNullOrEmpty(currentBranch))
                {
                    // Try relative path from current branch
                    var relativePath = currentBranch + "." + variableName;
                    var kTreeValue = kTree.GetValue(relativePath);
                    if (kTreeValue != null)
                    {
                        return kTreeValue;
                    }
                }
                else
                {
                    // This means we should fall back to regular variable lookup
                }
                
                // Also check function's associated K tree for relative paths
                if (string.IsNullOrEmpty(currentBranch) && currentFunctionValue != null && currentFunctionValue.AssociatedKTree != null)
                {
                    var functionKTreeValue = currentFunctionValue.AssociatedKTree.GetValue(variableName);
                    if (functionKTreeValue != null)
                    {
                        return functionKTreeValue;
                    }
                }
            }
            
            // Check global variables
            if (globalVariables.TryGetValue(variableName, out var globalValue))
            {
                return globalValue;
            }
            
            // Check if this is a built-in operator that can be used as a function
            if (IsBuiltInOperator(variableName))
            {
                return new SymbolValue(variableName);
            }
            
            // Special handling for _f (function self-reference)
            // _f should return the current function value for recursion, not be evaluated as a verb
            if (variableName == "_f")
            {
                return FunctionFunction(new NullValue());
            }
            
            // Check if this is a niladic system variable (e.g., _t, _d, _T)
            if (VerbRegistry.IsSystemVariable(variableName))
            {
                return EvaluateVerb(variableName, Array.Empty<K3Value>());
            }
            
            // Check parent evaluator (for nested function calls)
            if (parentEvaluator != null)
            {
                return parentEvaluator.GetVariable(variableName);
            }
            
            throw new Exception($"Undefined variable: {variableName}");
        }
        
        private K3Value SetVariable(string variableName, K3Value value)
        {
            // Check if this is a K tree dotted notation variable
            if (variableName.Contains('.'))
            {
                if (kTree.SetValue(variableName, value))
                {
                    return value;
                }
                // If K tree assignment fails, fall back to local assignment
            }

            // At the top level REPL (not inside a function), simple assignment
            // goes into the current k-tree branch.  Inside a function it is local.
            if (!isInFunctionCall && !variableName.Contains('.'))
            {
                var branch = kTree.CurrentBranch?.Value ?? "";
                var path = string.IsNullOrEmpty(branch) ? variableName : branch + "." + variableName;
                kTree.SetValue(path, value);
                return value;
            }

            // Local assignment inside a function
            localVariables[variableName] = value;
            return value;
        }

        private K3Value SetGlobalVariable(string variableName, K3Value value)
        {
            // Check if this is a K tree dotted notation variable
            if (variableName.Contains('.'))
            {
                // Handle K tree dotted notation: set in current branch
                return SetVariable(variableName, value);
            }
            
            // Set in global scope (main branch)
            if (parentEvaluator != null)
            {
                // If we have a parent, set the global variable there
                return parentEvaluator.SetGlobalVariable(variableName, value);
            }
            else
            {
                // Set in current evaluator's global scope
                globalVariables[variableName] = value;
                
                // Also use kTree so GetVariable finds the new value via kTree lookup
                var branch = kTree.CurrentBranch?.Value ?? ".k"; // Default branch is .k
                var path = string.IsNullOrEmpty(branch) ? variableName : branch + "." + variableName;
                kTree.SetValue(path, value);
                
                // Also set variable in EvalVerbHandler for _eval operations
                K3CSharp.Verbs.EvalVerbHandler.SetVariable(variableName, value);
                
                return value;
            }
        }

        
        private K3Value EvaluateDyadicOperatorWithRegistry(string opName, K3Value left, K3Value right)
        {
            // Handle IDENTIFIER case - this should not happen with preserved verb names
            if (opName == "IDENTIFIER")
            {
                throw new Exception("IDENTIFIER should not reach EvaluateDyadicOperatorWithRegistry");
            }
            
            // Special cases for system functions that ignore left argument
            if (opName == "_bd" || opName == "_db")
            {
                // These are monadic functions that ignore the left argument
                return opName == "_bd" ? BdFunction(right) : DbFunction(right);
            }
            
            // Handle dyadic underscore (cut/drop operation)
            if (opName == "_")
            {
                return DropOrCut(left, right);
            }
            
            // Handle single-argument operators first
            if (opName == "_ci")
            {
                return Ci(left);
            }
            if (opName == "_ic")
            {
                return Ic(left);
            }

            // Use dictionary lookup for standard dyadic operators
            var dyadicOps = new Dictionary<string, Func<K3Value, K3Value, K3Value>>
            {
                { "+", Plus },
                { "-", Minus },
                { "*", Times },
                { "%", Divide },
                { "^", Power },
                { "POWER", Power },
                { "!", ModRotate },
                { "&", Min },
                { "|", Max },
                { "<", Less },
                { ">", More },
                { "=", Equal },
                { "~", Match },
                { ",", Join },
                { "#", Take },
                { "_", DropOrCut },
                { "@", AtIndex },
                { ".", DotApply },
                { "$", Format },
                { "::", GlobalAssignment },
                { "_in", In },
                { "_draw", Draw },
                { "_bin", Bin },
                { "_div", MathDiv },
                { "_dot", MathDot },
                { "_mul", MathMul },
                { "_inv", MathInv },
                { "_lsq", MathLsq },
                { "_and", MathAnd },
                { "_or", MathOr },
                { "_xor", MathXor },
                { "_rot", MathRot },
                { "_shift", MathShift },
                { "_binl", Binl },
                { "_lin", Lin },
                { "_dv", Dv },
                { "_dvl", Dvl },
                { "_di", Di },
                { "_sm", Sm },
                { "_sv", Sv },
                { "_vs", Vs },
                { "_ss", SsFunction },
                { "_setenv", SetenvFunction },
                { "_bd", (left, right) => BdFunction(right) },  // _bd is monadic, ignore left
                { "_db", (left, right) => DbFunction(right) },  // _db is monadic, ignore left
                { "?", Find }
            };

            if (dyadicOps.TryGetValue(opName, out var dyadicOp))
            {
                return dyadicOp(left, right);
            }

            // Handle special cases with lambda expressions
            switch (opName)
            {
                // Adverbs must be agnostic to the verb they modify
                // Pass the actual verb (left operand) to the adverb handler
                // For over/scan: left argument is the initialization value (per spec)
                case "over": return Over(left, left, right);
                case "scan": return Scan(left, left, right);
                case "each": return HandleAdverbTick(left, new NullValue(), right);
                case "/:": return EachRight(left, new NullValue(), right);
                case "\\:": return EachLeft(left, new NullValue(), right);
                case "TYPE": return IoVerbDyadic(left, right, 4);
                case "STRING_REPRESENTATION": return IoVerbMonadic(right, 5);
                case "IO_VERB_0": return IoVerbDyadic(left, right, 0);
                case "IO_VERB_1": return IoVerbDyadic(left, right, 1);
                case "IO_VERB_2": return IoVerbDyadic(left, right, 2);
                case "IO_VERB_3": return IoVerbDyadic(left, right, 3);
                case "IO_VERB_4": return IoVerbDyadic(left, right, 4);
                case "IO_VERB_5": return IoVerbDyadic(left, right, 5);
                case "IO_VERB_6": return IoVerbDyadic(left, right, 6);
                case "IO_VERB_7": return IoVerbDyadic(left, right, 7);
                case "IO_VERB_8": return IoVerbDyadic(left, right, 8);
                case "IO_VERB_9": return IoVerbDyadic(left, right, 9);
                case "SETHINT":
                case "_sethint": return SetHintFunction(new List<K3Value> { left, right });
            }

            // Handle any other verb names by checking VerbRegistry first
            var verb = VerbRegistry.GetVerb(opName);
            if (verb != null)
            {
                // For registered verbs not explicitly handled, throw an error instead of infinite recursion
                throw new Exception($"Verb '{opName}' found in registry but not implemented in EvaluateDyadicOperatorWithRegistry");
            }
            throw new Exception($"Unknown dyadic operator: {opName}");
        }

        private K3Value EvaluateDyadicOp(ASTNode node)
        {
            if (node.Value is not SymbolValue op) throw new Exception("Dyadic operator must have a symbol value");

            // Handle nominalized adverbs with one child BEFORE the monadic operator switch.
            // This covers patterns like DyadicOp("/", [FunctionNode]) produced by {x'}/ noun form.
            if (node.Children.Count == 1 &&
                (op.Value == "each-right" || op.Value == "each-left" ||
                 op.Value == "each-prior" || op.Value == "each" ||
                 op.Value == "over" || op.Value == "scan" ||
                 op.Value == "/" || op.Value == "\\" || op.Value == "'" ||
                 op.Value == "/:" || op.Value == "\\:" || op.Value == "':"))
            {
                var innerVerbValue1 = Evaluate(node.Children[0]);
                string adverbName1 = op.Value switch
                {
                    "/" => "over", "\\" => "scan", "'" => "each",
                    "/:" => "each-right", "\\:" => "each-left", "':" => "each-prior",
                    var s => s
                };
                string innerVerbStr1 = innerVerbValue1 is SymbolValue sv1 ? sv1.Value : innerVerbValue1?.ToString() ?? "";
                string encoded1 = adverbName1 switch
                {
                    "each-right" => $"EACH_RIGHT:{innerVerbStr1}",
                    "each-left"  => $"EACH_LEFT:{innerVerbStr1}",
                    "each-prior" => $"EACH_PRIOR:{innerVerbStr1}",
                    "each"       => $"EACH:{innerVerbStr1}",
                    "over"       => $"OVER:{innerVerbStr1}",
                    "scan"       => $"SCAN:{innerVerbStr1}",
                    _            => $"{adverbName1}:{innerVerbStr1}"
                };
                return new FunctionValue(encoded1, new List<string> { "x", "y" });
            }

            // Handle monadic operators (which are implemented as dyadic ops with one child)
            if (node.Children.Count == 1)
            {
                var operand = Evaluate(node.Children[0]);
                
                return op.Value switch
                {
                    "-" => MonadicMinus(operand),
                    "+" => Transpose(operand),
                    "*" => First(operand),
                    "%" => Reciprocal(operand),
                    "&" => Where(operand),
                    "|" => Reverse(operand),
                    "TYPE" => IoVerbMonadic(operand, 4),
                    "STRING_REPRESENTATION" => IoVerbMonadic(operand, 5),
                    "IO_VERB_0" => IoVerbMonadic(operand, 0),
                    "IO_VERB_1" => IoVerbMonadic(operand, 1),
                    "IO_VERB_2" => IoVerbMonadic(operand, 2),
                    "IO_VERB_3" => IoVerbMonadic(operand, 3),
                    "IO_VERB_4" => IoVerbMonadic(operand, 4),
                    "IO_VERB_5" => IoVerbMonadic(operand, 5),
                    "IO_VERB_6" => IoVerbMonadic(operand, 6),
                    "IO_VERB_7" => IoVerbMonadic(operand, 7),
                    "IO_VERB_8" => IoVerbMonadic(operand, 8),
                    "IO_VERB_9" => IoVerbMonadic(operand, 9),
                                        "<" => GradeUp(operand),
                    ">" => GradeDown(operand),
                    "^" => Shape(operand),
                    "!" => Enumerate(operand),
                    "," => Enlist(operand),
                    "#" => Count(operand),
                    "_" => Floor(operand),
                    "?" => Unique(operand),
                    "=" => Group(operand),
                    "$" => Format(operand),
                    "." => DotApply(new NullValue(), operand),
                    "~" => Negate(operand),
                    "_log" => MathLog(operand),
                    "_exp" => MathExp(operand),
                    "_abs" => MathAbs(operand),
                    "ABS" => MathAbs(operand),  // Handle token mapping issue
                    "_sqr" => MathSqr(operand),
                    "SQR" => MathSqr(operand),  // Handle token mapping issue
                    "_sqrt" => MathSqrt(operand),
                    "SQRT" => MathSqrt(operand),  // Handle token mapping issue
                    "_floor" => MathFloor(operand),
                    "FLOOR_MATH" => MathFloor(operand),  // Handle token mapping issue
                    "_sin" => MathSin(operand),
                    "SIN" => MathSin(operand),  // Handle token mapping issue
                    "_cos" => MathCos(operand),
                    "COS" => MathCos(operand),  // Handle token mapping issue
                    "_tan" => MathTan(operand),
                    "TAN" => MathTan(operand),  // Handle token mapping issue
                    "_asin" => MathAsin(operand),
                    "ASIN" => MathAsin(operand),  // Handle token mapping issue
                    "_acos" => MathAcos(operand),
                    "ACOS" => MathAcos(operand),  // Handle token mapping issue
                    "_atan" => MathAtan(operand),
                    "ATAN" => MathAtan(operand),  // Handle token mapping issue
                    "_sinh" => MathSinh(operand),
                    "SINH" => MathSinh(operand),  // Handle token mapping issue
                    "_cosh" => MathCosh(operand),
                    "COSH" => MathCosh(operand),  // Handle token mapping issue
                    "_tanh" => MathTanh(operand),
                    "TANH" => MathTanh(operand),  // Handle token mapping issue
                    "_inv" => MathInv(operand),
                    "INV" => MathInv(operand),  // Handle token mapping issue
                    "_ceil" => MathCeil(operand),
                    "CEIL" => MathCeil(operand),  // Handle token mapping issue
                    "_lt" => LtFunction(operand),
                    "_jd" => JdFunction(operand),
                    "_dj" => DjFunction(operand),
                    "_T" => TFunction(operand),
                    "_in" => InFunction(operand),
                    "_bin" => BinFunction(operand),
                    "_binl" => BinlFunction(operand),
                    "_lin" => LinFunction(operand),
                    "_gtime" => GtimeFunction(operand),
                    "_ltime" => LtimeFunction(operand),
                    "_bd" => BdFunction(operand),
                    "_db" => DbFunction(operand),
                    "_ci" => Ci(operand),
                    "_ic" => Ic(operand),
                    "_v" => VarFunction(operand),
                    "_i" => IndexFunction(operand),
                    "_f" => FunctionFunction(operand),
                    "_n" => NullFunction(operand),
                    "_s" => SpaceFunction(operand),
                    "_h" => HostFunction(operand),
                    "_p" => PortFunction(operand),
                    "_P" => ProcessIdFunction(operand),
                    "_w" => WhoFunction(operand),
                    "_u" => UserFunction(operand),
                    "_a" => AddressFunction(operand),
                    "_k" => VersionFunction(operand),
                    "_o" => OsFunction(operand),
                    "_c" => CoresFunction(operand),
                    "_r" => RamFunction(operand),
                    "_m" => MachineIdFunction(operand),
                    "_y" => StackFunction(operand),
                    "_while" => WhileFunction(operand),
                    "_if" => IfFunction(operand),
                    "_d" => DirFunction(operand),
                    "do" => DoFunction(operand),
                    "while" => WhileFunction(operand),
                    "if" => IfFunction(operand),
                    "_exit" => ExitFunction(operand),
                    "_getenv" => GetenvFunction(operand),
                    "_size" => SizeFunction(operand),
                    "_not" => MathNot(operand),
                    "MIN" => operand, // Identity operation for monadic min
                    "MAX" => operand, // Identity operation for monadic max
                    "over" => ApplyAdverbSlash(operand, new NullValue(), new NullValue()),
                    "scan" => ApplyAdverbBackslash(operand, new NullValue(), new NullValue()),
                    "each" => ApplyAdverbTick(operand, new NullValue(), new NullValue()),
                    "each-right" => ApplyAdverbSlashColon(operand, new NullValue(), new NullValue()),
                    "each-left" => ApplyAdverbBackslashColon(operand, new NullValue(), new NullValue()),
                    "each-prior" => ApplyAdverbTickColon(operand, new NullValue(), new NullValue()),
                    "_parse" => Verbs.ParseVerbHandler.Parse(new[] { operand }),
                    "_eval" => EvaluateEvalVerb(operand),
                    "GETHINT" => GetHintFunction(new List<K3Value> { operand }),
                    "_gethint" => GetHintFunction(new List<K3Value> { operand }),
                    "DISPOSE" => DisposeFunction(new List<K3Value> { operand }),
                    "_dispose" => DisposeFunction(new List<K3Value> { operand }),
                    _ => throw new Exception($"Unknown monadic operator: {op.Value}")
                };
            }

            // Special handling for ' adverb with multiple children (adverb evaluation)
            if (op.Value.ToString() == "'" && node.Children.Count == 2)
            {
                // This is an adverb operation: verb' vector_of_args
                // Handle this using the adverb evaluation pipeline
                
                // Get the verb (first child)
                var verbValue = Evaluate(node.Children[0]);
                
                // Get the arguments vector (second child)
                var argsVector = Evaluate(node.Children[1]);
                
                // Handle the ' adverb (each) - pass the verb and all arguments
                return HandleAdverbTick(verbValue, new NullValue(), argsVector);
            }

            // Special handling for / adverb (over) with 2 children: {func}/args
            if (op.Value.ToString() == "/" && node.Children.Count == 2)
            {
                var verbNode = node.Children[0];
                var argument = Evaluate(node.Children[1]);
                if (argument == null) throw new Exception("Adverb argument cannot be null");
                
                // Evaluate the verb (function)
                var verbValue = Evaluate(verbNode);
                if (verbValue == null) throw new Exception("Adverb verb cannot be null");
                
                // Apply the over adverb
                return ApplyAdverbSlash(verbValue, new NullValue(), argument);
            }

            // Special handling for \ adverb (scan) with 2 children: {func}\args
            if (op.Value.ToString() == "\\" && node.Children.Count == 2)
            {
                var verbNode = node.Children[0];
                var argument = Evaluate(node.Children[1]);
                if (argument == null) throw new Exception("Adverb argument cannot be null");
                
                // Evaluate the verb (function)
                var verbValue = Evaluate(verbNode);
                if (verbValue == null) throw new Exception("Adverb verb cannot be null");
                
                // Apply the scan adverb
                return ApplyAdverbBackslash(verbValue, new NullValue(), argument);
            }
            
            // Special handling for 'each' (') with 3 children: DyadicOp("each", [verb, leftArg, rightArg])
            // This is the dyadic each form: x f' y — broadcast scalar or pair elements
            if (op.Value.ToString() == "each" && node.Children.Count == 3)
            {
                var verbNode3 = node.Children[0];
                // K evaluates right-to-left: evaluate right arg before left arg
                var rightArg3 = Evaluate(node.Children[2]);
                var leftArg3 = Evaluate(node.Children[1]);
                
                // One-adverb-at-a-time: if verbNode is a modified verb (1-child adverb node),
                // route to dyadic nested-adverb handler.
                bool isModifiedVerb3 = verbNode3.Type == ASTNodeType.DyadicOp &&
                    verbNode3.Children.Count == 1 && verbNode3.Value is SymbolValue;
                if (isModifiedVerb3)
                {
                    return ApplyOuterAdverbWithModifiedVerbDyadic(op.Value.ToString(), verbNode3, leftArg3, rightArg3);
                }
                
                var verbValue3 = Evaluate(verbNode3);
                return HandleAdverbTick(verbValue3, leftArg3, rightArg3);
            }

            // Special handling for two-glyph adverbs with multiple children (adverb evaluation)
            if ((op.Value.ToString() == "each-right" || 
                 op.Value.ToString() == "each-left" || 
                 op.Value.ToString() == "each-prior") && node.Children.Count == 3)
            {
                // This is an adverb operation: ADVERB(verb, 0, args)
                // Handle this using the adverb evaluation pipeline
                var verbNodeTw = node.Children[0];
                
                // One-adverb-at-a-time: if verbNode is a modified verb (1-child adverb node),
                // route to dyadic nested-adverb handler.
                bool isModifiedVerbTw = verbNodeTw.Type == ASTNodeType.DyadicOp &&
                    verbNodeTw.Children.Count == 1 && verbNodeTw.Value is SymbolValue;
                if (isModifiedVerbTw)
                {
                    // K evaluates right-to-left: evaluate right arg before left arg
                    var rightTw = Evaluate(node.Children[2]);
                    var leftTw = Evaluate(node.Children[1]);
                    return ApplyOuterAdverbWithModifiedVerbDyadic(op.Value.ToString(), verbNodeTw, leftTw, rightTw);
                }
                
                // Get the verb (first child)
                var verbValue = Evaluate(verbNodeTw);
                
                // Get the left argument (second child)
                var leftArg = Evaluate(node.Children[1]);
                
                // Get the right argument (third child)
                var argsVector = Evaluate(node.Children[2]);
                
                // Check for projection: if right arg is null and left arg is present, create projected function
                if (argsVector is NullValue && leftArg is not NullValue && verbNodeTw.Value is SymbolValue verbSymbol)
                {
                    string adverbName = op.Value.ToString();
                    return new AdverbProjectedFunctionValue(adverbName, verbSymbol.Value, 2, leftArg);
                }
                                
                // Handle the adverb based on its type
                if (verbValue == null)
                {
                    throw new Exception($"Verb value is null for adverb {op.Value}");
                }
                
                return op.Value.ToString() switch
                {
                    "each-right" => ApplyAdverbSlashColon(verbValue, leftArg, argsVector),
                    "each-left" => ApplyAdverbBackslashColon(verbValue, leftArg, argsVector),
                    "each-prior" => ApplyAdverbTickColon(verbValue, leftArg, argsVector),
                    _ => throw new Exception($"Unknown adverb: {op.Value}")
                };
            }
            
            // Handle adverb noun-form: DyadicOp("over"/"scan"/"each"/etc, [verb, 0]) with 2 children
            // This covers +/ as a value (argument to @[...] etc.)
            if (node.Children.Count == 2 &&
                (op.Value.ToString() == "over" || op.Value.ToString() == "scan" ||
                 op.Value.ToString() == "each" || op.Value.ToString() == "each-right" ||
                 op.Value.ToString() == "each-left" || op.Value.ToString() == "each-prior"))
            {
                var verbNode2 = node.Children[0];
                
                // One-adverb-at-a-time: if verbNode is a modified verb (1-child adverb node),
                // consume only the outermost adverb and pass inner modified verb as-is
                bool isModifiedVerb2 = verbNode2.Type == ASTNodeType.DyadicOp && 
                    verbNode2.Children.Count == 1 && verbNode2.Value is SymbolValue;
                if (isModifiedVerb2)
                {
                    var arg2 = Evaluate(node.Children[1]);
                    return ApplyOuterAdverbWithModifiedVerb(op.Value.ToString(), verbNode2, arg2);
                }
                
                var arg2val = Evaluate(node.Children[1]);
                var verbValue2 = Evaluate(verbNode2);
                var monadicLeft2 = new NullValue();
                return op.Value.ToString() switch
                {
                    "over" => ApplyAdverbSlash(verbValue2, monadicLeft2, arg2val),
                    "scan" => ApplyAdverbBackslash(verbValue2, monadicLeft2, arg2val),
                    "each" => HandleAdverbTick(verbValue2, monadicLeft2, arg2val),
                    "each-right" => ApplyAdverbSlashColon(verbValue2, monadicLeft2, arg2val),
                    "each-left" => ApplyAdverbBackslashColon(verbValue2, monadicLeft2, arg2val),
                    "each-prior" => ApplyAdverbTickColon(verbValue2, monadicLeft2, arg2val),
                    _ => throw new Exception($"Unknown adverb: {op.Value}")
                };
            }

            // Handle dyadic operators
            if (node.Children.Count == 2)
            {
                // Special handling for colon operator to avoid evaluating left side as variable lookup
                if (op.Value.ToString() == ":")
                {
                    var leftNode = node.Children[0];
                    
                    // For assignment, the right side should be evaluated as intermediate if this is not terminal
                    bool previousIntermediate = isIntermediateAssignment;
                    isIntermediateAssignment = true; // Mark as intermediate for right side evaluation
                    var rightValue = Evaluate(node.Children[1]);
                    isIntermediateAssignment = previousIntermediate; // Restore previous context
                    
                    // For assignment, the left side should be treated as a variable name, not evaluated
                    if (leftNode.Type == ASTNodeType.Variable)
                    {
                        var variableName = leftNode.Value is SymbolValue symbol ? symbol.Value : leftNode.Value?.ToString() ?? "";
                        return Assignment(variableName, rightValue);
                    }
                    else
                    {
                        // If left side is not a variable, evaluate it normally
                        var leftValue = Evaluate(leftNode);
                        return ColonOperator(leftValue, rightValue);
                    }
                }
                
                // For other dyadic operators, check for adverbs first
                var verbWithAdverbs = VerbAdverbParser.ParseVerbWithAdverbs(node);
                if (verbWithAdverbs != null && verbWithAdverbs.Adverbs.Count > 0)
                {
                    // This is a verb with adverbs - use enhanced evaluation
                    // Evaluate right before left to preserve K's LRS semantics
                    // This ensures inline assignments in the right subtree execute before the left is evaluated
                    var right = Evaluate(node.Children[1]);
                    var left = Evaluate(node.Children[0]);
                    
                    // Determine the effective arity and apply adverbs sequentially
                    var effectiveArity = verbWithAdverbs.GetEffectiveArity();
                    if (effectiveArity == 1)
                    {
                        // Monadic with adverbs
                        return adverbAwareEvaluator.EvaluateVerbWithAdverbs(verbWithAdverbs, left!);
                    }
                    else if (effectiveArity == 2)
                    {
                        // Dyadic with adverbs
                        return adverbAwareEvaluator.EvaluateVerbWithAdverbs(verbWithAdverbs, left!, right!);
                    }
                    else
                    {
                        throw new Exception($"Unsupported arity {effectiveArity} for verb with adverbs");
                    }
                }
                else
                {
                    // Regular dyadic operation - evaluate right before left (K right-to-left semantics)
                    bool previousIntermediate2 = isIntermediateAssignment;
                    isIntermediateAssignment = true; // Mark as intermediate for right side evaluation
                    
                    // Special handling for APPLY with Block node (multi-dimensional indexing)
                    K3Value right;
                    if (op.Value.ToString() == "@" && node.Children[1].Type == ASTNodeType.Block)
                    {
                        // For multi-dimensional indexing x[a;b;c], collect all indices from Block
                        var block = node.Children[1];
                        var indices = new List<K3Value>();
                        foreach (var child in block.Children)
                        {
                            indices.Add(Evaluate(child) ?? new NullValue());
                        }
                        right = new VectorValue(indices);
                    }
                    else if (op.Value.ToString() == "." && (node.Children[1].Type == ASTNodeType.ExpressionList || node.Children[1].Type == ASTNodeType.Block))
                    {
                        // For multi-dimensional indexing x[0;0;0] parsed as DOT_APPLY with ExpressionList/Block
                        // Collect all indices from children
                        var exprList = node.Children[1];
                        var indices = new List<K3Value>();
                        foreach (var child in exprList.Children)
                        {
                            var childVal = Evaluate(child);
                            indices.Add(childVal ?? new NullValue());
                        }
                        right = new VectorValue(indices);
                    }
                    else
                    {
                        right = Evaluate(node.Children[1]);
                    }
                    
                    isIntermediateAssignment = previousIntermediate2; // Restore previous context

                    var left = Evaluate(node.Children[0]);

                    return EvaluateDyadicOperatorWithRegistry(op.Value.ToString(), left!, right!);
                }
            }
            else if (node.Children.Count == 2 && 
                    (op.Value.ToString() == "each" || op.Value.ToString() == "over" || op.Value.ToString() == "scan" ||
                     op.Value.ToString() == "each-right" || op.Value.ToString() == "each-left" || op.Value.ToString() == "each-prior" ||
                     op.Value.ToString() == "/" || op.Value.ToString() == "\\" || op.Value.ToString() == "'" ||
                     op.Value.ToString() == "/:" || op.Value.ToString() == "\\:" || op.Value.ToString() == "':"))
            {
                // Handle 2-argument adverb structure from LRS parser: ADVERB(verb, argument)
                var verbNode = node.Children[0];
                var argument = Evaluate(node.Children[1]);
                
                // One-adverb-at-a-time: if verbNode is itself a modified verb (1-child adverb node),
                // consume only the outermost adverb. For each element during iteration, construct
                // a new 2-child node with the inner modified verb and the element, then evaluate it.
                bool isModifiedVerb = verbNode.Type == ASTNodeType.DyadicOp && 
                    verbNode.Children.Count == 1 && verbNode.Value is SymbolValue;
                
                if (isModifiedVerb)
                {
                    // The verb is a modified verb (e.g., +/ in +/'x, ,/ in ,//x)
                    // Apply just the outer adverb, passing the inner modified verb AST as-is
                    var adverbName = op.Value.ToString();
                    return ApplyOuterAdverbWithModifiedVerb(adverbName, verbNode, argument);
                }
                
                // Parse the verb with adverbs
                var verbWithAdverbs = VerbAdverbParser.ParseVerbWithAdverbs(verbNode);
                if (verbWithAdverbs != null)
                {
                    // Add the current adverb to the list
                    var adverbs = new List<string>(verbWithAdverbs.Adverbs) { op.Value.ToString() };
                    var enhancedVerbWithAdverbs = new VerbWithAdverbs(verbWithAdverbs.BaseVerb, adverbs, verbNode.StartPosition);
                    
                    // For 2-argument adverb structures, we need to handle it differently
                    // The argument contains both left and right operands that need to be extracted
                    return adverbAwareEvaluator.HandleTwoArgumentAdverb(enhancedVerbWithAdverbs, argument);
                }
                else
                {
                    // Fallback to legacy evaluation for simple cases
                    // 2-child structure comes from disambiguating colon (verb:' args) = monadic context
                    // Use :: as left argument to signal monadic context to adverb handlers
                    var verbValue = Evaluate(verbNode);
                    var monadicLeft = new NullValue();
                    return op.Value.ToString() switch
                    {
                        "over" or "/" => ApplyAdverbSlash(verbValue, monadicLeft, argument),
                        "scan" or "\\" => ApplyAdverbBackslash(verbValue, monadicLeft, argument),
                        "each" or "'" => HandleAdverbTick(verbValue, monadicLeft, argument),
                        "each-right" or "/:" => ApplyAdverbSlashColon(verbValue, monadicLeft, argument),
                        "each-left" or "\\:" => ApplyAdverbBackslashColon(verbValue, monadicLeft, argument),
                        "each-prior" or "':" => ApplyAdverbTickColon(verbValue, monadicLeft, argument),
                        _ => throw new Exception($"Unknown adverb: {op.Value}")
                    };
                }
            }
            else if (node.Children.Count == 3 && 
                    (op.Value.ToString() == "each" || op.Value.ToString() == "over" || op.Value.ToString() == "scan" ||
                     op.Value.ToString() == "each-right" || op.Value.ToString() == "each-left" || op.Value.ToString() == "each-prior"))
            {
                // Handle 3-argument adverb structure using adverb-aware evaluation
                var verbNode = node.Children[0];
                var leftArg = Evaluate(node.Children[1]);
                var rightArg = Evaluate(node.Children[2]);
                
                // Ensure non-null values for adverb evaluation
                K3Value safeLeft = leftArg ?? new NullValue();
                K3Value safeRight = rightArg ?? new NullValue();
                
                // Check for projection first (left arg provided, right arg is null)
                // This must be checked before other paths to ensure projections are captured
                if (safeRight is NullValue && safeLeft is not NullValue && verbNode.Value is SymbolValue verbSymbol)
                {
                    string adverbName = op.Value.ToString();
                    return new AdverbProjectedFunctionValue(adverbName, verbSymbol.Value, 2, safeLeft);
                }
                
                // One-adverb-at-a-time: if verbNode is a modified verb (1-child adverb node),
                // only consume the outermost adverb here and leave the inner modified verb intact.
                // For each element of the iteration, build a temp 3-child node with the inner
                // modified verb and the elements, then evaluate it recursively.
                bool isModifiedVerb = verbNode.Type == ASTNodeType.DyadicOp &&
                    verbNode.Children.Count == 1 && verbNode.Value is SymbolValue;
                if (isModifiedVerb)
                {
                    return ApplyOuterAdverbWithModifiedVerbDyadic(op.Value.ToString(), verbNode, safeLeft!, safeRight!);
                }
                
                // Parse the verb with adverbs
                var verbWithAdverbs = VerbAdverbParser.ParseVerbWithAdverbs(verbNode);
                if (verbWithAdverbs != null)
                {
                    // Add the current adverb to the list
                    var adverbs = new List<string>(verbWithAdverbs.Adverbs) { op.Value.ToString() };
                    var enhancedVerbWithAdverbs = new VerbWithAdverbs(verbWithAdverbs.BaseVerb, adverbs, verbNode.StartPosition);
                    
                    // Evaluate using adverb-aware evaluator
                    return adverbAwareEvaluator.EvaluateVerbWithAdverbs(enhancedVerbWithAdverbs, safeLeft!, safeRight!);
                }
                else
                {
                    // Fallback to legacy evaluation for simple cases
                    var verbValue = Evaluate(verbNode);
                    return op.Value.ToString() switch
                    {
                        "over" => ApplyAdverbSlash(verbValue, safeLeft!, safeRight!),
                        "scan" => ApplyAdverbBackslash(verbValue, safeLeft!, safeRight!),
                        "each" => HandleAdverbTick(verbValue, safeLeft!, safeRight!),
                        "each-right" => ApplyAdverbSlashColon(verbValue, safeLeft!, safeRight!),
                        "each-left" => ApplyAdverbBackslashColon(verbValue, safeLeft!, safeRight!),
                        "each-prior" => ApplyAdverbTickColon(verbValue, safeLeft!, safeRight!),
                        _ => throw new Exception($"Unknown adverb: {op.Value}")
                    };
                }
            }
            else if (node.Children.Count == 1 &&
                    (op.Value.ToString() == "each-right" || op.Value.ToString() == "each-left" ||
                     op.Value.ToString() == "each-prior" || op.Value.ToString() == "each" ||
                     op.Value.ToString() == "over" || op.Value.ToString() == "scan" ||
                     op.Value.ToString() == "/" || op.Value.ToString() == "\\" || op.Value.ToString() == "'" ||
                     op.Value.ToString() == "/:" || op.Value.ToString() == "\\:" || op.Value.ToString() == "':"))
            {
                // Nominalized modified verb: adverb node with only the verb child and no arguments.
                // This occurs when a multi-adverb expression like ,/:\: builds the inner
                // modified verb (,/:) as an argument to the outer adverb (\:).
                // Also occurs for point-free projections like {x'}/ assigned to a variable.
                // Evaluate the inner verb and wrap it in a FunctionValue encoding so
                // EachLeft/EachRight can call it with each element as the reduced verb.
                var innerVerbValue = Evaluate(node.Children[0]);
                string adverbName = op.Value.ToString() switch
                {
                    "/" => "over", "\\" => "scan", "'" => "each",
                    "/:" => "each-right", "\\:" => "each-left", "':" => "each-prior",
                    var s => s
                };
                string innerVerbStr = innerVerbValue is SymbolValue sv ? sv.Value : innerVerbValue?.ToString() ?? "";
                string encoded = adverbName switch
                {
                    "each-right" => $"EACH_RIGHT:{innerVerbStr}",
                    "each-left"  => $"EACH_LEFT:{innerVerbStr}",
                    "each-prior" => $"EACH_PRIOR:{innerVerbStr}",
                    "each"       => $"EACH:{innerVerbStr}",
                    "over"       => $"OVER:{innerVerbStr}",
                    "scan"       => $"SCAN:{innerVerbStr}",
                    _            => $"{adverbName}:{innerVerbStr}"
                };
                return new FunctionValue(encoded, new List<string> { "x", "y" });
            }
            else if (node.Children.Count == 0)
            {
                // Handle niladic operators
                throw new Exception($"Dyadic operator must have exactly 2 children, got {node.Children.Count}");
            }
            else
            {
                throw new Exception($"Dyadic operator must have exactly 2 children, got {node.Children.Count}");
            }
        }
        
        /// <summary>
        /// One-adverb-at-a-time: apply only the outermost adverb, keeping the inner modified verb
        /// as an AST node that gets re-evaluated for each element during iteration.
        /// </summary>
        private K3Value ApplyOuterAdverbWithModifiedVerb(string adverbName, ASTNode modifiedVerbNode, K3Value argument)
        {
            // Helper: apply the modified verb to a single argument by building a temp 2-child AST node
            K3Value ApplyModifiedVerbTo(K3Value arg)
            {
                var tempNode = new ASTNode(ASTNodeType.DyadicOp);
                tempNode.Value = modifiedVerbNode.Value;
                // Copy the verb child from the modified verb node
                tempNode.Children.Add(modifiedVerbNode.Children[0]);
                // Add the argument as a literal child
                tempNode.Children.Add(ASTNode.MakeLiteral(arg));
                return Evaluate(tempNode);
            }
            
            // Helper: apply the modified verb dyadically (left, right)
            K3Value ApplyModifiedVerbDyadic(K3Value left, K3Value right)
            {
                var tempNode = new ASTNode(ASTNodeType.DyadicOp);
                // The modified verb's adverb becomes the outer structure with 3 children
                tempNode.Value = modifiedVerbNode.Value;
                tempNode.Children.Add(modifiedVerbNode.Children[0]);
                tempNode.Children.Add(ASTNode.MakeLiteral(left));
                tempNode.Children.Add(ASTNode.MakeLiteral(right));
                return Evaluate(tempNode);
            }

            switch (adverbName)
            {
                case "each" or "'":
                {
                    // Apply modified verb to each element of the argument
                    if (argument is VectorValue vec)
                    {
                        var results = new List<K3Value>();
                        foreach (var elem in vec.Elements)
                            results.Add(ApplyModifiedVerbTo(elem));
                        return new VectorValue(results);
                    }
                    // Scalar: apply directly
                    return ApplyModifiedVerbTo(argument);
                }
                
                case "over" or "/":
                {
                    // Over-Monad applied to the modified verb: convergence pattern
                    // f/x means apply f repeatedly until result matches previous or initial
                    // e.g., ,//x means apply ,/ repeatedly until flat
                    if (argument is VectorValue vec && vec.Elements.Count > 0)
                    {
                        // Apply modified verb repeatedly until convergence (result matches previous or initial)
                        var current = argument;
                        var initial = current;
                        for (int i = 0; i < 1000; i++) // safety limit
                        {
                            var next = ApplyModifiedVerbTo(current);
                            // Check convergence: result matches previous or initial
                            if (next.ToString() == current.ToString() || next.ToString() == initial.ToString())
                                return next;
                            current = next;
                        }
                        return current;
                    }
                    return argument; // scalar: return as-is
                }
                
                case "scan" or "\\":
                {
                    // Scan-Monad applied to the modified verb: trace convergence
                    // f\x means apply f repeatedly, collecting all intermediate results
                    if (argument is VectorValue || argument is K3Value)
                    {
                        var results = new List<K3Value>();
                        var current = argument;
                        var initial = current;
                        results.Add(current);
                        for (int i = 0; i < 1000; i++) // safety limit
                        {
                            var next = ApplyModifiedVerbTo(current);
                            results.Add(next);
                            if (next.ToString() == current.ToString() || next.ToString() == initial.ToString())
                                break;
                            current = next;
                        }
                        return new VectorValue(results);
                    }
                    return argument;
                }
                
                case "each-prior" or "':":
                {
                    // Each-prior with modified verb
                    if (argument is VectorValue vec && vec.Elements.Count > 1)
                    {
                        var results = new List<K3Value>();
                        for (int i = 0; i < vec.Elements.Count; i++)
                        {
                            if (i == 0)
                                results.Add(vec.Elements[i]);
                            else
                                results.Add(ApplyModifiedVerbDyadic(vec.Elements[i], vec.Elements[i - 1]));
                        }
                        return new VectorValue(results);
                    }
                    return argument;
                }
                
                case "each-right" or "/:":
                {
                    // Each-right with modified verb: apply to each item of the argument
                    if (argument is VectorValue vec)
                    {
                        var results = new List<K3Value>();
                        foreach (var elem in vec.Elements)
                            results.Add(ApplyModifiedVerbTo(elem));
                        return new VectorValue(results);
                    }
                    return ApplyModifiedVerbTo(argument);
                }
                
                case "each-left" or "\\":
                {
                    // Each-left with modified verb
                    if (argument is VectorValue vec)
                    {
                        var results = new List<K3Value>();
                        foreach (var elem in vec.Elements)
                            results.Add(ApplyModifiedVerbTo(elem));
                        return new VectorValue(results);
                    }
                    return ApplyModifiedVerbTo(argument);
                }
                
                default:
                    throw new Exception($"Unknown adverb in one-adverb-at-a-time: {adverbName}");
            }
        }

        /// <summary>
        /// One-adverb-at-a-time dyadic: apply only the outermost adverb, keeping the inner modified
        /// verb as an AST node that gets re-evaluated for each element during iteration.
        /// Handles dyadic nested-adverb expressions like x,''-x or y _di\:\: 0 2.
        /// </summary>
        private K3Value ApplyOuterAdverbWithModifiedVerbDyadic(string adverbName, ASTNode modifiedVerbNode, K3Value left, K3Value right)
        {
            // Helper: apply the modified verb dyadically by building a temp 3-child AST node
            // The inner modified verb is (adverb, verb) with 1 child; we wrap it as (adverb, verb, L, R)
            K3Value ApplyInner(K3Value innerLeft, K3Value innerRight)
            {
                var tempNode = new ASTNode(ASTNodeType.DyadicOp);
                tempNode.Value = modifiedVerbNode.Value;
                tempNode.Children.Add(modifiedVerbNode.Children[0]);
                tempNode.Children.Add(ASTNode.MakeLiteral(innerLeft));
                tempNode.Children.Add(ASTNode.MakeLiteral(innerRight));
                return Evaluate(tempNode);
            }

            switch (adverbName)
            {
                case "each" or "'":
                {
                    // Each: pair-wise iteration; both args must be vectors of same length
                    if (left is VectorValue lv && right is VectorValue rv)
                    {
                        if (lv.Elements.Count != rv.Elements.Count)
                            throw new Exception($"length error: {lv.Elements.Count} != {rv.Elements.Count}");
                        var results = new List<K3Value>();
                        for (int i = 0; i < lv.Elements.Count; i++)
                            results.Add(ApplyInner(lv.Elements[i], rv.Elements[i]));
                        return new VectorValue(results);
                    }
                    // Scalar broadcast: left scalar, right vector
                    if (right is VectorValue rvs)
                    {
                        var results = new List<K3Value>();
                        foreach (var r in rvs.Elements)
                            results.Add(ApplyInner(left, r));
                        return new VectorValue(results);
                    }
                    // left vector, right scalar
                    if (left is VectorValue lvs)
                    {
                        var results = new List<K3Value>();
                        foreach (var l in lvs.Elements)
                            results.Add(ApplyInner(l, right));
                        return new VectorValue(results);
                    }
                    return ApplyInner(left, right);
                }

                case "each-left" or "\\:":
                {
                    // Each-left: iterate over left, use full right each time
                    if (left is VectorValue lv)
                    {
                        var results = new List<K3Value>();
                        foreach (var l in lv.Elements)
                            results.Add(ApplyInner(l, right));
                        return new VectorValue(results);
                    }
                    return ApplyInner(left, right);
                }

                case "each-right" or "/:":
                {
                    // Each-right: iterate over right, use full left each time
                    if (right is VectorValue rv)
                    {
                        var results = new List<K3Value>();
                        foreach (var r in rv.Elements)
                            results.Add(ApplyInner(left, r));
                        return new VectorValue(results);
                    }
                    return ApplyInner(left, right);
                }

                case "over" or "/":
                {
                    // Over (dyadic): n f/ x means apply f n-times with f being dyadic
                    // For modified verb nested adverbs, treat as fold with left as seed
                    if (right is VectorValue rv)
                    {
                        var acc = left;
                        foreach (var r in rv.Elements)
                            acc = ApplyInner(acc, r);
                        return acc;
                    }
                    return ApplyInner(left, right);
                }

                case "scan" or "\\":
                {
                    // Scan (dyadic): like Over but collect intermediate results
                    if (right is VectorValue rv)
                    {
                        var results = new List<K3Value>();
                        var acc = left;
                        foreach (var r in rv.Elements)
                        {
                            acc = ApplyInner(acc, r);
                            results.Add(acc);
                        }
                        return new VectorValue(results);
                    }
                    return ApplyInner(left, right);
                }

                case "each-prior" or "':":
                {
                    // Each-prior (dyadic): use left as initial, then pair consecutive elements
                    if (right is VectorValue rv && rv.Elements.Count > 0)
                    {
                        var results = new List<K3Value>();
                        var prev = left;
                        foreach (var r in rv.Elements)
                        {
                            results.Add(ApplyInner(r, prev));
                            prev = r;
                        }
                        return new VectorValue(results);
                    }
                    return ApplyInner(left, right);
                }

                default:
                    throw new Exception($"Unknown adverb in one-adverb-at-a-time dyadic: {adverbName}");
            }
        }
        
        private int DetermineVectorTypeFromElements(List<K3Value> elements)
        {
            if (elements.Count == 0)
                return 0; // Default to mixed list for empty vectors
            
            bool allNumeric = elements.All(e => e is IntegerValue || e is LongValue || e is FloatValue);
            bool hasFloat = elements.Any(e => e is FloatValue);
            
            // Float vector only when ALL elements are numeric and at least one is float
            if (allNumeric && hasFloat)
                return -2;
            
            // Integer vector only when ALL elements are integers/longs
            if (elements.All(e => e is IntegerValue || e is LongValue))
                return -1;
            
            // Character vector when all are characters
            if (elements.All(e => e is CharacterValue))
                return -3;
            
            // Symbol vector when all are symbols
            if (elements.All(e => e is SymbolValue))
                return -4;
            
            // Mixed list
            return 0;
        }
        
        private K3Value EvaluateVector(ASTNode node)
        {
            var elements = new List<K3Value>();
            foreach (var child in node.Children)
            {
                elements.Add(Evaluate(child));
            }
            
            // Check if this should be a List (mixed types) or Vector (homogeneous)
            if (elements.Count == 0)
            {
                // Empty parentheses should create an empty VectorValue
                return new VectorValue(new List<K3Value>());
            }
            
            // Check if all elements are the same type
            var firstType = elements[0].GetType();
            var isHomogeneous = elements.All(e => e.GetType() == firstType);
            
            if (isHomogeneous)
            {
                // Create homogeneous VectorValue with proper type
                int vectorType = DetermineVectorTypeFromElements(elements);
                
                // If this is a float vector, convert all integer elements to floats
                if (vectorType == -2) // Float vector
                {
                    var convertedElements = new List<K3Value>();
                    foreach (var element in elements)
                    {
                        if (element is IntegerValue intValue)
                            convertedElements.Add(new FloatValue((double)intValue.Value));
                        else if (element is LongValue longValue)
                            convertedElements.Add(new FloatValue((double)longValue.Value));
                        else
                            convertedElements.Add(element);
                    }
                    return new VectorValue(convertedElements, vectorType);
                }
                
                return new VectorValue(elements, vectorType);
            }
            else
            {
                // Create mixed-type VectorValue (generic list)
                var listElements = elements.Cast<K3Value>().ToList(); // Convert K3Value to object
                return new VectorValue(listElements, 0); // Type 0 for generic list
            }
        }
        private K3Value EvaluateFunction(ASTNode node)
        {
            // The function value should already be stored in node.Value from the parser
            if (node.Value is not FunctionValue functionValue)
            {
                throw new Exception("Function node must contain a FunctionValue");
            }
            
            // According to updated spec: niladic functions should remain as functions and not be
            // automatically evaluated. They should only be evaluated when explicitly applied.
            // All functions (including niladic) should return the function object.
            return functionValue;
        }

        private K3Value EvaluateControlFlow(string name, ASTNode argsNode)
        {
            // Control flow functions need to re-evaluate their arguments on each iteration
            // The argsNode is a Vector of AST nodes (from bracket parsing)
            var argNodes = new List<ASTNode>();
            if (argsNode.Type == ASTNodeType.Vector)
            {
                argNodes.AddRange(argsNode.Children);
            }
            else
            {
                argNodes.Add(argsNode);
            }

            switch (name)
            {
                case "do":
                {
                    if (argNodes.Count < 2)
                        throw new Exception("Do function requires at least 2 arguments: count and expression(s)");
                    var count = ToInteger(Evaluate(argNodes[0]));
                    if (count < 0)
                        throw new Exception("Do count must be non-negative");
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 1; j < argNodes.Count; j++)
                        {
                            Evaluate(argNodes[j]);
                        }
                    }
                    return new SymbolValue("");
                }
                case "while":
                {
                    if (argNodes.Count < 2)
                        throw new Exception("While function requires at least 2 arguments: condition and expression(s)");
                    int maxIterations = 10000; // Safety limit
                    int iter = 0;
                    while (iter++ < maxIterations)
                    {
                        var condResult = Evaluate(argNodes[0]);
                        if (!IsNonZeroInteger(condResult))
                            break;
                        for (int j = 1; j < argNodes.Count; j++)
                        {
                            Evaluate(argNodes[j]);
                        }
                    }
                    return new SymbolValue(""); // while returns empty string
                }
                case "if":
                {
                    if (argNodes.Count < 2)
                        throw new Exception("If function requires at least 2 arguments: condition and expression(s)");
                    var condResult = Evaluate(argNodes[0]);
                    if (IsNonZeroInteger(condResult))
                    {
                        for (int j = 1; j < argNodes.Count; j++)
                        {
                            Evaluate(argNodes[j]);
                        }
                    }
                    return new SymbolValue(""); // if returns empty string
                }
                default:
                    throw new Exception($"Unknown control flow: {name}");
            }
        }

        private K3Value EvaluateFunctionCall(ASTNode node)
        {
            if (node.Children.Count < 1)
            {
                throw new Exception("Function call requires a function");
            }

            var functionNode = node.Children[0];
            var fnName = functionNode.Value is SymbolValue fs ? fs.Value : functionNode.Value?.ToString() ?? "";
            
            var arguments = new List<K3Value>();
            
            for (int i = 1; i < node.Children.Count; i++)
            {
                arguments.Add(Evaluate(node.Children[i]));
            }

            // Handle variable function calls first (to avoid evaluating built-in functions as variables)
            if (functionNode.Type == ASTNodeType.Variable)
            {
                // Variable function call: functionName[args]
                var functionName = functionNode.Value is SymbolValue symbol ? symbol.Value : functionNode.Value?.ToString() ?? "";
                return CallVariableFunction(functionName, arguments);
            }
            
            // First evaluate the left side to see if it's a vector or dictionary
            var leftValue = Evaluate(functionNode);
            
            // Handle Make function specially
            if (leftValue is FunctionValue func && func.BodyText == "Make")
            {
                return MakeFunction(arguments[0]);
            }

            // Handle DeferredTakeProjection: (n#f) x => n # (f x)
            if (leftValue is DeferredTakeProjection dtp)
            {
                K3Value innerArg = arguments.Count == 1 ? arguments[0] : (K3Value)new VectorValue(arguments);
                K3Value funcResult;
                if (dtp.Func is FunctionValue dtpFv)
                {
                    var tmpNode = new ASTNode(ASTNodeType.Function);
                    tmpNode.Value = dtpFv;
                    funcResult = CallDirectFunction(tmpNode, new List<K3Value> { innerArg });
                }
                else if (dtp.Func is ProjectedFunctionValue dtpPfv)
                    funcResult = CallProjectedFunction(dtpPfv, new List<K3Value> { innerArg });
                else if (dtp.Func is AdverbProjectedFunctionValue dtpApfv)
                    funcResult = CallAdverbProjectedFunction(dtpApfv, new List<K3Value> { innerArg });
                else
                    funcResult = innerArg;
                return Take(dtp.Count, funcResult);
            }
            
            // Check if this should be treated as indexing instead of function call
            if (leftValue is VectorValue || leftValue is DictionaryValue)
            {
                // This is indexing: vector[index] or dictionary[index]
                if (arguments.Count != 1)
                {
                    throw new Exception("Indexing requires exactly one argument");
                }
                return VectorIndex(leftValue!, arguments[0]);
            }
            
            // Handle SymbolValue: may be a KTree path or a variable name that resolves to a dict/vector
            if (leftValue is SymbolValue symVal)
            {
                // Delegate to AtIndexOperation which handles symbol resolution
                if (arguments.Count == 0)
                    return AtIndexOperation(symVal, new NullValue());
                if (arguments.Count == 1)
                    return AtIndexOperation(symVal, arguments[0]);
                // Multiple args: wrap in vector (semicolon list)
                return AtIndexOperation(symVal, new VectorValue(arguments));
            }

            // Handle function calls differently based on the function node type
            if (functionNode.Type == ASTNodeType.Function)
            {
                // Direct function call: {[params] body}[args]
                return CallDirectFunction(functionNode, arguments);
            }
            else
            {
                // Evaluate the function expression and call it
                var function = leftValue;
                
                if (function is FunctionValue functionValue)
                {
                    // Create a temporary AST node for the function to reuse CallDirectFunction
                    var tempFunctionNode = new ASTNode(ASTNodeType.Function);
                    tempFunctionNode.Value = functionValue;
                    return CallDirectFunction(tempFunctionNode, arguments);
                }
                else if (function is AdverbProjectedFunctionValue apfv)
                {
                    return CallAdverbProjectedFunction(apfv, arguments);
                }
                else if (function is ProjectedFunctionValue pfv)
                {
                    return CallProjectedFunction(pfv, arguments);
                }
                else if (function.Type == ValueType.Symbol)
                {
                    var functionName = (function as SymbolValue)?.Value ?? throw new Exception("Invalid function name");
                    return CallVariableFunction(functionName, arguments);
                }
                
                throw new Exception($"Cannot call non-function: {function.Type}");
            }
        }

        private K3Value CreateSlotProjectedFunction(FunctionValue originalFunction, List<string> parameters, List<K3Value> arguments)
        {
            // Slot-based projection: f[1;;3] — some args are NullValue (blank), others are fixed.
            // Build a new function whose parameters are only the blank slots, with the fixed values
            // substituted into the body as literals.
            var remainingParameters = new List<string>();
            var bodyText = originalFunction.BodyText;
            
            for (int i = 0; i < parameters.Count; i++)
            {
                if (arguments[i] is NullValue)
                {
                    remainingParameters.Add(parameters[i]);
                }
                else
                {
                    // Substitute the fixed argument into the body text (whole-word replacement)
                    var paramName = parameters[i];
                    var argLiteral = arguments[i].ToString();
                    bodyText = System.Text.RegularExpressions.Regex.Replace(
                        bodyText,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(paramName)}\b",
                        argLiteral);
                }
            }
            
            var projected = new FunctionValue(bodyText, remainingParameters);
            return projected;
        }

        private K3Value CreateProjectedFunction(FunctionValue originalFunction, List<K3Value> providedArguments)
        {
            // Create a new function with reduced valence
            var remainingParameters = originalFunction.Parameters.Skip(providedArguments.Count).ToList();
            var projectedBody = GenerateProjectedBody(originalFunction, providedArguments);
            
            return new FunctionValue(projectedBody, remainingParameters);
        }

        private string GenerateProjectedBody(FunctionValue originalFunction, List<K3Value> providedArguments)
        {
            // For a simpler implementation, we'll create a closure-like approach
            // Store the provided arguments and create a function that takes the remaining ones
            
            if (originalFunction.Parameters.Count <= providedArguments.Count)
            {
                // No remaining parameters, just evaluate the original function
                return originalFunction.BodyText;
            }
            
            // Create a new function body with argument substitution
            var bodyText = originalFunction.BodyText;
            
            // Substitute provided arguments in the body
            for (int i = 0; i < providedArguments.Count && i < originalFunction.Parameters.Count; i++)
            {
                var paramName = originalFunction.Parameters[i];
                var argValue = providedArguments[i].ToString();
                
                // Replace parameter name with its value in the body
                bodyText = bodyText.Replace(paramName, argValue);
            }
            
            return bodyText;
        }

        
        
        
        
        private K3Value CallDirectFunction(ASTNode functionNode, List<K3Value> arguments)
        {
            if (functionNode.Value is not FunctionValue functionValue)
            {
                throw new Exception("Function node must contain a FunctionValue");
            }
            
            var parameters = functionValue.Parameters;
            var bodyText = functionValue.BodyText;
            // Vector argument unpacking: if we have 1 vector argument but need multiple parameters, unpack it
            // Also unpack for implicit-param functions when the vector contains NullValue slots (projection)
            // Skip unpacking for encoded adverb FVs (OVER:, SCAN:, EACH:, etc.) — they handle args via adverb dispatch
            bool isEncodedAdverb = bodyText.StartsWith("OVER:") || bodyText.StartsWith("SCAN:") ||
                                   bodyText.StartsWith("EACH:") || bodyText.StartsWith("EACH_RIGHT:") ||
                                   bodyText.StartsWith("EACH_LEFT:") || bodyText.StartsWith("EACH_PRIOR:");
            bool skipUnpack = bodyText.StartsWith("EACH:");
            if (!skipUnpack && arguments.Count == 1 && arguments[0] is VectorValue vectorArg && vectorArg.Elements.Count > 1)
            {
                if (parameters.Count > 1 ||
                    (parameters.Count == 0 && vectorArg.Elements.Any(e => e is NullValue)))
                {
                    arguments = new List<K3Value>(vectorArg.Elements);
                }
            }
            
            // Handle implicit parameters (x, y, z) for functions with no explicit params
            if (parameters.Count == 0 && arguments.Count > 0)
            {
                // K convention: {x*2} has implicit param x, {x+y} has x and y, etc.
                var implicitParams = new List<string>();
                if (arguments.Count >= 1) implicitParams.Add("x");
                if (arguments.Count >= 2) implicitParams.Add("y");
                if (arguments.Count >= 3) implicitParams.Add("z");
                parameters = implicitParams;
            }

            // Check for slot-based projection: any argument is NullValue (blank slot, e.g. f[1;;3])
            if (parameters.Count > 0 && arguments.Count == parameters.Count &&
                arguments.Any(a => a is NullValue))
            {
                return CreateSlotProjectedFunction(functionValue, parameters, arguments);
            }

            // Check for projection: fewer arguments than expected valence
            // Skip for encoded adverb FVs — they handle partial application via adverb dispatch
            if (!isEncodedAdverb && arguments.Count < parameters.Count)
            {
                return CreateProjectedFunction(functionValue, arguments);
            }

            if (!isEncodedAdverb && arguments.Count != parameters.Count)
            {
                throw new Exception($"Function expects {parameters.Count} arguments, got {arguments.Count}");
            }
            
            // Create a new evaluator scope for this function call
            var functionEvaluator = new Evaluator(this); // Pass parent to inherit currentFunctionValue
            functionEvaluator.isInFunctionCall = true;
            
            // Copy local variables to function scope (for nested functions)
            foreach (var kvp in localVariables)
            {
                functionEvaluator.localVariables[kvp.Key] = kvp.Value;
            }
            
            // Bind parameters to arguments (in local scope)
            for (int i = 0; i < Math.Min(parameters.Count, arguments.Count); i++)
            {
                functionEvaluator.SetVariable(parameters[i], arguments[i]);
            }
            
            // Set the associated K tree for anonymous functions
            functionEvaluator.kTree = functionValue.AssociatedKTree; // Pass the associated K tree
            
            // Bind parameters to arguments (in local scope)
            var bindCount = Math.Min(parameters.Count, arguments.Count);
            for (int i = 0; i < bindCount; i++)
            {
                functionEvaluator.SetVariable(parameters[i], arguments[i]);
            }
            // For encoded adverbs called with fewer args than params, set unbound params to NullValue
            // to prevent GetVariable from climbing to parent and finding stale values
            if (isEncodedAdverb)
            {
                for (int i = bindCount; i < parameters.Count; i++)
                    functionEvaluator.SetVariable(parameters[i], new NullValue());
            }
            
            // Set the current function value for AST caching optimization
            functionEvaluator.currentFunctionValue = functionValue;
            
            // DEBUG: Verify functionValue is set correctly
            if (functionEvaluator.currentFunctionValue == null)
                throw new InvalidOperationException("CallDirectFunction: currentFunctionValue is null after setting");
            
            // Execute the function body using recursive text evaluation
            return ExecuteFunctionBody(bodyText, functionEvaluator, functionValue.PreParsedTokens);
        }

        private K3Value ExecuteFunctionBody(string bodyText, Evaluator functionEvaluator, List<Token>? preParsedTokens = null)
        {
            // DEBUG: Check if currentFunctionValue is set
            if (functionEvaluator.currentFunctionValue == null)
                throw new InvalidOperationException("ExecuteFunctionBody: currentFunctionValue is null");
                        
            if (string.IsNullOrWhiteSpace(bodyText))
            {
                return new IntegerValue(0); // Empty function result
            }
            
            // Check if this is an FFI function with method hint
            if (functionEvaluator.currentFunctionValue?.Hint is SymbolValue hint && 
                HintSystem.IsMemberHint(hint.Value))
            {
                return ExecuteFFIFunction(functionEvaluator.currentFunctionValue, functionEvaluator);
            }
            
            // Handle depth-based each projections: EACH_DEPTH:n (created by 0', 1', 2', etc.)
            // These are created when an integer each is used as a projected function
            if (bodyText.StartsWith("EACH_DEPTH:"))
            {
                string depthStr = bodyText.Substring("EACH_DEPTH:".Length);
                if (int.TryParse(depthStr, out int depth))
                {
                    // Get the data argument from the function scope
                    K3Value? data = null;
                    try { data = functionEvaluator.GetVariable("y"); } catch { }
                    if (data == null || data is NullValue)
                        try { data = functionEvaluator.GetVariable("x"); } catch { }
                    
                    if (data != null && !(data is NullValue))
                    {
                        if (depth == 0)
                        {
                            // 0' is identity - return data unchanged
                            return data;
                        }
                        // Apply each at the specified depth
                        return ApplyEachAtDepth(data, depth);
                    }
                    // No data provided - return the function itself (projected)
                    return new FunctionValue(bodyText, new List<string> { "x", "y" });
                }
            }
            
            // Handle point-free adverb projections: OVER:verbStr, SCAN:verbStr, EACH:verbStr, etc.
            // These are created when a verb+adverb expression with no argument is assigned (e.g. d:{x'}/)
            // When called with 2 args (x=left, y=right), apply: left adverb(innerVerb) right
            // When called with 1 arg  (x=right only), apply monadically: adverb(innerVerb) right
            {
                string? adverbKind = null;
                string? innerVerbText = null;
                foreach (var prefix in new[] { "OVER:", "SCAN:", "EACH:", "EACH_RIGHT:", "EACH_LEFT:", "EACH_PRIOR:" })
                {
                    if (bodyText.StartsWith(prefix))
                    {
                        adverbKind = prefix.TrimEnd(':');
                        innerVerbText = bodyText.Substring(prefix.Length);
                        break;
                    }
                }
                if (adverbKind != null && innerVerbText != null)
                {
                    // Evaluate the inner verb string in the function scope
                    K3Value innerVerb;
                    // If the inner verb text is itself an encoded adverb body, wrap it as a FunctionValue directly
                    // (parsing it as K would misinterpret "EACH:..." as an assignment expression)
                    bool innerIsEncodedAdverb = innerVerbText.StartsWith("OVER:") || innerVerbText.StartsWith("SCAN:") ||
                                               innerVerbText.StartsWith("EACH:") || innerVerbText.StartsWith("EACH_RIGHT:") ||
                                               innerVerbText.StartsWith("EACH_LEFT:") || innerVerbText.StartsWith("EACH_PRIOR:");
                    if (innerIsEncodedAdverb)
                    {
                        innerVerb = new FunctionValue(innerVerbText, new List<string> { "x", "y" });
                    }
                    else
                    {
                        var lexer2 = new Lexer(innerVerbText);
                        var tokens2 = lexer2.Tokenize();
                        var ast2 = ParserConfig.ParseWithConfig(tokens2, innerVerbText);
                        innerVerb = ast2 != null ? (functionEvaluator.Evaluate(ast2) ?? new NullValue()) : new SymbolValue(innerVerbText);
                    }

                    // Get left (x) and right (y) from the function scope
                    K3Value? xVal = null, yVal = null;
                    try { xVal = functionEvaluator.GetVariable("x"); } catch { }
                    try { yVal = functionEvaluator.GetVariable("y"); } catch { }
                    bool hasLeft  = xVal != null && !(xVal is NullValue);
                    bool hasRight = yVal != null && !(yVal is NullValue);

                    var sentinelLeft  = new NullValue();
                    var sentinelRight = new NullValue();
                    var left2  = hasLeft  ? xVal! : sentinelLeft;
                    var right2 = hasRight ? yVal! : sentinelRight;

                    return adverbKind switch
                    {
                        "OVER"       => ApplyAdverbSlash(innerVerb, left2, right2),
                        "SCAN"       => ApplyAdverbBackslash(innerVerb, left2, right2),
                        "EACH"       => HandleAdverbTick(innerVerb, left2, right2),
                        "EACH_RIGHT" => ApplyAdverbSlashColon(innerVerb, left2, right2),
                        "EACH_LEFT"  => ApplyAdverbBackslashColon(innerVerb, left2, right2),
                        "EACH_PRIOR" => ApplyAdverbTickColon(innerVerb, left2, right2),
                        _ => throw new Exception($"Unknown encoded adverb projection: {adverbKind}")
                    };
                }
            }
            
            try
            {
                ASTNode? ast;
                
                // Try to get cached AST from function value if available
                if (functionEvaluator.currentFunctionValue != null)
                {
                    ast = functionEvaluator.currentFunctionValue.GetCachedAst();
                    if (ast != null)
                    {
                        return functionEvaluator.Evaluate(ast) ?? new NullValue();
                    }
                }
                
                // Fallback to parsing from text (deferred validation per spec)
                if (preParsedTokens != null && preParsedTokens.Count > 0)
                {
                    ast = ParserConfig.ParseWithConfig(preParsedTokens, bodyText);
                }
                else
                {
                    var lexer = new Lexer(bodyText);
                    var tokens = lexer.Tokenize();
                    ast = ParserConfig.ParseWithConfig(tokens, bodyText);
                }
                
                if (ast != null)
                {
                    // Cache parsed AST for future use
                    functionEvaluator.currentFunctionValue?.CacheAst(ast);
                    var result = functionEvaluator.Evaluate(ast) ?? new NullValue();
                                        return result;
                }
                else
                {
                                        return new NullValue();
                }
            }
            catch (Exception ex)
            {
                                throw new Exception($"Function execution error: {ex.Message}");
            }
        }
        
        private K3Value ExecuteFFIFunction(FunctionValue functionValue, Evaluator functionEvaluator)
        {
            try
            {
                // For FFI functions, the body text contains information about the .NET member to invoke
                // Extract the type and member information from the function body
                var bodyText = functionValue.BodyText;
                
                // Parse constructor function body: "constructor:TypeName"
                if (bodyText.StartsWith("constructor:"))
                {
                    var typeName = bodyText.Substring("constructor:".Length);
                    
                    // Try to find the type in already loaded assemblies
                    Type? foundType = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var types = assembly.GetTypes();
                        foundType = types.FirstOrDefault(t => t.FullName == typeName);
                        if (foundType != null) break;
                    }
                    
                    if (foundType != null)
                    {
                        // Get arguments from the function evaluator's local variables
                        var args = new List<K3Value>();
                        foreach (var param in functionValue.Parameters)
                        {
                            var argValue = functionEvaluator.GetVariable(param);
                            if (argValue != null)
                            {
                                args.Add(argValue);
                            }
                        }
                        
                        // Create instance using FFI
                        return ForeignFunctionInterface.CreateInstance(foundType, args);
                    }
                }
                // Parse instance method function body: "method:MethodName|ObjectHandle"
                else if (bodyText.StartsWith("method:"))
                {
                    var parts = bodyText.Substring("method:".Length).Split('|');
                    var methodName = parts[0];
                    var objectHandle = parts.Length > 1 ? parts[1] : null;
                    
                    // Get the target object using the handle
                    object? targetObject = null;
                    if (!string.IsNullOrEmpty(objectHandle))
                    {
                        targetObject = ObjectRegistry.GetObject(objectHandle);
                    }
                    
                    if (targetObject != null)
                    {
                        // Get the method from the object's type
                        var objectType = targetObject.GetType();
                        var method = objectType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                        
                        // Fall back to static method (e.g., Complex.Abs takes a Complex arg)
                        if (method == null)
                        {
                            method = objectType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                        }
                        
                        // Fall back to property getter
                        if (method == null)
                        {
                            var prop = objectType.GetProperty(methodName, BindingFlags.Public | BindingFlags.Instance);
                            if (prop != null)
                            {
                                return TypeMarshalling.NetToK3(prop.GetValue(targetObject));
                            }
                        }
                        
                        if (method != null)
                        {
                            // Get method arguments
                            var methodArgs = new List<object?>();
                            var methodParams = method.GetParameters();
                            
                            // For static methods that take the object type as first arg, inject targetObject
                            bool isStatic = method.IsStatic;
                            int paramOffset = 0;
                            if (isStatic && methodParams.Length > 0 && 
                                methodParams[0].ParameterType.IsAssignableFrom(objectType) &&
                                functionValue.Parameters.Count < methodParams.Length)
                            {
                                methodArgs.Add(targetObject);
                                paramOffset = 1;
                            }
                            
                            // Map function parameters to remaining method parameters
                            for (int i = paramOffset; i < methodParams.Length && (i - paramOffset) < functionValue.Parameters.Count; i++)
                            {
                                var paramName = functionValue.Parameters[i - paramOffset];
                                var argValue = functionEvaluator.GetVariable(paramName);
                                if (argValue != null)
                                {
                                    methodArgs.Add(TypeMarshalling.K3ToNet(argValue, methodParams[i].ParameterType));
                                }
                            }
                            
                            // Invoke the method
                            var result = method.Invoke(isStatic ? null : targetObject, methodArgs.ToArray());
                            
                            // Convert result back to K3 value
                            return TypeMarshalling.NetToK3(result);
                        }
                    }
                    
                    // If we can't find the object, throw an informative error
                    throw new Exception($"Cannot invoke instance method '{methodName}' - target object not found or handle '{objectHandle}' is invalid.");
                }
                
                // Parse static method function body: "static_method:TypeFullName|AssemblyName|MethodName[|ObjectHandle]"
                else if (bodyText.StartsWith("static_method:"))
                {
                    var rest = bodyText.Substring("static_method:".Length);
                    var parts = rest.Split('|');
                    var typeFullName = parts[0];
                    var assemblyName = parts.Length > 1 ? parts[1] : "";
                    var methodName = parts.Length > 2 ? parts[2] : parts[0];
                    var instanceHandle = parts.Length > 3 ? parts[3] : null;
                    
                    // Find the type across loaded assemblies
                    Type? foundType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!string.IsNullOrEmpty(assemblyName) && asm.GetName().Name != assemblyName)
                            continue;
                        foundType = asm.GetType(typeFullName);
                        if (foundType != null) break;
                    }
                    
                    if (foundType == null)
                        throw new Exception($"Cannot find type '{typeFullName}' for static method '{methodName}'");
                    
                    // Find the method - may have multiple overloads
                    var candidateMethods = foundType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.Name == methodName)
                        .ToArray();
                    
                    if (candidateMethods.Length == 0)
                        throw new Exception($"Static method '{methodName}' not found on type '{typeFullName}'");
                    
                    // Collect explicit arguments from function parameters
                    var args = new List<K3Value>();
                    foreach (var param in functionValue.Parameters)
                    {
                        var argValue = functionEvaluator.GetVariable(param);
                        if (argValue != null) args.Add(argValue);
                    }
                    
                    // If instance handle is encoded, prepend the instance as first argument
                    object? instanceObj = null;
                    if (!string.IsNullOrEmpty(instanceHandle))
                        instanceObj = ObjectRegistry.GetObject(instanceHandle);
                    
                    // Find best overload: count = args + (1 if instanceObj) 
                    int totalArgs = args.Count + (instanceObj != null ? 1 : 0);
                    MethodInfo? bestMethod = candidateMethods.FirstOrDefault(m => m.GetParameters().Length == totalArgs)
                        ?? candidateMethods.OrderBy(m => Math.Abs(m.GetParameters().Length - totalArgs)).First();
                    
                    var methodParams = bestMethod.GetParameters();
                    var netArgs = new object?[methodParams.Length];
                    int argOffset = 0;
                    
                    // Fill first param with instance object if present
                    if (instanceObj != null && methodParams.Length > 0)
                    {
                        netArgs[0] = instanceObj;
                        argOffset = 1;
                    }
                    
                    for (int i = argOffset; i < methodParams.Length && (i - argOffset) < args.Count; i++)
                    {
                        var argVal = args[i - argOffset];
                        // If arg is object dictionary, unwrap to actual .NET object
                        if (argVal is DictionaryValue dictArg && dictArg.Hint?.Value == "object")
                        {
                            if (dictArg.Entries.TryGetValue(new SymbolValue("_this"), out var thisEntry)
                                && thisEntry.Value is SymbolValue thisHandleInner)
                            {
                                var obj = ObjectRegistry.GetObject(thisHandleInner.Value);
                                if (obj != null)
                                {
                                    netArgs[i] = obj;
                                    continue;
                                }
                            }
                        }
                        netArgs[i] = TypeMarshalling.K3ToNet(argVal, methodParams[i].ParameterType);
                    }
                    
                    var staticResult = bestMethod.Invoke(null, netArgs);
                    return TypeMarshalling.NetToK3(staticResult);
                }
                // Parse property getter: "property_getter:PropertyName|ObjectHandle"
                else if (bodyText.StartsWith("property_getter:"))
                {
                    var rest = bodyText.Substring("property_getter:".Length);
                    var parts = rest.Split('|');
                    var propName = parts[0];
                    var objHandle = parts.Length > 1 ? parts[1] : null;
                    var targetObject = ObjectRegistry.GetObject(objHandle);
                    if (targetObject == null)
                        throw new Exception($"Cannot get property '{propName}': object not found");
                    var prop = targetObject.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null)
                        throw new Exception($"Property '{propName}' not found");
                    return TypeMarshalling.NetToK3(prop.GetValue(targetObject));
                }
                // Parse property setter: "property_setter:PropertyName|ObjectHandle"
                else if (bodyText.StartsWith("property_setter:"))
                {
                    var rest = bodyText.Substring("property_setter:".Length);
                    var parts = rest.Split('|');
                    var propName = parts[0];
                    var objHandle = parts.Length > 1 ? parts[1] : null;
                    var targetObject = ObjectRegistry.GetObject(objHandle);
                    if (targetObject == null)
                        throw new Exception($"Cannot set property '{propName}': object not found");
                    var prop = targetObject.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null)
                        throw new Exception($"Property '{propName}' not found");
                    var valueArg = functionEvaluator.GetVariable("value");
                    if (valueArg != null)
                        prop.SetValue(targetObject, TypeMarshalling.K3ToNet(valueArg, prop.PropertyType));
                    return new NullValue();
                }
                
                throw new Exception("FFI function execution failed: cannot parse function body");
            }
            catch (Exception ex)
            {
                throw new Exception($"FFI function execution error: {ex.Message}");
            }
        }


        public K3Value CallVariableFunction(string functionName, List<K3Value> arguments)
        {
            // First try to use the unified VerbRegistry-based evaluation, but only for verbs that have implementations
            var verb = VerbRegistry.GetVerb(functionName);
            if (verb != null && verb.Implementations != null && verb.Implementations.Length > arguments.Count && verb.Implementations[arguments.Count - 1] != null)
            {
                try
                {
                    return EvaluateVerb(functionName, arguments.ToArray());
                }
                catch (Exception)
                {
                    // Fallback to the original switch-based evaluation if VerbRegistry fails
                }
            }
            
            // Use the original switch-based evaluation for backwards compatibility
            // Check if it's a system variable first
            try
            {
                var sysVarResult = GetSystemVariable(functionName);
                // If the system variable returns a FunctionValue (like _f), call it with arguments
                if (sysVarResult is FunctionValue sysFunc)
                {
                    var tempNode = new ASTNode(ASTNodeType.Function);
                    tempNode.Value = sysFunc;
                    var result = CallDirectFunction(tempNode, arguments);
                    return result ?? new NullValue();
                }
                return sysVarResult ?? new NullValue();
            }
            catch (Exception)
            {
                // Not a system variable, continue with regular function evaluation
            }
            
            // Check if it's a built-in function first
            switch (functionName)
            {
                case "!":
                    // Handle monadic enumerate operator
                    if (arguments.Count == 1)
                    {
                        // Special case: !` enumerates keys in root dictionary
                        if (arguments[0] is SymbolValue sym && sym.Value == "")
                        {
                            return kTree.GetRootKeys();
                        }
                        return Enumerate(arguments[0]);
                    }
                    else if (arguments.Count >= 2)
                    {
                        return ModRotate(arguments[0], arguments[1]);
                    }
                    throw new Exception("! operator requires at least 1 argument");
                case "do":
                case "_do":
                    {
                        // Unwrap if arguments contains a single VectorValue (from bracket notation parsing)
                        var doArgs = (arguments.Count == 1 && arguments[0] is VectorValue doVec) ? doVec : (arguments.Count > 0 ? new VectorValue(arguments) : (K3Value)new NullValue());
                        return DoFunction(doArgs);
                    }
                case "while":
                case "_while":
                    {
                        var whileArgs = (arguments.Count == 1 && arguments[0] is VectorValue whileVec) ? whileVec : (arguments.Count > 0 ? new VectorValue(arguments) : (K3Value)new NullValue());
                        return WhileFunction(whileArgs);
                    }
                case "if":
                case "_if":
                    {
                        var ifArgs = (arguments.Count == 1 && arguments[0] is VectorValue ifVec) ? ifVec : (arguments.Count > 0 ? new VectorValue(arguments) : (K3Value)new NullValue());
                        return IfFunction(ifArgs);
                    }
                case "_t":
                    return TimeFunction(new NullValue());
                case "_d":
                    return DirectoryFunction(new NullValue());
                case "_getenv":
                    return GetenvFunction(arguments.Count > 0 ? arguments[0] : new NullValue());
                case "_size":
                    return SizeFunction(arguments.Count > 0 ? arguments[0] : new NullValue());
                case "_gethint":
                    return GetHintFunction(arguments);
                case "_sethint":
                    return SetHintFunction(arguments);
                case "_dispose":
                    return DisposeFunction(arguments);
                case "_unmarshall":
                    return UnmarshallFunction(arguments);
                case "_exit":
                    return ExitFunction(arguments.Count > 0 ? arguments[0] : new NullValue());
                case "_ssr":
                    if (arguments.Count == 3) return SsrFunction(arguments[0], arguments[1], arguments[2]);
                    throw new Exception("_ssr requires 3 arguments: text;pattern;replacement");
                case ":":
                    // Check if this is conditional evaluation (3+ arguments) or regular assignment
                    if (arguments.Count >= 3)
                    {
                        // Conditional evaluation: :[cond; true; false]
                        return ConditionalEvaluation(arguments);
                    }
                    else if (arguments.Count == 2)
                    {
                        // Assignment: variable : value
                        if (arguments[0] is SymbolValue variableName)
                        {
                            return Assignment(variableName.Value, arguments[1]);
                        }
                        else
                        {
                            throw new Exception("Assignment requires a variable name on the left side");
                        }
                    }
                    else if (arguments.Count == 1)
                    {
                        // Monadic colon - return value from function (not implemented yet)
                        throw new Exception("Monadic colon (return from function) is not yet implemented");
                    }
                    else
                    {
                        throw new Exception("Colon operator requires at least 1 argument");
                    }
                case "@":
                    // Check if this is amend-item (3+ arguments) or regular @ operator
                    if (arguments.Count >= 3)
                    {
                        // Check for special case: triadic @ with colon (trapped apply to enlisted argument)
                        if (arguments.Count == 3 && IsColon(arguments[1]))
                        {
                            // Trapped apply to enlisted argument: (d; :; y) -> trapped apply with enlisted y
                            var enlistedArgument = Enlist(arguments[2]);
                            return TrappedApply(arguments[0], enlistedArgument);
                        }
                        else
                        {
                            // Regular amend-item operation
                            // AmendItemFunction handles enlistment of indices internally
                            return AmendItemFunction(arguments);
                        }
                    }
                    else
                    {
                        // Regular @ operator - handle based on argument count
                        if (arguments.Count == 1)
                        {
                            // Monadic @ is ATOM
                            return Atom(arguments[0]);
                        }
                        else if (arguments.Count == 2)
                        {
                            // Dyadic @ is AT - use AtIndex
                            return AtIndex(arguments[0] ?? throw new ArgumentNullException(nameof(arguments)), arguments[1] ?? throw new ArgumentNullException(nameof(arguments)));
                        }
                        else
                        {
                            throw new Exception("@ operator with invalid number of arguments");
                        }
                    }
                case ".":
                    // Check if this is amend (3+ arguments) or regular . operator
                    if (arguments.Count >= 3)
                    {
                        // Check for special case: triadic dot with colon (trapped apply)
                        if (arguments.Count == 3 && IsColon(arguments[1]))
                        {
                            // Trapped apply: (d; :; y) - behave like dyadic dot apply but never throw exceptions
                            return TrappedApply(arguments[0], arguments[2]);
                        }
                        else
                        {
                            // Regular amend operation
                            return AmendFunction(arguments);
                        }
                    }
                    else
                    {
                        // Regular . operator - handle based on argument count
                        if (arguments.Count == 1)
                        {
                            // Monadic . is MAKE - use Make function
                            return MakeFunction(arguments[0]);
                        }
                        else if (arguments.Count == 2)
                        {
                            // Check for trapped apply pattern: the second argument might be a vector like (f; args; :)
                            if (arguments[1] is VectorValue vec && vec.Elements.Count == 3 && IsColon(vec.Elements[2]))
                            {
                                // Trapped apply: .[f; args; :] pattern detected in comma-enlisted form
                                return TrappedApply(vec.Elements[0], vec.Elements[1]);
                            }
                            // Dyadic . is APPLY - use DotApply
                            return DotApply(arguments[0], arguments[1]);
                        }
                        else
                        {
                            throw new Exception(". operator with 2+ arguments is not supported (use .[...] for amend)");
                        }
                    }
                // Dyadic operators
                case "+":
                    if (arguments.Count == 1)
                    {
                        // Plus as a function (transpose)
                        return Transpose(arguments[0]);
                    }
                    else if (arguments.Count >= 2) 
                    {
                        return Plus(arguments[0], arguments[1]);
                    }
                    throw new Exception("+ operator requires 1 or 2 arguments");
                case "-":
                    if (arguments.Count == 1) return MonadicMinus(arguments[0]);
                    if (arguments.Count >= 2) return Minus(arguments[0], arguments[1]);
                    throw new Exception("- operator requires 1 or 2 arguments");
                case "*":
                    if (arguments.Count == 1) return First(arguments[0]);
                    if (arguments.Count >= 2) return Times(arguments[0], arguments[1]);
                    throw new Exception("* operator requires 1 or 2 arguments");
                case "*:":
                    if (arguments.Count == 1) return First(arguments[0]);
                    throw new Exception("*: operator requires 1 argument");
                case "%":
                    if (arguments.Count >= 2) return Divide(arguments[0], arguments[1]);
                    throw new Exception("% operator requires 2 arguments");
                case "/":
                    if (arguments.Count >= 2) return Divide(arguments[0], arguments[1]);
                    throw new Exception("/ operator requires 2 arguments");
                case "^":
                    if (arguments.Count >= 2) return Power(arguments[0], arguments[1]);
                    throw new Exception("^ operator requires 2 arguments");
                case "<":
                    if (arguments.Count >= 2) return LessThan(arguments[0], arguments[1]);
                    throw new Exception("< operator requires 2 arguments");
                case ">":
                    if (arguments.Count >= 2) return GreaterThan(arguments[0], arguments[1]);
                    throw new Exception("> operator requires 2 arguments");
                case "=":
                    if (arguments.Count >= 2) return Equal(arguments[0], arguments[1]);
                    if (arguments.Count == 1) return Group(arguments[0]);
                    throw new Exception("= operator requires 1 or 2 arguments");
                case ",":
                    if (arguments.Count >= 2) return Join(arguments[0], arguments[1]);
                    if (arguments.Count == 1) return Enlist(arguments[0]);
                    throw new Exception(", operator requires 1 or 2 arguments");
                // Mathematical functions
                case "_abs":
                    if (arguments.Count == 1) return MathAbs(arguments[0]);
                    throw new Exception("_abs requires 1 argument");
                case "_sqr":
                    if (arguments.Count == 1) return MathSqr(arguments[0]);
                    throw new Exception("_sqr requires 1 argument");
                case "_sqrt":
                    if (arguments.Count == 1) return MathSqrt(arguments[0]);
                    throw new Exception("_sqrt requires 1 argument");
                case "_floor":
                    if (arguments.Count == 1) return MathFloor(arguments[0]);
                    throw new Exception("_floor requires 1 argument");
                case "_sin":
                    if (arguments.Count == 1) return MathSin(arguments[0]);
                    throw new Exception("_sin requires 1 argument");
                case "_cos":
                    if (arguments.Count == 1) return MathCos(arguments[0]);
                    throw new Exception("_cos requires 1 argument");
                case "_tan":
                    if (arguments.Count == 1) return MathTan(arguments[0]);
                    throw new Exception("_tan requires 1 argument");
                case "_asin":
                    if (arguments.Count == 1) return MathAsin(arguments[0]);
                    throw new Exception("_asin requires 1 argument");
                case "_acos":
                    if (arguments.Count == 1) return MathAcos(arguments[0]);
                    throw new Exception("_acos requires 1 argument");
                case "_atan":
                    if (arguments.Count == 1) return MathAtan(arguments[0]);
                    throw new Exception("_atan requires 1 argument");
                case "_sinh":
                    if (arguments.Count == 1) return MathSinh(arguments[0]);
                    throw new Exception("_sinh requires 1 argument");
                case "_cosh":
                    if (arguments.Count == 1) return MathCosh(arguments[0]);
                    throw new Exception("_cosh requires 1 argument");
                case "_tanh":
                    if (arguments.Count == 1) return MathTanh(arguments[0]);
                    throw new Exception("_tanh requires 1 argument");
                case "_log":
                    if (arguments.Count == 1) return MathLog(arguments[0]);
                    throw new Exception("_log requires 1 argument");
                case "_exp":
                    if (arguments.Count == 1) return MathExp(arguments[0]);
                    throw new Exception("_exp requires 1 argument");
                // Database functions
                case "_ic":
                    if (arguments.Count == 1) return Ic(arguments[0]);
                    throw new Exception("_ic requires 1 argument");
                case "_ci":
                    if (arguments.Count == 1) return Ci(arguments[0]);
                    throw new Exception("_ci requires 1 argument");
                case "_val":
                    if (arguments.Count == 1) return ValFunction(arguments[0]);
                    throw new Exception("_val requires 1 argument");
                // System functions
                case "_eval":
                    if (arguments.Count == 1) return Verbs.EvalVerbHandler.Evaluate(new[] { arguments[0] });
                    throw new Exception("_eval requires 1 argument");
                case "_parse":
                    if (arguments.Count == 1) return Verbs.ParseVerbHandler.Parse(new[] { arguments[0] });
                    throw new Exception("_parse requires 1 argument");
                default:
                    // If not in the switch, it's not a built-in function
                    break;
            }
            
            // Check if it's a user-defined function stored in a variable
            var functionValue = GetVariable(functionName);
            if (functionValue is FunctionValue userFunction)
            {
                // Create a temporary AST node for the function to reuse CallDirectFunction
                var tempFunctionNode = new ASTNode(ASTNodeType.Function);
                tempFunctionNode.Value = userFunction;
                return CallDirectFunction(tempFunctionNode, arguments);
            }
            else if (functionValue is AdverbProjectedFunctionValue apfv)
            {
                // Handle AdverbProjectedFunctionValue (e.g., xf:>':0, then xf[x])
                return CallAdverbProjectedFunction(apfv, arguments);
            }
            else if (functionValue is VectorValue vectorValue && arguments.Count == 1)
            {
                // This is vector indexing using square bracket syntax: vector[index]
                return VectorIndex(vectorValue, arguments[0]);
            }
            else if (functionValue is DictionaryValue dictValue && arguments.Count == 1)
            {
                // This might be dictionary indexing using square bracket syntax
                return AtIndexOperation(dictValue, arguments[0]);
            }
            else if (functionValue is DeferredTakeProjection dtp)
            {
                // Handle DeferredTakeProjection: (n#f) x => n # (f x)
                K3Value innerArg = arguments.Count == 1 ? arguments[0] : (K3Value)new VectorValue(arguments);
                K3Value funcResult;
                if (dtp.Func is FunctionValue dtpFv)
                {
                    var tmpNode = new ASTNode(ASTNodeType.Function);
                    tmpNode.Value = dtpFv;
                    funcResult = CallDirectFunction(tmpNode, new List<K3Value> { innerArg });
                }
                else if (dtp.Func is ProjectedFunctionValue dtpPfv)
                    funcResult = CallProjectedFunction(dtpPfv, new List<K3Value> { innerArg });
                else if (dtp.Func is AdverbProjectedFunctionValue dtpApfv)
                    funcResult = CallAdverbProjectedFunction(dtpApfv, new List<K3Value> { innerArg });
                else
                    funcResult = innerArg;
                return Take(dtp.Count, funcResult);
            }
            throw new Exception($"Variable '{functionName}' is not a function");
        }

        private K3Value? EvaluateBlock(ASTNode node)
        {
            K3Value? lastResult = null;
            
            foreach (var child in node.Children)
            {
                lastResult = EvaluateNode(child);
            }
            
            return lastResult;
        }

        private K3Value? EvaluateExpressionList(ASTNode node)
        {
            var results = new List<K3Value>();
            K3Value? lastResult = null;
            
            foreach (var child in node.Children)
            {
                var result = EvaluateNode(child);
                lastResult = result ?? new NullValue();
                results.Add(lastResult);
            }
            
            // Check if any child is an assignment - if so, this is likely a top-level statement sequence
            // In K, top-level semicolon-separated statements return only the last result
            bool hasAssignment = node.Children.Any(c => c.Type == ASTNodeType.Assignment || 
                                                         c.Type == ASTNodeType.ApplyAndAssign);
            if (hasAssignment)
            {
                return lastResult;
            }
            
            // Semicolon-separated list (parenthesized): type is determined by element homogeneity.
            // Mixed numeric types (int+float) stay as type-0 mixed lists — NOT promoted to float vectors.
            // Float promotion only applies to space-separated vector literals (handled in EvaluateVector).
            int vectorType;
            if (results.Count == 0)
                vectorType = 0;
            else if (results.All(e => e is IntegerValue || e is LongValue))
                vectorType = -1;
            else if (results.All(e => e is FloatValue))
                vectorType = -2;
            else if (results.All(e => e is CharacterValue))
                vectorType = -3;
            else if (results.All(e => e is SymbolValue))
                vectorType = -4;
            else
                vectorType = 0; // Mixed list
            
            return new VectorValue(results, vectorType);
        }

        /// <summary>
        /// Evaluate a statement block (semicolon-separated statements in a function body).
        /// Executes all statements sequentially but returns only the last expression's value.
        /// </summary>
        private K3Value? EvaluateStatementBlock(ASTNode node)
        {
            K3Value? lastResult = new NullValue();
            
            foreach (var child in node.Children)
            {
                lastResult = EvaluateNode(child) ?? new NullValue();
            }
            
            // Return only the last result (statements before last are evaluated for side effects)
            return lastResult;
        }

        private K3Value Find(K3Value left, K3Value right)
        {
            // Find operator: d ? y
            // If y occurs among the items of d then d?y is the smallest index of all occurrences
            // Otherwise, d?y is #d (the smallest nonnegative integer that is not a valid index of d)
            // When d is nil, the result is y
            // Uses Match for comparing items (tolerant comparison)
            
            // Handle nil case: when d is nil, result is y
            if (left is NullValue)
            {
                return right;
            }
            
            // Handle list case: d is a list
            if (left is VectorValue leftVec)
            {
                // Search for right in left vector
                for (int i = 0; i < leftVec.Elements.Count; i++)
                {
                    var matchResult = Match(leftVec.Elements[i], right);
                    if (matchResult is IntegerValue intVal && intVal.Value == 1)
                    {
                        return new IntegerValue(i); // 0-based indexing (K3 standard)
                    }
                }
                // Not found, return #d (count of d)
                return new IntegerValue(leftVec.Elements.Count);
            }
            else
            {
                // Handle scalar case: d is an atom
                var matchResult = Match(left, right);
                if (matchResult is IntegerValue intVal2 && intVal2.Value == 1)
                {
                    return new IntegerValue(0); // Found at index 0 (K3 0-based)
                }
                // Not found, return #d (count of scalar is 1)
                return new IntegerValue(1);
            }
        }

        private K3Value Assignment(string variableName, K3Value value)
        {
            // Assignment: variable : value
            // Uses local variable assignment
            SetVariable(variableName, value);
            
            // Also set variable in EvalVerbHandler for _eval operations
            K3CSharp.Verbs.EvalVerbHandler.SetVariable(variableName, value);
            
            // LRS behavior: Return value for intermediate assignments, null for terminal assignments
            return isIntermediateAssignment ? value : new NullValue();
        }

        private K3Value ColonOperator(K3Value left, K3Value right)
        {
            // Colon operator: left : right
            // Can be either:
            // 1. Assignment: variable : value (when left is a variable name symbol)
            // 2. Conditional evaluation: :[cond; true; false] (when left is null from bracket parsing)
            
            // Check if this is conditional evaluation (left is null from bracket parsing)
            if (left is NullValue)
            {
                // This is conditional evaluation: right should be a vector of arguments
                if (right is VectorValue args)
                {
                    return ConditionalEvaluation(args.Elements);
                }
                else
                {
                    throw new Exception("Conditional evaluation requires a list of arguments");
                }
            }
            else
            {
                // This is assignment: left : right
                if (left is SymbolValue variableName)
                {
                    return Assignment(variableName.Value, right);
                }
                else
                {
                    throw new Exception($"Assignment requires a variable name on the left side, but got {left.GetType().Name} with value {left}");
                }
            }
        }

        private K3Value ConditionalEvaluation(List<K3Value> arguments)
        {
            // Conditional evaluation: [cond; true; false] or [cond1;true1; cond2;true2; ; condN;trueN; false]
            // Arguments alternate between conditions and expressions to execute
            // Returns the result of the first true expression, or the else branch if all false

            if (arguments.Count < 3)
            {
                throw new Exception("Conditional evaluation requires at least 3 arguments");
            }

            // Process arguments in pairs: (condition, expression)
            for (int i = 0; i < arguments.Count - 1; i += 2)
            {
                var condition = arguments[i];
                var expression = arguments[i + 1];

                // Evaluate condition
                var conditionResult = EvaluateExpression(condition);

                // Check if condition is a non-zero integer
                if (IsNonZeroInteger(conditionResult))
                {
                    // Condition is true, execute the expression
                    return EvaluateExpression(expression);
                }
            }

            // If odd number of arguments, the last is the "else" branch
            if (arguments.Count % 2 == 1)
            {
                return EvaluateExpression(arguments[arguments.Count - 1]);
            }

            // All conditions were false and no else branch, return nil
            return new NullValue();
        }
        
                
        private K3Value EvaluateExpression(K3Value expression)
        {
            // If the expression is already evaluated, return it
            if (!(expression is FunctionValue))
            {
                return expression;
            }
            
            // If it's a function value, we need to evaluate it
            // For now, this is a simplified implementation
            // In a full implementation, we'd need to handle function evaluation properly
            return expression;
        }

        private K3Value VectorIndex(K3Value vector, K3Value index)
        {
            // Handle vector indexing: vector @ index
            // If vector is null, return the index (spec: _n@x returns x)
            if (vector is NullValue)
            {
                return index;
            }
            
            if (vector is VectorValue vec)
            {
                // Handle null or empty-vector indexing — "all" operation: d[] or d[_n]
                if (index is NullValue || (index is VectorValue emptyIdx && emptyIdx.Elements.Count == 0))
                {
                    // Return all elements of the vector
                    return new VectorValue(vec.Elements);
                }
                else if (index is IntegerValue intIndex)
                {
                    // Single index: return element at position
                    int idx = intIndex.Value;
                    if (idx < 0 || idx >= vec.Elements.Count)
                    {
                        throw new Exception($"Index {idx} out of bounds for vector of length {vec.Elements.Count}");
                    }
                    return vec.Elements[idx];
                }
                else if (index is VectorValue indexVec)
                {
                    // Check if this is each-indexing (vector of index vectors)
                    // e.g., x[(0 2 4; 1 3)] returns (x[0 2 4]; x[1 3])
                    bool isEachIndexing = indexVec.Elements.Count > 0 &&
                        indexVec.Elements.All(e => e is VectorValue);

                    if (isEachIndexing)
                    {
                        // Each indexing: index x with each vector in the index
                        var eachResult = new List<K3Value>();
                        foreach (var idxVector in indexVec.Elements)
                        {
                            if (idxVector is VectorValue iv)
                            {
                                var indexedElements = new List<K3Value>();
                                foreach (var idxValue in iv.Elements)
                                {
                                    if (idxValue is IntegerValue intIdx)
                                    {
                                        int idx = intIdx.Value;
                                        if (idx < 0 || idx >= vec.Elements.Count)
                                        {
                                            throw new Exception($"Index {idx} out of bounds for vector of length {vec.Elements.Count}");
                                        }
                                        indexedElements.Add(vec.Elements[idx]);
                                    }
                                    else
                                    {
                                        throw new Exception($"Vector indices must be integers, got {idxValue.Type}");
                                    }
                                }
                                eachResult.Add(new VectorValue(indexedElements));
                            }
                        }
                        return new VectorValue(eachResult);
                    }

                    // Check if this is multi-dimensional indexing (matrix indexing)
                    // e.g., x[<x[;y];] where indexVec contains [vector_of_row_indices; null]
                    bool isMultiDimensional = indexVec.Elements.Count > 0 &&
                        indexVec.Elements.Any(e => e is VectorValue || e is NullValue);

                    if (isMultiDimensional)
                    {
                        // Multi-dimensional indexing: x[rows;cols;...]
                        return MultiDimensionalIndex(vec, indexVec);
                    }

                    // Single-dimensional indexing: return elements at specified positions
                    var result = new List<K3Value>();
                    foreach (var idxValue in indexVec.Elements)
                    {
                        if (idxValue is IntegerValue intIdx)
                        {
                            int idx = intIdx.Value;
                            if (idx < 0 || idx >= vec.Elements.Count)
                            {
                                throw new Exception($"Index {idx} out of bounds for vector of length {vec.Elements.Count}");
                            }
                            result.Add(vec.Elements[idx]);
                        }
                        else
                        {
                            throw new Exception($"Vector indices must be integers, got {idxValue.Type}");
                        }
                    }
                    return new VectorValue(result);
                }
                else
                {
                    throw new Exception($"Index must be integer or vector of integers, got {index.Type}");
                }
            }
            else if (vector is DictionaryValue dict)
            {
                // Handle null indexing (_n) - "all" operation for dictionaries
                if (index is NullValue)
                {
                    // Return all values of the dictionary
                    var values = new List<K3Value>();
                    foreach (var entry in dict.Entries)
                    {
                        values.Add(entry.Value.Value);
                    }
                    return new VectorValue(values);
                }
                // Handle dictionary indexing: dictionary @ key
                else if (index is SymbolValue key)
                {
                    // Check if key is just a period (or multiple periods) for all attributes
                    bool getAllAttributes = key.Value == "." || key.Value.Contains(".");
                    
                    if (getAllAttributes)
                    {
                        // Return all attributes as a dictionary - include entries whose values contain attribute dictionaries
                        var attributesDict = new DictionaryValue();
                        foreach (var dictEntry in dict.Entries)
                        {
                            // Check if the entry's value is a DictionaryValue (contains attributes)
                            if (dictEntry.Value.Value is DictionaryValue)
                            {
                                // Add the entry by copying the tuple structure
                                // The entry is a tuple (Value, Attribute), so we add it as-is
                                attributesDict.Entries[dictEntry.Key] = dictEntry.Value;
                            }
                        }
                        return attributesDict;
                    }
                    
                    // Check if key ends with period for attribute retrieval
                    bool getAttribute = key.Value.EndsWith(".");
                    string lookupKey = getAttribute ? key.Value.Substring(0, key.Value.Length - 1) : key.Value;
                    var lookupSymbol = new SymbolValue(lookupKey);
                    
                    // Single key lookup
                    if (dict.Entries.TryGetValue(lookupSymbol, out var entry))
                    {
                        if (getAttribute)
                        {
                            // Return attributes (null if no attributes)
                            return entry.Attribute ?? new DictionaryValue();
                        }
                        else
                        {
                            // Return value
                            return entry.Value;
                        }
                    }
                    else
                    {
                        throw new Exception($"Key '{lookupSymbol.Value}' not found in dictionary");
                    }
                }
                else if (index is VectorValue keyVec)
                {
                    // Multiple keys lookup: return vector of values or attributes
                    var result = new List<K3Value>();
                    foreach (var keyElement in keyVec.Elements)
                    {
                        if (keyElement is SymbolValue symbolKey)
                        {
                            // Check if key ends with period for attribute retrieval
                            bool getAttribute = symbolKey.Value.EndsWith(".");
                            string lookupKey = getAttribute ? symbolKey.Value.Substring(0, symbolKey.Value.Length - 1) : symbolKey.Value;
                            var lookupSymbol = new SymbolValue(lookupKey);
                            
                            if (dict.Entries.TryGetValue(lookupSymbol, out var entry))
                            {
                                if (getAttribute)
                                {
                                    // Return attributes (null if no attributes)
                                    result.Add(entry.Attribute ?? new DictionaryValue());
                                }
                                else
                                {
                                    // Return value
                                    result.Add(entry.Value);
                                }
                            }
                            else
                            {
                                throw new Exception($"Key '{lookupKey}' not found in dictionary");
                            }
                        }
                        else
                        {
                            throw new Exception($"Dictionary keys must be symbols, got {keyElement.Type}");
                        }
                    }
                    return new VectorValue(result);
                }
                else
                {
                    throw new Exception($"Dictionary index must be symbol or vector of symbols, got {index.Type}");
                }
            }
            else
            {
                throw new Exception($"Cannot index into type: {vector.Type}");
            }
        }

        /// <summary>
        /// Vector deep indexing for . operator (index at depth)
        /// e.g., x . 1 2 3 navigates nested structure: x[1][2][3]
        /// </summary>
        private K3Value VectorDotIndex(VectorValue vector, VectorValue indices)
        {
            if (indices.Elements.Count == 0)
            {
                return vector;
            }
            
            // Get the first index
            var firstIdx = indices.Elements[0];
            if (firstIdx is not IntegerValue intIdx)
            {
                throw new Exception($"Deep index must be integer, got {firstIdx.Type}");
            }
            
            int idx = intIdx.Value;
            if (idx < 0 || idx >= vector.Elements.Count)
            {
                throw new Exception($"Index {idx} out of bounds for vector of length {vector.Elements.Count}");
            }
            
            // Get the element at the first index
            var element = vector.Elements[idx];
            
            // If there are more indices, recurse
            if (indices.Elements.Count > 1)
            {
                var remainingIndices = new VectorValue(indices.Elements.GetRange(1, indices.Elements.Count - 1));
                
                if (element is VectorValue elementVec)
                {
                    return VectorDotIndex(elementVec, remainingIndices);
                }
                else if (element is VectorValue charVec && charVec.VectorType == -3)
                {
                    // Character vector (string) - index into it
                    return VectorDotIndex(charVec, remainingIndices);
                }
                else
                {
                    throw new Exception($"Cannot index into type {element.Type} with remaining indices");
                }
            }
            
            // Return the final element
            return element;
        }

        private K3Value AtIndex(K3Value left, K3Value right)
        {
            // Check if this is Amend Item operation: @[d; i; f; y] or @[d; i; f]
            // This happens when left is null (from bracket notation) or when left is at symbol
            if ((left is NullValue || (left is SymbolValue sym && sym.Value == "@")) && 
                right is VectorValue args && args.Elements.Count >= 3)
            {
                return AmendItemFunction(args.Elements);
            }
            
            // @ operator for indexing: data @ index
            // If data is null, return index (spec: _n@x returns x)
            if (left is NullValue)
            {
                return right ?? throw new ArgumentNullException(nameof(right));
            }
            
            // @ operator for applying a projected function to arguments
            // e.g., +[3 3 5] @ 2 or +[3 3 5][2] (parsed as APPLY(projection, 2))
            if (left is ProjectedFunctionValue projLeft)
            {
                var arguments = right is VectorValue argVec2 
                    ? new List<K3Value>(argVec2.Elements) 
                    : new List<K3Value> { right ?? new NullValue() };
                return CallProjectedFunction(projLeft, arguments);
            }
            
            // @ operator for function application: data @ function
            // When right is a function, apply it to the left operand
            if (right is FunctionValue funcVal)
            {
                var tempNode = new ASTNode(ASTNodeType.Function);
                tempNode.Value = funcVal;
                return CallDirectFunction(tempNode, new List<K3Value> { left ?? new NullValue() });
            }
            if (right is ProjectedFunctionValue projVal)
            {
                return CallProjectedFunction(projVal, new List<K3Value> { left ?? new NullValue() });
            }
            if (right is AdverbProjectedFunctionValue adverbVal)
            {
                return CallAdverbProjectedFunction(adverbVal, new List<K3Value> { left ?? new NullValue() });
            }
            
            // Function application with multiple arguments: f[x;y;z] where left is FunctionValue and right is VectorValue
            if (left is FunctionValue funcLeft)
            {
                var argVec = right as VectorValue;
                if (argVec != null && argVec.Elements.Count > 1 && (funcLeft.Parameters.Count > 1 || funcLeft.Parameters.Count == 0))
                {
                    // Multi-parameter function or unknown arity with multiple args: unpack vector
                    var funcNode = new ASTNode(ASTNodeType.Function);
                    funcNode.Value = funcLeft;
                    return CallDirectFunction(funcNode, argVec.Elements.ToList());
                }
                // Single parameter or explicit single arg: pass as-is
                var singleFuncNode = new ASTNode(ASTNodeType.Function);
                singleFuncNode.Value = funcLeft;
                return CallDirectFunction(singleFuncNode, new List<K3Value> { right });
            }
            
            // Regular indexing operation
            return AtIndexOperation(left ?? throw new ArgumentNullException(nameof(left)), right ?? throw new ArgumentNullException(nameof(right)));
        }

        private K3Value AtIndexOperation(K3Value data, K3Value index)
        {
            // Handle symbol as path to a dictionary or function
            if (data is SymbolValue sym)
            {
                // Special handling for _f (function self-reference)
                if (sym.Value == "_f")
                {
                    if (currentFunctionValue != null)
                    {
                        // Apply the current function to the index
                        if (currentFunctionValue is FunctionValue func)
                        {
                            var tempNode = new ASTNode(ASTNodeType.Function);
                            tempNode.Value = func;
                            var args = new List<K3Value> { index! };
                            return CallDirectFunction(tempNode, args);
                        }
                        return currentFunctionValue;
                    }
                }
                
                // Per spec: f[x] is (f).,(x) — if symbol names a known verb,
                // call it as a function with the index as a single argument
                if (VerbRegistry.GetVerb(sym.Value) != null)
                {
                    var args = new List<K3Value> { index! };
                    return CallVariableFunction(sym.Value, args);
                }
                
                var resolvedValue = GetVariableValuePublic(sym.Value);
                if (resolvedValue != null)
                {
                    data = resolvedValue;
                }
            }
            
            // Handle dictionary indexing
            if (data is DictionaryValue dict)
            {
                // Check if this is an FFI dictionary (has type metadata keys like isclass, fullname, etc.)
                // Delegate to MethodInvocation.Index for FFI-specific handling
                if (dict.Entries.ContainsKey(new SymbolValue("isclass")) ||
                    dict.Entries.ContainsKey(new SymbolValue("fullname")) ||
                    dict.Entries.ContainsKey(new SymbolValue("namespace")))
                {
                    return MethodInvocation.Index(dict, index!, this);
                }
                
                // Handle _n (null) or empty-vector index — return all values: d[] or d[_n]
                if (index is NullValue || (index is VectorValue emptyDictIdx && emptyDictIdx.Elements.Count == 0))
                {
                    var allValues = new List<K3Value>();
                    foreach (var entry in dict.Entries)
                    {
                        allValues.Add(entry.Value.Value);
                    }
                    return new VectorValue(allValues);
                }
                else if (index is SymbolValue symbol)
                {
                    
                    // Check if this is all attributes access (symbol is exactly ".")
                    if (symbol.Value == ".")
                    {
                        // Return all attributes as a vector of dictionaries
                        // This should be equivalent to d[~!d]
                        var attributes = new List<K3Value>();
                        foreach (var entry in dict.Entries)
                        {
                            // Check if the entry has attributes (stored in the Attribute field of the tuple)
                            if (entry.Value.Attribute is DictionaryValue attrDict)
                            {
                                // Add the attribute dictionary
                                attributes.Add(attrDict);
                            }
                        }
                        return new VectorValue(attributes);
                    }
                    // Check if this is attribute access (symbol ends with .)
                    else if (symbol.Value.EndsWith("."))
                    {
                        // Remove the trailing . to get the key name
                        var keyName = symbol.Value.Substring(0, symbol.Value.Length - 1);
                        var keySymbol = new SymbolValue(keyName);
                        
                        foreach (var entry in dict.Entries)
                        {
                            if (entry.Key.Equals(keySymbol))
                            {
                                return (K3Value?)entry.Value.Attribute ?? new NullValue(); // Return Attribute from tuple
                            }
                        }
                        throw new Exception($"Key '{keyName}' not found in dictionary");
                    }
                    else
                    {
                        // Dictionary @ symbol - get value by key
                        // Check if this is an FFI object with method calls
                        if (dict.Entries.ContainsKey(new SymbolValue("_this")))
                        {
                            var thisEntry = dict.Entries[new SymbolValue("_this")];
                            var thisValue = (thisEntry.Value is SymbolValue thisSym) ? thisSym.Value : (thisEntry.Value.ToString() ?? "");
                            
                            // Special handling for _this access on disposed objects
                            if (symbol.Value == "_this" && ObjectRegistry.IsDisposed(thisValue))
                            {
                                return new SymbolValue("Disposed");
                            }
                            
                            // Only treat as FFI object if _this is a valid object handle and not Disposed
                            if (ObjectRegistry.ContainsObject(thisValue) && thisValue != "Disposed")
                            {
                                // First: check if key exists directly in the dict (e.g., FunctionValue for method)
                                foreach (var entry in dict.Entries)
                                {
                                    if (entry.Key.Equals(symbol))
                                    {
                                        return entry.Value.Value;
                                    }
                                }
                                // Fallback: invoke via reflection (e.g., property not in dict)
                                return MethodInvocation.CallObjectMethod(dict, symbol);
                            }
                            else
                            {
                                // Not a valid FFI object anymore (e.g., after _dispose) or disposed
                                // Use regular dictionary lookup
                                foreach (var entry in dict.Entries)
                                {
                                    if (entry.Key.Equals(symbol))
                                    {
                                        return entry.Value.Value; // Extract Value from tuple
                                    }
                                }
                                throw new Exception($"Key '{symbol.Value}' not found in dictionary");
                            }
                        }
                        else
                        {
                            // Regular dictionary lookup
                            foreach (var entry in dict.Entries)
                            {
                                if (entry.Key.Equals(symbol))
                                {
                                    return entry.Value.Value; // Extract Value from tuple
                                }
                            }
                            throw new Exception($"Key '{symbol.Value}' not found in dictionary");
                        }
                    }
                }
                else if (index is VectorValue indexVec)
                {
                    // Vector indexing - get multiple keys
                    var results = new List<K3Value>();
                    foreach (var idx in indexVec.Elements)
                    {
                        if (idx is SymbolValue idxSym)
                        {
                            // Handle attribute access
                            if (idxSym.Value.EndsWith("."))
                            {
                                var keyName = idxSym.Value.Substring(0, idxSym.Value.Length - 1);
                                var keySymbol = new SymbolValue(keyName);
                                 
                                foreach (var entry in dict.Entries)
                                {
                                    if (entry.Key.Equals(keySymbol))
                                    {
                                        results.Add((K3Value?)entry.Value.Attribute ?? new NullValue());
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // Regular key lookup
                                foreach (var entry in dict.Entries)
                                {
                                    if (entry.Key.Equals(idxSym))
                                    {
                                        results.Add(entry.Value.Value);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("Dictionary indexing requires symbol keys");
                        }
                    }
                    return new VectorValue(results);
                }
            }
            
            // Handle vector indexing
            if (data is VectorValue vector)
            {
                return VectorIndex(vector, index ?? throw new ArgumentNullException(nameof(index)));
            }
            
            // Handle DeferredTakeProjection via bracket notation
            if (data is DeferredTakeProjection dtp)
            {
                // Convert index to function argument
                K3Value innerArg = index ?? throw new ArgumentNullException(nameof(index));
                K3Value funcResult;
                if (dtp.Func is FunctionValue dtpFv)
                {
                    var tmpNode = new ASTNode(ASTNodeType.Function);
                    tmpNode.Value = dtpFv;
                    funcResult = CallDirectFunction(tmpNode, new List<K3Value> { innerArg });
                }
                else if (dtp.Func is ProjectedFunctionValue dtpPfv)
                    funcResult = CallProjectedFunction(dtpPfv, new List<K3Value> { innerArg });
                else if (dtp.Func is AdverbProjectedFunctionValue dtpApfv)
                    funcResult = CallAdverbProjectedFunction(dtpApfv, new List<K3Value> { innerArg });
                else
                    funcResult = innerArg;
                return Take(dtp.Count, funcResult);
            }
            
            // Handle function calls via bracket notation
            if (data is FunctionValue function)
            {
                // Convert index to function arguments
                List<K3Value> args;
                if (index is VectorValue indexVec)
                {
                    args = indexVec.Elements;
                }
                else if (index is SymbolValue)
                {
                    // Single symbol argument - treat as single argument
                    args = new List<K3Value> { index ?? throw new ArgumentNullException(nameof(index)) };
                }
                else
                {
                    // Single non-vector argument
                    args = new List<K3Value> { index ?? throw new ArgumentNullException(nameof(index)) };
                }
                
                // Call the function
                return CallFunction(function, args);
            }
            
            // Handle adverb projected function calls via bracket notation: xf[x] where xf is >':0,
            if (data is AdverbProjectedFunctionValue apfv)
            {
                // Convert index to function arguments
                List<K3Value> args;
                if (index is VectorValue indexVec)
                {
                    args = indexVec.Elements;
                }
                else if (index is SymbolValue)
                {
                    args = new List<K3Value> { index ?? throw new ArgumentNullException(nameof(index)) };
                }
                else
                {
                    args = new List<K3Value> { index ?? throw new ArgumentNullException(nameof(index)) };
                }
                
                return CallAdverbProjectedFunction(apfv, args);
            }
            
            // Scalar indexed with empty args: x[] returns x (atom identity)
            if (index is NullValue || (index is VectorValue emptyIdx2 && emptyIdx2.Elements.Count == 0))
            {
                if (data is IntegerValue || data is LongValue || data is FloatValue || data is CharacterValue || data is SymbolValue)
                    return data;
            }
            
            throw new Exception("Index operation requires dictionary or vector");
        }

        /// <summary>
        /// Handle multi-dimensional indexing for matrices/tables
        /// e.g., x[rows;cols] where rows and cols are vectors or null
        /// </summary>
        private K3Value MultiDimensionalIndex(VectorValue matrix, VectorValue indexVec)
        {
            if (indexVec.Elements.Count == 0)
            {
                return matrix; // No indices specified, return entire matrix
            }

            // Parse all dimensions into a list of index lists (null means "all")
            var dimensions = new List<List<int>?>();
            var explicitSingleFlags = new List<bool>();
            
            foreach (var dimValue in indexVec.Elements)
            {
                var (indices, isExplicitSingle) = ParseDimensionIndex(dimValue);
                dimensions.Add(indices);
                explicitSingleFlags.Add(isExplicitSingle);
            }
            
            // Apply dimensions recursively
            return ApplyDimensions(matrix, dimensions, 0, explicitSingleFlags);
        }

        /// <summary>
        /// Parse a dimension index value into a list of indices (null means "all elements")
        /// </summary>
        private (List<int>? indices, bool isExplicitSingle) ParseDimensionIndex(K3Value dimValue)
        {
            if (dimValue is NullValue || (dimValue is VectorValue v && v.Elements.Count == 0))
            {
                return (null, false); // All elements
            }
            else if (dimValue is VectorValue vec)
            {
                var indices = new List<int>();
                foreach (var idx in vec.Elements)
                {
                    if (idx is IntegerValue intIdx)
                    {
                        indices.Add(intIdx.Value);
                    }
                    else
                    {
                        throw new Exception($"Indices must be integers, got {idx.Type}");
                    }
                }
                // 1-item vector like ,0 is NOT explicit single (returns 1-item vector)
                return (indices, false);
            }
            else if (dimValue is IntegerValue single)
            {
                // Atom like 0 is explicit single (unwraps the result)
                return (new List<int> { single.Value }, true);
            }
            else
            {
                throw new Exception($"Index must be integer or vector of integers, got {dimValue.Type}");
            }
        }

        /// <summary>
        /// Recursively apply dimensions to navigate nested structure
        /// </summary>
        private K3Value ApplyDimensions(K3Value value, List<List<int>?> dimensions, int depth, List<bool> explicitSingleFlags)
        {
            if (depth >= dimensions.Count)
            {
                return value; // No more dimensions to apply
            }
            
            var indices = dimensions[depth];
            var isExplicitSingle = explicitSingleFlags[depth];
            
            if (value is VectorValue vec)
            {
                if (indices == null)
                {
                    // Select all elements, then apply remaining dimensions to each
                    var results = new List<K3Value>();
                    foreach (var element in vec.Elements)
                    {
                        results.Add(ApplyDimensions(element, dimensions, depth + 1, explicitSingleFlags));
                    }
                    return new VectorValue(results);
                }
                else
                {
                    // Select specific indices
                    var results = new List<K3Value>();
                    foreach (var idx in indices)
                    {
                        if (idx < 0 || idx >= vec.Elements.Count)
                        {
                            throw new Exception($"Index {idx} out of bounds for vector of length {vec.Elements.Count}");
                        }
                        results.Add(ApplyDimensions(vec.Elements[idx], dimensions, depth + 1, explicitSingleFlags));
                    }
                    
                    // If explicitly selected a single element (atom index), unwrap
                    if (isExplicitSingle && results.Count == 1)
                    {
                        return results[0];
                    }
                    return new VectorValue(results);
                }
            }
            else
            {
                // Not a vector - can't apply non-null indices
                if (indices != null)
                {
                    throw new Exception($"Cannot index into type {value.Type}");
                }
                return value;
            }
        }

        
        private K3Value DropOrCut(K3Value left, K3Value right)
        {
            // Dyadic underscore _: cut/drop operation according to K specification
            
            if (left is VectorValue cutIndices && right is VectorValue sourceVector)
            {
                // Vector cut operation: 0 2 4 _ 0 1 2 3 4 5 6 7 returns (0 1;2 3;4 5 6 7)
                // Check for negative indices (domain error)
                foreach (var index in cutIndices.Elements)
                {
                    if (index is IntegerValue intValue && intValue.Value < 0)
                    {
                        throw new Exception("Domain error: negative indices in cut operation");
                    }
                }
                
                var result = new List<K3Value>();
                int startIndex = 0;
                
                for (int i = 0; i < cutIndices.Elements.Count; i++)
                {
                    if (cutIndices.Elements[i] is IntegerValue cutIndex)
                    {
                        // Get elements from startIndex to cutIndex
                        var segment = new List<K3Value>();
                        for (int j = startIndex; j < cutIndex.Value && j < sourceVector.Elements.Count; j++)
                        {
                            segment.Add(sourceVector.Elements[j]);
                        }
                        
                        if (segment.Count > 0)
                        {
                            result.Add(new VectorValue(segment));
                        }
                        
                        startIndex = cutIndex.Value;
                    }
                }
                
                // Add the remainder (elements from last index to end)
                var remainder = new List<K3Value>();
                for (int j = startIndex; j < sourceVector.Elements.Count; j++)
                {
                    remainder.Add(sourceVector.Elements[j]);
                }
                
                if (remainder.Count > 0)
                {
                    result.Add(new VectorValue(remainder));
                }
                
                return new VectorValue(result);
            }
            else if (left is IntegerValue dropCount && right is VectorValue rightVector)
            {
                if (dropCount.Value >= 0)
                {
                    // Drop from front: 4 _ 0 1 2 3 4 5 6 7 returns 4 5 6 7
                    if (dropCount.Value >= rightVector.Elements.Count)
                    {
                        return new VectorValue(new List<K3Value>()); // Empty vector
                    }
                    
                    var result = new List<K3Value>();
                    for (int i = dropCount.Value; i < rightVector.Elements.Count; i++)
                    {
                        result.Add(rightVector.Elements[i]);
                    }
                    return new VectorValue(result);
                }
                else
                {
                    // Drop from end: -4 _ 0 1 2 3 4 5 6 7 returns 0 1 2 3
                    int dropFromEnd = Math.Abs(dropCount.Value);
                    if (dropFromEnd >= rightVector.Elements.Count)
                    {
                        return new VectorValue(new List<K3Value>()); // Empty vector
                    }
                    
                    var result = new List<K3Value>();
                    for (int i = 0; i < rightVector.Elements.Count - dropFromEnd; i++)
                    {
                        result.Add(rightVector.Elements[i]);
                    }
                    return new VectorValue(result);
                }
            }
            else if (!(right is VectorValue))
            {
                // Convert right to vector if it's not already
                var targetVector = right is VectorValue rv ? rv : new VectorValue(new List<K3Value> { right });
                
                if (left is VectorValue cutIndicesVector)
                {
                    // Vector cut operation for non-vector right
                    // Check for negative indices (domain error)
                    foreach (var index in cutIndicesVector.Elements)
                    {
                        if (index is IntegerValue intValue && intValue.Value < 0)
                        {
                            throw new Exception("Domain error: negative indices in cut operation");
                        }
                    }
                    
                    var result = new List<K3Value>();
                    int startIndex = 0;
                    
                    for (int i = 0; i < cutIndicesVector.Elements.Count; i++)
                    {
                        if (cutIndicesVector.Elements[i] is IntegerValue cutIndex)
                        {
                            // Get elements from startIndex to cutIndex
                            var segment = new List<K3Value>();
                            for (int j = startIndex; j < cutIndex.Value && j < targetVector.Elements.Count; j++)
                            {
                                segment.Add(targetVector.Elements[j]);
                            }
                            
                            if (segment.Count > 0)
                            {
                                result.Add(new VectorValue(segment));
                            }
                            
                            startIndex = cutIndex.Value;
                        }
                    }
                    
                    // Add the remainder
                    var remainder = new List<K3Value>();
                    for (int j = startIndex; j < targetVector.Elements.Count; j++)
                    {
                        remainder.Add(targetVector.Elements[j]);
                    }
                    
                    if (remainder.Count > 0)
                    {
                        result.Add(new VectorValue(remainder));
                    }
                    
                    return new VectorValue(result);
                }
                else if (left is IntegerValue dropCountValue)
                {
                    if (dropCountValue.Value >= 0)
                    {
                        // Drop from front
                        if (dropCountValue.Value >= targetVector.Elements.Count)
                        {
                            return new VectorValue(new List<K3Value>());
                        }
                        
                        var result = new List<K3Value>();
                        for (int i = dropCountValue.Value; i < targetVector.Elements.Count; i++)
                        {
                            result.Add(targetVector.Elements[i]);
                        }
                        return new VectorValue(result);
                    }
                    else
                    {
                        // Drop from end
                        int dropFromEnd = Math.Abs(dropCountValue.Value);
                        if (dropFromEnd >= targetVector.Elements.Count)
                        {
                            return new VectorValue(new List<K3Value>());
                        }
                        
                        var result = new List<K3Value>();
                        for (int i = 0; i < targetVector.Elements.Count - dropFromEnd; i++)
                        {
                            result.Add(targetVector.Elements[i]);
                        }
                        return new VectorValue(result);
                    }
                }
            }
            
            throw new Exception("Drop/Cut operation requires vector arguments or integer+vector");
        }

        private K3Value Atom(K3Value operand)
        {
            // @ operator: returns 1 if scalar, 0 if vector
            if (operand is VectorValue)
                return new IntegerValue(0);
            else
                return new IntegerValue(1);
        }

        private K3Value Negate(K3Value operand)
        {
            // ~ operator has two meanings:
            // 1. For integers: boolean NOT (0 -> 1, non-zero -> 0) - use LogicalNegate
            // 2. For symbols: attribute handle (adds period suffix)
            
            if (operand is IntegerValue || operand is LongValue || operand is FloatValue)
            {
                // Boolean NOT for numeric types
                return LogicalNegate(operand);
            }
            else if (operand is CharacterValue charVal)
            {
                // Logical negation for characters: 1 if null character (0), 0 otherwise
                return new IntegerValue(charVal.Value == "\0" ? 1 : 0);
            }
            else if (operand is SymbolValue symbol)
            {
                // Attribute handle: adds period suffix
                return new SymbolValue(symbol.Value + ".");
            }
            else if (operand is VectorValue vec)
            {
                // Check if this is a vector of symbols (for attribute handle)
                if (vec.Elements.Count > 0 && vec.Elements[0] is SymbolValue)
                {
                    // Attribute handle for each symbol element
                    var result = new List<K3Value>();
                    foreach (var element in vec.Elements)
                    {
                        if (element is SymbolValue sym)
                            result.Add(new SymbolValue(sym.Value + "."));
                        else
                            throw new Exception("Attribute handle can only be applied to symbols or vectors of symbols");
                    }
                    return new VectorValue(result, -4); // Symbol vector
                }
                // For character vectors (including matrices represented as nested vectors),
                // apply logical negation element-wise: 1 for null char, 0 otherwise
                if (vec.Elements.Count > 0 && (vec.Elements[0] is CharacterValue ||
                    (vec.Elements[0] is VectorValue innerVec && innerVec.Elements.Count > 0 && innerVec.Elements[0] is CharacterValue)))
                {
                    var result = new List<K3Value>();
                    foreach (var element in vec.Elements)
                    {
                        result.Add(Negate(element));
                    }
                    return new VectorValue(result);
                }
                // For all other vectors (including numeric), use LogicalNegate which handles them recursively
                return LogicalNegate(operand);
            }
            else
            {
                throw new Exception($"Negate operator cannot be applied to {operand.GetType().Name}");
            }
        }

        private K3Value DotApply(K3Value left, K3Value right)
        {
            // Handle symbol as path to a dictionary
            if (left is SymbolValue pathSym)
            {
                var resolvedValue = GetVariableValuePublic(pathSym.Value);
                if (resolvedValue != null && !(resolvedValue is NullValue))
                {
                    left = resolvedValue;
                }
            }
            
            // Check if this is Amend operation: .[d; i; f; y] or .[d; i; f]
            // This happens when left is null (from bracket notation) or when left is the dot symbol
            if (left is NullValue || (left is SymbolValue sym && sym.Value == "."))
            {
                // Unwrap enlisted vector: .(,v) -> unwrap to get the inner vector
                var amendArgs = right;
                if (amendArgs is VectorValue wrappedVec && wrappedVec.Elements.Count == 1 && wrappedVec.Elements[0] is VectorValue innerVec)
                {
                    amendArgs = innerVec;
                }
                if (amendArgs is VectorValue args && args.Elements.Count >= 3
                    && !args.Elements.All(e => e is CharacterValue))
                {
                    // Check for trapped apply: .[f; args; :] pattern - colon is the LAST element (index 2)
                    if (args.Elements.Count == 3 && IsColon(args.Elements[2]))
                    {
                        return TrappedApply(args.Elements[0], args.Elements[1]);
                    }
                    // Check if this is actually a dict-make: all elements are 2-element pairs with symbol keys
                    bool isDictMake = args.Elements.All(e =>
                        e is VectorValue pair && pair.Elements.Count >= 2 && pair.Elements[0] is SymbolValue);
                    if (!isDictMake)
                    {
                        return AmendFunction(args.Elements);
                    }
                }
            }
            
            // Dot-apply operator: function . argument
            // Similar to function application but with different precedence
            // If left is null, this is monadic dot with multiple meanings based on argument type
            if (left is NullValue)
            {
                // Monadic dot operations based on argument type
                
                // Case 1: Dictionary argument - unmake dictionary
                if (right is DictionaryValue dictValue)
                {
                    var result = new List<K3Value>();
                    foreach (var entry in dictValue.Entries)
                    {
                        // Create triplet: (key; value; attribute)
                        var triplet = new List<K3Value> { entry.Key, entry.Value.Value };
                        if (entry.Value.Attribute != null)
                        {
                            triplet.Add(entry.Value.Attribute);
                        }
                        else
                        {
                            triplet.Add(new NullValue());
                        }
                        result.Add(new VectorValue(triplet));
                    }
                    return new VectorValue(result);
                }
                
                // Case 2a: Empty vector - .() returns an empty dictionary
                else if (right is VectorValue emptyVec && emptyVec.Elements.Count == 0)
                {
                    return new DictionaryValue();
                }
                
                // Case 2: Character vector argument - execute
                else if (right is VectorValue charVector && charVector.Elements.All(e => e is CharacterValue))
                {
                    // Convert to string and execute as K code
                    var code = string.Join("", charVector.Elements.Select(e => 
                        e is CharacterValue cv ? cv.Value : ""));
                    
                    try
                    {
                        var lexer = new Lexer(code);
                        var tokens = lexer.Tokenize();
                        var ast = ParserConfig.ParseWithConfig(tokens, code);
                        if (ast != null)
                        {
                            return Evaluate(ast) ?? new NullValue();
                        }
                        return new NullValue();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Execution error in character vector: {ex.Message}");
                    }
                }
                
                // Case 3: LRS parser issue - vector with single NullValue instead of symbols
                else if (right is VectorValue nullVector && nullVector.VectorType == 0 && nullVector.Elements.Count == 1 && nullVector.Elements[0] is NullValue)
                {
                    // This handles the case where LRS parser creates a vector with a single NullValue
                    // instead of parsing `a`b as symbols. We need to create a dictionary with symbols a and b.
                    var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
                    entries.Add(new SymbolValue("a"), (new NullValue(), null));
                    entries.Add(new SymbolValue("b"), (new NullValue(), null));
                    return new DictionaryValue(entries);
                }
                
                // Case 4: List of individual symbols - create dictionary with null values (LRS parser issue with consecutive symbols)
                else if (right is VectorValue list && list.VectorType == 0 && list.Elements.All(e => e is SymbolValue))
                {
                    // This handles the case where LRS parser parses `a`b as individual symbols instead of a symbol vector
                    var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
                    foreach (SymbolValue symbol in list.Elements)
                    {
                        entries.Add(symbol, (new NullValue(), null));
                    }
                    return new DictionaryValue(entries);
                }
                
                // Case 4: List (type 0) argument - make dictionary
                else if (right is VectorValue dictList && dictList.VectorType == 0)
                {
                    // Check if this has the correct structure for dictionary creation
                    // Each element should be a vector with at least 2 elements (key, value)
                    var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
                    
                    foreach (var element in dictList.Elements)
                    {
                        if (element is VectorValue pair && pair.Elements.Count >= 2)
                        {
                            var key = pair.Elements[0];
                            var value = pair.Elements[1];
                            K3Value? attr = pair.Elements.Count >= 3 ? pair.Elements[2] : null;
                            
                            if (key is SymbolValue symbolKey)
                            {
                                DictionaryValue? dictAttr = null;
                                if (attr is DictionaryValue dv)
                                    dictAttr = dv;
                                
                                entries.Add(symbolKey, (value ?? new NullValue(), dictAttr));
                            }
                            else
                            {
                                throw new Exception($"Dictionary key must be a symbol, got {key?.GetType().Name}");
                            }
                        }
                        else
                        {
                            throw new Exception("Invalid dictionary triplet format during conversion.");
                        }
                    }
                    
                    return new DictionaryValue(entries);
                }
                
                // Case 4: Symbol vector - create dictionary with null values (special case)
                else if (right is VectorValue symbolVector && symbolVector.VectorType == -4 && symbolVector.Elements.All(e => e is SymbolValue))
                {
                    var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
                    foreach (SymbolValue symbol in symbolVector.Elements)
                    {
                        entries.Add(symbol, (new NullValue(), null));
                    }
                    return new DictionaryValue(entries);
                }
                
                // Default case: return the argument (spec: _n . x returns x)
                return right ?? throw new ArgumentNullException(nameof(right));
            }
            else
            {
                // Handle dictionary dot-apply with symbol vectors (spec: d@`v is equivalent to d .,`v)
                if (left is DictionaryValue dict)
                {
                    if (right is NullValue)
                    {
                        // d[] or d[_n] — return all values
                        var values = dict.Entries.Values.Select(e => e.Value).ToList();
                        return new VectorValue(values);
                    }
                    else
                    {
                        // For symbol vectors, use dictionary indexing
                        return AtIndexOperation(dict, right ?? throw new ArgumentNullException(nameof(right)));
                    }
                }
                else if (left is VectorValue vector)
                {
                    // Vector deep indexing: vector . indices
                    // If right is a vector of integers, do deep indexing at depth
                    // e.g., x . 1 2 3 is equivalent to x[1][2][3]
                    if (right is VectorValue indexVec && indexVec.Elements.All(e => e is IntegerValue))
                    {
                        return VectorDotIndex(vector, indexVec);
                    }
                    // Otherwise, use regular vector indexing
                    return VectorIndex(vector, right ?? throw new ArgumentNullException(nameof(right)));
                }
                else if (left is FunctionValue function)
                {
                    List<K3Value> arguments;
                    if (right is VectorValue argVector)
                    {
                        arguments = new List<K3Value>(argVector.Elements);
                    }
                    else
                    {
                        arguments = new List<K3Value> { right ?? throw new ArgumentNullException(nameof(right)) };
                    }
                    return CallFunction(function, arguments);
                }
                else if (left is ProjectedFunctionValue projectedFunc)
                {
                    List<K3Value> arguments;
                    if (right is VectorValue argVector)
                    {
                        arguments = new List<K3Value>(argVector.Elements);
                    }
                    else
                    {
                        arguments = new List<K3Value> { right ?? throw new ArgumentNullException(nameof(right)) };
                    }
                    return CallProjectedFunction(projectedFunc, arguments);
                }
                else if (left is AdverbProjectedFunctionValue adverbProjFunc)
                {
                    List<K3Value> arguments;
                    if (right is VectorValue argVector)
                    {
                        arguments = new List<K3Value>(argVector.Elements);
                    }
                    else
                    {
                        arguments = new List<K3Value> { right ?? throw new ArgumentNullException(nameof(right)) };
                    }
                    return CallAdverbProjectedFunction(adverbProjFunc, arguments);
                }
                else if (left != null && left.Type == ValueType.Symbol)
                {
                    var functionName = (left as SymbolValue)?.Value ?? throw new Exception("Invalid function name for dot-apply");
                    
                    // Unpack vector arguments into individual arguments for bracket notation
                    List<K3Value> arguments;
                    if (right is VectorValue argVector)
                    {
                        arguments = new List<K3Value>(argVector.Elements);
                    }
                    else
                    {
                        arguments = new List<K3Value> { right ?? throw new ArgumentNullException(nameof(right)) };
                    }
                    return CallVariableFunction(functionName, arguments);
                }
                else
                {
                    throw new Exception("Dot-apply operator requires a function, vector, or dictionary on the left side");
                }
            }
        }

        private K3Value GlobalAssignment(K3Value left, K3Value right)
        {
            // Global assignment operator: variable :: value
            // Assigns to global variable regardless of current scope
            if (left.Type != ValueType.Symbol)
            {
                throw new Exception("Global assignment requires a variable name on the left side");
            }
            
            var variableName = (left as SymbolValue)?.Value ?? throw new Exception("Invalid variable name for global assignment");
            
            // Evaluate the right side
            var value = right;
            
            // Store in global variables (access parent evaluator if available)
            if (parentEvaluator != null)
            {
                parentEvaluator.globalVariables[variableName] = value;
            }
            else
            {
                globalVariables[variableName] = value;
            }
            
            return value;
        }

        private bool IsTypeConversionSpecifier(K3Value left)
        {
            return (left is IntegerValue intValue && intValue.Value == 0) ||
                   (left is LongValue longValue && longValue.Value == 0) ||
                   (left is FloatValue floatValue && floatValue.Value == 0.0) ||
                   (left is SymbolValue symValue && symValue.Value == "") ||
                   (left is CharacterValue charValue && charValue.Value == " ");
        }
        
        private K3Value PerformTypeConversion(K3Value left, K3Value right)
        {
            // Type conversions only work on character vectors according to the spec
            if (!IsCharacterVectorOrList(right))
            {
                throw new Exception($"Type conversion requires character vector input, got {right.Type}");
            }
            
            return ConvertType(left, right);
        }
        
        private bool IsCharacterVectorOrList(K3Value value)
        {
            if (value is CharacterValue)
                return true;
                
            if (value is VectorValue vec)
            {
                // Check if all leaf elements are character vectors
                return AllLeafElementsAreCharacterVectors(vec);
            }
            
            return false;
        }
        
        private bool AllLeafElementsAreCharacterVectors(VectorValue vec)
        {
            foreach (var element in vec.Elements)
            {
                if (element is VectorValue nestedVec)
                {
                    if (!AllLeafElementsAreCharacterVectors(nestedVec))
                        return false;
                }
                else if (!(element is CharacterValue))
                {
                    return false;
                }
            }
            return true;
        }

        private K3Value EvaluateStringExpression(K3Value value)
        {
            // {} form specifier - evaluate each leaf input expression and preserve structure
            // This is similar to the consistent recursion approach used in Format
            return EvaluateStringExpressionRecursive(value);
        }

        private K3Value EvaluateStringExpressionRecursive(K3Value value)
        {
            // Handle vectors with consistent recursion
            if (value is VectorValue vec)
            {
                // Check if this is a character vector (string) - should be a leaf node
                if (vec.Elements.Count > 0 && vec.Elements.All(e => e is CharacterValue))
                {
                    // Character vector - evaluate as string expression using dot execute
                    var str = string.Join("", vec.Elements.Cast<CharacterValue>().Select(c => c.Value));
                    return ExecuteStringExpression(str);
                }
                
                // Regular vector - recursively evaluate each element
                var vecResult = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    vecResult.Add(EvaluateStringExpressionRecursive(element));
                }
                return new VectorValue(vecResult);
            }
            else
            {
                // For non-vector values, convert to string and evaluate as expression
                var str = value is SymbolValue sym ? sym.Value : value.ToString();
                return ExecuteStringExpression(str);
            }
        }

        private K3Value ExecuteStringExpression(string expression)
        {
            // Execute the string expression using dot execute
            // This evaluates the expression in the current variable context

            try
            {
                var lexer = new Lexer(expression);
                var tokens = lexer.Tokenize();
                var ast = ParserConfig.ParseWithConfig(tokens, expression);
                if (ast != null)
                {
                    return Evaluate(ast) ?? new NullValue();
                }
                return new NullValue();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error evaluating string expression '{expression}': {ex.Message}");
            }
        }

        private K3Value EvaluateDoStatement(List<ASTNode> args)
        {
            // Do statement: do[count; expression] or do[count; expression1; ; expressionN]
            // Execute expressions count times, return null (type 6) per spec
            
            if (args.Count < 2)
            {
                throw new Exception("Do statement requires at least 2 arguments: count and expression(s)");
            }
            
            // Evaluate count first (once)
            var countValue = Evaluate(args[0]);
            var count = ToInteger(countValue);
            
            if (count < 0)
            {
                throw new Exception("Do count must be non-negative");
            }
            
            // Get expression nodes (skip the count)
            var expressionNodes = args.Skip(1).ToList();
            
            // Execute count times
            for (int i = 0; i < count; i++)
            {
                foreach (var exprNode in expressionNodes)
                {
                    // Re-evaluate the expression on each iteration
                    Evaluate(exprNode);
                }
            }
            
            // Do statements always return null (type 6) per spec
            return new NullValue();
        }
        
        private K3Value EvaluateConditionalExpression(List<ASTNode> args)
        {
            // Conditional expression: :[cond;true;false] or multi-condition form
            // Even arg count: :[c1;t1;c2;t2;...;cN;tN]  — no else branch
            // Odd arg count:  :[c1;t1;c2;t2;...;cN;tN;else]
            // Short-circuits: returns result of first true branch, or else/null if none match
            
            if (args.Count < 2)
            {
                throw new Exception("Conditional expression requires at least 2 arguments: condition and true expression");
            }
            
            // Determine the index of the optional else branch
            // Odd count => last arg is else; even count => no else
            int pairCount = args.Count / 2;
            bool hasElse = args.Count % 2 == 1;
            
            for (int i = 0; i < pairCount; i++)
            {
                var conditionValue = Evaluate(args[i * 2]);
                var condition = ToInteger(conditionValue);
                if (condition != 0)
                {
                    return Evaluate(args[i * 2 + 1]);
                }
            }
            
            // No condition matched — return else branch or null
            return hasElse ? Evaluate(args[args.Count - 1]) : new NullValue();
        }
        
        private K3Value EvaluateIfStatement(List<ASTNode> args)
        {
            // If statement: if[condition; expression] or if[condition; expression1; ; expressionN]
            // Execute expressions if condition is not equal to 0, return null (type 6) per spec
            
            if (args.Count < 2)
            {
                throw new Exception("If statement requires at least 2 arguments: condition and expression(s)");
            }
            
            // Evaluate condition
            var conditionValue = Evaluate(args[0]);
            var condition = ToInteger(conditionValue);
            
            if (condition != 0)
            {
                // Condition is true, execute expressions
                var expressionNodes = args.Skip(1).ToList();
                foreach (var exprNode in expressionNodes)
                {
                    Evaluate(exprNode);
                }
            }
            
            // If statements always return null (type 6) per spec
            return new NullValue();
        }
        
        private K3Value EvaluateWhileStatement(List<ASTNode> args)
        {
            // While statement: while[condition; expression] or while[condition; expression1; ; expressionN]
            // Execute expressions while condition is not equal to 0, return null (type 6) per spec
            
            if (args.Count < 2)
            {
                throw new Exception("While statement requires at least 2 arguments: condition and expression(s)");
            }
            
            var expressionNodes = args.Skip(1).ToList();
            
            while (true)
            {
                // Re-evaluate condition each iteration
                var conditionValue = Evaluate(args[0]);
                var condition = ToInteger(conditionValue);
                
                if (condition == 0)
                {
                    break;
                }
                
                // Execute expressions
                foreach (var exprNode in expressionNodes)
                {
                    Evaluate(exprNode);
                }
            }
            
            // While statements always return null (type 6) per spec
            return new NullValue();
        }

        private int ToInteger(K3Value value)
        {
            if (value is IntegerValue intValue)
            {
                return intValue.Value;
            }
            else if (value is LongValue longValue)
            {
                return (int)longValue.Value;
            }
            else if (value is FloatValue floatValue)
            {
                return (int)floatValue.Value;
            }
            else
            {
                throw new Exception("Cannot convert to integer");
            }
        }

        private K3Value DoFunction(K3Value operand)
        {
            // Do function: do[count; expression] or do[count; expression1; ; expressionN]
            // Execute expressions count times, return empty string (matching k.exe behavior)
            
            if (operand is VectorValue args && args.Elements.Count >= 2)
            {
                var countValue = args.Elements[0] is FunctionValue countFunc
                    ? Evaluate(ParserConfig.ParseWithConfig(countFunc.PreParsedTokens ?? new List<Token>(), "") ?? new ASTNode(ASTNodeType.Literal, new NullValue()))
                    : EvaluateExpression(args.Elements[0]);
                var count = ToInteger(countValue);
                
                if (count < 0)
                {
                    throw new Exception("Do count must be non-negative");
                }
                
                var expressions = args.Elements.Skip(1).ToList();
                
                for (int i = 0; i < count; i++)
                {
                    foreach (var expr in expressions)
                    {
                        // Handle FunctionValue (contains AST to evaluate) vs regular K3Value
                        if (expr is FunctionValue func)
                        {
                            // Parse and evaluate the function body
                            var ast = ParserConfig.ParseWithConfig(func.PreParsedTokens ?? new List<Token>(), "");
                            if (ast != null) Evaluate(ast); // Execute but don't store result
                        }
                        else
                        {
                            EvaluateExpression(expr); // Execute but don't store result
                        }
                    }
                }
                
                // Return empty string to match k.exe behavior
                return new SymbolValue("");
            }
            else
            {
                throw new Exception("Do function requires at least 2 arguments: count and expression(s)");
            }
        }

        private K3Value WhileFunction(K3Value operand)
        {
            // While function: while[condition; expression] or while[condition; expression1; ; expressionN]
            // Execute expressions while condition is not equal to 0
            
            if (operand is VectorValue args && args.Elements.Count >= 2)
            {
                var condition = args.Elements[0];
                var expressions = args.Elements.Skip(1).ToList();
                K3Value result = new SymbolValue(""); // Empty symbol for when loop doesn't execute
                
                while (true)
                {
                    // Evaluate condition
                    var conditionResult = EvaluateExpression(condition);
                    
                    // Check if condition is zero (false)
                    if (!IsNonZeroInteger(conditionResult))
                    {
                        break;
                    }
                    
                    // Execute all expressions
                    foreach (var expr in expressions)
                    {
                        result = EvaluateExpression(expr);
                    }
                }
                
                return result;
            }
            else
            {
                throw new Exception("While function requires at least 2 arguments: condition and expression(s)");
            }
        }

        private K3Value IfFunction(K3Value operand)
        {
            // If function: if[condition; expression] or if[condition; expression1; ; expressionN]
            // Execute expressions if condition is not equal to 0, return empty string (matching k.exe behavior)
            
            if (operand is VectorValue args && args.Elements.Count >= 2)
            {
                var condition = args.Elements[0];
                var expressions = args.Elements.Skip(1).ToList();
                
                // Evaluate condition
                var conditionResult = EvaluateExpression(condition);
                
                // Check if condition is non-zero (true)
                if (IsNonZeroInteger(conditionResult))
                {
                    // Execute all expressions but don't store result
                    foreach (var expr in expressions)
                    {
                        EvaluateExpression(expr);
                    }
                }
                
                // Return empty string to match k.exe behavior
                return new SymbolValue("");
            }
            else
            {
                throw new Exception("If function requires at least 2 arguments: condition and expression(s)");
            }
        }

        
        
        private K3Value TrappedApply(K3Value data, K3Value argument)
        {
            // Trapped apply: behave like dyadic dot apply but never throw exceptions
            // Always return a 2-item vector: [success_flag; result_or_error]
            try
            {
                // Try to perform the regular dot apply operation
                var result = DotApply(data, argument);
                
                // Success: return (0; result)
                var successFlag = new IntegerValue(0);
                var resultVector = new VectorValue(new List<K3Value> { successFlag, result });
                return resultVector;
            }
            catch (Exception ex)
            {
                // Error: return (1; error_message)
                var errorFlag = new IntegerValue(1);
                var errorMessageChars = new List<K3Value>();
                foreach (char c in ex.Message)
                {
                    errorMessageChars.Add(new CharacterValue(c.ToString()));
                }
                var errorMessage = new VectorValue(errorMessageChars, -3);
                var errorVector = new VectorValue(new List<K3Value> { errorFlag, errorMessage });
                return errorVector;
            }
        }
        
        private K3Value EvaluateEvalVerb(K3Value operand)
        {
            // Set the current evaluator instance so _eval can access global variables
            Verbs.EvalVerbHandler.SetEvaluator(this);
            return Verbs.EvalVerbHandler.Evaluate(new[] { operand });
        }
        
        private K3Value BdFunction(K3Value operand)
        {
            try
            {
                var primitiveValue = ConvertToPrimitive(operand);
                var serializer = new KSerializer();
                var bytes = serializer.Serialize(primitiveValue!);
                
                // Convert raw bytes to character vector (type -3)
                var charElements = new List<K3Value>();
                for (int i = 0; i < bytes.Length; i++)
                {
                    charElements.Add(new CharacterValue(((char)bytes[i]).ToString()));
                }
                
                return new VectorValue(charElements, -3); // Return character vector (type -3)
            }
            catch (Exception ex)
            {
                throw new Exception($"_bd (bytes from data) operation failed: {ex.Message}");
            }
        }
        
        private K3Value DbFunction(K3Value operand)
        {
            try
            {
                if (operand is VectorValue vec && vec.VectorType == -3)
                {
                    // Extract bytes directly from character vector
                    var bytes = new List<byte>();
                    foreach (var element in vec.Elements.OfType<CharacterValue>())
                    {
                        if (element.Value.Length == 1)
                        {
                            bytes.Add((byte)element.Value[0]);
                        }
                    }
                    
                    var deserializer = new KDeserializer();
                    var result = deserializer.Deserialize(bytes.ToArray());
                    
                    // Convert back to K3Value
                    return result switch
                    {
                        IntegerValue iv => iv,
                        FloatValue fv => fv,
                        CharacterValue cv => cv,
                        SymbolValue sv => sv,
                        VectorValue vv => vv,
                        DictionaryValue dv => dv,
                        FunctionValue fv => fv,
                        NullValue nv => nv,
                        int i => new IntegerValue(i),
                        double d => new FloatValue(d),
                        char c => new CharacterValue(c.ToString()),
                        string s when s.StartsWith("`") => new SymbolValue(s),
                        string s => CreateCharacterVectorFromString(s),
                        null => new NullValue(),
                        _ => throw new Exception($"Unsupported deserialized type: {result.GetType()}")
                    };
                }
                else
                {
                    throw new Exception("_db (data from bytes) requires a character vector (type -3) as input");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"_db (data from bytes) operation failed: {ex.Message}", ex);
            }
        }
        
        private K3Value GetHintFunction(List<K3Value> arguments)
        {
            // Monadic _gethint x: return current hint of x
            if (arguments.Count != 1)
            {
                throw new Exception("_gethint requires exactly 1 argument");
            }
            
            var value = arguments[0];
            return value.Hint ?? (K3Value)new NullValue();
        }
        
        private K3Value SetHintFunction(List<K3Value> arguments)
        {
            // Dyadic x _sethint y: set hint of x to symbol y
            if (arguments.Count != 2)
            {
                throw new Exception("_sethint requires exactly 2 arguments");
            }
            
            var value = arguments[0];
            var hintSymbol = arguments[1];
            
            if (!(hintSymbol is SymbolValue hint))
            {
                throw new Exception("_sethint: second argument must be a symbol");
            }
            
            // Create a new value with the hint set
            K3Value hintedValue;
            switch (value.Type)
            {
                case ValueType.Integer:
                    hintedValue = new IntegerValue(((IntegerValue)value).Value, hint);
                    break;
                case ValueType.Float:
                    hintedValue = new FloatValue(((FloatValue)value).Value, hint);
                    break;
                case ValueType.Long:
                    hintedValue = new LongValue(((LongValue)value).Value, hint);
                    break;
                case ValueType.Character:
                    hintedValue = new CharacterValue(((CharacterValue)value).Value, hint);
                    break;
                case ValueType.Symbol:
                    hintedValue = new SymbolValue(((SymbolValue)value).Value, hint);
                    break;
                case ValueType.Vector:
                    hintedValue = new VectorValue(((VectorValue)value).Elements, hint);
                    break;
                case ValueType.Dictionary:
                    hintedValue = new DictionaryValue(((DictionaryValue)value).Entries);
                    hintedValue.Hint = hint;
                    break;
                case ValueType.Function:
                    hintedValue = new FunctionValue(((FunctionValue)value).BodyText, ((FunctionValue)value).Parameters, ((FunctionValue)value).PreParsedTokens, ((FunctionValue)value).OriginalSourceText, hint);
                    break;
                case ValueType.Null:
                    hintedValue = new NullValue();
                    break;
                default:
                    throw new Exception($"_sethint: unsupported value type {value.Type}");
            }
            
            return hintedValue;
        }

        private K3Value DisposeFunction(List<K3Value> arguments)
        {
            // Monadic _dispose x: dispose object x
            if (arguments.Count == 1)
            {
                var obj = arguments[0];
                
                // Check if object has _this entry (object dictionary)
                if (obj is DictionaryValue dict)
                {
                    // Find the _this entry
                    SymbolValue thisKey = new SymbolValue("_this");
                    if (dict.Entries.TryGetValue(thisKey, out var thisEntry))
                    {
                        // Use .Value directly to get the raw handle string (not ToString which adds backtick)
                        var handle = (thisEntry.Value is SymbolValue sym) ? sym.Value : thisEntry.Value.ToString();
                        var netObj = ObjectRegistry.GetObject(handle);
                        
                        if (netObj != null)
                        {
                            // Call Dispose() if object implements IDisposable
                            if (netObj is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                            
                            // Mark as disposed in registry (keep it registered to prevent reuse)
                            ObjectRegistry.MarkAsDisposed(handle);
                        }
                        
                        // Return the original dictionary - _this will show "Disposed" when accessed via indexing
                        return dict;
                    }
                    
                    // Return original dictionary if no _this found
                    return dict;
                }
                else
                {
                    throw new Exception("_dispose: argument must be an object dictionary with _this entry");
                }
            }
            else
            {
                throw new Exception("_dispose: requires exactly 1 argument");
            }
        }

        private K3Value UnmarshallFunction(List<K3Value> arguments)
        {
            // Monadic _unmarshall x: refresh object properties from global registry
            if (arguments.Count == 1)
            {
                var obj = arguments[0];
                
                // Check if object has _this entry (object dictionary)
                if (obj is DictionaryValue dict)
                {
                    // Find the _this entry
                    SymbolValue thisKey = new SymbolValue("_this");
                    if (dict.Entries.TryGetValue(thisKey, out var thisEntry))
                    {
                        // Use .Value directly to get the raw handle string (not ToString which adds backtick)
                        var handle = (thisEntry.Value is SymbolValue sym) ? sym.Value : thisEntry.Value.ToString();
                        var netObj = ObjectRegistry.GetObject(handle);
                        
                        if (netObj != null)
                        {
                            // Use reflection to refresh non-static properties
                            var objType = netObj.GetType();
                            var properties = objType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            
                            var newEntries = new Dictionary<SymbolValue, (K3Value Value, DictionaryValue? Attribute)>();
                            
                            // Copy all existing entries
                            foreach (var entry in dict.Entries)
                            {
                                newEntries[entry.Key] = entry.Value;
                            }
                            
                            // Refresh property values
                            foreach (var prop in properties)
                            {
                                try
                                {
                                    var propValue = prop.GetValue(netObj);
                                    var kValue = TypeMarshalling.NetToK3(propValue);
                                    
                                    var propKey = new SymbolValue(prop.Name);
                                    newEntries[propKey] = (kValue, null);
                                }
                                catch
                                {
                                    // Skip properties that can't be read
                                }
                            }
                            
                            var newDict = new DictionaryValue(newEntries);
                            return newDict;
                        }
                        else
                        {
                            throw new Exception("_unmarshall: object not found in registry");
                        }
                    }
                    else
                    {
                        throw new Exception("_unmarshall: dictionary must have _this entry");
                    }
                }
                else
                {
                    throw new Exception("_unmarshall: argument must be an object dictionary with _this entry");
                }
            }
            else
            {
                throw new Exception("_unmarshall: requires exactly 1 argument");
            }
        }
        
        private static VectorValue CreateCharacterVectorFromString(string s)
        {
            var charElements = new List<K3Value>();
            foreach (char c in s)
            {
                charElements.Add(new CharacterValue(c.ToString()));
            }
            return new VectorValue(charElements, -3);
        }
        
        private object? ConvertToPrimitive(K3Value value)
        {
            return value switch
            {
                IntegerValue iv => iv.Value,
                FloatValue fv => fv.Value,
                CharacterValue cv => cv.Value,
                SymbolValue sv => "`" + sv.Value,
                NullValue => null,
                VectorValue vv => ConvertVectorToPrimitive(vv),
                DictionaryValue dv => ConvertDictionaryToPrimitive(dv),
                FunctionValue fv => ConvertFunctionToPrimitive(fv),
                _ => throw new NotSupportedException($"Cannot convert {value.GetType()} to primitive type")
            };
        }
        
        private object ConvertDictionaryToPrimitive(DictionaryValue dict)
        {
            // Return DictionaryValue directly - KSerializer will handle serialization
            return dict;
        }
        
        private object ConvertFunctionToPrimitive(FunctionValue func)
        {
            // Return FunctionValue directly - KSerializer will handle serialization
            return func;
        }
        
        private object ConvertVectorToPrimitive(VectorValue vector)
        {
            // Return VectorValue directly - KSerializer will handle serialization
            return vector;
        }
        
        private K3Value ConvertVectorToDictionary(K3Value operand)
        {
            // Convert vector to dictionary based on KDeserializer logic
            if (operand is VectorValue vector && vector.Elements.Count > 0)
            {
                var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
                
                foreach (var element in vector.Elements)
                {
                    if (element is VectorValue vectorValue && vectorValue.Elements.Count >= 2)
                    {
                        var key = vectorValue.Elements[0];
                        var value = vectorValue.Elements[1];
                        K3Value? attr = vectorValue.Elements.Count >= 3 ? vectorValue.Elements[2] : null;
                        
                        // Ensure key is a SymbolValue (dictionary keys are always symbols)
                        if (key is SymbolValue symbolKey)
                        {
                            // Convert attribute to DictionaryValue if it exists and is a dictionary, otherwise null
                            DictionaryValue? dictAttr = null;
                            if (attr is DictionaryValue dv)
                                dictAttr = dv;
                            
                            entries.Add(symbolKey, (value, dictAttr));
                        }
                        else
                        {
                            throw new InvalidOperationException($"Dictionary key must be a symbol, got {key?.GetType().Name}");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid dictionary triplet format during conversion.");
                    }
                }
                
                var result = new DictionaryValue(entries);
                return result;
            }
            else if (operand is VectorValue emptyVector && emptyVector.Elements.Count == 0)
            {
                // Empty vector -> empty dictionary
                return new DictionaryValue();
            }
            else
            {
                // For other types, just return the operand as-is
                return operand ?? throw new ArgumentNullException(nameof(operand));
            }
        }
        
        private K3Value MakeFunction(K3Value operand)
        {
            // Monadic dot: Make dictionary/Unmake dictionary/evaluate string
            
            // Case 2: Character vector argument - execute as K code
            if (operand is VectorValue charVector && charVector.Elements.Count > 0 && charVector.Elements.All(e => e is CharacterValue))
            {
                // This is a character vector (string) - evaluate as K code
                var stringValue = string.Join("", charVector.Elements.Select(e => ((CharacterValue)e).Value));
                // Check if this is a REPL command (starts with backslash)
                if (stringValue.StartsWith("\\"))
                {
                    // This is a REPL command, execute it directly and return null
                    // REPL commands are void operations that write to console
                    Program.HandleReplCommand(stringValue, this);
                    return new NullValue();
                }
                
                return ExecuteStringExpression(stringValue);
            }
            else if (operand is VectorValue nullVector && nullVector.VectorType == 0 && nullVector.Elements.Count == 1 && nullVector.Elements[0] is NullValue)
            {
                // This handles the case where LRS parser creates a vector with a single NullValue
                // instead of parsing `a`b as symbols. We need to create a dictionary with symbols a and b.
                var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
                entries.Add(new SymbolValue("a"), (new NullValue(), null));
                entries.Add(new SymbolValue("b"), (new NullValue(), null));
                return new DictionaryValue(entries);
            }
            else if (operand is DictionaryValue dv)
            {
                // Unmake dictionary - return list of triplets
                var result = new List<K3Value>();
                
                foreach (var kvp in dv.Entries)
                {
                    // Create triplet: (key;value;attribute)
                    K3Value attribute = kvp.Value.Attribute ?? (K3Value)new NullValue();
                    var triplet = new List<K3Value> { kvp.Key, kvp.Value.Value, attribute };
                    result.Add(new VectorValue(triplet));
                }
                return new VectorValue(result);
            }
            else if (operand is VectorValue symbolVector && symbolVector.Elements.All(e => e is SymbolValue))
            {
                // Special case: Symbol vector - create dictionary with null values
                // This handles both proper symbol vectors (VectorType == -4) and LRS parser issue (VectorType == 0)
                var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
                foreach (SymbolValue symbol in symbolVector.Elements)
                {
                    entries.Add(symbol, (new NullValue(), null));
                }
                return new DictionaryValue(entries);
            }
            else
            {
                // Make dictionary from operand (expects list of triplets)
                var result = ConvertVectorToDictionary(operand ?? throw new ArgumentNullException(nameof(operand)));
                return (K3Value?)(result) ?? throw new InvalidOperationException("ConvertVectorToDictionary returned null");
            }
        }

        private K3Value EvaluateProjectedFunction(ASTNode node)
        {
            // A projected function represents a partially applied function
            // The node.Value contains the operator/function name
            // The first child contains the arity (how many more arguments are needed)
            
            if (node.Value is SymbolValue operatorSymbol)
            {
                var operatorName = operatorSymbol.Value;
                
                // Check if this is an adverb projected function (verb + adverb)
                if (node.Children.Count >= 2 && node.Children[0].Value is SymbolValue verbSymbol)
                {
                    // This is an adverb projected function: verb stored as first child, arity as second
                    var adverbVerb = verbSymbol.Value;
                    int adverbArity = 1; // Default
                    if (node.Children[1].Value is IntegerValue adverbArityValue)
                    {
                        adverbArity = adverbArityValue.Value;
                    }
                    
                    // Create a special projected function for adverbs
                    return new AdverbProjectedFunctionValue(operatorName, adverbVerb, adverbArity);
                }
                
                // Get the arity (how many more arguments are needed)
                int regularArity = 1; // Default for monadic operators
                if (node.Children.Count > 0 && node.Children[0].Value is IntegerValue regularArityValue)
                {
                    regularArity = regularArityValue.Value;
                }
                
                // Capture bound arguments from children produced by CreateProjectionNode.
                // Children layout: [arity, leftArg_or_::, rightArg_or_::] where "::" = unbound.
                List<K3Value?>? boundArgs = null;
                if (node.Children.Count >= 3)
                {
                    var leftChild = node.Children[1];
                    var rightChild = node.Children[2];
                    bool leftUnbound = leftChild.Value is SymbolValue lsv && lsv.Value == "::";
                    bool rightUnbound = rightChild.Value is SymbolValue rsv && rsv.Value == "::";
                    if (!leftUnbound || !rightUnbound)
                    {
                        K3Value? leftVal = leftUnbound ? null : Evaluate(leftChild);
                        K3Value? rightVal = rightUnbound ? null : Evaluate(rightChild);
                        boundArgs = new List<K3Value?> { leftVal, rightVal };
                    }
                }
                
                return new ProjectedFunctionValue(operatorName, regularArity, boundArgs);
            }
            
            throw new Exception($"Invalid projected function node: {node.Value}");
        }

        private K3Value ValFunction(K3Value operand)
        {
            // _val returns the valence (arity) of a verb or function
            if (operand is SymbolValue sym)
            {
                var verbName = sym.Value;
                var verb = VerbRegistry.GetVerb(verbName);
                
                if (verb != null)
                {
                    // Return the highest supported arity for the verb
                    if (verb.SupportedArities.Length > 0)
                    {
                        return new IntegerValue(verb.SupportedArities.Max());
                    }
                }
                
                // Check if it's a user-defined function
                var functionValue = GetVariable(verbName);
                if (functionValue is FunctionValue func)
                {
                    // For user functions, return the number of required parameters
                    // This is a simplified implementation - in a full version, we'd need to track parameter counts
                    return new IntegerValue(1); // Default to monadic for user functions
                }
            }
            else if (operand is FunctionValue func)
            {
                // Handle projected functions - return remaining required arguments
                if (func.BodyText?.Contains("EACH_RIGHT:") == true || 
                    func.BodyText?.Contains("EACH_LEFT:") == true ||
                    func.BodyText?.Contains("EACH:") == true)
                {
                    return new IntegerValue(2); // Projected adverb functions are dyadic
                }
                
                return new IntegerValue(1); // Default to monadic
            }
            
            // For non-function operands, return 0 (no valence)
            return new IntegerValue(0);
        }

        /// <summary>
        /// Unified evaluation method using VerbRegistry - the core of the verb system restructuring
        /// </summary>
        public K3Value EvaluateVerb(string verbName, K3Value[] arguments)
        {
            // Fast check for verb existence
            if (!VerbRegistry.HasVerb(verbName))
            {
                throw new Exception($"Unknown verb: {verbName}");
            }

            // Validate arity with enhanced error messages
            var arity = arguments.Length;
            var validationError = VerbRegistry.ValidateVerbArity(verbName, arity);
            if (!string.IsNullOrEmpty(validationError))
            {
                throw new Exception(validationError);
            }

            // Get the implementation for this arity
            var verb = VerbRegistry.GetVerb(verbName);
            if (verb?.Implementations != null && verb.Implementations.Length > arity && verb.Implementations[arity] != null)
            {
                return verb.Implementations[arity]!(arguments);
            }

            // Fallback to CallVariableFunction for backwards compatibility
            return CallVariableFunction(verbName, arguments.ToList());
        }

        /// <summary>
        /// Returns the current function value for _f recursion support
        /// </summary>
        private K3Value GetCurrentFunctionValue()
        {
            return currentFunctionValue is not null ? currentFunctionValue : new NullValue();
        }

        /// <summary>
        /// Get system variable value - handles system variables as true variables
        /// </summary>
        public K3Value GetSystemVariable(string variableName)
        {
            
            var verb = VerbRegistry.GetVerb(variableName);
            // Use the same logic as IsSystemVariable: check both Type and IsSystemVariable property
            if (verb != null && (verb.Type == VerbType.SystemVariable || verb.IsSystemVariable == true))
            {
                // Handle system variables based on their names
                return variableName switch
                {
                    "_d" => kTree.CurrentBranch ?? new SymbolValue(""), // Current K-Tree branch
                    "_v" => new IntegerValue(1), // K3 version placeholder
                    "_i" => new IntegerValue(1), // Session ID placeholder
                    "_f" => GetCurrentFunctionValue(), // Current function for self-reference
                    "_n" => new IntegerValue(0), // Null placeholder
                    "_s" => new IntegerValue(0), // Seconds placeholder
                    "_h" => new IntegerValue(DateTime.Now.Hour),
                    "_p" => new IntegerValue(0), // Process ID placeholder
                    "_P" => new IntegerValue(0), // Parent process ID placeholder
                    "_w" => new IntegerValue(DateTime.Now.DayOfWeek - DayOfWeek.Sunday),
                    "_u" => new IntegerValue(0), // User ID placeholder
                    "_a" => new IntegerValue(0), // Account placeholder
                    "_k" => new IntegerValue(0), // K-tree placeholder
                    "_o" => new IntegerValue(0), // OS placeholder
                    "_c" => new IntegerValue(0), // CPU placeholder
                    "_r" => new IntegerValue(0), // RAM placeholder
                    "_m" => new IntegerValue(0), // Memory placeholder
                    "_y" => new IntegerValue(DateTime.Now.Year),
                    _ => throw new Exception($"Unknown system variable: {variableName}")
                };
            }
            
            throw new Exception($"Not a system variable: {variableName}");
        }

        /// <summary>
        /// Enhanced evaluation for projected functions
        /// </summary>
        public K3Value EvaluateProjectedFunction(string functionName, K3Value[] arguments)
        {
            var verb = VerbRegistry.GetVerb(functionName);
            if (verb == null || verb.Type != VerbType.ProjectedFunction)
            {
                throw new Exception($"Not a projected function: {functionName}");
            }

            // Get the remaining arity for the projected function
            var remainingArity = VerbRegistry.GetRemainingArity(functionName);
            
            if (arguments.Length != remainingArity)
            {
                var validationError = VerbRegistry.ValidateVerbArity(functionName, arguments.Length);
                throw new Exception($"Projected function error: {validationError}");
            }

            // Use the regular evaluation path for projected functions
            return EvaluateVerb(functionName, arguments);
        }

        /// <summary>
        /// Check if a function can be projected with adverbs
        /// </summary>
        public bool CanProjectFunction(string functionName)
        {
            return VerbRegistry.SupportsAdverbs(functionName);
        }

        /// <summary>
        /// Create a projected function from a base verb and adverb
        /// </summary>
        public K3Value CreateProjectedFunction(string baseVerb, string adverb, K3Value[] projectedArgs)
        {
            var projectedName = $"{baseVerb}_{adverb}";
            
            // Register the projected function dynamically
            var baseVerbInfo = VerbRegistry.GetVerb(baseVerb);
            if (baseVerbInfo == null)
            {
                throw new Exception($"Cannot project unknown verb: {baseVerb}");
            }

            // Calculate remaining arity
            var remainingArity = baseVerbInfo.SupportedArities.Max() - projectedArgs.Length;
            var supportedArities = remainingArity > 0 ? new[] { remainingArity } : new[] { 0 };
            
            VerbRegistry.RegisterProjectedFunction(
                projectedName, 
                supportedArities, 
                $"Projected function: {baseVerb} {adverb}"
            );

            // Create a function value that represents the projection
            // Store projection info in the RightArgument property for now
            var projectionInfo = new SymbolValue($"{baseVerb}:{adverb}:{string.Join(",", projectedArgs.Select(a => a.ToString()))}");
            
            return new FunctionValue(
                bodyText: projectedName,
                parameters: new List<string>(), // Will be filled during evaluation
                originalSourceText: $"Projected function: {baseVerb} {adverb}"
            )
            {
                RightArgument = projectionInfo
            };
        }

        private K3Value EvaluateTriadicOp(ASTNode node)
        {
            if (node.Value is not SymbolValue op) throw new Exception("Triadic operator must have a symbol value");
            if (node.Children.Count < 3) throw new Exception("Triadic operator requires 3 arguments");
            
            var opName = op.Value;
            
            // Check for trapped apply: .[f; args; :] - colon is the THIRD arg (Children[2])
            if (opName == "." && IsColonNode(node.Children[2]))
            {
                var arg1 = Evaluate(node.Children[0]);
                var arg2 = Evaluate(node.Children[1]);
                return TrappedApply(arg1, arg2);
            }
            
            var arg1Eval = Evaluate(node.Children[0]) ?? new NullValue();
            var arg2Eval = Evaluate(node.Children[1]) ?? new NullValue();
            var arg3Eval = Evaluate(node.Children[2]) ?? new NullValue();
            
            // For now, dispatch to existing evaluators for triadic dot and at operations
            // According to the plan, these should dispatch to existing evaluators
            if (opName == ".")
            {
                // Triadic dot: dispatch to existing evaluator
                return EvaluateTriadicDot(arg1Eval!, arg2Eval!, arg3Eval!);
            }
            else if (opName == "@")
            {
                // Triadic at: dispatch to existing evaluator
                return EvaluateTriadicAt(arg1Eval!, arg2Eval!, arg3Eval!);
            }
            else if (opName == "_ssr")
            {
                // _ssr is a ternary system function: _ssr[text;pattern;replacement]
                return SsrFunction(arg1Eval!, arg2Eval!, arg3Eval!);
            }
            else
            {
                throw new Exception($"Triadic operator '{opName}' not yet implemented");
            }
        }
        
        /// <summary>
        /// Check if an AST node represents a colon token (for trapped apply detection)
        /// </summary>
        private bool IsColonNode(ASTNode node)
        {
            // Check if the node is a literal colon symbol
            if (node.Type == ASTNodeType.Literal && node.Value is SymbolValue sym)
            {
                return sym.Value == ":";
            }
            // Also check if it's a single token that is a colon
            if (node.Value is SymbolValue sym2 && sym2.Value == ":")
            {
                return true;
            }
            return false;
        }

        private K3Value EvaluateTetradicOp(ASTNode node)
        {
            if (node.Value is not SymbolValue op) throw new Exception("Tetradic operator must have a symbol value");
            if (node.Children.Count < 4) throw new Exception("Tetradic operator requires 4 arguments");
            
            var arg1 = Evaluate(node.Children[0]) ?? new NullValue();
            var arg2 = Evaluate(node.Children[1]) ?? new NullValue();
            var arg3 = Evaluate(node.Children[2]) ?? new NullValue();
            var arg4 = Evaluate(node.Children[3]) ?? new NullValue();
            
            var opName = op.Value;
            
            // For now, dispatch to existing evaluators for tetradic dot and at operations
            if (opName == ".")
            {
                // Tetradic dot: dispatch to existing evaluator
                return EvaluateTetradicDot(arg1, arg2, arg3, arg4);
            }
            else if (opName == "@")
            {
                // Tetradic at: dispatch to existing evaluator
                return EvaluateTetradicAt(arg1, arg2, arg3, arg4);
            }
            else
            {
                throw new Exception($"Tetradic operator '{opName}' not yet implemented");
            }
        }

        private K3Value EvaluateVariadicOp(ASTNode node)
        {
            if (node.Value is not SymbolValue op) throw new Exception("Variadic operator must have a symbol value");
            if (node.Children.Count < 2) throw new Exception("Variadic operator requires at least 2 arguments");
            
            var opName = op.Value;
            
            // According to the plan, variadic adverbs should parse but signal "not yet implemented"
            throw new Exception($"Variadic adverb '{opName}' not yet implemented");
        }

        // Placeholder methods for triadic operations (to be implemented)
        private K3Value EvaluateTriadicDot(K3Value arg1, K3Value arg2, K3Value arg3)
        {
            // Check for trapped apply: .[f; args; :]
            if (arg2 != null && IsColon(arg2))
            {
                // Trapped apply: behave like dyadic dot apply but never throw exceptions
                return TrappedApply(arg1 ?? new NullValue(), arg3 ?? new NullValue());
            }
            
            // Check if arg3 is a SymbolValue (verb from MonadicOp wrapper)
            // This happens when the parser detects disambiguating colon syntax: .[d; i; verb:]
            if (arg3 is SymbolValue verbSymbol)
            {
                // The verb should be applied monadically to the selected element
                var amendArgs = new List<K3Value> { arg1 ?? new NullValue(), arg2 ?? new NullValue(), arg3 };
                return AmendFunction(amendArgs);
            }
            
            // Otherwise, it's a triadic amend operation: .[d; i; f]
            // where arg1=data, arg2=indices, arg3=function
            var amendArgs2 = new List<K3Value> { arg1 ?? new NullValue(), arg2 ?? new NullValue(), arg3 ?? new NullValue() };
            return AmendFunction(amendArgs2);
        }

        private K3Value EvaluateTriadicAt(K3Value arg1, K3Value arg2, K3Value arg3)
        {
            var amendArgs = new List<K3Value> { arg1, arg2, arg3 };
            return AmendItemFunction(amendArgs);
        }

        private K3Value EvaluateTetradicDot(K3Value arg1, K3Value arg2, K3Value arg3, K3Value arg4)
        {
            // Tetradic dot: .[d; i; f; y] - deep path amend (AmendFunction, not AmendItemFunction)
            // arg1=data, arg2=indices (path), arg3=function, arg4=value
            var amendArgs = new List<K3Value> { arg1, arg2, arg3, arg4 };
            return AmendFunction(amendArgs);
        }

        private K3Value EvaluateTetradicAt(K3Value arg1, K3Value arg2, K3Value arg3, K3Value arg4)
        {
            // Tetradic at: @[d; i; f; y] - amend item operation
            // arg1=data, arg2=indices, arg3=function, arg4=value
            var amendArgs = new List<K3Value> { arg1, arg2, arg3, arg4 };
            return AmendItemFunction(amendArgs);
        }

        /// <summary>
        /// Enhanced evaluator for verbs with adverbs (nested class for access to private methods)
        /// </summary>
        private class AdverbAwareEvaluator
        {
            private readonly Evaluator evaluator;
            private string currentVerb = "+"; // Track current verb context

            public AdverbAwareEvaluator(Evaluator evaluator)
            {
                this.evaluator = evaluator;
            }

            /// <summary>
            /// Evaluate a verb with adverbs applied sequentially from outermost to innermost
            /// </summary>
            /// <param name="verbWithAdverbs">The verb with adverbs to evaluate</param>
            /// <param name="arguments">Arguments to pass to the verb</param>
            /// <returns>Result of applying verb with adverbs</returns>
            public K3Value EvaluateVerbWithAdverbs(VerbWithAdverbs verbWithAdverbs, params K3Value[] arguments)
            {
                // Set current verb context
                currentVerb = verbWithAdverbs.BaseVerb;
                
                // Check if verb is monadic (has disambiguating colon suffix)
                bool isMonadicVerb = verbWithAdverbs.BaseVerb.EndsWith(":");
                
                // Also check verb arity from VerbRegistry
                var verbInfo = VerbRegistry.GetVerb(verbWithAdverbs.BaseVerb);
                bool isMonadicOnly = verbInfo != null && verbInfo.SupportedArities.Length == 1 && verbInfo.SupportedArities[0] == 1;
                
                // Check if verb has a monadic variant (e.g., + has +:)
                string monadicVariant = verbWithAdverbs.BaseVerb + ":";
                bool hasMonadicVariant = VerbRegistry.IsVerb(monadicVariant);
                
                // Special handling for over adverb
                if (verbWithAdverbs.Adverbs.Contains("over"))
                {
                    // If verb is monadic (either by colon suffix or monadic-only), use Over Monad
                    if ((isMonadicVerb || isMonadicOnly) && arguments.Length >= 2)
                    {
                        string verbToUse = isMonadicVerb ? verbWithAdverbs.BaseVerb : monadicVariant;
                        var left = arguments[0];
                        var x = arguments[1];
                        return evaluator.OverMonad(verbToUse, left, x);
                    }
                    
                    // Check if we have initialization (left argument is not dummy 0)
                    if (arguments.Length == 2 && arguments[0] is IntegerValue leftInt && leftInt.Value != 0)
                    {
                        // Use provided initialization
                        return ApplyOverAdverbWithInit(arguments[0], arguments[1], arguments);
                    }
                    else
                    {
                        // Use dummy initialization (0)
                        return ApplyOverAdverb(arguments[1], arguments);
                    }
                }
                
                // Special handling for scan adverb
                if (verbWithAdverbs.Adverbs.Contains("scan"))
                {
                    // If verb is monadic (either by colon suffix or monadic-only), use Scan Monad
                    if ((isMonadicVerb || isMonadicOnly) && arguments.Length >= 2)
                    {
                        string verbToUse = isMonadicVerb ? verbWithAdverbs.BaseVerb : monadicVariant;
                        var left = arguments[0];
                        var x = arguments[1];
                        return evaluator.ScanMonad(verbToUse, left, x);
                    }

                    // Check if we have initialization (left argument is not dummy 0)
                    if (arguments.Length == 2 && arguments[0] is IntegerValue leftInt && leftInt.Value != 0)
                    {
                        // Use provided initialization
                        return ApplyScanAdverbWithInit(arguments[0], arguments[1], arguments);
                    }
                    else
                    {
                        // Use dummy initialization (first element)
                        return ApplyScanAdverb(arguments[1], arguments);
                    }
                }
                
                // Start with the base verb and arguments
                K3Value result = ApplyBaseVerb(verbWithAdverbs.BaseVerb, arguments);
                
                // Apply adverbs from innermost to outermost (reverse order of parsing)
                var adverbsReversed = verbWithAdverbs.Adverbs.AsEnumerable().Reverse().ToList();
                
                foreach (var adverb in adverbsReversed)
                {
                    result = ApplyAdverb(adverb, result, arguments);
                }
                
                return result;
            }

            private K3Value ApplyBaseVerb(string verb, K3Value[] arguments)
            {
                if (arguments[0] == null)
                    throw new ArgumentNullException(nameof(arguments), "First argument cannot be null");
                    
                return verb switch
                {
                    "+" => arguments.Length == 1 ? evaluator.Transpose(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("+", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "-" => arguments.Length == 1 ? evaluator.MonadicMinus(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("-", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "*" => arguments.Length == 1 ? evaluator.First(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("*", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "%" => arguments.Length == 1 ? throw new Exception("% operator requires 2 arguments") : evaluator.EvaluateDyadicOperatorWithRegistry("%", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "^" => arguments.Length == 1 ? evaluator.Power(arguments[0], new IntegerValue(1)) : evaluator.EvaluateDyadicOperatorWithRegistry("^", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "<" => arguments.Length == 1 ? evaluator.LessThan(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry("<", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    ">" => arguments.Length == 1 ? evaluator.GreaterThan(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry(">", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "=" => arguments.Length == 1 ? K3Value.Equals(arguments[0], new IntegerValue(0)) ? new IntegerValue(1) : new IntegerValue(0) : evaluator.EvaluateDyadicOperatorWithRegistry("=", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "!" => arguments.Length == 1 ? evaluator.Match(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry("!", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "&" => arguments.Length == 1 ? evaluator.Where(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("&", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "|" => arguments.Length == 1 ? evaluator.Reverse(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("|", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "~" => arguments.Length == 1 ? evaluator.Match(arguments[0], new IntegerValue(0)) : evaluator.EvaluateDyadicOperatorWithRegistry("~", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "," => arguments.Length == 1 ? evaluator.Enlist(arguments[0]) : evaluator.Join(arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "." => evaluator.DotApply(arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "@" => evaluator.AtIndex(arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "#" => arguments.Length == 1 ? evaluator.Count(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("#", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "_" => arguments.Length == 1 ? evaluator.Floor(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("_", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "?" => arguments.Length == 1 ? evaluator.Unique(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("?", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    "$" => arguments.Length == 1 ? evaluator.Format(arguments[0]) : evaluator.EvaluateDyadicOperatorWithRegistry("$", arguments[0], arguments[1] ?? throw new ArgumentNullException(nameof(arguments))),
                    _ => throw new Exception($"Unknown verb: {verb}")
                };
            }

            public K3Value HandleTwoArgumentAdverb(VerbWithAdverbs verbWithAdverbs, K3Value argument)
            {
                // For 2-argument adverb structures, handle over/scan/each adverbs correctly
                if (verbWithAdverbs.Adverbs.Contains("over"))
                {
                    // Over adverb (/) - use existing implementation
                    return evaluator.ApplyAdverbSlash(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("scan"))
                {
                    // Scan adverb (\) - use existing implementation
                    return evaluator.ApplyAdverbBackslash(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each"))
                {
                    // Each adverb (') - use existing implementation
                    return evaluator.HandleAdverbTick(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each-right"))
                {
                    // Each-right adverb (/:) - use existing implementation
                    return evaluator.ApplyAdverbSlashColon(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each-left"))
                {
                    // Each-left adverb (\:) - use existing implementation
                    return evaluator.ApplyAdverbBackslashColon(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                else if (verbWithAdverbs.Adverbs.Contains("each-prior"))
                {
                    // Each-prior adverb (':) - use existing implementation
                    return evaluator.ApplyAdverbTickColon(CreateVerbValue(verbWithAdverbs.BaseVerb), new IntegerValue(0), argument);
                }
                
                // Fallback to treating it as a single argument adverb
                return EvaluateVerbWithAdverbs(verbWithAdverbs, argument);
            }
            
            private K3Value CreateVerbValue(string verbSymbol)
            {
                // Create a verb value from the verb symbol
                return verbSymbol switch
                {
                    "+" => new SymbolValue("+"),
                    "-" => new SymbolValue("-"),
                    "*" => new SymbolValue("*"),
                    "%" => new SymbolValue("%"),
                    "^" => new SymbolValue("^"),
                    "<" => new SymbolValue("<"),
                    ">" => new SymbolValue(">"),
                    "=" => new SymbolValue("="),
                    "!" => new SymbolValue("!"),
                    "&" => new SymbolValue("&"),
                    "|" => new SymbolValue("|"),
                    "~" => new SymbolValue("~"),
                    "," => new SymbolValue(","),
                    "." => new SymbolValue("."),
                    "@" => new SymbolValue("@"),
                    "#" => new SymbolValue("#"),
                    "_" => new SymbolValue("_"),
                    "?" => new SymbolValue("?"),
                    "$" => new SymbolValue("$"),
                    _ => throw new Exception($"Unknown verb symbol: {verbSymbol}")
                };
            }

            private K3Value ApplyAdverb(string adverb, K3Value verbResult, K3Value[] originalArguments)
            {
                return adverb switch
                {
                    "over" => ApplyOverAdverb(verbResult, originalArguments),
                    "scan" => ApplyScanAdverb(verbResult, originalArguments),
                    "each" => ApplyEachAdverb(verbResult, originalArguments),
                    "each-right" => ApplyEachRightAdverb(verbResult, originalArguments),
                    "each-left" => ApplyEachLeftAdverb(verbResult, originalArguments),
                    "each-prior" => ApplyEachPriorAdverb(verbResult, originalArguments),
                    _ => throw new Exception($"Unknown adverb: {adverb}")
                };
            }

            private K3Value ApplyOverAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Over adverb (/) - reduce/fold operation
                if (verbResult is VectorValue vector)
                {
                    if (vector.Elements.Count == 0)
                    {
                        // Return identity element based on the base verb
                        return GetIdentityElementForVerb(currentVerb);
                    }
                    else if (vector.Elements.Count == 1)
                    {
                        return vector.Elements[0]; // Single element, return as-is
                    }
                    else
                    {
                        // Reduce operation: apply verb cumulatively
                        K3Value result = vector.Elements[0];
                        for (int i = 1; i < vector.Elements.Count; i++)
                        {
                            result = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { result, vector.Elements[i] });
                        }
                        return result;
                    }
                }
                return verbResult;
            }
            
            private K3Value ApplyOverAdverbWithInit(K3Value initialization, K3Value verbResult, K3Value[] originalArguments)
            {
                // Over adverb (/) with initialization - reduce/fold operation with provided init
                if (verbResult is VectorValue vector)
                {
                    if (vector.Elements.Count == 0)
                    {
                        return initialization; // Empty vector, return initialization
                    }
                    else
                    {
                        // Check if the verb is monadic (has disambiguating colon suffix)
                        string verb = GetVerbFromContext(originalArguments);
                        
                        // Check verb arity from VerbRegistry
                        var verbInfo = VerbRegistry.GetVerb(verb);
                        bool isMonadicOnly = verbInfo != null && verbInfo.SupportedArities.Length == 1 && verbInfo.SupportedArities[0] == 1;
                        
                        // Also check for colon suffix which forces monadic interpretation
                        bool hasDisambiguatingColon = verb.EndsWith(":");
                        
                        if (isMonadicOnly || hasDisambiguatingColon)
                        {
                            // For monadic verbs with initialization, ignore initialization and apply monadic verb to the vector
                            // This handles cases like 1 +:/x where 1 is a conditional selector, not initialization
                            return evaluator.ApplyMonadicVerb(verb, verbResult);
                        }
                        else
                        {
                            // Reduce operation: start with initialization, apply verb cumulatively
                            K3Value result = initialization;
                            foreach (var element in vector.Elements)
                            {
                                result = ApplyBaseVerb(verb, new[] { result, element });
                            }
                            return result;
                        }
                    }
                }
                return verbResult;
            }

            private K3Value ApplyScanAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Scan adverb (\) - cumulative application
                if (verbResult is VectorValue vector)
                {
                    var results = new List<K3Value>();
                    K3Value cumulative = vector.Elements[0];
                    results.Add(cumulative);
                    
                    for (int i = 1; i < vector.Elements.Count; i++)
                    {
                        cumulative = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { cumulative, vector.Elements[i] });
                        results.Add(cumulative);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }
            
            private K3Value ApplyScanAdverbWithInit(K3Value initialization, K3Value verbResult, K3Value[] originalArguments)
            {
                // Scan adverb (\) with initialization - cumulative application with provided init
                if (verbResult is VectorValue vector)
                {
                    var results = new List<K3Value>();
                    K3Value cumulative = initialization;
                    results.Add(cumulative);
                    
                    foreach (var element in vector.Elements)
                    {
                        cumulative = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { cumulative, element });
                        results.Add(cumulative);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private K3Value ApplyEachAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each adverb (') - apply verb element-wise between vectors
                if (originalArguments.Length == 2 && originalArguments[0] is VectorValue leftVec && originalArguments[1] is VectorValue rightVec)
                {
                    // Apply verb element-wise between corresponding elements
                    var results = new List<K3Value>();
                    var minLength = Math.Min(leftVec.Elements.Count, rightVec.Elements.Count);
                    
                    for (int i = 0; i < minLength; i++)
                    {
                        var result = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { leftVec.Elements[i], rightVec.Elements[i] });
                        results.Add(result);
                    }
                    return new VectorValue(results);
                }
                else if (verbResult is VectorValue vector)
                {
                    // Fallback for single vector case
                    var results = new List<K3Value>();
                    foreach (var element in vector.Elements)
                    {
                        // For each, apply the verb with the same arity as original
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), 
                            originalArguments.Length == 1 ? new[] { element } : new[] { element, originalArguments[1] });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private K3Value ApplyEachRightAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each-right adverb (/:) - apply verb with right argument to each element of left
                if (originalArguments.Length >= 2 && originalArguments[0] is VectorValue leftVector)
                {
                    var results = new List<K3Value>();
                    foreach (var element in leftVector.Elements)
                    {
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { element, originalArguments[1] });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private K3Value ApplyEachLeftAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each-left adverb (\:) - apply verb with left argument to each element of right
                if (originalArguments.Length >= 2 && originalArguments[1] is VectorValue rightVector)
                {
                    var results = new List<K3Value>();
                    foreach (var element in rightVector.Elements)
                    {
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { originalArguments[0], element });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private K3Value ApplyEachPriorAdverb(K3Value verbResult, K3Value[] originalArguments)
            {
                // Each-prior adverb (':) - apply verb with previous element
                if (verbResult is VectorValue vector)
                {
                    var results = new List<K3Value>();
                    results.Add(vector.Elements[0]); // First element stays the same
                    
                    for (int i = 1; i < vector.Elements.Count; i++)
                    {
                        var singleResult = ApplyBaseVerb(GetVerbFromContext(originalArguments), new[] { vector.Elements[i], vector.Elements[i-1] });
                        results.Add(singleResult);
                    }
                    return new VectorValue(results);
                }
                return verbResult;
            }

            private string GetVerbFromContext(K3Value[] arguments)
            {
                // Return the current verb context being tracked
                return currentVerb;
            }
            
            private K3Value GetIdentityElementForVerb(string verb)
            {
                // Return the appropriate identity element for the given verb
                return verb switch
                {
                    "*" => new IntegerValue(1),        // Multiplication identity
                    "+" => new IntegerValue(0),        // Addition identity
                    "&" => new IntegerValue(int.MaxValue), // Min identity (for now, use max int)
                    "|" => new IntegerValue(int.MinValue), // Max identity (for now, use min int)
                    "^" => new IntegerValue(1),        // Power identity
                    "%" => new IntegerValue(1),        // Divide identity
                    "-" => new IntegerValue(0),        // Subtract identity (monadic case)
                    _ => new IntegerValue(0)           // Default identity
                };
            }
        }
    }

    // Custom comparer for K3Value to use in HashSet operations
    public class K3ValueComparer : IEqualityComparer<K3Value>
    {
        public bool Equals(K3Value? x, K3Value? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            
            // Use Match function for comparison (same as _in uses)
            var evaluator = new Evaluator();
            var matchResult = evaluator.Match(x, y);
            
            if (matchResult is IntegerValue intVal)
            {
                return intVal.Value == 1;
            }
            
            return false;
        }

        public int GetHashCode(K3Value obj)
        {
            if (obj is null) return 0;
            
            // Generate hash code based on type and value
            return obj.Type switch
            {
                ValueType.Integer => obj is IntegerValue iv ? iv.Value.GetHashCode() : 0,
                ValueType.Long => obj is LongValue lv ? lv.Value.GetHashCode() : 0,
                ValueType.Float => obj is FloatValue fv ? fv.Value.GetHashCode() : 0,
                ValueType.Character => obj is CharacterValue cv ? cv.Value.GetHashCode() : 0,
                ValueType.Symbol => obj is SymbolValue sv ? sv.Value.GetHashCode() : 0,
                ValueType.Vector => obj is VectorValue vv ? vv.Elements.Count.GetHashCode() : 0,
                _ => obj.ToString().GetHashCode()
            };
        }
    }

    /// <summary>
    /// Represents a verb with its attached adverbs for proper evaluation
    /// </summary>
    public class VerbWithAdverbs
    {
        public string BaseVerb { get; }
        public List<string> Adverbs { get; }
        public int Position { get; }

        public VerbWithAdverbs(string baseVerb, List<string> adverbs, int position = -1)
        {
            BaseVerb = baseVerb;
            Adverbs = adverbs ?? new List<string>();
            Position = position;
        }

        /// <summary>
        /// Get the effective arity of the verb with adverbs applied
        /// </summary>
        public int GetEffectiveArity()
        {
            // Base arity depends on the verb
            int baseArity = GetBaseVerbArity(BaseVerb);
            
            // Apply adverb arity modifications
            foreach (var adverb in Adverbs)
            {
                baseArity = ApplyAdverbArityModification(baseArity, adverb);
            }
            
            return baseArity;
        }

        private int GetBaseVerbArity(string verb)
        {
            // Determine base verb arity using VerbRegistry
            var verbInfo = VerbRegistry.GetVerb(verb);
            if (verbInfo != null && verbInfo.SupportedArities.Length > 0)
            {
                // Return the minimum supported arity as the base arity
                return verbInfo.SupportedArities.Min();
            }
            return 1; // Default to monadic
        }

        private int ApplyAdverbArityModification(int currentArity, string adverb)
        {
            return adverb switch
            {
                "/" => currentArity, // Over: same arity
                "\\" => currentArity, // Scan: same arity  
                "'" => currentArity, // Each: same arity
                "/:" => Math.Max(currentArity, 2), // Each-right: at least dyadic
                "\\:" => Math.Max(currentArity, 2), // Each-left: at least dyadic
                "':" => Math.Max(currentArity, 2), // Each-prior: at least dyadic
                _ => currentArity
            };
        }
    }

    /// <summary>
    /// Parser for extracting verbs and their attached adverbs from AST nodes
    /// </summary>
    public class VerbAdverbParser
    {
        /// <summary>
        /// Parse a verb with adverbs from an AST node
        /// </summary>
        /// <param name="node">AST node to parse</param>
        /// <returns>VerbWithAdverbs object or null if not a verb with adverbs</returns>
        public static VerbWithAdverbs? ParseVerbWithAdverbs(ASTNode node)
        {
            // Handle simple literal verbs (like +, -, *, etc.)
            if (node.Type == ASTNodeType.Literal && node.Value is SymbolValue symbolValue)
            {
                var verbSymbol = symbolValue.Value.ToString();
                if (IsVerb(verbSymbol))
                {
                    return new VerbWithAdverbs(verbSymbol, new List<string>(), node.StartPosition);
                }
                return null;
            }
            
            if (node.Type != ASTNodeType.DyadicOp || node.Value == null)
                return null;

            var opSymbol = node.Value.ToString();
            
            // Check if this is an adverb
            if (IsAdverb(opSymbol))
            {
                // Parse the left side to find the base verb and any nested adverbs
                var leftResult = ParseVerbWithAdverbs(node.Children[0]);
                if (leftResult != null)
                {
                    // Add this adverb to the list
                    var adverbs = new List<string>(leftResult.Adverbs) { opSymbol };
                    return new VerbWithAdverbs(leftResult.BaseVerb, adverbs, node.StartPosition);
                }
                else if (node.Children[0].Type == ASTNodeType.Literal && node.Children[0].Value is SymbolValue leftSymbolValue)
                {
                    // Base verb found
                    var baseVerb = leftSymbolValue.Value.ToString();
                    return new VerbWithAdverbs(baseVerb, new List<string> { opSymbol }, node.StartPosition);
                }
            }
            
            // Check if this is a base verb
            if (IsVerb(opSymbol))
            {
                return new VerbWithAdverbs(opSymbol, new List<string>(), node.StartPosition);
            }

            return null;
        }

        private static bool IsAdverb(string symbol)
        {
            return symbol == "/" || symbol == "\\" || symbol == "'" || 
                   symbol == "/:" || symbol == "\\:" || symbol == "':";
        }

        private static bool IsVerb(string symbol)
        {
            // Check if this is a known verb symbol
            return VerbRegistry.IsVerb(symbol);
        }
    }
}
