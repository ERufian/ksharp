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
    public partial class Evaluator
    {
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
    }
}
