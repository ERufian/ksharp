// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
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