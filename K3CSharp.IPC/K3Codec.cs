using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace K3CSharp.IPC
{
    /// <summary>
    /// Message type byte sent in the 4th header byte of a K3 IPC frame.
    /// </summary>
    /// <remarks>
    /// <para>Values correspond to the K3 built-ins:</para>
    /// <list type="bullet">
    ///   <item><description><see cref="Set"/> (0) — async request, dispatched to <c>.m.s</c> in k.</description></item>
    ///   <item><description><see cref="Get"/> (1) — synchronous request, dispatched to <c>.m.g</c>.</description></item>
    ///   <item><description><see cref="Response"/> (2) — response to a <see cref="Get"/>.</description></item>
    /// </list>
    /// </remarks>
    public static class K3MsgType
    {
        /// <summary>Async "set" message (<c>3:</c> on the k side).</summary>
        public const byte Set = 0;

        /// <summary>Synchronous "get" message (<c>4:</c> on the k side).</summary>
        public const byte Get = 1;

        /// <summary>Response to a previous <see cref="Get"/>.</summary>
        public const byte Response = 2;
    }

    /// <summary>
    /// Type tag codes used in the K3 IPC wire format. The negated form of a
    /// scalar tag indicates a homogeneous list (vector) of that scalar type.
    /// </summary>
    public static class K3Type
    {
        /// <summary>Heterogeneous list.</summary>
        public const int Lst = 0;
        /// <summary>32-bit integer.</summary>
        public const int Int = 1;
        /// <summary>64-bit float (double).</summary>
        public const int Flt = 2;
        /// <summary>Single character (byte).</summary>
        public const int Chr = 3;
        /// <summary>Symbol.</summary>
        public const int Sym = 4;
        /// <summary>Dictionary.</summary>
        public const int Dct = 5;
        /// <summary>Null (<c>::</c>).</summary>
        public const int Nul = 6;
        /// <summary>Function.</summary>
        public const int Fun = 7;
    }

    /// <summary>
    /// Parsed K3 IPC message header (8 bytes total on the wire).
    /// </summary>
    public readonly struct K3MessageHeader
    {
        /// <summary>True if the message is little-endian; false for big-endian.</summary>
        public bool IsLittleEndian { get; }

        /// <summary>The <see cref="K3MsgType"/> value.</summary>
        public byte MsgType { get; }

        /// <summary>Length of the message body in bytes (excluding the header).</summary>
        public int MsgLength { get; }

        /// <summary>Construct a new header descriptor.</summary>
        public K3MessageHeader(bool isLittleEndian, byte msgType, int msgLength)
        {
            IsLittleEndian = isLittleEndian;
            MsgType = msgType;
            MsgLength = msgLength;
        }
    }

    /// <summary>
    /// Encoder/decoder for the K3 IPC wire format. This is a direct C# port of
    /// the <c>k3ipc-go</c> reference implementation.
    /// </summary>
    public static class K3Codec
    {
        /// <summary>The header is always 8 bytes long.</summary>
        public const int HeaderSize = 8;

        // ----------------------------------------------------------------
        //   Encoding
        // ----------------------------------------------------------------

        /// <summary>
        /// Encode a K3 value as an IPC message with <see cref="K3MsgType.Set"/>.
        /// Use <see cref="K3Msg"/> if you need a different message type.
        /// </summary>
        public static byte[] Bd(object? value)
        {
            using var ms = new MemoryStream();
            // Header placeholder: endianFlag=1 (little), 0, 0, msgType=0, msgLen(4 bytes)=0
            ms.WriteByte(1);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);

            int dLen = EmitBd(ms, value);

            byte[] bytes = ms.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), dLen);
            return bytes;
        }

        /// <summary>
        /// Encode a K3 value as a full IPC message with the specified message type.
        /// </summary>
        public static byte[] K3Msg(object? value, byte msgType)
        {
            byte[] res = Bd(value);
            res[3] = msgType;
            return res;
        }

        private static int EmitBd(Stream stream, object? value)
        {
            switch (value)
            {
                case null:
                    WriteInt32LE(stream, K3Type.Nul);
                    WriteInt32LE(stream, 0);
                    return 8;

                case int i32:
                    WriteInt32LE(stream, K3Type.Int);
                    WriteInt32LE(stream, i32);
                    return 8;

                case long i64:
                    // Narrow to int32 like the reference implementation does for Go's int type.
                    if (i64 > int.MaxValue || i64 < int.MinValue)
                    {
                        throw new OverflowException(
                            $"K3 IPC only supports 32-bit integers; value {i64} is out of range.");
                    }
                    WriteInt32LE(stream, K3Type.Int);
                    WriteInt32LE(stream, (int)i64);
                    return 8;

                case double d:
                    WriteInt32LE(stream, K3Type.Flt);
                    // k adds an extra int here to keep the payload 64-bit aligned.
                    WriteInt32LE(stream, 1);
                    WriteInt64LE(stream, BitConverter.DoubleToInt64Bits(d));
                    return 16;

                case byte b:
                    WriteInt32LE(stream, K3Type.Chr);
                    // KCHR is always padded to 4 bytes.
                    stream.WriteByte(b);
                    stream.WriteByte(0);
                    stream.WriteByte(0);
                    stream.WriteByte(0);
                    return 8;

                case string s:
                {
                    byte[] utf8 = Encoding.UTF8.GetBytes(s);
                    WriteInt32LE(stream, -K3Type.Chr);
                    WriteInt32LE(stream, utf8.Length);
                    stream.Write(utf8, 0, utf8.Length);
                    stream.WriteByte(0);
                    return 8 + utf8.Length + 1;
                }

                case KSym sym:
                {
                    byte[] utf8 = Encoding.UTF8.GetBytes(sym.Name);
                    WriteInt32LE(stream, K3Type.Sym);
                    stream.Write(utf8, 0, utf8.Length);
                    stream.WriteByte(0);
                    return 4 + utf8.Length + 1;
                }

                case KSym[] syms:
                {
                    WriteInt32LE(stream, -K3Type.Sym);
                    WriteInt32LE(stream, syms.Length);
                    int totalStrLen = 0;
                    foreach (KSym s in syms)
                    {
                        byte[] utf8 = Encoding.UTF8.GetBytes(s.Name);
                        stream.Write(utf8, 0, utf8.Length);
                        stream.WriteByte(0);
                        totalStrLen += utf8.Length + 1;
                    }
                    return 8 + totalStrLen;
                }

                case object?[] list:
                {
                    WriteInt32LE(stream, K3Type.Lst);
                    WriteInt32LE(stream, list.Length);
                    int dLen = 8;
                    foreach (object? item in list)
                    {
                        dLen += EmitBd(stream, item);
                        // Pad each element to an 8-byte boundary. Because the
                        // outer header is exactly 8 bytes, using stream.Position
                        // and data length are equivalent for alignment.
                        while (stream.Position % 8 != 0)
                        {
                            stream.WriteByte(0);
                            dLen++;
                        }
                    }
                    return dLen;
                }

                case IReadOnlyDictionary<string, object?> dict:
                {
                    WriteInt32LE(stream, K3Type.Dct);
                    WriteInt32LE(stream, dict.Count);
                    int dLen = 8;
                    foreach (KeyValuePair<string, object?> kvp in dict)
                    {
                        // Each entry is encoded as a 3-element list: [key, value, attrs].
                        // Attributes are unused and always sent as null.
                        var triple = new object?[] { new KSym(kvp.Key), kvp.Value, null };
                        dLen += EmitBd(stream, triple);
                    }
                    return dLen;
                }

                default:
                    throw new ArgumentException(
                        $"Bd: cannot serialize value of type {value.GetType()}",
                        nameof(value));
            }
        }

        // ----------------------------------------------------------------
        //   Decoding
        // ----------------------------------------------------------------

        /// <summary>
        /// Decode a K3 IPC message (header + body) and return the contained value.
        /// </summary>
        public static object? Db(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int pos = 0;
            K3MessageHeader hdr = ParseMessageHeader(buffer, ref pos);
            return ReadValue(buffer, ref pos, hdr.IsLittleEndian, align: false);
        }

        /// <summary>
        /// Parse an 8-byte K3 IPC message header starting at <paramref name="offset"/>.
        /// On return, <paramref name="offset"/> is advanced past the header.
        /// </summary>
        public static K3MessageHeader ParseMessageHeader(byte[] buffer, ref int offset)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + HeaderSize > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset),
                    "Buffer is too small to contain a K3 IPC header.");
            }

            byte endianFlag = buffer[offset + 0];
            // buffer[offset + 1] and buffer[offset + 2] are reserved/ignored.
            byte msgType = buffer[offset + 3];
            bool isLittle = endianFlag == 1;
            int msgLen = isLittle
                ? BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset + 4, 4))
                : BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(offset + 4, 4));
            offset += HeaderSize;
            return new K3MessageHeader(isLittle, msgType, msgLen);
        }

        /// <summary>
        /// Parse an 8-byte K3 IPC header from the first bytes of a buffer.
        /// </summary>
        public static K3MessageHeader ParseMessageHeader(byte[] buffer)
        {
            int pos = 0;
            return ParseMessageHeader(buffer, ref pos);
        }

        private static object? ReadValue(byte[] buf, ref int pos, bool isLittle, bool align)
        {
            int dataType = ReadInt32(buf, ref pos, isLittle);
            int count = 0;
            if (dataType <= 0 || dataType == K3Type.Dct)
            {
                count = ReadInt32(buf, ref pos, isLittle);
            }

            switch (dataType)
            {
                case K3Type.Int:
                    return ReadInt32(buf, ref pos, isLittle);

                case K3Type.Flt:
                {
                    ReadInt32(buf, ref pos, isLittle); // 4-byte pad
                    long bits = ReadInt64(buf, ref pos, isLittle);
                    return BitConverter.Int64BitsToDouble(bits);
                }

                case K3Type.Chr:
                {
                    byte ch = buf[pos++];
                    if (align)
                    {
                        pos += 3; // KCHR padding
                    }
                    return ch;
                }

                case -K3Type.Chr:
                {
                    string s = Encoding.UTF8.GetString(buf, pos, count);
                    pos += count;
                    pos++; // null terminator
                    AlignIfNecessary(ref pos, count + 1, align);
                    return s;
                }

                case K3Type.Sym:
                {
                    int strStart = pos;
                    string sym = ReadCString(buf, ref pos);
                    // Alignment is based on the byte count of (dataType + string + \0).
                    // ReadCString consumes (string_bytes + 1 null terminator).
                    int consumed = 4 + (pos - strStart);
                    AlignIfNecessary(ref pos, consumed, align);
                    return new KSym(sym);
                }

                case -K3Type.Sym:
                {
                    var syms = new KSym[count];
                    for (int i = 0; i < count; i++)
                    {
                        syms[i] = new KSym(ReadCString(buf, ref pos));
                    }
                    return syms;
                }

                case K3Type.Nul:
                    // Matches the Go reference exactly: only the 4-byte type
                    // tag is consumed. The wire format pads K3NUL out to 8
                    // bytes but k3ipc-go does not skip the trailing 4 bytes
                    // here either, and all known callers tolerate the gap.
                    return null;

                case K3Type.Lst:
                {
                    var items = new object?[count];
                    for (int i = 0; i < count; i++)
                    {
                        items[i] = ReadValue(buf, ref pos, isLittle, align: true);
                    }
                    return items;
                }

                case K3Type.Dct:
                {
                    var dict = new Dictionary<string, object?>(count);
                    for (int i = 0; i < count; i++)
                    {
                        // Each entry is [key, value, attrs].
                        object? entry = ReadValue(buf, ref pos, isLittle, align: false);
                        if (entry is object?[] kva && kva.Length >= 2 && kva[0] is KSym keySym)
                        {
                            dict[keySym.Name] = kva[1];
                        }
                        else
                        {
                            throw new InvalidDataException(
                                $"K3 IPC dict entry {i} is malformed.");
                        }
                    }
                    return dict;
                }

                default:
                    throw new NotImplementedException(
                        $"K3 IPC decoder: unsupported data type {dataType}.");
            }
        }

        private static string ReadCString(byte[] buf, ref int pos)
        {
            int start = pos;
            while (pos < buf.Length && buf[pos] != 0)
            {
                pos++;
            }
            string s = Encoding.UTF8.GetString(buf, start, pos - start);
            if (pos < buf.Length) pos++; // consume the null terminator
            return s;
        }

        private static void AlignIfNecessary(ref int pos, int dLen, bool align)
        {
            if (align && (dLen % 8 != 0))
            {
                pos += 8 - (dLen % 8);
            }
        }

        private static int ReadInt32(byte[] buf, ref int pos, bool isLittle)
        {
            int v = isLittle
                ? BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(pos, 4))
                : BinaryPrimitives.ReadInt32BigEndian(buf.AsSpan(pos, 4));
            pos += 4;
            return v;
        }

        private static long ReadInt64(byte[] buf, ref int pos, bool isLittle)
        {
            long v = isLittle
                ? BinaryPrimitives.ReadInt64LittleEndian(buf.AsSpan(pos, 8))
                : BinaryPrimitives.ReadInt64BigEndian(buf.AsSpan(pos, 8));
            pos += 8;
            return v;
        }

        private static void WriteInt32LE(Stream s, int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            s.Write(bytes);
        }

        private static void WriteInt64LE(Stream s, long value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
            s.Write(bytes);
        }

        // ----------------------------------------------------------------
        //   Debug / test helpers
        // ----------------------------------------------------------------

        /// <summary>
        /// Parse a string of space-separated decimal byte values (as used in the
        /// Go reference test suite) into a byte array.
        /// </summary>
        public static byte[] NumStrToBytes(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
            string[] parts = s.Split(' ');
            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                bytes[i] = (byte)int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            }
            return bytes;
        }

        /// <summary>
        /// Render a byte array as space-separated decimal values, matching the
        /// format used by the Go reference test suite.
        /// </summary>
        public static string BytesToNumStr(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            var parts = new string[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                parts[i] = bytes[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return string.Join(" ", parts);
        }
    }
}
