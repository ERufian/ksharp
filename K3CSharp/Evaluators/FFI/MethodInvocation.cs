// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace K3CSharp
{
    /// <summary>
    /// Handles method binding and invocation for FFI objects
    /// Provides apply operations (@, ., []) for method calling
    /// </summary>
    public static class MethodInvocation
    {
        /// <summary>
        /// Apply operation (@) for calling methods on objects
        /// </summary>
        /// <param name="left">Object dictionary or function, or symbol path</param>
        /// <param name="right">Arguments or method name</param>
        /// <param name="evaluator">Evaluator instance for path resolution</param>
        /// <returns>Method invocation result</returns>
        public static K3Value Apply(K3Value left, K3Value right, Evaluator evaluator)
        {
            // Handle symbol as path to a dictionary
            if (left is SymbolValue sym && evaluator != null)
            {
                var resolvedValue = evaluator.GetVariableValuePublic(sym.Value);
                if (resolvedValue != null)
                {
                    left = resolvedValue;
                }
            }
            
            // Handle function application
            if (left is FunctionValue func)
            {
                return CallFunction(func, right);
            }
            
            // Handle object method calls
            if (left is DictionaryValue dict)
            {
                return CallObjectMethod(dict, right);
            }
            
            throw new Exception("Apply (@) requires function or object dictionary");
        }

        /// <summary>
        /// Dot operation (.) for property/method access
        /// </summary>
        /// <param name="left">Object dictionary, or symbol path</param>
        /// <param name="right">Property/method name</param>
        /// <param name="evaluator">Evaluator instance for path resolution</param>
        /// <returns>Property value or method function</returns>
        public static K3Value Dot(K3Value left, K3Value right, Evaluator evaluator)
        {
            // Handle symbol as path to a dictionary
            if (left is SymbolValue sym && evaluator != null)
            {
                var resolvedValue = evaluator.GetVariableValuePublic(sym.Value);
                if (resolvedValue != null)
                {
                    left = resolvedValue;
                }
            }
            
            if (left is DictionaryValue dict && right is SymbolValue symbol)
            {
                return GetMember(dict, symbol.Value);
            }
            
            throw new Exception("Dot (.) requires object dictionary and symbol");
        }

        /// <summary>
        /// Index operation ([]) for indexed access
        /// </summary>
        /// <param name="left">Object dictionary or array, or symbol path</param>
        /// <param name="right">Index</param>
        /// <param name="evaluator">Evaluator instance for path resolution</param>
        /// <returns>Indexed value</returns>
        public static K3Value Index(K3Value left, K3Value right, Evaluator evaluator)
        {
            // Handle symbol as path to a dictionary
            if (left is SymbolValue sym && evaluator != null)
            {
                var resolvedValue = evaluator.GetVariableValuePublic(sym.Value);
                if (resolvedValue != null)
                {
                    left = resolvedValue;
                }
            }
            
            // Handle empty brackets (identity operation)
            if (right is NullValue)
            {
                // For vectors: return the vector itself
                if (left is VectorValue)
                {
                    return left;
                }
                // For dictionaries: return the values
                if (left is DictionaryValue dictionary)
                {
                    var values = new List<K3Value>();
                    foreach (var entry in dictionary.Entries.Values)
                    {
                        values.Add(entry.Value);
                    }
                    return new VectorValue(values);
                }
            }
            
            // Handle array indexing
            if (left is VectorValue vector)
            {
                return IndexVector(vector, right);
            }
            
            // Handle object dictionary indexing
            if (left is DictionaryValue dict)
            {
                return IndexDictionary(dict, right);
            }
            
            throw new Exception("Index ([]) requires vector or object dictionary");
        }

        private static K3Value CallFunction(FunctionValue func, K3Value args)
        {
            // For now, just return the function itself
            // Full argument handling would need evaluator integration
            return func;
        }

        public static K3Value CallObjectMethod(DictionaryValue dict, K3Value args)
        {
            // Find _this entry to get the object
            if (!dict.Entries.TryGetValue(new SymbolValue("_this"), out var thisEntry))
            {
                throw new Exception("Object dictionary missing _this entry");
            }
            
            var handle = (thisEntry.Value is SymbolValue handleSym) ? handleSym.Value : thisEntry.Value.ToString();
            var target = ObjectRegistry.GetObject(handle);
            
            if (target == null)
            {
                throw new Exception($"Object not found for handle: {handle}");
            }
            
            // If args is a symbol, treat as method name with no arguments
            if (args is SymbolValue methodSymbol)
            {
                return InvokeMethod(target, methodSymbol.Value, new List<K3Value>());
            }
            
            // If args is a vector, treat as method name + arguments
            if (args is VectorValue vec && vec.Elements.Count > 0)
            {
                var methodSym = vec.Elements[0] as SymbolValue;
                if (methodSym != null)
                {
                    var methodArgs = vec.Elements.Skip(1).ToList();
                    return InvokeMethod(target, methodSym.Value, methodArgs);
                }
            }
            
            throw new Exception("Invalid method call format");
        }

        private static K3Value GetMember(DictionaryValue dict, string memberName)
        {
            if (dict.Entries.TryGetValue(new SymbolValue(memberName), out var entry))
            {
                return entry.Value;
            }
            
            throw new Exception($"Member '{memberName}' not found");
        }

        private static K3Value IndexDictionary(DictionaryValue dict, K3Value index)
        {
            // Try to get by index key
            SymbolValue indexKey;
            if (index is SymbolValue sym)
            {
                indexKey = sym;
            }
            else
            {
                indexKey = new SymbolValue(index.ToString());
            }
            
            if (dict.Entries.TryGetValue(indexKey, out var entry))
            {
                return entry.Value;
            }
            
            throw new Exception($"Index '{index}' not found in dictionary");
        }

        private static K3Value IndexVector(VectorValue vector, K3Value index)
        {
            if (index is IntegerValue intIdx)
            {
                var idx = intIdx.Value;
                if (idx >= 0 && idx < vector.Elements.Count)
                {
                    return vector.Elements[idx];
                }
            }
            
            throw new Exception("Vector index out of range");
        }

        private static K3Value InvokeMethod(object target, string methodName, List<K3Value> args)
        {
            var type = target.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == methodName)
                .ToList();
            
            if (methods.Count == 0)
            {
                // Fall back to property access
                var property = type.GetProperty(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && args.Count == 0)
                {
                    var propValue = property.GetValue(target);
                    return TypeMarshalling.NetToK3(propValue);
                }
                
                // Also check fields
                var field = type.GetField(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && args.Count == 0)
                {
                    var fieldValue = field.GetValue(target);
                    return TypeMarshalling.NetToK3(fieldValue);
                }
                
                throw new Exception($"Method '{methodName}' not found");
            }
            
            // Find best method match based on parameter count
            var bestMethod = FindBestMethodMatch(methods, args);
            
            try
            {
                var parameters = bestMethod.GetParameters();
                var netArgs = new object?[parameters.Length];
                
                for (int i = 0; i < Math.Min(args.Count, parameters.Length); i++)
                {
                    netArgs[i] = TypeMarshalling.K3ToNet(args[i], parameters[i].ParameterType);
                }
                
                var result = bestMethod.Invoke(target, netArgs!);
                return TypeMarshalling.NetToK3(result);
            }
            catch (Exception ex)
            {
                throw new Exception($"Method invocation failed: {ex.Message}", ex);
            }
        }

        private static MethodInfo FindBestMethodMatch(List<MethodInfo> methods, List<K3Value> args)
        {
            // Simple matching: find method with closest parameter count
            var exactMatch = methods.FirstOrDefault(m => m.GetParameters().Length == args.Count);
            if (exactMatch != null)
                return exactMatch;
            
            // Find method with closest parameter count
            return methods.OrderBy(m => Math.Abs(m.GetParameters().Length - args.Count)).First();
        }
    }
}