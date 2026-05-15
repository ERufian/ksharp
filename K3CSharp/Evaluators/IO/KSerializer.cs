using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace K3CSharp
{
    /// <summary>
    /// Main K serializer implementing unified serialization pattern
    /// </summary>
    public class KSerializer
    {
        public byte[] Serialize(object value)
        {
            return value switch
            {
                int i => SerializeInteger(i),
                double d => SerializeFloat(d),
                char c => SerializeCharacter(c),
                string s when s.StartsWith("`") => SerializeSymbol(s),
                string s when s.Length == 1 => SerializeCharacter(s[0]),
                string s => SerializeCharacterVector(s),
                null => SerializeNull(),
                K3CSharp.VectorValue list => SerializeList(list), // Use VectorValue for mixed lists
                K3CSharp.DictionaryValue dict => SerializeDictionary(dict), // Use DictionaryValue
                K3CSharp.FunctionValue func => SerializeAnonymousFunction(func), // Use FunctionValue directly
                _ => throw new NotSupportedException($"Unsupported type: {value.GetType()}")
            };
        }
        
        private byte[] SerializeMessage(int length)
        {
            var writer = new KBinaryWriter();
            writer.WriteByte(1);   // Architecture: little-endian
            writer.WriteByte(0);   // Message type: _bd serialization
            writer.WriteInt16(0);  // Reserved: 2 bytes
            writer.WriteInt32(length); // Data length
            return writer.ToArray();
        }
        
        private byte[] PadTo8ByteBoundary(byte[] data)
        {
            int currentLength = data.Length;
            int paddingNeeded = (8 - (currentLength % 8)) % 8;
            if (paddingNeeded == 0) return data;
            byte[] padded = new byte[currentLength + paddingNeeded];
            Array.Copy(data, 0, padded, 0, currentLength);
            return padded;
        }
        
        private byte[] SerializeIntegerData(int value)
        {
            var writer = new KBinaryWriter();
            writer.WriteInt32(1);  // Integer flag
            writer.WriteInt32(value); // Integer value
            return writer.GetBuffer();
        }
        
        private byte[] SerializeRawIntegerData(int value)
        {
            var writer = new KBinaryWriter();
            writer.WriteInt32(value); // Just the raw integer value (4 bytes)
            return writer.GetBuffer();
        }
        
        private byte[] SerializeInteger(int value)
        {
            var data = SerializeIntegerData(value);
            var message = SerializeMessage(data.Length);
            return message.Concat(data).ToArray();
        }
        
        private byte[] SerializeFloatData(double value)
        {
            var writer = new KBinaryWriter();
            writer.WriteInt32(2);  // Float flag
            writer.WriteInt32(1);  // Subtype field (required for floats in k.exe)
            writer.WriteDouble(value); // Float value
            return writer.GetBuffer();
        }
        
        private byte[] SerializeRawFloatData(double value)
        {
            var writer = new KBinaryWriter();
            writer.WriteDouble(value); // Just the raw float value (8 bytes)
            return writer.GetBuffer();
        }
        
        private byte[] SerializeFloat(double value)
        {
            var data = SerializeFloatData(value); // Use format with subtype flag
            var message = SerializeMessage(data.Length);
            return message.Concat(data).ToArray();
        }
        
        private byte[] SerializeCharacterData(char value)
        {
            var writer = new KBinaryWriter();
            writer.WriteInt32(3);  // Character flag
            writer.WriteByte((byte)value); // Character value
            writer.WritePadding(3); // 3 bytes padding to align to 4-byte boundary
            return writer.GetBuffer();
        }
        
        private byte[] SerializeRawCharacterData(char value)
        {
            var writer = new KBinaryWriter();
            writer.WriteByte((byte)value); // Just the raw character value (1 byte)
            return writer.GetBuffer();
        }
        
        private byte[] SerializeCharacter(char value)
        {
            var data = SerializeCharacterData(value);
            var message = SerializeMessage(data.Length);
            return message.Concat(data).ToArray();
        }
        
        private byte[] SerializeSymbolData(string symbol, bool isInMixedList = false)
        {
            var writer = new KBinaryWriter();
            // Remove initial backtick for serialization (k.exe stores symbol value without backtick)
            var symbolValue = symbol.StartsWith("`") ? symbol.Substring(1) : symbol;
            var symbolData = Encoding.UTF8.GetBytes(symbolValue);
            writer.WriteInt32(4);  // Symbol flag
            writer.WriteBytes(symbolData); // Symbol data
            writer.WriteByte(0); // Null terminator
            
            // No padding added here - handled at list level if needed
            
            return writer.GetBuffer();
        }
        
        private byte[] SerializeRawSymbolData(string symbol)
        {
            var writer = new KBinaryWriter();
            // Remove initial backtick and get raw symbol value
            var symbolValue = symbol.StartsWith("`") ? symbol.Substring(1) : symbol;
            var symbolData = Encoding.UTF8.GetBytes(symbolValue);
            writer.WriteBytes(symbolData); // Just the raw symbol bytes
            writer.WriteByte(0); // Null terminator
            return writer.GetBuffer();
        }
        
        private byte[] SerializeSymbol(string symbol)
        {
            var data = SerializeSymbolData(symbol);
            var message = SerializeMessage(data.Length);
            return message.Concat(data).ToArray();
        }
        
        public byte[] SerializeCharacterVector(string str)
        {
            var chars = str.ToCharArray();
            var writer = new KBinaryWriter();
            
            writer.WriteByte(1);   // Architecture: little-endian
            writer.WriteByte(0);   // Message type: _bd serialization
            writer.WriteInt16(0);  // Reserved: 2 bytes
            writer.WriteInt32(8 + chars.Length + 1); // Length: flag(4) + count(4) + data + null(1)
            writer.WriteInt32(-3); // CharacterVector flag
            writer.WriteInt32(chars.Length); // Element count
            writer.WriteBytes(chars.Select(c => (byte)c).ToArray()); // Character data
            writer.WriteByte(0); // Null terminator
            return writer.ToArray();
        }
        
        private byte[] SerializeNullData()
        {
            var writer = new KBinaryWriter();
            writer.WriteInt32(6);  // Null subtype
            return PadTo8ByteBoundary(writer.GetBuffer());
        }
        
        private byte[] SerializeNull()
        {
            var nullData = SerializeNullData();
            var message = SerializeMessage(nullData.Length);
            return message.Concat(nullData).ToArray();
        }
        
        private byte[] SerializeList(K3CSharp.VectorValue list)
        {
            var listData = SerializeListData(list);
            var message = SerializeMessage(listData.Length);
            return message.Concat(listData).ToArray();
        }
        
        private byte[] SerializeListData(K3CSharp.VectorValue list)
        {
            var elementData = new List<byte>();
            int vectorType = GetVectorType(list);
            
            // For typed vectors (-1, -2, -3, -4), use raw serialization without type headers
            // For mixed lists (0), use full serialization
            bool useRawSerialization = vectorType <= -1 && vectorType >= -4;
            
            foreach (var element in list.Elements)
            {
                if (useRawSerialization)
                {
                    // Use raw serialization for typed vectors
                    byte[] rawData = element switch
                    {
                        K3CSharp.IntegerValue iv => SerializeRawIntegerData(iv.Value),
                        K3CSharp.LongValue lv => SerializeRawIntegerData((int)lv.Value), // Convert long to int for raw serialization
                        K3CSharp.FloatValue fv => SerializeRawFloatData(fv.Value),
                        K3CSharp.CharacterValue cv => SerializeRawCharacterData(cv.Value[0]),
                        K3CSharp.SymbolValue sv => SerializeRawSymbolData(sv.Value),
                        _ => throw new NotSupportedException($"Unsupported element type for raw serialization: {element.GetType()}")
                    };
                    elementData.AddRange(rawData);
                }
                else
                {
                    // Use full serialization for mixed lists
                // Symbols in mixed lists need 4-byte alignment
                var serialized = SerializeElementData(element, true);
                elementData.AddRange(serialized);
                }
            }
            
            if (vectorType == -3)
            {
                elementData.Add(0); // Null terminator
            }
            
            // Padding now handled at element level for leaves and simple vectors
            // Removed complex padding for mixed lists
            
            var writer = new KBinaryWriter();
            writer.WriteInt32(vectorType);
            writer.WriteInt32(list.Elements.Count);
            writer.WriteBytes(elementData.ToArray());
            return writer.ToArray();
        }
        
        private int GetVectorType(K3CSharp.VectorValue list)
        {
            // For empty vectors, use the VectorType property if available
            if (list.Elements.Count == 0)
            {
                return list.VectorType ?? 0; // Use stored type, default to mixed list if not set
            }
            
            // If VectorType is explicitly set, respect it (e.g., forced mixed list)
            if (list.VectorType.HasValue)
            {
                return list.VectorType.Value;
            }
            
            // For non-empty vectors without explicit type, determine from element types
            var firstElement = list.Elements[0];
            if (list.Elements.All(e => e is IntegerValue || e is LongValue))
                return -1; // Integer vector
            else if (list.Elements.All(e => e is FloatValue))
                return -2; // Float vector  
            else if (list.Elements.All(e => e is CharacterValue))
                return -3; // Character vector
            else if (list.Elements.All(e => e is SymbolValue))
                return -4; // Symbol vector
            else
                return 0; // Mixed list
        }
        
        private byte[] SerializeElementData(object element, bool isInMixedList = false)
        {
            return element switch
            {
                // Handle nested lists (for dictionary triplets)
                K3CSharp.VectorValue nestedList => GetVectorType(nestedList) <= -1 && GetVectorType(nestedList) >= -4 ? PadTo8ByteBoundary(SerializeListData(nestedList)) : SerializeListData(nestedList),
                
                // Handle complex types that can be list elements - return only data, not full message
                K3CSharp.DictionaryValue dict => SerializeDictionaryData(dict),
                K3CSharp.FunctionValue func => isInMixedList ? PadTo8ByteBoundary(SerializeAnonymousFunctionData(func)) : SerializeAnonymousFunctionData(func),
                
                // Handle primitive K3Value objects - pad to 8-byte boundary
                K3CSharp.IntegerValue iv => PadTo8ByteBoundary(SerializeIntegerData(iv.Value)),
                K3CSharp.FloatValue fv => PadTo8ByteBoundary(SerializeFloatData(fv.Value)),
                K3CSharp.CharacterValue cv => PadTo8ByteBoundary(SerializeCharacterData(cv.Value[0])),
                K3CSharp.SymbolValue sv => PadTo8ByteBoundary(SerializeSymbolData(sv.Value, isInMixedList)),
                K3CSharp.NullValue => PadTo8ByteBoundary(SerializeNullData()),
                
                _ => throw new NotSupportedException($"Unsupported element type: {element.GetType()}")
            };
        }
        
        private byte[] SerializeDictionary(K3CSharp.DictionaryValue dict)
        {
            var dictData = SerializeDictionaryData(dict);
            var message = SerializeMessage(dictData.Length);
            return message.Concat(dictData).ToArray();
        }
        
        private byte[] SerializeDictionaryData(K3CSharp.DictionaryValue dict)
        {
            // Convert dictionary to list of triplets using unmake functionality
            var triplets = new List<K3CSharp.K3Value>();
            
            foreach (var kvp in dict.Entries)
            {
                var key = kvp.Key;
                var (value, attr) = kvp.Value;
                
                // Create triplet as VectorValue (mixed list) with 3 elements: (key; value; attribute)
                var attribute = attr as K3CSharp.K3Value ?? new K3CSharp.NullValue();
                var tripletElements = new List<K3CSharp.K3Value> { key, value, attribute };
                var tripletList = new K3CSharp.VectorValue(tripletElements);
                
                // Add triplet as element to main list
                triplets.Add(tripletList);
            }
            
            // Create list from triplets (type 0 list)
            var tripletListContainer = new K3CSharp.VectorValue(triplets);
            
            // Serialize as list data first
            var listData = SerializeListData(tripletListContainer);
            
            // Change type from 0 (list) to 5 (dictionary)
            if (listData.Length >= 4)
            {
                listData[0] = 5; // Dictionary flag instead of list flag
            }
            
            return listData;
        }
        
        private byte[] SerializeAnonymousFunction(K3CSharp.FunctionValue func)
        {
            var functionData = SerializeAnonymousFunctionData(func);
            var message = SerializeMessage(functionData.Length);
            return message.Concat(functionData).ToArray();
        }
        
        private byte[] SerializeAnonymousFunctionData(K3CSharp.FunctionValue func)
        {
            var writer = new KBinaryWriter();
            // Use the original source text if available, otherwise fall back to reconstructed text
            var functionSourceText = !string.IsNullOrEmpty(func.OriginalSourceText) 
                ? func.OriginalSourceText 
                : "{" + "[" + string.Join(";", func.Parameters) + "]" + func.BodyText + "}";
            
            // Check for undefined variables (k.exe adds .k metadata when there are parsing errors)
            var hasUndefinedVariables = HasUndefinedVariables(func);
            
            var functionSource = Encoding.UTF8.GetBytes(functionSourceText);
            
            writer.WriteInt32(10); // Function flag
            
            // Add .k metadata if there are undefined variables (like k.exe does)
            if (hasUndefinedVariables)
            {
                // k.exe writes exactly 3 bytes: ".k\0"
                var metadata = Encoding.ASCII.GetBytes(".k\0");
                writer.WriteBytes(metadata);
            }
            else
            {
                writer.WriteByte(0); // Extra null byte only when no metadata
            }
            
            writer.WriteBytes(functionSource); // Function source
            writer.WriteByte(0); // Null terminator
            
            return writer.ToArray();
        }
        
        private bool HasUndefinedVariables(K3CSharp.FunctionValue func)
        {
            var parameterSet = new HashSet<string>(func.Parameters, StringComparer.Ordinal);
            
            // If PreParsedTokens are available, use them
            if (func.PreParsedTokens != null && func.PreParsedTokens.Count > 0)
            {
                foreach (var token in func.PreParsedTokens)
                {
                    // Check for identifiers that are not parameters and not keywords
                    if (token.Type == TokenType.IDENTIFIER && 
                        !parameterSet.Contains(token.Lexeme) &&
                        !IsKKeyword(token.Lexeme))
                    {
                        return true; // Found undefined variable
                    }
                }
                return false;
            }
            
            // Fallback: parse the source text to detect undefined variables
            var functionSourceText = !string.IsNullOrEmpty(func.OriginalSourceText) 
                ? func.OriginalSourceText 
                : "{" + "[" + string.Join(";", func.Parameters) + "]" + func.BodyText + "}";
            
            // Extract the body part (inside the braces)
            var bodyStart = functionSourceText.IndexOf('[');
            var bodyEnd = functionSourceText.LastIndexOf('}');
            
            if (bodyStart >= 0 && bodyEnd > bodyStart)
            {
                var bodyText = functionSourceText.Substring(bodyStart, bodyEnd - bodyStart);
                
                // Extract identifiers by finding alphabetic sequences
                // Skip anything that starts with a digit or is an operator
                var identifiers = new List<string>();
                var currentIdentifier = new StringBuilder();
                
                foreach (char c in bodyText)
                {
                    if (char.IsLetter(c) || (currentIdentifier.Length > 0 && char.IsDigit(c)))
                    {
                        currentIdentifier.Append(c);
                    }
                    else if (currentIdentifier.Length > 0)
                    {
                        identifiers.Add(currentIdentifier.ToString());
                        currentIdentifier.Clear();
                    }
                }
                
                if (currentIdentifier.Length > 0)
                {
                    identifiers.Add(currentIdentifier.ToString());
                }
                
                foreach (var identifier in identifiers)
                {
                    // Skip if it's a keyword, number, or empty
                    if (IsKKeyword(identifier) || string.IsNullOrEmpty(identifier))
                        continue;
                    
                    // Skip if it's purely numeric
                    if (double.TryParse(identifier, out _))
                        continue;
                    
                    // Check if it's an identifier not in parameters
                    if (!parameterSet.Contains(identifier))
                    {
                        return true; // Found potential undefined variable
                    }
                }
            }
            
            return false;
        }
        
        private bool IsKKeyword(string identifier)
        {
            // K keywords that should not be considered undefined variables
            var keywords = new HashSet<string>
            {
                "if", "do", "while", "for", "select", "exec", "exit", "return",
                "_in", "_sv", "_vs", "_ssr", "_bd", "_db", "_getenv", "_setenv",
                "_host", "_size", "_exit", "_d", "_cd", "_env", "_pid", "_uid",
                "_gid", "_time", "_date", "_year", "_month", "_day", "_hour",
                "_minute", "_second", "_millis", "_unix", "_gmt", "_local",
                "_abs", "_neg", "_sqrt", "_log", "_exp", "_sin", "_cos", "_tan",
                "_asin", "_acos", "_atan", "_floor", "_ceil", "_round", "_sign",
                "_recip", "_not", "_null", "_type", "_val", "_fill", "_rand",
                "_first", "_last", "_count", "_sum", "_avg", "_min", "_max",
                "_dev", "_var", "_med", "_mode", "_rank", "_unique", "_distinct",
                "_group", "_by", "_over", "_scan", "_each", "_prior", "_enlist",
                "_flatten", "_reverse", "_sort", "_grade", "_iasc", "_idesc",
                "_cross", "_join", "_union", "_intersect", "_except", "_in",
                "_like", "_match", "_split", "_drop", "_take", "_cut", "_vs",
                "_sv", "_ss", "_si", "_sc", "_sf", "_sl", "_sk", "_sp", "_sw",
                "_st", "_sd", "_sm", "_sn", "_sq", "_sr", "_sx", "_sy", "_sz"
            };
            
            return keywords.Contains(identifier);
        }
    }
}
