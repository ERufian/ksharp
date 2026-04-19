using System;

namespace K3CSharp
{
    /// <summary>
    /// Raised when k code requests process shutdown via <c>_exit</c>.
    /// The host catches this so it can stop the IPC listener cleanly.
    /// </summary>
    public sealed class K3ExitException : Exception
    {
        public int ExitCode { get; }

        public K3ExitException(int exitCode)
            : base($"K3 requested exit with code {exitCode}.")
        {
            ExitCode = exitCode;
        }
    }
}
