using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using K3CSharp.IPC;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private const int SelfIpcHandle = int.MaxValue - 1;

        private sealed class IpcRequestContext
        {
            public int Handle { get; init; }
            public IPAddress? RemoteAddress { get; init; }
        }

        private sealed class K3IpcRuntime : IDisposable
        {
            private readonly Evaluator evaluator;
            private readonly TcpListener listener;
            private readonly CancellationTokenSource cts = new();
            private readonly Task acceptLoop;
            private int nextIncomingHandle = 1;
            private bool disposed;

            public K3IpcRuntime(Evaluator evaluator, int port)
            {
                this.evaluator = evaluator;
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                LocalPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                acceptLoop = Task.Run(() => AcceptLoopAsync(cts.Token));
            }

            public int LocalPort { get; }

            private async Task AcceptLoopAsync(CancellationToken cancel)
            {
                while (!cancel.IsCancellationRequested)
                {
                    TcpClient tcpClient;
                    try
                    {
                        tcpClient = await listener.AcceptTcpClientAsync(cancel).ConfigureAwait(false);
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
                        return;
                    }

                    int handle = Interlocked.Increment(ref nextIncomingHandle);
                    _ = Task.Run(() => HandleConnectionAsync(handle, tcpClient, cancel), cancel);
                }
            }

            private async Task HandleConnectionAsync(int handle, TcpClient tcpClient, CancellationToken cancel)
            {
                IPAddress? remoteAddress = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address;
                try
                {
                    using (tcpClient)
                    using (NetworkStream stream = tcpClient.GetStream())
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
                                break;
                            }
                            catch (IOException)
                            {
                                break;
                            }

                            int pos = 0;
                            K3MessageHeader header = K3Codec.ParseMessageHeader(frame, ref pos);
                            object? incoming = K3IpcClient.DecodeFrame(frame);

                            object? reply = evaluator.HandleIncomingIpcMessage(
                                header.MsgType,
                                incoming,
                                handle,
                                remoteAddress);

                            if (reply != null || header.MsgType == K3MsgType.Get)
                            {
                                byte[] outFrame = K3Codec.K3Msg(reply, K3MsgType.Response);
                                await stream.WriteAsync(outFrame, cancel).ConfigureAwait(false);
                                await stream.FlushAsync(cancel).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (K3ExitException)
                {
                    // Exit has already been requested on the evaluator.
                }
                catch
                {
                    // Swallow per-connection exceptions so the listener keeps serving.
                }
                finally
                {
                    evaluator.HandleIncomingIpcClose(handle, remoteAddress);
                }
            }

            private void ThrowIfDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(K3IpcRuntime));
                }
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                cts.Cancel();
                try { listener.Stop(); } catch { }

                try
                {
                    acceptLoop.Wait(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // Best effort shutdown.
                }
                cts.Dispose();
            }
        }

        private static readonly AsyncLocal<IpcRequestContext?> currentIpcRequest = new();
        private readonly object evaluationLock = new();
        private readonly object clientLock = new();
        private readonly ManualResetEventSlim shutdownEvent = new(false);
        private readonly Dictionary<int, K3IpcClient> ipcClients = new();
        private K3IpcRuntime? ipcRuntime;
        private string ipcHost = "127.0.0.1";
        private int nextClientHandle = 1;
        private int? requestedExitCode;

        public bool IsExitRequested => GetRootEvaluator().requestedExitCode.HasValue;

        public int ExitCode => GetRootEvaluator().requestedExitCode ?? 0;

        public bool IsIpcActive => GetRootEvaluator().ipcRuntime != null;

        public WaitHandle GetShutdownWaitHandle()
        {
            return GetRootEvaluator().shutdownEvent.WaitHandle;
        }

        public object GetEvaluationLock()
        {
            return GetRootEvaluator().evaluationLock;
        }

        internal Evaluator GetRootEvaluator()
        {
            Evaluator current = this;
            while (current.parentEvaluator != null)
            {
                current = current.parentEvaluator;
            }

            return current;
        }

        public void StartIpcServer(int port)
        {
            var root = GetRootEvaluator();
            root.StopIpcServer();
            root.shutdownEvent.Reset();
            root.ipcRuntime = new K3IpcRuntime(root, port);
            root.ipcHost = "127.0.0.1";
        }

        public void StopIpcServer()
        {
            var root = GetRootEvaluator();
            root.ipcRuntime?.Dispose();
            root.ipcRuntime = null;
            lock (root.clientLock)
            {
                foreach (var client in root.ipcClients.Values)
                {
                    try { client.Dispose(); } catch { }
                }
                root.ipcClients.Clear();
            }
        }

        public void RequestExit(int exitCode)
        {
            var root = GetRootEvaluator();
            root.requestedExitCode = exitCode;
            root.shutdownEvent.Set();
        }

        public void WaitForShutdown()
        {
            GetRootEvaluator().shutdownEvent.Wait();
        }

        public int GetListeningPort()
        {
            var root = GetRootEvaluator();
            return root.ipcRuntime?.LocalPort ?? 0;
        }

        public string GetIpcHost()
        {
            return GetRootEvaluator().ipcHost;
        }

        private int GetCurrentIncomingHandle()
        {
            return currentIpcRequest.Value?.Handle ?? 0;
        }

        private IPAddress? GetCurrentIncomingAddress()
        {
            return currentIpcRequest.Value?.RemoteAddress;
        }

        private IDisposable PushIpcRequestContext(int handle, IPAddress? remoteAddress)
        {
            IpcRequestContext? prior = currentIpcRequest.Value;
            currentIpcRequest.Value = new IpcRequestContext
            {
                Handle = handle,
                RemoteAddress = remoteAddress,
            };

            return new RestoreRequestContext(prior);
        }

        private sealed class RestoreRequestContext : IDisposable
        {
            private readonly IpcRequestContext? prior;

            public RestoreRequestContext(IpcRequestContext? prior)
            {
                this.prior = prior;
            }

            public void Dispose()
            {
                currentIpcRequest.Value = prior;
            }
        }

        internal object? HandleIncomingIpcMessage(byte msgType, object? incoming, int handle, IPAddress? remoteAddress)
        {
            using var _ = PushIpcRequestContext(handle, remoteAddress);
            K3Value payload = ConvertFromIpcObject(incoming);
            lock (evaluationLock)
            {
                if (msgType == K3MsgType.Get)
                {
                    K3Value result = InvokeSyncIpcHook(payload);
                    return ConvertToIpcObject(result);
                }

                InvokeAsyncIpcHook(payload);
                return null;
            }
        }

        internal void HandleIncomingIpcClose(int handle, IPAddress? remoteAddress)
        {
            using var _ = PushIpcRequestContext(handle, remoteAddress);
            lock (evaluationLock)
            {
                K3Value? closeHook = kTree.GetValue(".m.c");
                if (closeHook is not null and not NullValue)
                {
                    InvokeHook(closeHook, new List<K3Value>());
                }
            }
        }

        private K3Value InvokeSyncIpcHook(K3Value payload)
        {
            K3Value? hook = kTree.GetValue(".m.g");
            if (hook is null || hook is NullValue)
            {
                return EvaluateDefaultIpcGet(payload);
            }

            return InvokeHook(hook, new List<K3Value> { payload });
        }

        private void InvokeAsyncIpcHook(K3Value payload)
        {
            K3Value? hook = kTree.GetValue(".m.s");
            if (hook is null || hook is NullValue)
            {
                EvaluateDefaultIpcSet(payload);
                return;
            }

            InvokeHook(hook, new List<K3Value> { payload });
        }

        private K3Value InvokeHook(K3Value hook, List<K3Value> arguments)
        {
            return CallFunction(hook, arguments);
        }

        private K3Value EvaluateDefaultIpcGet(K3Value payload)
        {
            try
            {
                string source = ConvertPayloadToSource(payload);
                K3Value result = Program.ExecuteLine(source, this);
                return new VectorValue(new List<K3Value>
                {
                    new IntegerValue(0),
                    result,
                });
            }
            catch (K3ExitException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new VectorValue(new List<K3Value>
                {
                    new IntegerValue(1),
                    CreateCharacterVector(ex.Message),
                });
            }
        }

        private void EvaluateDefaultIpcSet(K3Value payload)
        {
            string source = ConvertPayloadToSource(payload);
            Program.ExecuteLine(source, this);
        }

        private string ConvertPayloadToSource(K3Value payload)
        {
            if (payload is CharacterValue ch)
            {
                return ch.Value;
            }

            if (payload is VectorValue vec && vec.VectorType == -3)
            {
                return string.Concat(vec.Elements.OfType<CharacterValue>().Select(x => x.Value));
            }

            return payload.ToString();
        }

        private static VectorValue CreateCharacterVector(string text)
        {
            return new VectorValue(text.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList(), -3);
        }

        private int OpenIpcConnection(K3Value operand)
        {
            var root = GetRootEvaluator();
            (string host, int port) = ParseConnectionSpec(operand);
            var client = new K3IpcClient(host, port);
            lock (root.clientLock)
            {
                int handle = root.nextClientHandle++;
                root.ipcClients[handle] = client;
                return handle;
            }
        }

        private void CloseIpcConnection(int handle)
        {
            var root = GetRootEvaluator();
            K3IpcClient? client;
            lock (root.clientLock)
            {
                if (!root.ipcClients.TryGetValue(handle, out client))
                {
                    throw new Exception($"3: invalid IPC handle {handle}");
                }

                root.ipcClients.Remove(handle);
            }

            client.Dispose();
        }

        private void SendAsyncIpc(int handle, K3Value payload)
        {
            GetIpcClient(handle).Send(ConvertToIpcObject(payload), K3MsgType.Set);
        }

        private K3Value SendSyncIpc(int handle, K3Value payload)
        {
            object? result = GetIpcClient(handle).Query(ConvertToIpcObject(payload));
            return ConvertFromIpcObject(result);
        }

        private K3IpcClient GetIpcClient(int handle)
        {
            var root = GetRootEvaluator();
            lock (root.clientLock)
            {
                if (!root.ipcClients.TryGetValue(handle, out var client))
                {
                    throw new Exception($"3: invalid IPC handle {handle}");
                }

                return client;
            }
        }

        private bool IsSelfConnectionSpec(K3Value value)
        {
            try
            {
                (string host, int port) = ParseConnectionSpec(value);
                if (port != GetListeningPort() || port == 0)
                {
                    return false;
                }

                string normalized = host.Trim().ToLowerInvariant();
                return normalized == "127.0.0.1"
                    || normalized == "localhost"
                    || normalized == GetIpcHost().Trim().ToLowerInvariant()
                    || normalized == Dns.GetHostName().Trim().ToLowerInvariant();
            }
            catch
            {
                return false;
            }
        }

        private void SendAsyncSelfIpc(K3Value payload)
        {
            using var _ = PushIpcRequestContext(SelfIpcHandle, IPAddress.Loopback);
            lock (GetRootEvaluator().evaluationLock)
            {
                InvokeAsyncIpcHook(payload);
            }
        }

        private K3Value SendSyncSelfIpc(K3Value payload)
        {
            using var _ = PushIpcRequestContext(SelfIpcHandle, IPAddress.Loopback);
            lock (GetRootEvaluator().evaluationLock)
            {
                return InvokeSyncIpcHook(payload);
            }
        }

        private (string Host, int Port) ParseConnectionSpec(K3Value value)
        {
            if (value is not VectorValue vec || vec.Elements.Count != 2)
            {
                throw new Exception("3: connection spec must be (`host;port)");
            }

            string host = vec.Elements[0] switch
            {
                SymbolValue sym => sym.Value,
                VectorValue hostChars when hostChars.VectorType == -3 =>
                    string.Concat(hostChars.Elements.OfType<CharacterValue>().Select(x => x.Value)),
                _ => throw new Exception("3: host must be a symbol or character vector"),
            };

            int port = vec.Elements[1] switch
            {
                IntegerValue iv => iv.Value,
                LongValue lv => checked((int)lv.Value),
                _ => throw new Exception("3: port must be an integer"),
            };

            return (host, port);
        }

        private bool TryGetHandle(K3Value value, out int handle)
        {
            switch (value)
            {
                case IntegerValue iv:
                    handle = iv.Value;
                    return true;
                case LongValue lv:
                    handle = checked((int)lv.Value);
                    return true;
                default:
                    handle = 0;
                    return false;
            }
        }

        internal object? ConvertToIpcObject(K3Value value)
        {
            return value switch
            {
                NullValue => null,
                IntegerValue iv => iv.Value,
                LongValue lv when lv.Value >= int.MinValue && lv.Value <= int.MaxValue => (int)lv.Value,
                LongValue lv => throw new Exception($"IPC only supports 32-bit integers; value {lv.Value} is out of range."),
                FloatValue fv => fv.Value,
                CharacterValue cv when cv.Value.Length > 0 => (byte)cv.Value[0],
                SymbolValue sv => new KSym(sv.Value),
                DictionaryValue dict => dict.Entries.ToDictionary(
                    x => x.Key.Value,
                    x => ConvertToIpcObject(x.Value.Value)),
                VectorValue vec when vec.VectorType == -3 => string.Concat(vec.Elements.OfType<CharacterValue>().Select(x => x.Value)),
                VectorValue vec when vec.VectorType == -4 => vec.Elements
                    .OfType<SymbolValue>()
                    .Select(x => new KSym(x.Value))
                    .ToArray(),
                VectorValue vec => vec.Elements.Select(ConvertToIpcObject).ToArray(),
                _ => throw new Exception($"IPC does not support values of type {value.Type}."),
            };
        }

        internal K3Value ConvertFromIpcObject(object? value)
        {
            return value switch
            {
                null => new NullValue(),
                int i => new IntegerValue(i),
                double d => new FloatValue(d),
                byte b => new CharacterValue(((char)b).ToString()),
                string s => CreateCharacterVector(s),
                KSym sym => new SymbolValue(sym.Name),
                KSym[] syms => new VectorValue(syms.Select(x => (K3Value)new SymbolValue(x.Name)).ToList(), -4),
                IDictionary<string, object?> dict => new DictionaryValue(dict.ToDictionary(
                    x => new SymbolValue(x.Key),
                    x => (ConvertFromIpcObject(x.Value), (DictionaryValue?)null))),
                object?[] arr => new VectorValue(arr.Select(ConvertFromIpcObject).ToList(), 0),
                _ => throw new Exception($"Unsupported IPC value type: {value.GetType().FullName}"),
            };
        }
    }
}
