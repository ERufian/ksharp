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
    /// Binary reader for K serialization format with little-endian encoding
    /// </summary>
    public class KSerializationReader
    {
        private readonly byte[] data;
        private int position = 0;
        
        public KSerializationReader(byte[] data)
        {
            this.data = data;
        }
        
        public int ReadInt32()
        {
            if (position + 4 > data.Length) throw new ArgumentException("Insufficient data for Int32");
            var bytes = data[position..(position + 4)];
            position += 4;
            
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToInt32(bytes, 0);
            }
            else
            {
                // Reverse for big-endian systems
                return BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
            }
        }
        
        public double ReadDouble()
        {
            if (position + 8 > data.Length) throw new ArgumentException("Insufficient data for Double");
            var bytes = data[position..(position + 8)];
            position += 8;
            
            if (BitConverter.IsLittleEndian)
            {
                return BitConverter.ToDouble(bytes, 0);
            }
            else
            {
                // Reverse for big-endian systems
                return BitConverter.ToDouble(bytes.Reverse().ToArray(), 0);
            }
        }
        
        public byte[] ReadBytes(int count)
        {
            if (position + count > data.Length) throw new ArgumentException("Insufficient data for bytes");
            var result = data[position..(position + count)];
            position += count;
            return result;
        }
        
        public string ReadString()
        {
            return ReadNullTerminatedString();
        }
        
        public byte ReadByte()
        {
            if (position >= data.Length) throw new ArgumentException("Insufficient data for byte");
            return data[position++];
        }
        
        private string ReadNullTerminatedString()
        {
            var start = position;
            while (position < data.Length && data[position] != 0)
            {
                position++;
            }
            var result = Encoding.UTF8.GetString(data, start, position - start);
            
            // Skip the null terminator if present
            if (position < data.Length && data[position] == 0)
            {
                position++;
            }
            
            return result;
        }
        
        public int Position 
        { 
            get => position; 
            set => position = value; 
        }
        
        public bool HasMoreData => position < data.Length;
    }
}