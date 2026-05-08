using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private K3Value AmendFunction(List<K3Value> arguments)
        {
            // Amend operation: .[d; i; f; y] or .[d; i; f]
            // d: data structure to amend (list, dictionary, or atom)
            // i: indices (path into nested structure)
            // f: function to apply
            // y: optional new value
            if (arguments.Count < 3)
            {
                throw new Exception("Amend operation requires at least 3 arguments: data, indices, function");
            }

            var data = arguments[0];
            var indices = arguments[1];
            var function = arguments[2];
            var value = arguments.Count > 3 ? arguments[3] : null;

            // Handle symbol path as first arg: in-place amend of a global variable
            if (data is SymbolValue pathSym && pathSym.Value != ":")
            {
                var resolvedData = GetVariableValuePublic(pathSym.Value);
                if (resolvedData != null)
                {
                    var amendedArgs = new List<K3Value>(arguments) { [0] = resolvedData };
                    var amended = AmendFunction(amendedArgs);
                    SetVariable(pathSym.Value, amended);
                    return pathSym;
                }
            }

            // Check for direct value assignment (third argument is colon)
            if (function is SymbolValue colonSym && colonSym.Value == ":")
            {
                if (arguments.Count == 4)
                {
                    // Tetradic: .[d; i; :; y] - amend with direct value
                    // Replace the value at indices with the new value directly
                    // Convert indices to a path
                    var colonPath = new List<K3Value>();
                    if (indices is VectorValue colonIndexVec)
                    {
                        colonPath.AddRange(colonIndexVec.Elements);
                    }
                    else if (indices is IntegerValue || indices is SymbolValue)
                    {
                        colonPath.Add(indices);
                    }
                    else if (indices is NullValue)
                    {
                        // Empty path - replace the entire value
                        return value ?? data;
                    }

                    return SetValueAtPath(data, colonPath, 0, value ?? data);
                }
                else
                {
                    throw new Exception("Amend with colon requires 4 arguments: .[d; i; :; y]");
                }
            }

            // For dot-amend, indices form a path into nested structures
            // Convert indices to a path (list of index levels)
            var path = new List<K3Value>();
            if (indices is VectorValue indexVec)
            {
                path.AddRange(indexVec.Elements);
            }
            else if (indices is IntegerValue || indices is SymbolValue)
            {
                path.Add(indices);
            }
            else if (indices is NullValue)
            {
                // Empty path - amend the entire value
                return ApplyAmendFunction(data, function, value);
            }

            return AmendAtPath(data, path, 0, function, value);
        }
        
        private K3Value AmendItemFunction(List<K3Value> arguments)
        {
            // Amend Item operation: @[d; i; f; y] or @[d; i; f]
            // d: data structure to amend (list, dictionary, or atom)
            // i: indices to amend (each index is a separate position)
            // f: function to apply (monadic or dyadic)
            // y: optional value for dyadic function

            if (arguments.Count < 3)
            {
                throw new Exception("Amend Item operation requires at least 3 arguments: data, indices, function");
            }

            var data = arguments[0];
            var indices = arguments[1];
            var function = arguments[2];
            var value = arguments.Count > 3 ? arguments[3] : null;

            // Handle symbol path as first arg: in-place amend of a global variable
            if (data is SymbolValue pathSym && pathSym.Value != ":")
            {
                var resolvedData = GetVariableValuePublic(pathSym.Value);
                if (resolvedData != null)
                {
                    var amendedArgs = new List<K3Value>(arguments) { [0] = resolvedData };
                    var amended = AmendItemFunction(amendedArgs);
                    SetVariable(pathSym.Value, amended);
                    return pathSym;
                }
            }

            // Check for error trapping (third argument is colon)
            if (function is SymbolValue colonSym && colonSym.Value == ":")
            {
                if (arguments.Count == 3)
                {
                    // Triadic: @[f; args; :] - trapped apply
                    try
                    {
                        // This is trapped apply, not amend operation
                        // Apply the first argument (function) to the second argument (indices)
                        var result = ApplyTrappedFunction(data, indices);
                        // Return success tuple: (0; result)
                        return new VectorValue(new List<K3Value> { new IntegerValue(0), result });
                    }
                    catch (Exception ex)
                    {
                        // Return error tuple: (1; error_message)
                        var errorMsg = new VectorValue(ex.Message.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList());
                        return new VectorValue(new List<K3Value> { new IntegerValue(1), errorMsg });
                    }
                }
                else if (arguments.Count == 4)
                {
                    // Tetradic: @[d; i; :; y] - amend by directly setting values at indices
                    // The colon is a marker, not a function to call
                    // Replace items at indices with the value directly
                    if (data is VectorValue dataList)
                    {
                        // For @ amend, we set values directly at the specified indices
                        return AmendListWithValue(dataList, indices, value!);
                    }
                    else if (data is DictionaryValue dataDict)
                    {
                        return AmendDictionaryWithValue(dataDict, indices, value!);
                    }
                    else if (data is CharacterValue || data is IntegerValue || data is FloatValue)
                    {
                        // For atomic data with indices, just return the value
                        return value!;
                    }
                    else
                    {
                        throw new Exception("Amend Item with colon not supported for this data type");
                    }
                }
            }

            // For @, indices are used directly (each index = a separate position to amend)
            // Handle different data types
            if (data is VectorValue list)
            {
                return AmendList(list, indices, function, value);
            }
            else if (data is DictionaryValue dict)
            {
                return AmendDictionary(dict, indices, function, value);
            }
            else if (data is CharacterValue || data is IntegerValue || data is FloatValue)
            {
                // Handle atomic data
                if (!(indices is NullValue || (indices is VectorValue idxVec && idxVec.Elements.Count == 0)))
                {
                    throw new Exception("For atomic data, indices must be empty list");
                }
                return AmendAtom(data, function, value);
            }
            else
            {
                throw new Exception("Amend Item operation not supported for this data type");
            }
        }
        
        private K3Value AmendList(VectorValue list, K3Value indices, K3Value function, K3Value? value)
        {
            // Create a copy of the list to modify
            var result = new List<K3Value>(list.Elements);

            // Normalize single integer index to a vector
            if (indices is IntegerValue singleIdx)
            {
                indices = new VectorValue(new List<K3Value> { singleIdx });
            }

            if (indices is VectorValue indexVec)
            {
                for (int i = 0; i < indexVec.Elements.Count; i++)
                {
                    var index = indexVec.Elements[i];
                    if (index is IntegerValue intIndex)
                    {
                        int idx = (int)intIndex.Value;
                        if (idx < 0 || idx >= result.Count)
                        {
                            throw new Exception($"Index {idx} out of bounds for list of length {result.Count}");
                        }

                        // Apply function to current value
                        var currentValue = result[idx];
                        var newValue = ApplyAmendFunction(currentValue, function, value);
                        result[idx] = newValue;
                    }
                    else
                    {
                        throw new Exception("List indices must be integers");
                    }
                }
            }
            else if (indices is NullValue)
            {
                // Amend all items in the list
                for (int i = 0; i < result.Count; i++)
                {
                    var currentValue = result[i];
                    var newValue = ApplyAmendFunction(currentValue, function, value);
                    result[i] = newValue;
                }
            }
            else
            {
                throw new Exception("Indices must be a vector or null");
            }
            
            return new VectorValue(result);
        }
        
        private K3Value AmendDictionary(DictionaryValue dict, K3Value indices, K3Value function, K3Value? value)
        {
            // Create a copy of the dictionary to modify
            var result = new Dictionary<SymbolValue, (K3Value Value, DictionaryValue?)>(dict.Entries);
            
            if (indices is SymbolValue symbol)
            {
                // Single key amendment
                if (result.TryGetValue(symbol, out var current))
                {
                    var currentValue = current.Value;
                    var newValue = ApplyAmendFunction(currentValue, function, value);
                    result[symbol] = (newValue, current.Item2);
                }
                else
                {
                    throw new Exception($"Key '{symbol.Value}' not found in dictionary");
                }
            }
            else if (indices is VectorValue indexVec)
            {
                // Multiple key amendments
                for (int i = 0; i < indexVec.Elements.Count; i++)
                {
                    var index = indexVec.Elements[i];
                    if (index is SymbolValue keySymbol)
                    {
                        if (result.TryGetValue(keySymbol, out var current))
                        {
                            var currentValue = current.Value;
                            var newValue = ApplyAmendFunction(currentValue, function, value);
                            result[keySymbol] = (newValue, current.Item2);
                        }
                        else
                        {
                            throw new Exception($"Key '{keySymbol.Value}' not found in dictionary");
                        }
                    }
                    else
                    {
                        throw new Exception("Dictionary indices must be symbols");
                    }
                }
            }
            else
            {
                throw new Exception("Dictionary indices must be symbols or vector of symbols");
            }
            
            return new DictionaryValue(result);
        }
        
        private K3Value AmendListWithValue(VectorValue list, K3Value indices, K3Value newValue)
        {
            // Create a copy of the list to modify
            var result = new List<K3Value>(list.Elements);

            // Normalize single integer index to a vector
            if (indices is IntegerValue singleIdx)
            {
                indices = new VectorValue(new List<K3Value> { singleIdx });
            }

            if (indices is VectorValue indexVec)
            {
                // When newValue is a vector with the same count as indices, assign element-wise
                // e.g., @[5#0;1 3;:;10 20] assigns 10 to index 1 and 20 to index 3
                bool elementWise = newValue is VectorValue valVec && valVec.Elements.Count == indexVec.Elements.Count;
                var valueElements = elementWise ? ((VectorValue)newValue).Elements : null;
                
                for (int i = 0; i < indexVec.Elements.Count; i++)
                {
                    var index = indexVec.Elements[i];
                    if (index is IntegerValue intIndex)
                    {
                        int idx = (int)intIndex.Value;
                        if (idx < 0 || idx >= result.Count)
                        {
                            throw new Exception($"Index {idx} out of bounds for list of length {result.Count}");
                        }
                        result[idx] = elementWise ? valueElements![i] : newValue;
                    }
                    else
                    {
                        throw new Exception("List indices must be integers");
                    }
                }
            }
            else if (indices is NullValue)
            {
                // Amend all items in the list
                for (int i = 0; i < result.Count; i++)
                {
                    result[i] = newValue;
                }
            }
            else
            {
                throw new Exception("Indices must be a vector or null");
            }
            
            return new VectorValue(result);
        }
        
        private K3Value AmendDictionaryWithValue(DictionaryValue dict, K3Value indices, K3Value newValue)
        {
            // Create a copy of the dictionary to modify
            var result = new Dictionary<SymbolValue, (K3Value Value, DictionaryValue?)>(dict.Entries);
            
            if (indices is SymbolValue symbol)
            {
                // Single key amendment
                if (result.ContainsKey(symbol))
                {
                    var current = result[symbol];
                    result[symbol] = (newValue, current.Item2);
                }
                else
                {
                    throw new Exception($"Key '{symbol.Value}' not found in dictionary");
                }
            }
            else if (indices is VectorValue indexVec)
            {
                // Multiple key amendments
                for (int i = 0; i < indexVec.Elements.Count; i++)
                {
                    var index = indexVec.Elements[i];
                    if (index is SymbolValue keySymbol)
                    {
                        if (result.ContainsKey(keySymbol))
                        {
                            var current = result[keySymbol];
                            result[keySymbol] = (newValue, current.Item2);
                        }
                        else
                        {
                            throw new Exception($"Key '{keySymbol.Value}' not found in dictionary");
                        }
                    }
                    else
                    {
                        throw new Exception("Dictionary indices must be symbols");
                    }
                }
            }
            else
            {
                throw new Exception("Dictionary indices must be symbols or vector of symbols");
            }
            
            return new DictionaryValue(result);
        }
        
        private K3Value AmendAtPath(K3Value data, List<K3Value> path, int pathIndex, K3Value function, K3Value? value)
        {
            if (pathIndex >= path.Count)
            {
                // End of path: apply the function here
                return ApplyAmendFunction(data, function, value);
            }

            var index = path[pathIndex];

            if (data is VectorValue list)
            {
                if (index is NullValue)
                {
                    // Null index: amend every element — .[d;,_n;f;y] / .[d;(_n;...);f;y]
                    // When value is a vector of matching length, distribute value[i] to element i
                    // e.g., a[;0 1]:x where a is 4×5 and x is 4×2 — each row i gets x[i]
                    var result = new List<K3Value>(list.Elements);
                    bool distribute = value is VectorValue valVec && valVec.Elements.Count == result.Count;
                    for (int i = 0; i < result.Count; i++)
                    {
                        var elementValue = distribute ? ((VectorValue)value!).Elements[i] : value;
                        result[i] = AmendAtPath(result[i], path, pathIndex + 1, function, elementValue);
                    }
                    return new VectorValue(result);
                }
                else if (index is IntegerValue intIdx)
                {
                    int idx = (int)intIdx.Value;
                    if (idx < 0 || idx >= list.Elements.Count)
                    {
                        throw new Exception($"Index {idx} out of bounds for list of length {list.Elements.Count}");
                    }
                    var result = new List<K3Value>(list.Elements);
                    result[idx] = AmendAtPath(result[idx], path, pathIndex + 1, function, value);
                    return new VectorValue(result);
                }
                else if (index is VectorValue idxVec)
                {
                    // Multiple indices at this level
                    // When value is a vector with matching count, distribute value[i] to index indices[i]
                    // e.g., row[0 1]:9 9 assigns 9 to index 0 and 9 to index 1
                    var result = new List<K3Value>(list.Elements);
                    bool distribute = value is VectorValue valVec2 && valVec2.Elements.Count == idxVec.Elements.Count;
                    for (int i = 0; i < idxVec.Elements.Count; i++)
                    {
                        var subIdx = idxVec.Elements[i];
                        if (subIdx is IntegerValue subIntIdx)
                        {
                            int idx = (int)subIntIdx.Value;
                            if (idx < 0 || idx >= result.Count)
                            {
                                throw new Exception($"Index {idx} out of bounds for list of length {result.Count}");
                            }
                            var elementValue = distribute ? ((VectorValue)value!).Elements[i] : value;
                            result[idx] = AmendAtPath(result[idx], path, pathIndex + 1, function, elementValue);
                        }
                        else
                        {
                            throw new Exception("List indices must be integers");
                        }
                    }
                    return new VectorValue(result);
                }
                else
                {
                    throw new Exception("List indices must be integers");
                }
            }
            else if (data is DictionaryValue dict)
            {
                if (index is SymbolValue sym)
                {
                    var result = new Dictionary<SymbolValue, (K3Value Value, DictionaryValue?)>(dict.Entries);
                    if (!result.ContainsKey(sym))
                    {
                        throw new Exception($"Key '{sym.Value}' not found in dictionary");
                    }
                    if (result.TryGetValue(sym, out var current))
                    {
                        var newVal = AmendAtPath(current.Value, path, pathIndex + 1, function, value);
                        result[sym] = (newVal, current.Item2);
                    }
                    return new DictionaryValue(result);
                }
                else
                {
                    throw new Exception("Dictionary key must be a symbol");
                }
            }
            else
            {
                // Atomic data at a non-terminal path position
                throw new Exception("Cannot index into atomic value");
            }
        }

        private K3Value SetValueAtPath(K3Value data, List<K3Value> path, int pathIndex, K3Value newValue)
        {
            if (pathIndex >= path.Count)
            {
                // End of path: return the new value
                return newValue;
            }

            var index = path[pathIndex];

            if (data is VectorValue list)
            {
                if (index is IntegerValue intIdx)
                {
                    int idx = (int)intIdx.Value;
                    if (idx < 0 || idx >= list.Elements.Count)
                    {
                        throw new Exception($"Index {idx} out of bounds for list of length {list.Elements.Count}");
                    }
                    var result = new List<K3Value>(list.Elements);
                    result[idx] = SetValueAtPath(result[idx], path, pathIndex + 1, newValue);
                    return new VectorValue(result);
                }
                else if (index is VectorValue idxVec)
                {
                    // Multiple indices at this level
                    var result = new List<K3Value>(list.Elements);
                    foreach (var subIdx in idxVec.Elements)
                    {
                        if (subIdx is IntegerValue subIntIdx)
                        {
                            int idx = (int)subIntIdx.Value;
                            if (idx < 0 || idx >= result.Count)
                            {
                                throw new Exception($"Index {idx} out of bounds for list of length {result.Count}");
                            }
                            result[idx] = SetValueAtPath(result[idx], path, pathIndex + 1, newValue);
                        }
                        else
                        {
                            throw new Exception("List indices must be integers");
                        }
                    }
                    return new VectorValue(result);
                }
                else
                {
                    throw new Exception("List indices must be integers");
                }
            }
            else if (data is DictionaryValue dict)
            {
                if (index is SymbolValue sym)
                {
                    var result = new Dictionary<SymbolValue, (K3Value Value, DictionaryValue?)>(dict.Entries);
                    if (!result.ContainsKey(sym))
                    {
                        throw new Exception($"Key '{sym.Value}' not found in dictionary");
                    }
                    if (result.TryGetValue(sym, out var current))
                    {
                        var newVal = SetValueAtPath(current.Value, path, pathIndex + 1, newValue);
                        result[sym] = (newVal, current.Item2);
                    }
                    return new DictionaryValue(result);
                }
                else
                {
                    throw new Exception("Dictionary key must be a symbol");
                }
            }
            else
            {
                // Atomic data at a non-terminal path position
                throw new Exception("Cannot index into atomic value");
            }
        }

        private K3Value AmendAtom(K3Value atom, K3Value function, K3Value? value)
        {
            // For atoms, just apply the function directly
            return ApplyAmendFunction(atom, function, value);
        }
        
        private K3Value ApplyAmendFunction(K3Value currentValue, K3Value function, K3Value? value)
        {
            // Check if function is colon - this means direct assignment (not a function to call)
            if (function is SymbolValue symbol && symbol.Value == ":")
            {
                // Direct assignment: just return the value (ignoring currentValue)
                return value ?? currentValue;
            }
            
            // Apply function to current value
            if (value != null)
            {
                // Dyadic function: f[currentValue; value]
                var arguments = new List<K3Value> { currentValue, value };
                return CallFunction(function, arguments);
            }
            else
            {
                // Monadic function: f[currentValue]
                var arguments = new List<K3Value> { currentValue };
                return CallFunction(function, arguments);
            }
        }

                
        private K3Value CallFunction(K3Value function, List<K3Value> arguments)
        {
            if (function is FunctionValue func)
            {
                var tempFunctionNode = new ASTNode(ASTNodeType.Function);
                tempFunctionNode.Value = func;
                var result = CallDirectFunction(tempFunctionNode, arguments);
                return result;
            }
            else if (function is SymbolValue symbol)
            {
                return CallVariableFunction(symbol.Value, arguments);
            }
            else if (function is ProjectedFunctionValue projected)
            {
                return CallProjectedFunction(projected, arguments);
            }
            else if (function is AdverbProjectedFunctionValue adverbProjected)
            {
                return CallAdverbProjectedFunction(adverbProjected, arguments);
            }
            else
            {
                throw new Exception("Function must be a FunctionValue, SymbolValue, ProjectedFunctionValue, or AdverbProjectedFunctionValue");
            }
        }
        
        private K3Value ApplyTrappedFunction(K3Value function, K3Value arguments)
        {
            // Apply function to arguments for trapped apply
            if (arguments is VectorValue argVec)
            {
                return CallFunction(function, argVec.Elements.ToList());
            }
            else
            {
                return CallFunction(function, new List<K3Value> { arguments });
            }
        }
        
        private K3Value CallProjectedFunction(ProjectedFunctionValue projected, List<K3Value> arguments)
        {
            // If the projection has bound arguments, merge them with the provided arguments
            // Bound args have null for unbound positions; fill those with provided arguments
            if (projected.BoundArguments != null && projected.BoundArguments.Count > 0)
            {
                var mergedArgs = new List<K3Value>();
                int argIdx = 0;
                foreach (var boundArg in projected.BoundArguments)
                {
                    if (boundArg == null)
                    {
                        // Unbound position — fill from provided arguments
                        if (argIdx < arguments.Count)
                            mergedArgs.Add(arguments[argIdx++]);
                        else
                            mergedArgs.Add(new NullValue());
                    }
                    else
                    {
                        mergedArgs.Add(boundArg);
                    }
                }
                // If there are extra provided arguments beyond the unbound slots, append them
                while (argIdx < arguments.Count)
                    mergedArgs.Add(arguments[argIdx++]);
                
                return CallVariableFunction(projected.OperatorName, mergedArgs);
            }
            
            // Call a projected function like +/, over, etc.
            return CallVariableFunction(projected.OperatorName, arguments);
        }
        
        private K3Value CallAdverbProjectedFunction(AdverbProjectedFunctionValue adverbProjected, List<K3Value> arguments)
        {
            // Call an adverb projected function like +/ (over with verb +) or >':0, (each-prior with verb > and left arg 0)
            K3Value verb;
            
            // Use the stored FunctionValue if available (user-defined functions)
            if (adverbProjected.FuncValue != null)
            {
                verb = adverbProjected.FuncValue;
            }
            else if (VerbRegistry.HasVerb(adverbProjected.Verb))
            {
                verb = new SymbolValue(adverbProjected.Verb);
            }
            else
            {
                // Try to resolve as a user-defined function variable
                var resolved = GetVariable(adverbProjected.Verb);
                if (resolved != null && resolved is FunctionValue)
                {
                    verb = resolved;
                }
                else
                {
                    verb = new SymbolValue(adverbProjected.Verb);
                }
            }
            
            // Handle arguments: if we have a single vector argument, pass it as is
            // Otherwise, pass individual arguments
            K3Value dataArg;
            if (arguments.Count == 1 && arguments[0] is VectorValue argVec)
            {
                dataArg = argVec; // Pass the vector as-is
            }
            else
            {
                // Create a vector from arguments for Over function
                dataArg = new VectorValue(arguments);
            }
            
            // Use bound left argument if available, otherwise use default (0 as sentinel)
            K3Value leftArg = adverbProjected.BoundLeftArgument ?? new NullValue();
            
            // Call the appropriate adverb function based on the adverb name
            return adverbProjected.AdverbName switch
            {
                "over" => verb is FunctionValue fvOver ? ApplyAdverbSlash(fvOver, leftArg, dataArg) : Over(verb, leftArg, dataArg),
                "scan" => verb is FunctionValue fvScan ? ApplyAdverbBackslash(fvScan, leftArg, dataArg) : Scan(verb, leftArg, dataArg),
                "each" => HandleAdverbTick(verb, leftArg, dataArg),
                "each-prior" => verb is FunctionValue fvEP ? ApplyAdverbTickColon(fvEP, leftArg, dataArg) : EachPrior(verb, leftArg, dataArg),
                _ => throw new Exception($"Unknown adverb projected function: {adverbProjected.AdverbName}")
            };
        }
    }
}
