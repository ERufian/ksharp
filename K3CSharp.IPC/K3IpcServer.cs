using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace K3CSharp.IPC
{
    /// <summary>
    /// Callback invoked for every incoming K3 IPC message. Returning a non-null
    /// value causes the server to reply with a <see cref="K3MsgType.Response"/>
    /// frame; returning <c>null</c> leaves the message unanswered (matching
    /// the fire-and-forget semantics of <see cref="K3MsgType.Set"/>).
    /// </summary>
    public delegate object? K3MessageHandler(K3MessageHeader header, object? value);

    /// <summary>
    /// TCP server that accepts K3 IPC connections and dispatches incoming
    /// messages to a user-supplied handler.
    /// </summary>
    /// <remarks>
    /// The reference <c>k3ipc-go</c> server listens on port 1024 and simply
    /// prints incoming messages. This class generalizes that design: supply a
    /// <see cref="K3MessageHandler"/> to customize how each message is handled.
    /// </remarks>
    public sealed class K3IpcServer : IDisposable
    {
        /// <summary>Default listening port used by the reference implementation.</summary>
        public const int DefaultPort = 1024;

        private readonly TcpListener _listener;
        private readonly K3MessageHandler _handler;
        private CancellationTokenSource? _cts;
        private Task? _acceptLoop;
        private bool _disposed;

        /// <summary>
        /// Bind to the given local address/port. Call <see cref="Start"/> to
        /// begin accepting connections.
        /// </summary>
        public K3IpcServer(IPAddress address, int port, K3MessageHandler handler)
        {
            _listener = new TcpListener(address, port);
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>Bind to <see cref="IPAddress.Any"/> on the given port.</summary>
        public K3IpcServer(int port, K3MessageHandler handler)
            : this(IPAddress.Any, port, handler) { }

        /// <summary>The endpoint the server is (or will be) listening on.</summary>
        public IPEndPoint LocalEndPoint =>
            (IPEndPoint)(_listener.LocalEndpoint ?? throw new InvalidOperationException(
                "Server has not been started yet."));

        /// <summary>Start accepting incoming connections in the background.</summary>
        public void Start()
        {
            ThrowIfDisposed();
            if (_acceptLoop != null) throw new InvalidOperationException("Server already started.");
            _cts = new CancellationTokenSource();
            _listener.Start();
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        /// <summary>Stop accepting new connections and wait for existing ones to drain.</summary>
        public void Stop()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { _listener.Stop(); } catch { /* best effort */ }
            try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* best effort */ }
            _cts.Dispose();
            _cts = null;
            _acceptLoop = null;
        }

        private async Task AcceptLoopAsync(CancellationToken cancel)
        {
            while (!cancel.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancel).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    // Listener was stopped.
                    return;
                }

                _ = Task.Run(() => HandleConnection(client, cancel), cancel);
            }
        }

        private void HandleConnection(TcpClient tcp, CancellationToken cancel)
        {
            try
            {
                using (tcp)
                using (NetworkStream stream = tcp.GetStream())
                {
                    while (!cancel.IsCancellationRequested)
                    {
                        byte[] frame;
                        try
                        {
                            frame = K3IpcClient.ReadMessageFrame(stream);
                        }
                        catch (EndOfStreamException)
                        {
                            return; // remote closed
                        }
                        catch (IOException)
                        {
                            return;
                        }

                        int pos = 0;
                        K3MessageHeader header = K3Codec.ParseMessageHeader(frame, ref pos);
                        object? value = K3Codec.Db(frame);

                        object? reply;
                        try
                        {
                            reply = _handler(header, value);
                        }
                        catch (Exception ex)
                        {
                            reply = new object?[]
                            {
                                1, // error status, matches the convention used by .m.g
                                ex.Message,
                            };
                        }

                        if (reply != null || header.MsgType == K3MsgType.Get)
                        {
                            byte[] out_ = K3Codec.K3Msg(reply, K3MsgType.Response);
                            stream.Write(out_, 0, out_.Length);
                            stream.Flush();
                        }
                    }
                }
            }
            catch
            {
                // Swallow per-connection errors so the accept loop can keep going.
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(K3IpcServer));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
