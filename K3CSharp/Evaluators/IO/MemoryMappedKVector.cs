// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace K3CSharp
{
    /// <summary>
    /// Memory-mapped K vector implementation for optimized access to fixed-width vectors
    /// Provides lazy loading with O(1) random access for supported vector types
    /// </summary>
    public class MemoryMappedKVector : VectorValue, IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly long _dataOffset;
        private readonly int _elementSize;
        private readonly int _length;
        private readonly bool _isOptimized;
        private bool _disposed = false;
        private List<K3Value> _lazyElements = new List<K3Value>();

        // Cache for frequently accessed elements
        private readonly Dictionary<int, K3Value> _elementCache = new Dictionary<int, K3Value>();
        private const int MaxCacheSize = 1000;

        public MemoryMappedKVector(string filePath, int vectorType, int length) 
            : base(new List<K3Value>(), vectorType)
        {
            _length = length;
            // VectorType is set by base constructor, no need to set it here
            
            // Determine if this type can be optimized
            _isOptimized = IsOptimizableType(vectorType);
            
            if (_isOptimized)
            {
                _elementSize = GetElementSize(vectorType);
                _dataOffset = 16; // Skip 8-byte _bd header + 4-byte type + 4-byte length
                
                // Create memory-mapped file
                _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
                _accessor = _mmf.CreateViewAccessor();
                
                // Validate that we can read vector type from file
                ValidateVectorTypeFromFile();
            }
            else
            {
                // For non-optimized types, we'll need to load the entire vector
                throw new NotSupportedException($"Vector type {vectorType} is not optimized for memory-mapped access");
            }
        }

        /// <summary>
        /// Get the element at specified index with lazy loading
        /// </summary>
        public K3Value GetElement(int index)
        {
            if (index < 0 || index >= _length)
                throw new IndexOutOfRangeException($"Index {index} is out of range for vector of length {_length}");

            // Check cache first
            if (_elementCache.ContainsKey(index))
                return _elementCache[index];

            K3Value element = new IntegerValue(0); // Default value, will be replaced

            if (_isOptimized)
            {
                element = ReadElementFromMemoryMap(index);
            }
            else
            {
                // This should not happen as we throw in constructor for non-optimized types
                throw new InvalidOperationException("Attempted to access non-optimized memory-mapped vector");
            }

            // Cache the element if cache is not full
            if (_elementCache.Count < MaxCacheSize)
            {
                _elementCache[index] = element;
            }

            return element;
        }

        /// <summary>
        /// Get all elements (for compatibility with existing VectorValue interface)
        /// Note: This will load all elements into memory, losing optimization benefits
        /// </summary>
        public new List<K3Value> Elements
        {
            get
            {
                if (_lazyElements.Count == 0)
                {
                    for (int i = 0; i < _length; i++)
                    {
                        _lazyElements.Add(GetElement(i));
                    }
                }
                return _lazyElements;
            }
        }

        /// <summary>
        /// Get the count of elements in the vector
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// Check if this vector type can be optimized with memory mapping
        /// </summary>
        private bool IsOptimizableType(int vectorType)
        {
            return vectorType switch
            {
                -1 => true,  // Integer vector
                -2 => true,  // Float vector  
                -3 => true,  // Character vector
                -64 => true, // Long vector
                _ => false
            };
        }

        /// <summary>
        /// Get the size in bytes for each element of the specified vector type
        /// </summary>
        private int GetElementSize(int vectorType)
        {
            return vectorType switch
            {
                -1 => 4,   // Integer: 4 bytes
                -2 => 8,   // Float: 8 bytes (double)
                -3 => 2,   // Character: 2 bytes (UTF-16)
                -64 => 8,  // Long: 8 bytes
                _ => throw new ArgumentException($"Unsupported vector type: {vectorType}")
            };
        }

        /// <summary>
        /// Read an element directly from the memory-mapped file
        /// </summary>
        private K3Value ReadElementFromMemoryMap(int index)
        {
            long position = _dataOffset + (index * _elementSize);

            return VectorType switch
            {
                -1 => new IntegerValue(_accessor.ReadInt32(position)),           // Integer
                -2 => new FloatValue(_accessor.ReadDouble(position)),         // Float  
                -3 => new CharacterValue(_accessor.ReadChar(position).ToString()), // Character
                -64 => new LongValue(_accessor.ReadInt64(position)),          // Long
                _ => throw new InvalidOperationException($"Unsupported vector type: {VectorType}")
            };
        }

        /// <summary>
        /// Validate that the file contains the expected vector type
        /// </summary>
        private void ValidateVectorTypeFromFile()
        {
            // The vector type is stored at offset 8 (after the 8-byte _bd header)
            int fileVectorType = _accessor.ReadInt32(8);
            
            if (fileVectorType != VectorType)
            {
                throw new InvalidOperationException($"File contains vector type {fileVectorType}, but expected {VectorType}");
            }

            // Validate the length matches
            int fileLength = _accessor.ReadInt32(12);
            if (fileLength != _length)
            {
                throw new InvalidOperationException($"File contains vector of length {fileLength}, but expected {_length}");
            }
        }

        /// <summary>
        /// Dispose of memory-mapped file resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _accessor?.Dispose();
                _mmf?.Dispose();
                _elementCache.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are cleaned up
        /// </summary>
        ~MemoryMappedKVector()
        {
            Dispose();
        }
    }
}