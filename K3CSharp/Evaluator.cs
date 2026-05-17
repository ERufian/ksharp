// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using K3CSharp.Parsing;

namespace K3CSharp
{
    public partial class Evaluator : IEvaluatorContext
    {
        private readonly Dictionary<string, K3Value> globalVariables = new Dictionary<string, K3Value>();
        private readonly Dictionary<string, K3Value> localVariables = new Dictionary<string, K3Value>();
        private readonly Dictionary<string, int> symbolTable = new Dictionary<string, int>();
        public bool isInFunctionCall = false; // Track if we're evaluating a function call
        public static int floatPrecision = 7; // Default precision for floating point display

        // Stack depth tracking to prevent unrecoverable StackOverflowException
        private int callDepth = 0;
        private const int MaxCallDepth = 40;

        // Script name (without extension) for _v, and command-line args for _i
        public string ScriptName { get; set; } = "";
        public List<string> CommandLineArgs { get; set; } = new List<string>();

        // K Tree for global namespace management
        private KTree kTree = new KTree();

        // Dependency and trigger tracking
        private long _globalChangeCounter = 1;
        private readonly Dictionary<string, long> _variableChangeVersion = new();
        private readonly Dictionary<string, long> _dependencyLastVersion = new();
        private readonly HashSet<string> _evaluatingDependencies = new();
        private readonly HashSet<string> _executingTriggers = new();

        // Reference to the current function being executed (for AST caching)
        public FunctionValue? currentFunctionValue = null;

        // Reference to parent evaluator for global scope access
        private Evaluator? parentEvaluator = null;

        // Track whether current assignment is intermediate (used by another operator) or terminal
        private bool isIntermediateAssignment = false;

        // Adverb-aware evaluator for enhanced verb/adverb handling
        private readonly AdverbAwareEvaluator adverbAwareEvaluator;

        // Extracted handler classes
        internal readonly MathHandler mathHandler;
        internal readonly FormatHandler formatHandler;
        internal readonly RandHandler randHandler;
        internal readonly TimeHandler timeHandler;
        internal readonly VarsHandler varsHandler;
        internal readonly FunctionInverseHandler functionInverseHandler;
        internal readonly ListHandler listHandler;
        internal readonly HashgroupHandler hashgroupHandler;

        /// <summary>
        /// Constructor for Evaluator
        /// </summary>
        public Evaluator()
        {
            adverbAwareEvaluator = new AdverbAwareEvaluator(this);
            mathHandler = new MathHandler(this);
            formatHandler = new FormatHandler(this);
            randHandler = new RandHandler();
            timeHandler = new TimeHandler(this);
            varsHandler = new VarsHandler(this);
            functionInverseHandler = new FunctionInverseHandler(this);
            listHandler = new ListHandler(this);
            hashgroupHandler = new HashgroupHandler(this);
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
            mathHandler = new MathHandler(this);
            formatHandler = new FormatHandler(this);
            randHandler = new RandHandler();
            timeHandler = new TimeHandler(this);
            varsHandler = new VarsHandler(this);
            functionInverseHandler = new FunctionInverseHandler(this);
            listHandler = new ListHandler(this);
            hashgroupHandler = new HashgroupHandler(this);
            // Inherit stack depth from parent to track recursion
            callDepth = (parent?.callDepth ?? -1) + 1;
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
            RandHandler.RandomSeed = -314159;
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
                "_d" => varsHandler.DirFunction(new NullValue()),
                "_n" => varsHandler.NullFunction(new NullValue()),
                "_t" => timeHandler.TimeFunction(new NullValue()),
                "_T" => timeHandler.TFunction(new NullValue()),
                "_v" => varsHandler.VarFunction(new NullValue()),
                "_i" => varsHandler.IndexFunction(new NullValue()),
                "_f" => varsHandler.FunctionFunction(new NullValue()),
                "_s" => varsHandler.SpaceFunction(new NullValue()),
                "_h" => varsHandler.HostFunction(new NullValue()),
                "_p" => varsHandler.PortFunction(new NullValue()),
                "_P" => varsHandler.ProcessIdFunction(new NullValue()),
                "_w" => varsHandler.WhoFunction(new NullValue()),
                "_u" => varsHandler.UserFunction(new NullValue()),
                "_a" => varsHandler.AddressFunction(new NullValue()),
                "_k" => varsHandler.VersionFunction(new NullValue()),
                "_o" => varsHandler.OsFunction(new NullValue()),
                "_c" => varsHandler.CoresFunction(new NullValue()),
                "_r" => varsHandler.RamFunction(new NullValue()),
                "_m" => varsHandler.MachineIdFunction(new NullValue()),
                "_y" => varsHandler.StackFunction(new NullValue()),
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
                        return varsHandler.FunctionFunction(new NullValue());
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
                    
                    // Implicit iteration for monadic atomic verbs on vectors
                    var monoVerbInfo = VerbRegistry.GetVerb(verbSymbol.Value);
                    if (monoVerbInfo != null && monoVerbInfo.IsMonadicAtomic && operand is VectorValue monoVec)
                    {
                        var monoResults = new List<K3Value>();
                        foreach (var elem in monoVec.Elements)
                        {
                            var childNode = new ASTNode(ASTNodeType.MonadicOp);
                            childNode.Value = verbSymbol;
                            var literalNode = new ASTNode(ASTNodeType.Literal);
                            literalNode.Value = elem;
                            childNode.Children.Add(literalNode);
                            monoResults.Add(Evaluate(childNode));
                        }
                        return new VectorValue(monoResults, DetermineVectorType(monoResults));
                    }

                    // Dispatch via centralized monadic verb table
                    var monoResult = DispatchMonadic(verbSymbol.Value, operand);
                    if (monoResult == null)
                        throw new Exception($"Unknown monadic operator: {verbSymbol.Value}");
                    return monoResult;

                case ASTNodeType.TetradicOp:
                    return EvaluateTetradicOp(node);

                case ASTNodeType.VariadicOp:
                    return EvaluateVariadicOp(node);

                case ASTNodeType.Adnoun:
                    return EvaluateAdnoun(node);

                case ASTNodeType.NotImplemented:
                    var message = node.Value is CharacterValue charVal ? charVal.Value : node.Value?.ToString() ?? "Not implemented";
                    throw new Exception($"Not yet implemented: {message}");

                default:
                    throw new Exception($"Unknown AST node type: {node.Type}");
            }
        }

        private bool IsBuiltInOperator(string operatorName)
        {
            return operatorName == ":" || MonadicDispatch.ContainsKey(operatorName) || DyadicDispatch.ContainsKey(operatorName);
        }

        private static bool IsColon(K3Value value)
        {
            // Check if the value represents a colon (:)
            return value is SymbolValue symbol && symbol.Value == ":";
        }
        
        private bool TryParseAttributeAccess(string name, out string varPath, out string attrName)
        {
            varPath = "";
            attrName = "";
            if (!name.Contains(".."))
                return false;
            var parts = name.Split(new[] { ".." }, StringSplitOptions.None);
            if (parts.Length != 2)
                return false;
            var baseName = parts[0];
            attrName = parts[1];
            if (string.IsNullOrEmpty(baseName) || string.IsNullOrEmpty(attrName))
                return false;
            var branch = kTree.CurrentBranch?.Value ?? "";
            varPath = baseName.StartsWith(".") ? baseName : (string.IsNullOrEmpty(branch) ? baseName : branch + "." + baseName);
            return true;
        }

        private K3Value? GetAttributeValue(string variableName)
        {
            if (!TryParseAttributeAccess(variableName, out var varPath, out var attrName))
                return null;
            var attrDict = kTree.GetAttribute(varPath);
            if (attrDict == null)
                return new DictionaryValue();
            if (attrDict.Entries.TryGetValue(new SymbolValue(attrName), out var entry))
                return entry.Value;
            return new NullValue();
        }

        private bool SetAttributeValue(string variableName, K3Value value)
        {
            if (!TryParseAttributeAccess(variableName, out var varPath, out var attrName))
                return false;
            var (dict, key) = kTree.ResolvePath(varPath);
            if (dict == null || key == null)
                return false;
            bool existed = dict.Entries.TryGetValue(key, out var entry);
            if (!existed)
            {
                // Create the variable if it doesn't exist
                dict.Entries[key] = (new NullValue(), null!);
                entry = dict.Entries[key];
            }
            var attrDict = entry.Attribute ?? new DictionaryValue();
            attrDict.Entries[new SymbolValue(attrName)] = (value, null!);
            dict.Entries[key] = (entry.Value, attrDict);
            return true;
        }

        private K3Value? GetVariableValue(string variableName)
        {
            // Handle attribute access: v..d
            if (variableName.Contains(".."))
            {
                var attrValue = GetAttributeValue(variableName);
                if (attrValue != null)
                    return attrValue;
            }

            K3Value? kTreeValue;
            // Check if this is an absolute path (starts with dot)
            if (variableName.StartsWith("."))
            {
                // Absolute path - look up directly from root
                kTreeValue = kTree.GetValue(variableName);
                if (kTreeValue != null)
                    return ResolveDependency(variableName, kTreeValue);
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
                return ResolveDependency(relativePath, kTreeValue);
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

            // Handle attribute access: v..d
            if (variableName.Contains(".."))
            {
                var attrValue = GetAttributeValue(variableName);
                if (attrValue != null)
                    return attrValue;
            }

            // Check if this is a K tree dotted notation variable
            if (variableName.Contains('.'))
            {
                var kTreeValue = kTree.GetValue(variableName);
                if (kTreeValue != null)
                {
                    return ResolveDependency(variableName, kTreeValue);
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
                        return ResolveDependency(relativePath, kTreeValue);
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
                return varsHandler.FunctionFunction(new NullValue());
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
            // Handle attribute assignment: v..d
            if (variableName.Contains(".."))
            {
                if (SetAttributeValue(variableName, value))
                    return value;
            }

            // Check if this is a K tree dotted notation variable
            if (variableName.Contains('.'))
            {
                if (kTree.SetValue(variableName, value))
                {
                    _variableChangeVersion[variableName] = _globalChangeCounter++;
                    FireTriggerIfNeeded(variableName);
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
                _variableChangeVersion[path] = _globalChangeCounter++;
                FireTriggerIfNeeded(path);
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
                _variableChangeVersion[path] = _globalChangeCounter++;
                FireTriggerIfNeeded(path);

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
            
            // Handle monadic-only verbs that appear in dyadic context (use only left arg)
            if (opName == "_ci")
                return listHandler.Ci(left);
            if (opName == "_ic")
                return listHandler.Ic(left);

            // Look up in the centralized dyadic dispatch table
            if (DyadicDispatch.TryGetValue(opName, out var dyadicOp))
            {
                // Check for atomic function - apply implicit iteration if applicable
                var verbInfo = VerbRegistry.GetVerb(opName);
                if (verbInfo != null && (verbInfo.IsDyadicAtomic || verbInfo.IsLeftAtomic))
                {
                    K3Value? result = null;
                    bool bothAreVectors = left is VectorValue && right is VectorValue;
                    
                    if (verbInfo.IsDyadicAtomic)
                    {
                        // Both-atomic: when both args are vectors, element-wise has precedence
                        if (bothAreVectors)
                        {
                            result = ApplyImplicitIterationBoth(left, right, dyadicOp);
                        }
                        if (result == null)
                        {
                            // Try left-atomic (scalar left, vector right → iterate right)
                            result = ApplyImplicitIterationRight(left, right, dyadicOp);
                        }
                        if (result == null)
                        {
                            // Try right-atomic (vector left, scalar right → iterate left)
                            result = ApplyImplicitIterationLeft(left, right, dyadicOp);
                        }
                    }
                    else if (verbInfo.IsRightAtomic)
                    {
                        result = ApplyImplicitIterationRight(left, right, dyadicOp);
                    }
                    else if (verbInfo.IsLeftAtomic)
                    {
                        result = ApplyImplicitIterationLeft(left, right, dyadicOp);
                    }
                    
                    // If implicit iteration succeeded, return it
                    if (result != null)
                    {
                        return result;
                    }
                }
                
                return dyadicOp(left, right);
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
                
                // Implicit iteration for monadic atomic verbs on vectors
                var dyadicMonoVerbInfo = VerbRegistry.GetVerb(op.Value);
                if (dyadicMonoVerbInfo != null && dyadicMonoVerbInfo.IsMonadicAtomic && operand is VectorValue dyadicMonoVec)
                {
                    var dyadicMonoResults = new List<K3Value>();
                    foreach (var elem in dyadicMonoVec.Elements)
                    {
                        var childNode = new ASTNode(ASTNodeType.MonadicOp);
                        childNode.Value = new SymbolValue(op.Value);
                        var literalNode = new ASTNode(ASTNodeType.Literal);
                        literalNode.Value = elem;
                        childNode.Children.Add(literalNode);
                        dyadicMonoResults.Add(Evaluate(childNode));
                    }
                    return new VectorValue(dyadicMonoResults, DetermineVectorType(dyadicMonoResults));
                }

                // Special case: "." in DyadicOp context uses DotApply (amend/apply semantics)
                if (op.Value == ".")
                    return DotApply(new NullValue(), operand);

                // Dispatch via centralized monadic verb table
                var dyadicMonoResult = DispatchMonadic(op.Value, operand);
                if (dyadicMonoResult != null)
                    return dyadicMonoResult;
                throw new Exception($"Unknown monadic operator: {op.Value}");
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
                
                // Verb composition: (f/ g) where g is a verb creates a composed dyadic function
                // Per spec: "Each glyph represents its monadic interpretation except the rightmost one,
                // which represents its dyadic interpretation."
                // E.g., (+/*)[x;y] = +/(x * y), (*/^)[x;y] = */(x ^ y)
                if (arg2val is SymbolValue composedInnerVerb && VerbRegistry.HasVerb(composedInnerVerb.Value))
                {
                    var adverbName = op.Value.ToString();
                    // Create a FunctionValue that applies inner verb dyadically, then outer adverb+verb monadically
                    string outerVerbStr = verbValue2 is SymbolValue sv ? sv.Value : verbValue2?.ToString() ?? "";
                    string innerVerbStr = composedInnerVerb.Value;
                    string bodyText = $"{outerVerbStr}{GetAdverbGlyph(adverbName)}(x{innerVerbStr}y)";
                    string originalSource = $"{outerVerbStr}{GetAdverbGlyph(adverbName)}{innerVerbStr}";
                    return new FunctionValue(bodyText, new List<string> { "x", "y" }, originalSourceText: originalSource);
                }
                
                var monadicLeft2 = new NullValue();
                return op.Value.ToString() switch
                {
                    "over" => ApplyAdverbSlash(verbValue2!, monadicLeft2, arg2val!),
                    "scan" => ApplyAdverbBackslash(verbValue2!, monadicLeft2, arg2val!),
                    "each" => HandleAdverbTick(verbValue2!, monadicLeft2, arg2val!),
                    "each-right" => ApplyAdverbSlashColon(verbValue2!, monadicLeft2, arg2val!),
                    "each-left" => ApplyAdverbBackslashColon(verbValue2!, monadicLeft2, arg2val!),
                    "each-prior" => ApplyAdverbTickColon(verbValue2!, monadicLeft2, arg2val!),
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
                
                // Handle 2-child adverb structures (e.g., each-prior, over, scan)
                // These are created by the parser for noun-bound adverbs and modified verbs
                if (op.Value.ToString() == "each" || op.Value.ToString() == "over" || op.Value.ToString() == "scan" ||
                    op.Value.ToString() == "each-right" || op.Value.ToString() == "each-left" || op.Value.ToString() == "each-prior" ||
                    op.Value.ToString() == "/" || op.Value.ToString() == "\\" || op.Value.ToString() == "'" ||
                    op.Value.ToString() == "/:" || op.Value.ToString() == "\\:" || op.Value.ToString() == "':")
                {
                    var verbNode = node.Children[0];
                    var argument = Evaluate(node.Children[1]);
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
                    else if (op.Value.ToString() == "." && node.Children[1].Type == ASTNodeType.ExpressionList)
                    {
                        // For multi-dimensional indexing x[0;0;0] parsed as DOT_APPLY with ExpressionList
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
                     op.Value.ToString() == "each-right" || op.Value.ToString() == "each-left" || op.Value.ToString() == "each-prior" ||
                     op.Value.ToString() == "/" || op.Value.ToString() == "\\" || op.Value.ToString() == "'" ||
                     op.Value.ToString() == "/:" || op.Value.ToString() == "\\:" || op.Value.ToString() == "':"))
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
                
                // Verb composition: if right is a verb and left is sentinel, create composed function
                // Per spec: "Each glyph represents its monadic interpretation except the rightmost one,
                // which represents its dyadic interpretation."
                // E.g., (+/*)[x;y] = +/(x * y), (*/^)[x;y] = */(x ^ y)
                if (safeLeft is NullValue && safeRight is SymbolValue composedInnerVerb3 && VerbRegistry.HasVerb(composedInnerVerb3.Value))
                {
                    var adverbName3 = op.Value.ToString();
                    var verbValue3 = Evaluate(verbNode);
                    string outerVerbStr3 = verbValue3 is SymbolValue sv3 ? sv3.Value : verbValue3?.ToString() ?? "";
                    string innerVerbStr3 = composedInnerVerb3.Value;
                    string bodyText3 = $"{outerVerbStr3}{GetAdverbGlyph(adverbName3)}(x{innerVerbStr3}y)";
                    string originalSource3 = $"{outerVerbStr3}{GetAdverbGlyph(adverbName3)}{innerVerbStr3}";
                    return new FunctionValue(bodyText3, new List<string> { "x", "y" }, originalSourceText: originalSource3);
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
        private static string GetAdverbGlyph(string adverbName)
        {
            return adverbName switch
            {
                "over" => "/",
                "scan" => "\\",
                "each" => "'",
                "each-right" => "/:",
                "each-left" => "\\:",
                "each-prior" => "':",
                _ => "/"
            };
        }

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
            K3Value? sysVarResult = null;
            try
            {
                sysVarResult = GetSystemVariable(functionName);
            }
            catch (Exception ex) when (ex.Message.StartsWith("Not a system variable"))
            {
                // Not a system variable, continue with regular function evaluation
            }

            if (sysVarResult != null)
            {
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
            
            // Handle dyadic system functions called via bracket notation (e.g., _lsq[y;x])
            // These are registered in VerbRegistry but dispatched through EvaluateDyadicOperatorWithRegistry
            if (arguments.Count == 2)
            {
                var verbForBracket = VerbRegistry.GetVerb(functionName);
                if (verbForBracket != null && verbForBracket.Type == VerbType.SystemFunction &&
                    verbForBracket.SupportedArities.Contains(2))
                {
                    return EvaluateDyadicOperatorWithRegistry(functionName, arguments[0], arguments[1]);
                }
            }
            
            // Check if it's a built-in function first
            switch (functionName)
            {
                case "?":
                    // Monadic: unique; Dyadic: find or function inverse; Triadic: function inverse with guess
                    if (arguments.Count == 1)
                        return hashgroupHandler.Unique(arguments[0]);
                    else if (arguments.Count == 2)
                        return Find(arguments[0], arguments[1]);
                    else if (arguments.Count == 3)
                        return functionInverseHandler.FunctionInverse(arguments[0], arguments[1], arguments[2]);
                    throw new Exception("? operator requires 1-3 arguments");
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
                    return timeHandler.TimeFunction(new NullValue());
                case "_d":
                    return timeHandler.DirectoryFunction(new NullValue());
                case "_getenv":
                    return listHandler.GetenvFunction(arguments.Count > 0 ? arguments[0] : new NullValue());
                case "_size":
                    return listHandler.SizeFunction(arguments.Count > 0 ? arguments[0] : new NullValue());
                case "_host":
                    return listHandler.HostDnsFunction(arguments.Count > 0 ? arguments[0] : new NullValue());
                case "_gethint":
                    return GetHintFunction(arguments);
                case "_sethint":
                    return SetHintFunction(arguments);
                case "_dispose":
                    return DisposeFunction(arguments);
                case "_unmarshall":
                    return UnmarshallFunction(arguments);
                case "_exit":
                    return listHandler.ExitFunction(arguments.Count > 0 ? arguments[0] : new NullValue());
                case "_ssr":
                    if (arguments.Count == 3) return listHandler.SsrFunction(arguments[0], arguments[1], arguments[2]);
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
                    if (arguments.Count == 1) return hashgroupHandler.Group(arguments[0]);
                    throw new Exception("= operator requires 1 or 2 arguments");
                case ",":
                    if (arguments.Count >= 2) return Join(arguments[0], arguments[1]);
                    if (arguments.Count == 1) return Enlist(arguments[0]);
                    throw new Exception(", operator requires 1 or 2 arguments");
                case "#":
                    if (arguments.Count >= 2) return Take(arguments[0], arguments[1]);
                    if (arguments.Count == 1) return Count(arguments[0]);
                    throw new Exception("# operator requires 1 or 2 arguments");
                // Mathematical functions
                case "_abs":
                    if (arguments.Count == 1) return mathHandler.MathAbs(arguments[0]);
                    throw new Exception("_abs requires 1 argument");
                case "_sqr":
                    if (arguments.Count == 1) return mathHandler.MathSqr(arguments[0]);
                    throw new Exception("_sqr requires 1 argument");
                case "_sqrt":
                    if (arguments.Count == 1) return mathHandler.MathSqrt(arguments[0]);
                    throw new Exception("_sqrt requires 1 argument");
                case "_floor":
                    if (arguments.Count == 1) return mathHandler.MathFloor(arguments[0]);
                    throw new Exception("_floor requires 1 argument");
                case "_sin":
                    if (arguments.Count == 1) return mathHandler.MathSin(arguments[0]);
                    throw new Exception("_sin requires 1 argument");
                case "_cos":
                    if (arguments.Count == 1) return mathHandler.MathCos(arguments[0]);
                    throw new Exception("_cos requires 1 argument");
                case "_tan":
                    if (arguments.Count == 1) return mathHandler.MathTan(arguments[0]);
                    throw new Exception("_tan requires 1 argument");
                case "_asin":
                    if (arguments.Count == 1) return mathHandler.MathAsin(arguments[0]);
                    throw new Exception("_asin requires 1 argument");
                case "_acos":
                    if (arguments.Count == 1) return mathHandler.MathAcos(arguments[0]);
                    throw new Exception("_acos requires 1 argument");
                case "_atan":
                    if (arguments.Count == 1) return mathHandler.MathAtan(arguments[0]);
                    throw new Exception("_atan requires 1 argument");
                case "_sinh":
                    if (arguments.Count == 1) return mathHandler.MathSinh(arguments[0]);
                    throw new Exception("_sinh requires 1 argument");
                case "_cosh":
                    if (arguments.Count == 1) return mathHandler.MathCosh(arguments[0]);
                    throw new Exception("_cosh requires 1 argument");
                case "_tanh":
                    if (arguments.Count == 1) return mathHandler.MathTanh(arguments[0]);
                    throw new Exception("_tanh requires 1 argument");
                case "_log":
                    if (arguments.Count == 1) return mathHandler.MathLog(arguments[0]);
                    throw new Exception("_log requires 1 argument");
                case "_exp":
                    if (arguments.Count == 1) return mathHandler.MathExp(arguments[0]);
                    throw new Exception("_exp requires 1 argument");
                // Database functions
                case "_ic":
                    if (arguments.Count == 1) return listHandler.Ic(arguments[0]);
                    throw new Exception("_ic requires 1 argument");
                case "_ci":
                    if (arguments.Count == 1) return listHandler.Ci(arguments[0]);
                    throw new Exception("_ci requires 1 argument");
                case "_sv":
                    if (arguments.Count == 2) return listHandler.Sv(arguments[0], arguments[1]);
                    throw new Exception("_sv requires 2 arguments");
                case "_vs":
                    if (arguments.Count == 2) return listHandler.Vs(arguments[0], arguments[1]);
                    throw new Exception("_vs requires 2 arguments");
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
            else if (functionValue is ProjectedFunctionValue pfv)
            {
                // Handle ProjectedFunctionValue (e.g., count:#: storing monadic count)
                return CallProjectedFunction(pfv, arguments);
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
            // Function Inverse: f ? y — when left is a function, compute inverse
            if (left is ProjectedFunctionValue || left is FunctionValue)
            {
                return functionInverseHandler.FunctionInverse(left, right);
            }

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
                // Find does not promote arguments; comparison tolerance only applies
                // when both element and search value are floats
                for (int i = 0; i < leftVec.Elements.Count; i++)
                {
                    var element = leftVec.Elements[i];
                    bool elementIsFloat = element is FloatValue;
                    bool rightIsFloat = right is FloatValue;
                    
                    // No type promotion: float vs non-float never matches
                    if (elementIsFloat != rightIsFloat)
                        continue;
                    
                    var matchResult = Match(element, right);
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
                // Find does not promote arguments
                bool leftIsFloat = left is FloatValue;
                bool rightIsFloat = right is FloatValue;
                
                if (leftIsFloat == rightIsFloat)
                {
                    var matchResult = Match(left, right);
                    if (matchResult is IntegerValue intVal2 && intVal2.Value == 1)
                    {
                        return new IntegerValue(0); // Found at index 0 (K3 0-based)
                    }
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

            // Scatter selection adnoun: matrix/tensor'[i;j;...]
            // Intercept encoded EACH functions before unpacking arguments
            if (left is FunctionValue func && func.BodyText.StartsWith("EACH:"))
            {
                var innerVerbText = func.BodyText.Substring("EACH:".Length);
                bool innerIsEncodedAdverb = innerVerbText.StartsWith("OVER:") || innerVerbText.StartsWith("SCAN:") ||
                                           innerVerbText.StartsWith("EACH:") || innerVerbText.StartsWith("EACH_RIGHT:") ||
                                           innerVerbText.StartsWith("EACH_LEFT:") || innerVerbText.StartsWith("EACH_PRIOR:");
                K3Value innerVerb;
                if (innerIsEncodedAdverb)
                {
                    innerVerb = new FunctionValue(innerVerbText, new List<string> { "x", "y" });
                }
                else
                {
                    var lexer = new Lexer(innerVerbText);
                    var tokens = lexer.Tokenize();
                    var ast = ParserConfig.ParseWithConfig(tokens, innerVerbText);
                    innerVerb = ast != null ? (Evaluate(ast) ?? new NullValue()) : new SymbolValue(innerVerbText);
                }

                if (innerVerb is VectorValue matrix && right is VectorValue indices && indices.Elements.Count >= 2)
                {
                    return ScatterSelection(matrix, new List<K3Value>(indices.Elements));
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

        private K3Value ExecuteAtContext(string branchPath, string expression)
        {
            // Save current branch, switch to target branch, execute, then restore
            var savedBranch = kTree.CurrentBranch;
            kTree.CurrentBranch = new SymbolValue(branchPath);
            try
            {
                return ExecuteStringExpression(expression);
            }
            finally
            {
                kTree.CurrentBranch = savedBranch;
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
                    "_v" => new SymbolValue(ScriptName), // Script name (without extension)
                    "_i" => CommandLineArgs.Count == 0
                        ? new VectorValue(new List<K3Value>())
                        : new VectorValue(CommandLineArgs.Select(arg =>
                            (K3Value)new VectorValue(arg.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList(), -3)
                        ).ToList()), // Command-line args as list of character vectors
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

        private K3Value EvaluateAdnoun(ASTNode node)
        {
            var adnounType = node.Value is SymbolValue sym ? sym.Value : "";
            if (adnounType == "scatter")
                return EvaluateScatterSelection(node);
            throw new Exception($"Unknown adnoun type: {adnounType}");
        }

        private K3Value EvaluateScatterSelection(ASTNode node)
        {
            if (node.Children.Count < 3)
                throw new Exception("Scatter selection requires a matrix and at least two index arguments");

            var matrixValue = Evaluate(node.Children[0]);
            if (matrixValue == null)
                throw new Exception("Type error: scatter selection target is null");

            var indexValues = new List<K3Value>();
            for (int i = 1; i < node.Children.Count; i++)
            {
                var idx = Evaluate(node.Children[i]);
                if (idx == null)
                    throw new Exception("Type error: scatter selection index is null");
                indexValues.Add(idx);
            }

            return ScatterSelection(matrixValue, indexValues);
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
                return listHandler.SsrFunction(arg1Eval!, arg2Eval!, arg3Eval!);
            }
            else if (opName == "?")
            {
                // Triadic ?: ?[f; y; x] — Function Inverse with initial guess x
                return functionInverseHandler.FunctionInverse(arg1Eval!, arg2Eval!, arg3Eval);
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


        #region Atomic Function Helpers - Implicit Iteration

        /// <summary>
        /// Apply verb with implicit iteration over left argument (left-atomic)
        /// When left is a vector and right is a scalar, apply verb to each left element with right
        /// </summary>
        private K3Value? ApplyImplicitIterationLeft(K3Value left, K3Value right, Func<K3Value, K3Value, K3Value> verbFunc)
        {
            if (left is VectorValue leftVector && leftVector.VectorType != -3 && right is not VectorValue)
            {
                // Iterate over left vector elements
                var results = new List<K3Value>();
                foreach (var element in leftVector.Elements)
                {
                    results.Add(verbFunc(element, right));
                }
                return new VectorValue(results);
            }
            return null; // No iteration needed
        }

        /// <summary>
        /// Apply verb with implicit iteration over right argument (right-atomic)
        /// When right is a vector and left is a scalar, apply verb to left with each right element
        /// </summary>
        private K3Value? ApplyImplicitIterationRight(K3Value left, K3Value right, Func<K3Value, K3Value, K3Value> verbFunc)
        {
            if (right is VectorValue rightVector && rightVector.VectorType != -3 && left is not VectorValue)
            {
                // Iterate over right vector elements
                var results = new List<K3Value>();
                foreach (var element in rightVector.Elements)
                {
                    results.Add(verbFunc(left, element));
                }
                return new VectorValue(results);
            }
            return null; // No iteration needed
        }

        /// <summary>
        /// Apply verb with implicit iteration over both arguments (both-atomic)
        /// When both arguments are vectors of same length, apply verb element-wise
        /// </summary>
        private K3Value? ApplyImplicitIterationBoth(K3Value left, K3Value right, Func<K3Value, K3Value, K3Value> verbFunc)
        {
            if (left is VectorValue leftVector && right is VectorValue rightVector
                && leftVector.VectorType != -3 && rightVector.VectorType != -3)
            {
                // Check if vectors have same length
                if (leftVector.Elements.Count == rightVector.Elements.Count)
                {
                    // Iterate element-wise
                    var results = new List<K3Value>();
                    for (int i = 0; i < leftVector.Elements.Count; i++)
                    {
                        results.Add(verbFunc(leftVector.Elements[i], rightVector.Elements[i]));
                    }
                    return new VectorValue(results);
                }
            }
            return null; // No iteration needed
        }

        #endregion
        }

}