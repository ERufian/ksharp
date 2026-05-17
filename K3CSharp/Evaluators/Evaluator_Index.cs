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
    public partial class Evaluator
    {
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
            
            // @ operator for applying an adverb-projected function to arguments
            // e.g., f'[a] (parsed as APPLY(AdverbProjectedFunctionValue, vector))
            if (left is AdverbProjectedFunctionValue adverbProjLeft)
            {
                var arguments = new List<K3Value> { right ?? new NullValue() };
                return CallAdverbProjectedFunction(adverbProjLeft, arguments);
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
                // Encoded adverb functions (EACH:, OVER:, etc.) should receive the vector as-is
                // so the adverb dispatch can iterate over it monadically
                bool isEncodedAdverbFunc = funcLeft.BodyText != null &&
                    (funcLeft.BodyText.StartsWith("EACH:") || funcLeft.BodyText.StartsWith("OVER:") ||
                     funcLeft.BodyText.StartsWith("SCAN:") || funcLeft.BodyText.StartsWith("EACH_RIGHT:") ||
                     funcLeft.BodyText.StartsWith("EACH_LEFT:") || funcLeft.BodyText.StartsWith("EACH_PRIOR:"));
                
                var argVec = right as VectorValue;
                if (!isEncodedAdverbFunc && argVec != null && argVec.Elements.Count > 1 && (funcLeft.Parameters.Count > 1 || funcLeft.Parameters.Count == 0))
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
            // d@s / d[s] - execution at context when index is a character vector
            if (index is VectorValue charVec && charVec.Elements.Count > 0 && charVec.Elements.All(e => e is CharacterValue))
            {
                var str = string.Join("", charVec.Elements.Select(e => ((CharacterValue)e).Value));
                string? branchPath = null;

                if (data is SymbolValue symData)
                {
                    var symValue = symData.Value;
                    branchPath = symValue.StartsWith(".") ? symValue : (kTree.CurrentBranch?.Value ?? ".k") + "." + symValue;
                }
                else if (data is DictionaryValue dictData)
                {
                    branchPath = kTree.FindPath(dictData);
                }

                if (!string.IsNullOrEmpty(branchPath))
                {
                    var dictAtPath = kTree.GetValue(branchPath);
                    if (dictAtPath is DictionaryValue)
                    {
                        return ExecuteAtContext(branchPath, str);
                    }
                }
            }

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

        private K3Value ScatterSelection(K3Value matrixValue, List<K3Value> indexValues)
        {
            // Determine result length from vector indices; scalars are implicitly replicated
            int resultLength = 1;
            bool hasVectorIndex = false;
            foreach (var iv in indexValues)
            {
                if (iv is VectorValue ivv)
                {
                    if (!hasVectorIndex)
                    {
                        resultLength = ivv.Elements.Count;
                        hasVectorIndex = true;
                    }
                    else if (ivv.Elements.Count != resultLength)
                    {
                        throw new Exception("Length error: scatter selection index vectors must have equal length");
                    }
                }
            }

            // Validate all indices are integers and determine per-element index
            var perElementIndices = new List<List<int>>();
            for (int pos = 0; pos < resultLength; pos++)
            {
                var elementIndices = new List<int>();
                foreach (var iv in indexValues)
                {
                    int idx;
                    if (iv is VectorValue ivv)
                    {
                        var elem = ivv.Elements[pos];
                        if (elem is IntegerValue ivi)
                            idx = (int)ivi.Value;
                        else if (elem is LongValue ivl)
                            idx = (int)ivl.Value;
                        else
                            throw new Exception("Type error: scatter selection indices must be integers");
                    }
                    else if (iv is IntegerValue ii)
                    {
                        idx = (int)ii.Value;
                    }
                    else if (iv is LongValue il)
                    {
                        idx = (int)il.Value;
                    }
                    else
                    {
                        throw new Exception("Type error: scatter selection indices must be integers");
                    }
                    elementIndices.Add(idx);
                }
                perElementIndices.Add(elementIndices);
            }

            // Extract elements by repeated indexing
            var results = new List<K3Value>();
            foreach (var elementIndices in perElementIndices)
            {
                K3Value current = matrixValue;
                foreach (int idx in elementIndices)
                {
                    if (current is VectorValue vec)
                    {
                        if (idx < 0 || idx >= vec.Elements.Count)
                            throw new Exception($"Index error: index {idx} out of bounds for vector of length {vec.Elements.Count}");
                        current = vec.Elements[idx];
                    }
                    else
                    {
                        throw new Exception("Type error: cannot index into atomic value during scatter selection");
                    }
                }
                results.Add(current);
            }

            return new VectorValue(results);
        }

        private int GetIntValue(K3Value value)
        {
            if (value is IntegerValue iv) return (int)iv.Value;
            if (value is LongValue lv) return (int)lv.Value;
            throw new Exception("Type error: expected integer value");
        }
    }
}
