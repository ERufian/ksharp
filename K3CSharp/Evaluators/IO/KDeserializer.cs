// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace K3CSharp
{
    /// <summary>
    /// Main K deserializer implementing unified serialization pattern
    /// </summary>
    public class KDeserializer
    {
        public object Deserialize(byte[] data)
        {
            if (data.Length < 8) throw new ArgumentException("Invalid data - minimum 8 bytes required");
            
            var reader = new KSerializationReader(data);
            var architecture = reader.ReadByte();    // Byte 0: Architecture (should be 1)
            var messageType = reader.ReadByte();    // Byte 1: Message type (should be 0 for _bd)
            var reserved1 = reader.ReadByte();       // Byte 2: Reserved
            var reserved2 = reader.ReadByte();       // Byte 3: Reserved
            var messageLength = reader.ReadInt32();  // Bytes 4-7: Message Length
            
            // Architecture should be 1 for little-endian
            if (architecture != 1) 
                throw new ArgumentException($"Invalid architecture: {architecture}");
            
            // Message type should be 0 for _bd serialization
            if (messageType != 0) 
                throw new ArgumentException($"Invalid message type: {messageType}");
            
            // Read the actual serialized data payload
            var payloadData = reader.ReadBytes(messageLength);
            var payloadReader = new KSerializationReader(payloadData);
            
            return DeserializeValue(payloadReader);
        }
        
        private object DeserializeScalar(KSerializationReader reader)
        {
            return new IntegerValue(reader.ReadInt32()); // Integer value as K3Value
        }
        
        private object DeserializeFloat(KSerializationReader reader, int typeId)
        {
            // typeId is already read and should be 2
            if (typeId != 2)
                throw new ArgumentException($"Invalid type flag for float: {typeId}");
            
            // Read subtype flag (4 bytes)
            var subtypeFlag = reader.ReadInt32();
            if (subtypeFlag != 1)
                throw new ArgumentException($"Invalid subtype flag for float: {subtypeFlag}");
            
            // Read the actual double value (8 bytes)
            var doubleValue = reader.ReadDouble();
            
            return new FloatValue(doubleValue);
        }
        
        private object DeserializeCharacter(KSerializationReader reader)
        {
            return new CharacterValue(((char)reader.ReadByte()).ToString()); // Character as K3Value
        }
        
        private object DeserializeSymbol(KSerializationReader reader, bool isInMixedList = false)
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
            
            // No padding read here - handled at list level
            
            return new SymbolValue(Encoding.UTF8.GetString(bytes.ToArray()));
        }
        
        private object DeserializeIntegerVector(KSerializationReader reader)
        {
            var elementCount = reader.ReadInt32();
            var elements = new List<int>();
            
            for (int i = 0; i < elementCount; i++)
            {
                elements.Add(reader.ReadInt32());
            }
            
            var vectorElements = elements.Select(x => (K3Value)new IntegerValue(x)).ToList();
            return new VectorValue(vectorElements);
        }
        
        private object DeserializeFloatVector(KSerializationReader reader)
        {
            var elementCount = reader.ReadInt32();
            var elementList = new List<K3Value>();
            
            for (int i = 0; i < elementCount; i++)
            {
                elementList.Add(new FloatValue(reader.ReadDouble()));
            }
            
            return new VectorValue(elementList);
        }
        
        private object DeserializeCharacterVector(KSerializationReader reader)
        {
            var elementCount = reader.ReadInt32();
            var elements = new List<char>();
            
            for (int i = 0; i < elementCount; i++)
            {
                elements.Add((char)reader.ReadByte());
            }
            
            // Read and discard null terminator
            if (reader.HasMoreData) reader.ReadByte();
            
            var vectorElements = elements.Select(x => (K3Value)new CharacterValue(x.ToString())).ToList();
            return new VectorValue(vectorElements);
        }
        
        private object DeserializeSymbolVector(KSerializationReader reader)
        {
            var elementCount = reader.ReadInt32();
            var elements = new List<string>();
            
            // Symbol vectors store symbols concatenated with null terminators
            for (int i = 0; i < elementCount; i++)
            {
                var symbol = reader.ReadString();
                elements.Add(symbol); // Don't add backtick here - SymbolValue.ToString() will add it
            }
            
            var vectorElements = elements.Select(x => (K3Value)new SymbolValue(x)).ToList();
            return new VectorValue(vectorElements);
        }
        
        private object DeserializeList(KSerializationReader reader, bool isInMixedList = false)
        {
            // Note: typeId (listFlag) was already read by DeserializeValue
            // We only need to read the elementCount here
            var elementCount = reader.ReadInt32();
            var elements = new List<K3Value>();
            
            // Simplified deserialization using the same padding rules as serialization
            // Each element is padded to 8-byte boundaries, so we can read sequentially
            for (int i = 0; i < elementCount; i++)
            {
                var element = (K3Value)DeserializeValue(reader, isInMixedList);
                elements.Add(element);
                
                // Skip padding to 8-byte boundary after each element
                int currentPos = reader.Position;
                int paddingNeeded = (8 - (currentPos % 8)) % 8;
                if (paddingNeeded > 0 && paddingNeeded < 8)
                {
                    reader.ReadBytes(paddingNeeded);
                }
            }
            
            return new VectorValue(elements);
        }
        
        private int GetElementSize(K3Value element)
        {
            // For mixed lists, we need to calculate the actual serialized size
            // This is complex because elements can be nested structures
            return element switch
            {
                IntegerValue => 8, // 4 bytes type + 4 bytes value
                FloatValue => 12, // 4 bytes type + 8 bytes value
                CharacterValue => 5, // 4 bytes type + 1 byte value
                SymbolValue sv => 5 + sv.Value.Length - 1, // 4 bytes type + symbol data + null
                NullValue => 4, // 4 bytes type only
                VectorValue vv => 8 + GetVectorSize(vv), // 8 bytes header + element data
                DictionaryValue dv => 8 + GetDictionarySize((DictionaryValue)element),
                _ => 8 // Default for complex nested structures
            };
        }
        
        private int GetVectorSize(VectorValue vector)
        {
            int size = 0;
            foreach (var element in vector.Elements)
            {
                size += GetElementSize(element);
            }
            return size;
        }
        
        private int GetDictionarySize(DictionaryValue dict)
        {
            // Simplified - dictionaries are serialized as lists of triplets
            var elements = dict.Entries.SelectMany(kvp => 
            {
                var (val, attr) = kvp.Value;
                var elementList = new List<K3Value>();
                var tripletVector = new VectorValue(new List<K3Value> { kvp.Key, val });
                elementList.Add(tripletVector);
                // Only add attribute if it's not null
                if (attr != null)
                {
                    elementList.Add(attr);
                }
                return elementList;
            }).ToList();
            return GetVectorSize(new VectorValue(elements));
        }
        private object DeserializeDictionary(KSerializationReader reader)
        {
            var elementCount = reader.ReadInt32();
            var entries = new Dictionary<SymbolValue, (K3Value, DictionaryValue?)>();
            
            // Dictionaries are serialized as lists of triplet vectors: [(key;value;attr), (key;value;attr), ...]
            // elementCount is the number of triplet vectors (same as list.Elements.Count in serialization)
            for (int i = 0; i < elementCount; i++)
            {
                // Each element is a triplet vector (key; value; attributes)
                // The triplet is serialized as a list, so deserialize it as such
                var triplet = (K3Value)DeserializeValue(reader, false);
                
                if (triplet is VectorValue vectorValue && vectorValue.Elements.Count >= 2)
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
                        throw new InvalidOperationException($"Dictionary key must be a symbol, got {key.GetType().Name}");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Invalid dictionary triplet format during deserialization.");
                }
            }
            
            return new DictionaryValue(entries);
        }
        
        private object DeserializeNull(KSerializationReader reader)
        {
            // Null data is padded to 8-byte boundaries, skip the padding
            int currentPos = reader.Position;
            int paddingNeeded = (8 - (currentPos % 8)) % 8;
            if (paddingNeeded > 0 && paddingNeeded < 8)
            {
                reader.ReadBytes(paddingNeeded);
            }
            return new NullValue();
        }
        
        private object DeserializeAnonymousFunction(KSerializationReader reader, int typeId, bool isInMixedList = false)
        {
            var hasErrorMetadata = false;
            if (reader.HasMoreData)
            {
                var nextByte = reader.ReadByte();
                if (nextByte == 0x2E) // '.' character
                {
                    // Read the rest of .k metadata
                    var kByte = reader.ReadByte();
                    var nullByte = reader.ReadByte();
                    hasErrorMetadata = (kByte == 0x6B && nullByte == 0x00); // "k\0"
                }
                else if (nextByte == 0x00)
                {
                    // This is the null byte for functions without undefined variables
                    // Continue to read function source
                }
                else
                {
                    // Put the byte back if it's not .k metadata
                    reader.Position--;
                }
            }
            
            // Read function source until null terminator
            var functionBytes = new List<byte>();
            byte b;
            while (reader.HasMoreData && (b = reader.ReadByte()) != 0)
            {
                functionBytes.Add(b);
            }
            var functionSource = Encoding.UTF8.GetString(functionBytes.ToArray());
            
            // Skip padding to 8-byte boundary after function data if in mixed list
            if (isInMixedList)
            {
                int currentPos = reader.Position;
                int paddingNeeded = (8 - (currentPos % 8)) % 8;
                if (paddingNeeded > 0 && paddingNeeded < 8)
                {
                    reader.ReadBytes(paddingNeeded);
                }
            }
            
            return new FunctionValue(functionSource, new List<string>(), null!, functionSource);
        }
        
        private object DeserializeValue(KSerializationReader reader, bool isInMixedList = false)
        {
            // Save current position to peek at type
            var startPos = reader.Position;
            var typeId = reader.ReadInt32();
            
            return typeId switch
            {
                1 => DeserializeScalar(reader),
                2 => DeserializeFloat(reader, typeId),
                3 => DeserializeCharacter(reader),
                4 => DeserializeSymbol(reader, isInMixedList),
                6 => DeserializeNull(reader),
                -1 => DeserializeIntegerVector(reader),
                -2 => DeserializeFloatVector(reader),
                -3 => DeserializeCharacterVector(reader),
                -4 => DeserializeSymbolVector(reader),
                0 => DeserializeList(reader, isInMixedList),
                5 => DeserializeDictionary(reader),
                7 => DeserializeAnonymousFunction(reader, typeId, isInMixedList), // Legacy type 7
                10 => DeserializeAnonymousFunction(reader, typeId, isInMixedList), // Functions use type 10
                _ => throw new NotSupportedException($"Unsupported type: {typeId}")
            };
        }
    }
}