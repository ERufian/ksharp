using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace K3CSharp.IPC
{
    /// <summary>
    /// TCP client for talking to a running K3 instance over its IPC protocol.
    /// </summary>
    /// <remarks>
    /// <para>The standard K3 interpreter listens on port 5000 by default. It
    /// dispatches synchronous <c>Get</c> messages to the <c>.m.g</c> function
    /// and asynchronous <c>Set</c> messages to <c>.m.s</c>.</para>
    /// <para>This client mirrors the one embedded in the <c>k3ipc-go</c>
    /// reference implementation: <see cref="Query"/> sends a synchronous
    /// request and returns the decoded response, while <see cref="Send"/> is
    /// used for one-way messages.</para>
    /// </remarks>
    public sealed class K3IpcClient : IDisposable
    {
        /// <summary>The default port used by a K3 interpreter for IPC.</summary>
        public const int DefaultK3Port = 5000;

        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private bool _disposed;

        /// <summary>Connect to the K3 instance at <paramref name="host"/>:<paramref name="port"/>.</summary>
        public K3IpcClient(string host, int port = DefaultK3Port)
        {
            _tcp = new TcpClient();
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();
        }

        /// <summary>
        /// Wrap an already-connected <see cref="TcpClient"/>. The client takes
        /// ownership of the underlying connection and will close it on dispose.
        /// </summary>
        public K3IpcClient(TcpClient tcp)
        {
            _tcp = tcp ?? throw new ArgumentNullException(nameof(tcp));
            _stream = _tcp.GetStream();
        }

        /// <summary>
        /// Send a message without waiting for a response.
        /// </summary>
        public void Send(object? value, byte msgType = K3MsgType.Set)
        {
            ThrowIfDisposed();
            byte[] bytes = K3Codec.K3Msg(value, msgType);
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush();
        }

        /// <summary>
        /// Send a synchronous GET request and wait for the decoded response.
        /// </summary>
        /// <remarks>
        /// On the k side, a GET message is dispatched to <c>.m.g</c>, which
        /// in the reference <c>kipc.k</c> evaluates the request and stringifies
        /// the result. The response is conventionally a 2-element list
        /// <c>(status; value)</c>, where <c>status = 0</c> indicates success.
        /// </remarks>
        public object? Query(object? value)
        {
            ThrowIfDisposed();
            Send(value, K3MsgType.Get);
            return Receive().Value;
        }

        /// <summary>
        /// Read one full message from the connection and return its header
        /// together with the decoded value.
        /// </summary>
        public (K3MessageHeader Header, object? Value) Receive()
        {
            ThrowIfDisposed();
            byte[] frame = ReadMessageFrame(_stream);
            int pos = 0;
            K3MessageHeader hdr = K3Codec.ParseMessageHeader(frame, ref pos);
            object? value = pos < frame.Length
                ? DecodeFrame(frame)
                : null;
            return (hdr, value);
        }

        /// <summary>
        /// Read one complete IPC frame (8-byte header + body) from a stream.
        /// </summary>
        public static byte[] ReadMessageFrame(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] header = new byte[K3Codec.HeaderSize];
            ReadExactly(stream, header, 0, header.Length);

            int tmp = 0;
            K3MessageHeader hdr = K3Codec.ParseMessageHeader(header, ref tmp);
            int total = K3Codec.HeaderSize + hdr.MsgLength;

            byte[] frame = new byte[total];
            Buffer.BlockCopy(header, 0, frame, 0, K3Codec.HeaderSize);
            if (hdr.MsgLength > 0)
            {
                ReadExactly(stream, frame, K3Codec.HeaderSize, hdr.MsgLength);
            }
            return frame;
        }

        /// <summary>Decode a buffer that contains a full IPC frame.</summary>
        public static object? DecodeFrame(byte[] frame) => K3Codec.Db(frame);

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, offset + total, count - total);
                if (n <= 0)
                {
                    throw new EndOfStreamException(
                        $"Connection closed after {total} of {count} expected bytes.");
                }
                total += n;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(K3IpcClient));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _stream.Dispose(); } catch { /* best effort */ }
            try { _tcp.Close(); } catch { /* best effort */ }
            _tcp.Dispose();
        }

        /// <summary>
        /// Open a connection, send a single synchronous request, and return the response.
        /// </summary>
        public static object? SendQuery(string host, int port, object? value,
                                        CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var client = new K3IpcClient(host, port);
            return client.Query(value);
        }

        /// <summary>Async variant of <see cref="SendQuery"/>.</summary>
        public static Task<object?> SendQueryAsync(string host, int port, object? value,
                                                   CancellationToken cancellationToken = default)
        {
            return Task.Run(() => SendQuery(host, port, value, cancellationToken),
                            cancellationToken);
        }
    }
}
