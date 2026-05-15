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
    /// Utility class for memory-mapped file operations in K data reading
    /// </summary>
    public static class MemoryMappedFileUtils
    {
        /// <summary>
        /// Expected K data file header: FD FF FF FF 01 00 00 00
        /// </summary>
        private static readonly byte[] ExpectedFileHeader = new byte[] { 0xFD, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00 };

        /// <summary>
        /// Validate that a file is a valid K data file and extract vector information
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>Tuple containing (isValid, vectorType, length)</returns>
        public static (bool isValid, int vectorType, int length) ValidateKDataFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"The system cannot find file specified: {filePath}");

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fileStream);

                // Validate file header (first 8 bytes)
                byte[] fileHeader = reader.ReadBytes(8);
                if (fileHeader.Length < 8)
                    return (false, 0, 0);

                for (int i = 0; i < ExpectedFileHeader.Length; i++)
                {
                    if (fileHeader[i] != ExpectedFileHeader[i])
                        return (false, 0, 0);
                }

                // Read vector type and length from the _bd message header
                // _bd message format: 01 00 00 00 [4-byte type] [4-byte length] [data...]
                reader.ReadBytes(4); // Skip _bd message header (01 00 00 00)
                int vectorType = reader.ReadInt32();
                int length = reader.ReadInt32();

                return (true, vectorType, length);
            }
            catch (Exception)
            {
                return (false, 0, 0);
            }
        }

        /// <summary>
        /// Check if a vector type can be optimized with memory mapping
        /// </summary>
        /// <param name="vectorType">The vector type to check</param>
        /// <returns>True if the type can be optimized</returns>
        public static bool IsOptimizableType(int vectorType)
        {
            return vectorType switch
            {
                -1 => true,   // Integer vector: 4 bytes per element
                -2 => true,   // Float vector: 8 bytes per element
                -3 => true,   // Character vector: 2 bytes per element
                -64 => true,  // Long vector: 8 bytes per element
                _ => false
            };
        }

        /// <summary>
        /// Get the element size in bytes for a given vector type
        /// </summary>
        /// <param name="vectorType">The vector type</param>
        /// <returns>Size in bytes of each element</returns>
        public static int GetElementSize(int vectorType)
        {
            return vectorType switch
            {
                -1 => 4,    // Integer: 4 bytes
                -2 => 8,    // Float: 8 bytes (double precision)
                -3 => 2,    // Character: 2 bytes (UTF-16)
                -64 => 8,   // Long: 8 bytes
                _ => throw new ArgumentException($"Unsupported vector type for memory mapping: {vectorType}")
            };
        }

        /// <summary>
        /// Calculate the file offset for a specific element index in a memory-mapped vector
        /// </summary>
        /// <param name="index">Element index (0-based)</param>
        /// <param name="vectorType">Type of vector</param>
        /// <returns>Byte offset from start of file</returns>
        public static long CalculateElementOffset(int index, int vectorType)
        {
            // File layout: [8-byte file header] [8-byte _bd header] [4-byte type] [4-byte length] [data...]
            // Data starts at offset 8 + 8 = 16 bytes
            const long dataStartOffset = 16;
            int elementSize = GetElementSize(vectorType);
            
            return dataStartOffset + (index * elementSize);
        }

        /// <summary>
        /// Create a memory-mapped file view accessor for a K data file
        /// </summary>
        /// <param name="filePath">Path to the K data file</param>
        /// <returns>MemoryMappedViewAccessor for the file</returns>
        public static MemoryMappedViewAccessor CreateMemoryMappedAccessor(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The system cannot find file specified: {filePath}");

            var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
            return mmf.CreateViewAccessor();
        }

        /// <summary>
        /// Safely create and manage a memory-mapped file with proper disposal
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Tuple of (MemoryMappedFile, MemoryMappedViewAccessor)</returns>
        public static (MemoryMappedFile mmf, MemoryMappedViewAccessor accessor) CreateMemoryMappedFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The system cannot find file specified: {filePath}");

            var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
            var accessor = mmf.CreateViewAccessor();
            
            return (mmf, accessor);
        }

        /// <summary>
        /// Get vector type information from a K data file without fully loading it
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>VectorTypeInfo with type and length information</returns>
        public static VectorTypeInfo GetVectorTypeInfo(string filePath)
        {
            var (isValid, vectorType, length) = ValidateKDataFile(filePath);
            
            if (!isValid)
                throw new InvalidOperationException("Invalid K data file");

            return new VectorTypeInfo
            {
                VectorType = vectorType,
                Length = length,
                IsOptimizable = IsOptimizableType(vectorType),
                ElementSize = IsOptimizableType(vectorType) ? GetElementSize(vectorType) : 0
            };
        }
    }

    /// <summary>
    /// Information about a vector type in a K data file
    /// </summary>
    public class VectorTypeInfo
    {
        public int VectorType { get; set; }
        public int Length { get; set; }
        public bool IsOptimizable { get; set; }
        public int ElementSize { get; set; }
    }
}