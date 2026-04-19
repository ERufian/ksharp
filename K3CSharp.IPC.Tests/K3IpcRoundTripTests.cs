using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using K3CSharp.IPC;

namespace K3CSharp.IPC.Tests
{
    /// <summary>
    /// End-to-end smoke tests that spin up a <see cref="K3IpcServer"/> on a
    /// loopback port, issue requests via a <see cref="K3IpcClient"/>, and
    /// verify the round-trip.
    /// </summary>
    public static class K3IpcRoundTripTests
    {
        /// <summary>Run all round-trip tests and return true on full success.</summary>
        public static bool RunAll()
        {
            Console.WriteLine();
            Console.WriteLine("K3IpcClient / K3IpcServer round-trip tests:");
            var tests = new (string Name, Func<string?> Body)[]
            {
                ("echo-int",    EchoInt),
                ("echo-string", EchoString),
                ("echo-list",   EchoList),
                ("error-reply", ErrorReply),
            };

            int passed = 0;
            foreach (var (name, body) in tests)
            {
                string? err = null;
                try
                {
                    err = body();
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }
                if (err == null)
                {
                    Console.WriteLine($"  [PASS] {name}");
                    passed++;
                }
                else
                {
                    Console.WriteLine($"  [FAIL] {name}  -- {err}");
                }
            }
            Console.WriteLine($"  {passed}/{tests.Length} round-trip tests passed");
            return passed == tests.Length;
        }

        private static string? EchoInt()
        {
            return WithServer(
                (hdr, val) => new object?[] { 0, val },
                client =>
                {
                    var result = (object?[]?)client.Query(42);
                    if (result == null || result.Length != 2) return "unexpected reply shape";
                    if (!Equals(result[0], 0)) return $"status was {result[0]}, expected 0";
                    if (!Equals(result[1], 42)) return $"payload was {result[1]}, expected 42";
                    return null;
                });
        }

        private static string? EchoString()
        {
            return WithServer(
                (hdr, val) => new object?[] { 0, val },
                client =>
                {
                    var result = (object?[]?)client.Query("hello");
                    if (result == null || result.Length != 2) return "unexpected reply shape";
                    if (!"hello".Equals(result[1])) return $"payload was {result[1]}";
                    return null;
                });
        }

        private static string? EchoList()
        {
            var payload = new object?[] { 1, 2, 3 };
            return WithServer(
                (hdr, val) => new object?[] { 0, val },
                client =>
                {
                    var result = (object?[]?)client.Query(payload);
                    if (result == null || result.Length != 2) return "unexpected reply shape";
                    if (!(result[1] is object?[] echoed)) return "nested reply was not a list";
                    if (echoed.Length != payload.Length) return "echoed list has wrong length";
                    for (int i = 0; i < echoed.Length; i++)
                    {
                        if (!Equals(echoed[i], payload[i])) return $"mismatch at [{i}]: {echoed[i]}";
                    }
                    return null;
                });
        }

        private static string? ErrorReply()
        {
            // Handler throws; server should translate that into {1; message}.
            return WithServer(
                (hdr, val) => throw new InvalidOperationException("boom"),
                client =>
                {
                    var result = (object?[]?)client.Query(0);
                    if (result == null || result.Length != 2) return "unexpected reply shape";
                    if (!Equals(result[0], 1)) return $"status was {result[0]}, expected 1";
                    if (!"boom".Equals(result[1])) return $"error text was {result[1]}";
                    return null;
                });
        }

        private static string? WithServer(K3MessageHandler handler,
                                          Func<K3IpcClient, string?> useClient)
        {
            using var server = new K3IpcServer(IPAddress.Loopback, 0, handler);
            server.Start();
            int port = server.LocalEndPoint.Port;
            try
            {
                using var client = new K3IpcClient(IPAddress.Loopback.ToString(), port);
                return useClient(client);
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
