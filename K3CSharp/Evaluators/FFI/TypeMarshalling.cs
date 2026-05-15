// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace K3CSharp
{
    /// <summary>
    /// Type marshalling between K3 values and .NET types with hint support
    /// </summary>
    public static class TypeMarshalling
    {
        /// <summary>
        /// Convert a K3Value to a .NET object using hint information
        /// </summary>
        /// <param name="kValue">The K3Value to convert</param>
        /// <param name="targetType">The target .NET type (optional, can use hint)</param>
        /// <returns>The converted .NET object</returns>
        public static object? K3ToNet(K3Value kValue, Type? targetType = null)
        {
            if (kValue == null) return null;
            
            // Use hint if target type not specified
            if (targetType == null && kValue.Hint != null)
            {
                targetType = HintSystem.GetMappedType(kValue.Hint.Value);
            }
            
            // Default to object type if still null
            targetType ??= typeof(object);
            
            return kValue.Type switch
            {
                ValueType.Integer => MarshalInteger(kValue, targetType),
                ValueType.Long => MarshalLong(kValue, targetType),
                ValueType.Float => MarshalFloat(kValue, targetType),
                ValueType.Character => MarshalCharacter(kValue, targetType),
                ValueType.Symbol => MarshalSymbol(kValue, targetType),
                ValueType.Vector => MarshalVector(kValue, targetType),
                ValueType.Dictionary => MarshalDictionary(kValue, targetType),
                ValueType.Function => MarshalFunction(kValue, targetType),
                ValueType.Null => null,
                _ => throw new NotSupportedException($"Cannot marshal K3Value type {kValue.Type} to .NET type {targetType}")
            };
        }

        /// <summary>
        /// Convert a .NET object to a K3Value with automatic hint assignment
        /// </summary>
        /// <param name="netValue">The .NET object to convert</param>
        /// <param name="hint">Optional hint to use (will be auto-detected if null)</param>
        /// <returns>The converted K3Value</returns>
        public static K3Value NetToK3(object? netValue, SymbolValue? hint = null)
        {
            if (netValue == null)
            {
                return new NullValue();
            }
            
            // Auto-detect hint if not provided
            if (hint == null)
            {
                hint = new SymbolValue(DetectHintFromType(netValue.GetType()));
            }
            
            Type valueType = netValue.GetType();
            
            if (IsBasicType(valueType))
            {
                return MarshalBasicType(netValue, hint);
            }
            else if (valueType.IsArray || IsCollectionType(valueType))
            {
                return MarshalCollection(netValue, hint);
            }
            else if (IsDictionaryType(valueType))
            {
                return MarshalNetDictionary(netValue, hint);
            }
            else
            {
                // Complex object - create dictionary representation
                return MarshalComplexObject(netValue, hint);
            }
        }

        #region K3 to .NET Marshalling

        private static object? MarshalInteger(K3Value kValue, Type targetType)
        {
            var intValue = ((IntegerValue)kValue).Value;
            
            if (targetType == typeof(int) || targetType == typeof(int?))
                return intValue;
            if (targetType == typeof(long) || targetType == typeof(long?))
                return (long)intValue;
            if (targetType == typeof(double) || targetType == typeof(double?))
                return (double)intValue;
            if (targetType == typeof(float) || targetType == typeof(float?))
                return (float)intValue;
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                return (decimal)intValue;
            if (targetType == typeof(short) || targetType == typeof(short?))
                return (short)intValue;
            if (targetType == typeof(byte) || targetType == typeof(byte?))
                return (byte)intValue;
            if (targetType == typeof(bool) || targetType == typeof(bool?))
                return intValue != 0;
            
            return Convert.ChangeType(intValue, targetType);
        }

        private static object? MarshalLong(K3Value kValue, Type targetType)
        {
            var longValue = ((LongValue)kValue).Value;
            
            if (targetType == typeof(long) || targetType == typeof(long?))
                return longValue;
            if (targetType == typeof(int) || targetType == typeof(int?))
                return (int)longValue;
            if (targetType == typeof(double) || targetType == typeof(double?))
                return (double)longValue;
            if (targetType == typeof(float) || targetType == typeof(float?))
                return (float)longValue;
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                return (decimal)longValue;
            if (targetType == typeof(bool) || targetType == typeof(bool?))
                return longValue != 0;
            
            return Convert.ChangeType(longValue, targetType);
        }

        private static object? MarshalFloat(K3Value kValue, Type targetType)
        {
            var floatValue = ((FloatValue)kValue).Value;
            
            if (targetType == typeof(double) || targetType == typeof(double?))
                return floatValue;
            if (targetType == typeof(float) || targetType == typeof(float?))
                return (float)floatValue;
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                return (decimal)floatValue;
            if (targetType == typeof(int) || targetType == typeof(int?))
                return (int)floatValue;
            if (targetType == typeof(long) || targetType == typeof(long?))
                return (long)floatValue;
            if (targetType == typeof(bool) || targetType == typeof(bool?))
                return floatValue != 0;
            
            return Convert.ChangeType(floatValue, targetType);
        }

        private static object? MarshalCharacter(K3Value kValue, Type targetType)
        {
            var charValue = ((CharacterValue)kValue).Value;
            
            if (targetType == typeof(string))
                return charValue;
            if (targetType == typeof(char) || targetType == typeof(char?))
                return charValue.Length > 0 ? charValue[0] : '\0';
            
            return charValue;
        }

        private static object? MarshalSymbol(K3Value kValue, Type targetType)
        {
            var symbolValue = ((SymbolValue)kValue).Value;
            
            if (targetType == typeof(string))
                return symbolValue;
            if (targetType == typeof(Type))
            {
                // Try to resolve as a type name
                var type = Type.GetType(symbolValue);
                if (type != null) return type;
                
                // Try with common prefixes
                var prefixes = new[] { "System.", "Microsoft.", "K3CSharp." };
                foreach (var prefix in prefixes)
                {
                    type = Type.GetType(prefix + symbolValue);
                    if (type != null) return type;
                }
                
                throw new TypeLoadException($"Cannot resolve type: {symbolValue}");
            }
            
            return symbolValue;
        }

        private static object? MarshalVector(K3Value kValue, Type targetType)
        {
            var vector = ((VectorValue)kValue).Elements;
            
            // Debug: log targetType
            // Special handling for string target type (convert char vector to string)
            if (targetType == typeof(string))
            {
                var chars = vector.Select(e => e is CharacterValue cv ? cv.Value[0] : e.ToString()[0]).ToArray();
                return new string(chars);
            }
            
            // Special handling for char[] target type (convert char vector to char[])
            if (targetType == typeof(char[]))
            {
                var chars = vector.Select(e => e is CharacterValue cv ? cv.Value[0] : e.ToString()[0]).ToArray();
                return chars;
            }
            
            // Special handling for char* target type (convert char vector to char[] then to string)
            if (targetType == typeof(char*))
            {
                // char* is unsafe, convert to string instead
                var chars = vector.Select(e => e is CharacterValue cv ? cv.Value[0] : e.ToString()[0]).ToArray();
                return new string(chars);
            }
            
            if (targetType.IsArray)
            {
                return MarshalToArray(vector, targetType.GetElementType()!);
            }
            else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return MarshalToList(vector, targetType.GetGenericArguments()[0]);
            }
            else if (targetType == typeof(ArrayList))
            {
                var arrayList = new ArrayList();
                foreach (var element in vector)
                {
                    arrayList.Add(K3ToNet(element));
                }
                return arrayList;
            }
            
            // Default to object array
            return MarshalToArray(vector, typeof(object));
        }

        private static object? MarshalDictionary(K3Value kValue, Type targetType)
        {
            var dict = ((DictionaryValue)kValue).Entries;
            
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = targetType.GetGenericArguments()[0];
                var valueType = targetType.GetGenericArguments()[1];
                return MarshalToGenericDictionary(dict, keyType, valueType);
            }
            else if (targetType == typeof(Hashtable))
            {
                var hashtable = new Hashtable();
                foreach (var kvp in dict)
                {
                    hashtable[kvp.Key.Value] = K3ToNet(kvp.Value.Value);
                }
                return hashtable;
            }
            
            // Default to Hashtable
            var ht = new Hashtable();
            foreach (var kvp in dict)
            {
                ht[kvp.Key.Value] = K3ToNet(kvp.Value.Value);
            }
            return ht;
        }

        private static object? MarshalFunction(K3Value kValue, Type targetType)
        {
            // For now, return the function as a delegate wrapper
            // This will be expanded in Phase 3 with method invocation
            var function = (FunctionValue)kValue;
            return new K3FunctionDelegate(function);
        }

        #endregion

        #region .NET to K3 Marshalling

        private static K3Value MarshalBasicType(object netValue, SymbolValue hint)
        {
            return netValue switch
            {
                int i => new IntegerValue(i, hint),
                long l => new LongValue(l, hint),
                double d => new FloatValue(d, hint),
                float f => new FloatValue(f, hint),
                string s => new CharacterValue(s, hint),
                char c => new CharacterValue(c.ToString(), hint),
                bool b => new IntegerValue(b ? 1 : 0, hint),
                decimal d => new FloatValue((double)d, hint),
                short s => new IntegerValue(s, hint),
                byte b => new IntegerValue(b, hint),
                _ => new CharacterValue(netValue.ToString()!, hint)
            };
        }

        private static K3Value MarshalCollection(object netValue, SymbolValue hint)
        {
            var elements = new List<K3Value>();
            
            if (netValue is Array array)
            {
                foreach (var item in array)
                {
                    elements.Add(NetToK3(item));
                }
            }
            else if (netValue is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    elements.Add(NetToK3(item));
                }
            }
            
            return new VectorValue(elements, hint);
        }

        private static K3Value MarshalNetDictionary(object netValue, SymbolValue hint)
        {
            var entries = new Dictionary<SymbolValue, (K3Value Value, DictionaryValue? Attribute)>();
            
            if (netValue is Hashtable hashtable)
            {
                foreach (DictionaryEntry entry in hashtable)
                {
                    var key = new SymbolValue(entry.Key.ToString()!);
                    var value = NetToK3(entry.Value);
                    entries[key] = (value, null);
                }
            }
            else if (netValue is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = new SymbolValue(entry.Key.ToString()!);
                    var value = NetToK3(entry.Value);
                    entries[key] = (value, null);
                }
            }
            
            return new DictionaryValue(entries);
        }

        private static K3Value MarshalComplexObject(object netValue, SymbolValue hint)
        {
            var entries = new Dictionary<SymbolValue, (K3Value Value, DictionaryValue? Attribute)>();
            var type = netValue.GetType();
            
            // Register the object and get its handle first
            var handle = ObjectRegistry.RegisterObject(netValue);
            
            // Add properties: store current value directly AND a setter function
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Skip properties that return the same type as the containing object (circular reference)
                if (property.PropertyType == type)
                    continue;
                
                // Skip indexer properties
                if (property.GetIndexParameters().Length > 0)
                    continue;
                
                if (property.CanRead)
                {
                    try
                    {
                        var propValue = property.GetValue(netValue);
                        var k3Value = NetToK3(propValue);
                        entries[new SymbolValue(property.Name)] = (k3Value, null);
                    }
                    catch
                    {
                        // If property read fails, store a getter function instead
                        var getter = CreatePropertyGetter(netValue, property, handle);
                        entries[new SymbolValue(property.Name)] = (getter, null);
                    }
                }
                
                if (property.CanWrite)
                {
                    var setterKey = new SymbolValue($"set_{property.Name}");
                    var setter = CreatePropertySetter(netValue, property, handle);
                    entries[setterKey] = (setter, null);
                }
            }
            
            // Add public instance fields: store current value directly
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    var fieldValue = field.GetValue(netValue);
                    var k3Value = NetToK3(fieldValue);
                    entries[new SymbolValue(field.Name)] = (k3Value, null);
                }
                catch
                {
                    var getter = CreateFieldGetter(netValue, field, handle);
                    entries[new SymbolValue(field.Name)] = (getter, null);
                }
                
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    var setterKey = new SymbolValue($"set_{field.Name}");
                    var setter = CreateFieldSetter(netValue, field, handle);
                    entries[setterKey] = (setter, null);
                }
            }
            
            // Add special _this entry for object reference
            entries[new SymbolValue("_this")] = (new SymbolValue(handle), null);
            
            // Add instance methods as callable entries
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!method.IsSpecialName) // Exclude property accessors, etc.
                {
                    var key = new SymbolValue(method.Name);
                    var methodFunc = CreateMethodFunction(netValue, method);
                    entries[key] = (methodFunc, null);
                }
            }
            
            // Add static methods as callable entries (so instance can call e.g. Complex.Abs(c))
            // The handle is encoded so the handler can automatically inject the instance as first arg
            var assemblyName = type.Assembly.GetName().Name ?? "";
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!method.IsSpecialName)
                {
                    var key = new SymbolValue(method.Name);
                    // Skip the first parameter (the instance itself) for the exposed parameter list
                    var methodParams = method.GetParameters();
                    var remainingParams = methodParams.Length > 0 && methodParams[0].ParameterType.IsAssignableFrom(type)
                        ? methodParams.Skip(1).Select(p => p.Name ?? "").ToList()
                        : methodParams.Select(p => p.Name ?? "").ToList();
                    // Format: static_method:TypeFullName|Assembly|MethodName|ObjectHandle
                    var bodyText = $"static_method:{type.FullName}|{assemblyName}|{method.Name}|{handle}";
                    var methodFunc = new FunctionValue(bodyText, remainingParams, null!, "", new SymbolValue("method"));
                    entries[key] = (methodFunc, null);
                }
            }
            
            var result = new DictionaryValue(entries);
            result.Hint = hint;
            return result;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Create a K function that calls an instance method
        /// </summary>
        private static FunctionValue CreateMethodFunction(object target, MethodInfo method)
        {
            var parameters = method.GetParameters().Select(p => p.Name ?? "").ToList();
            
            // Register the target object and get its handle
            var handle = ObjectRegistry.RegisterObject(target);
            
            // Include the object handle in the function body
            var bodyText = $"method:{method.Name}|{handle}";
            
            return new FunctionValue(
                bodyText,
                parameters,
                null!,
                "",
                new SymbolValue("method"));
        }

        /// <summary>
        /// Create a property getter function
        /// </summary>
        private static FunctionValue CreatePropertyGetter(object target, PropertyInfo property, string handle)
        {
            return new FunctionValue(
                $"property_getter:{property.Name}|{handle}",
                new List<string>(),
                null!,
                "",
                new SymbolValue("property_getter"));
        }

        /// <summary>
        /// Create a property setter function
        /// </summary>
        private static FunctionValue CreatePropertySetter(object target, PropertyInfo property, string handle)
        {
            return new FunctionValue(
                $"property_setter:{property.Name}|{handle}",
                new List<string> { "value" },
                null!,
                "",
                new SymbolValue("property_setter"));
        }

        /// <summary>
        /// Create a field getter function
        /// </summary>
        private static FunctionValue CreateFieldGetter(object target, FieldInfo field, string handle)
        {
            return new FunctionValue(
                $"field_getter:{field.Name}|{handle}",
                new List<string>(),
                null!,
                "",
                new SymbolValue("field_getter"));
        }

        /// <summary>
        /// Create a field setter function
        /// </summary>
        private static FunctionValue CreateFieldSetter(object target, FieldInfo field, string handle)
        {
            return new FunctionValue(
                $"field_setter:{field.Name}|{handle}",
                new List<string> { "value" },
                null!,
                "",
                new SymbolValue("field_setter"));
        }

        private static Array MarshalToArray(List<K3Value> vector, Type elementType)
        {
            var array = Array.CreateInstance(elementType, vector.Count);
            for (int i = 0; i < vector.Count; i++)
            {
                var converted = K3ToNet(vector[i], elementType);
                array.SetValue(converted, i);
            }
            return array;
        }

        private static object MarshalToList(List<K3Value> vector, Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;
            
            foreach (var item in vector)
            {
                var converted = K3ToNet(item, elementType);
                list.Add(converted);
            }
            
            return list;
        }

        private static object MarshalToGenericDictionary(Dictionary<SymbolValue, (K3Value Value, DictionaryValue? Attribute)> dict, Type keyType, Type valueType)
        {
            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var result = (IDictionary)Activator.CreateInstance(dictType)!;
            
            foreach (var kvp in dict)
            {
                var key = Convert.ChangeType(kvp.Key.Value, keyType);
                var value = K3ToNet(kvp.Value.Value, valueType);
                result.Add(key, value);
            }
            
            return result;
        }

        private static bool IsBasicType(Type type)
        {
            return type.IsPrimitive || 
                   type == typeof(string) || 
                   type == typeof(decimal) ||
                   type == typeof(char) ||
                   type == typeof(double) ||
                   type == typeof(float) ||
                   type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(short) ||
                   type == typeof(byte) ||
                   type == typeof(bool);
        }

        private static bool IsCollectionType(Type type)
        {
            return type.IsArray || 
                   (type.IsGenericType && 
                    (type.GetGenericTypeDefinition() == typeof(List<>) ||
                     type.GetGenericTypeDefinition() == typeof(IEnumerable<>)));
        }

        private static bool IsDictionaryType(Type type)
        {
            return type == typeof(Hashtable) ||
                   (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) ||
                   typeof(IDictionary).IsAssignableFrom(type);
        }

        private static string DetectHintFromType(Type type)
        {
            if (type == typeof(int) || type == typeof(int?))
                return "int";
            if (type == typeof(long) || type == typeof(long?))
                return "long";
            if (type == typeof(double) || type == typeof(double?) || type == typeof(float) || type == typeof(float?))
                return "float";
            if (type == typeof(char) || type == typeof(char?))
                return "char";
            if (type == typeof(string))
                return "string";
            if (type == typeof(bool) || type == typeof(bool?))
                return "bool";
            if (IsCollectionType(type))
                return "list";
            if (IsDictionaryType(type))
                return "hashtable";
            if (typeof(Delegate).IsAssignableFrom(type))
                return "method";
            
            return "object";
        }

        #endregion
    }

    /// <summary>
    /// Delegate wrapper for K3 functions to be used in .NET contexts
    /// </summary>
    public class K3FunctionDelegate
    {
        private readonly FunctionValue _function;
        
        public K3FunctionDelegate(FunctionValue function)
        {
            _function = function;
        }
        
        public FunctionValue Function => _function;
        
        public override string ToString()
        {
            return $"K3Function({_function.BodyText})";
        }
    }
}