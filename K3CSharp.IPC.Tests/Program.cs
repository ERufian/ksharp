using System;

namespace K3CSharp.IPC.Tests
{
    /// <summary>
    /// Entry point for the K3CSharp.IPC test harness.
    /// Runs the wire-format vectors first, then the client/server round-trip tests.
    /// </summary>
    public static class Program
    {
        /// <summary>Run every suite and exit with 0 on success, 1 on any failure.</summary>
        public static int Main(string[] args)
        {
            Console.WriteLine("=== K3CSharp.IPC test harness ===");
            bool allPassed = true;
            allPassed &= K3CodecTests.RunAll();
            allPassed &= K3IpcRoundTripTests.RunAll();
            allPassed &= K3ShellIpcDemoTests.RunAll();

            Console.WriteLine();
            Console.WriteLine(allPassed ? "All K3CSharp.IPC tests passed." : "One or more tests FAILED.");
            return allPassed ? 0 : 1;
        }
    }
}
