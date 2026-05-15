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
    /// Binary writer for K serialization format with little-endian encoding
    /// </summary>
    public class KBinaryWriter
    {
        private readonly List<byte> buffer = new List<byte>();
        
        public void WriteInt32(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                buffer.AddRange(bytes);
            }
            else
            {
                // Reverse for big-endian systems
                buffer.AddRange(bytes.Reverse());
            }
        }
        
        public void WriteDouble(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                buffer.AddRange(bytes);
            }
            else
            {
                // Reverse for big-endian systems
                buffer.AddRange(bytes.Reverse());
            }
        }
        
        public void WriteBytes(byte[] value)
        {
            buffer.AddRange(value);
        }
        
        public void WriteString(string value)
        {
            buffer.AddRange(Encoding.UTF8.GetBytes(value));
        }
        
        public void WriteByte(byte value)
        {
            buffer.Add(value);
        }
        
        public void WriteInt16(short value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                buffer.AddRange(bytes);
            }
            else
            {
                // Reverse for big-endian systems
                buffer.AddRange(bytes.Reverse());
            }
        }
        
        public void WritePadding(int count)
        {
            for (int i = 0; i < count; i++)
            {
                buffer.Add(0);
            }
        }
        
        public int Count => buffer.Count;
        
        public byte[] GetBuffer() => buffer.ToArray();
        
        public byte[] ToArray() => buffer.ToArray();
        
        public int Length => buffer.Count;
    }
}