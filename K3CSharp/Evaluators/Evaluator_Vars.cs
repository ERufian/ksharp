// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace K3CSharp
{
    public partial class Evaluator
    {
        // Internal variable and system information functions
        
        public K3Value DirFunction(K3Value operand)
        {
            // _d (directory) function - returns current directory/branch
            return kTree.CurrentBranch != null ? kTree.CurrentBranch : new NullValue();
        }
        
        private K3Value NullFunction(K3Value operand)
        {
            // _n - null value
            // Return singleton null value for optimization purposes
            return new NullValue();
        }
        
        private K3Value VarFunction(K3Value operand)
        {
            // _v - script name (without extension), or empty symbol if no script
            return new SymbolValue(ScriptName);
        }

        private K3Value IndexFunction(K3Value operand)
        {
            // _i - command-line arguments as list of character vectors
            if (CommandLineArgs.Count == 0)
                return new VectorValue(new List<K3Value>());

            var elements = CommandLineArgs.Select(arg =>
                (K3Value)new VectorValue(arg.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList(), -3)
            ).ToList();
            return new VectorValue(elements);
        }
        
        private K3Value FunctionFunction(K3Value operand)
        {
            // _f - current function value for self-reference
            // Inside a function body, _f[args] calls the currently-executing function.
            if (currentFunctionValue != null)
            {
                return currentFunctionValue;
            }
            // Outside a function context, return the operand unchanged (no self to reference)
            return operand ?? new NullValue();
        }
        
        private K3Value SpaceFunction(K3Value operand)
        {
            // _s - system memory information
            // Return information about memory usage of the system
            // For K# we will return information obtained by GC.GetTotalMemory
            var memoryInfo = GC.GetTotalMemory(false);
            var results = new List<K3Value>
            {
                new IntegerValue((int)(memoryInfo / (1024 * 1024))), // Convert bytes to MB
                new IntegerValue(Environment.ProcessId),
                new IntegerValue(0) // Pinned objects count (placeholder)
            };
            return new VectorValue(results);
        }
        
        private K3Value HostFunction(K3Value operand)
        {
            // _h - preferred host for IPC connection tuples
            return new SymbolValue(GetIpcHost());
        }
        
        private K3Value PortFunction(K3Value operand)
        {
            // _p - port number
            return new IntegerValue(GetListeningPort());
        }
        
        private K3Value ProcessIdFunction(K3Value operand)
        {
            // _P - process ID
            // Return process ID of the current process as an integer
            return new IntegerValue(Environment.ProcessId);
        }
        
        private K3Value WhoFunction(K3Value operand)
        {
            // _w - current incoming IPC handle
            return new IntegerValue(GetCurrentIncomingHandle());
        }
        
        private K3Value UserFunction(K3Value operand)
        {
            // _u - username
            // Return username from calling process when responding to an IPC call
            // If not using IPC, return username of user running REPL
            return new SymbolValue(Environment.UserName ?? "");
        }
        
        private K3Value AddressFunction(K3Value operand)
        {
            // _a - IPv4 address
            var address = GetCurrentIncomingAddress();
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
            {
                return new IntegerValue(0);
            }

            var bytes = address.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return new IntegerValue(BitConverter.ToInt32(bytes, 0));
        }
        
        private K3Value VersionFunction(K3Value operand)
        {
            // _k - version
            // Return version of K# as a character vector: "3.33." + Program.Version
            var version = "3.33." + Program.Version;
            var chars = version.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
            return new VectorValue(chars, -3);
        }
        
        private K3Value OsFunction(K3Value operand)
        {
            // _o - operating system
            // Return name of operating system as a symbol
            var os = Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => "w64",
                PlatformID.Win32S => "w32", 
                PlatformID.WinCE => "w32",
                PlatformID.Unix => "linux",
                PlatformID.MacOSX => "osx",
                _ => "unknown"
            };
            return new SymbolValue(os);
        }
        
        private K3Value CoresFunction(K3Value operand)
        {
            // _c - number of cores
            // Return number of cores of the machine as an integer
            return new IntegerValue(Environment.ProcessorCount);
        }
        
        private K3Value RamFunction(K3Value operand)
        {
            // _r - amount of RAM
            // Return amount of RAM of the machine as an integer
            // Using GC.GetTotalMemory for available memory
            var totalMemory = GC.GetTotalMemory(false);
            return new IntegerValue((int)(totalMemory / (1024 * 1024))); // Convert bytes to MB
        }
        
        private K3Value MachineIdFunction(K3Value operand)
        {
            // _m - machine ID
            // Return machine ID as a character vector
            var machineId = Environment.MachineName ?? "";
            var chars = machineId.Select(c => (K3Value)new CharacterValue(c.ToString())).ToList();
            return new VectorValue(chars);
        }
        
        private K3Value StackFunction(K3Value operand)
        {
            // _y - stack trace
            // Return stack trace of the current call stack and arguments as a list
            var stackTrace = Environment.StackTrace ?? "";
            var lines = stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var results = new List<K3Value>();
            
            foreach (var line in lines.Take(10)) // Limit to first 10 lines
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var args = line.Split(' ').Select(arg => (K3Value)new SymbolValue(arg.Trim())).ToList();
                    results.Add(new VectorValue(args));
                }
            }
            
            return new VectorValue(results);
        }
    }
}