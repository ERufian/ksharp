# kharp - k version 3 Language Interpreter in C#

A comprehensive implementation of the K programming language, version 3, a vector programming language from the APL family. 

---

### **LEGAL TERMS**: 
ksharp

Copyright (c) 2026 Eusebio Rufian-Zilbermann et al.

This software is licensed under the terms of the  **MIT License with Commons Clause**.  
You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.

Full license text: [LICENSE](LICENSE.txt)

Highlighted details: This Software is provided "AS IS". You are responsible for (a) Maintaining appropriate backup copies of any data file that this software is expected to modify, (b) Ensuring that data written to a storage system using this software can be read back in its entirety and with integrity. The author(s) of this product cannot be held responsible for any data loss resulting directly or indirectly from the use of this product. 
---

## 📚 **Table of Contents**

- [🎯 Current Status](#-current-status)
  - [Latest Test Results](#-latest-test-results)
  - [Recent Major Improvements](#-recent-major-improvements)
- [ Quick Start](#-quick-start)
- [✅ Implemented Features](#-implemented-features)
  - [Core Data Types](#core-data-types)
  - [Native Operators](#native-operators)
  - [Core Adverb System](#core-adverb-system)
  - [Adverbs for Verbialized Nouns](#adverbs-for-verbialized-nouns)
  - [Amend, Index, Apply, Assign](#amend-index-apply-assign)
  - [Core Function System](#core-function-system)
  - [Attributes](#attributes)
  - [Conditionals](#conditionals)
  - [I/O and Communication](#io-and-communication)
  - [System Variables and Functions](#system-variables-and-functions)
- [📡 IPC Operations](#-ipc-operations)
- [⚙ Statement Parsing System](#-statement-parsing-system)
- [🔧 K 3 Features Not Available in K#](#-k-3-features-not-available-in-k)
- [🎉 ksharp Enhancements Over k version 3](#-ksharp-enhancements-over-k-version-3)
  - [Foreign Function Interface (FFI)](#foreign-function-interface-ffi)
  - [Hint System with _gethint and _sethint](#hint-system-with-_gethint-and-_sethint)
  - [Object Instantiation and Disposal](#object-instantiation-and-disposal)
  - [The _dotnet Tree](#the-_dotnet-tree)
  - [Method Invocation](#method-invocation)
  - [Error Handling](#error-handling)
  - [Performance Considerations](#performance-considerations)
- [🏗️ Architecture](#️-architecture)
  - [Project Structure](#-project-structure)
  - [Core Components](#core-components)
- [🛠️ Building and Running](#️-building-and-running)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
    - [Windows](#windows)
    - [Linux (Ubuntu/Debian)](#linux-ubuntudebian)
    - [Linux (Fedora/CentOS)](#linux-fedoracentos)
    - [macOS](#macos)
  - [Build](#build)
  - [Run](#run)
  - [Troubleshooting](#troubleshooting)
- [🤝 Contributing](#-contributing)
- [👨‍💻 Authorship](#-authorship)
- [Note Regarding Project Name](#note-regarding-project-name)

## 🎯 Current Status

**ksharp has now reached beta status.** The core language in the K Reference Manual is fully implemented: native verbs, adverbs, amend, index, apply and assign, functions, conditionals, I/O and communication, system variables and system functions. The Foreign Function Interface is designed to interoperate with Microsoft's .NET and .NET Framework.

### 📈 Latest Test Results
- **Test Suite**: 1545/1545 tests passing (100% success rate)

### 🎯 Recent Major Improvements
  - **📂 Support for delimited file I/O (May 2026)** - Enhanced `0:` and `1:` functionality, fixed `5:` functionality
  - **🚀 Improved comparison tolerance (May 2026)** - Updated comparison tolerance to better match K compatibility 
  - **🎯 Full test suite passing at 100% (May 2026)** - Successfully resolved parsing issues that were preventing some K idioms from producing correct results 
  - **🔥 Support for adnouns (May 2026)** - The over, scan, each and each-prior adverbs can now be used with nouns (vectors, matrices and tensors) for scatter indexing, transitive closure and state transitions.

---

## 🚀 **Quick Start**

### **Run K3Sharp Interpreter**
```bash
cd K3CSharp
dotnet run
```

**[K User Manual](https://nsl.com/k/training/kusrlite.pdf)** - Complete K language guide with tutorials and examples

## ✅ **Implemented Features**

**[K Reference Manual](https://nsl.com/k/training/kreflite.pdf)** - Detailed reference for all K functions, operators, and concepts

### **Core Data Types** ✅
- **Atomic Types**: Integer, Float, Character, Symbol, Dictionary, Nil/Self-typed Null, Function, 64-bit Integer
- **Collections**: List, \(complex or mixed-type, including nesting\), Integer vector, Float vector, Character vector, Symbol vector, 64-bit Integer vector
- **Special Values**: Self-typed Null \(`0n`\), infinity \(`0I`, `0i`, `0Ij`\), negative infinity \(`-0I`, `-0i`, `-0Ij`\), negative zero \(`-0.0`\), integral null/NaN \(`0N`, `0n`, `0Nj`\)
- **Type System**: Dynamic typing with automatic promotion
- **Null Handling**: IEEE 754 compliant null propagation

### **Native Operators, and meaningful glyphs** ✅
- `!` monadic - enumerate
- `!` dyadic, atomic right argument - mod \(modulo\)
- `!` dyadic, vector right argument - rotate
- `#` monadic - count
- `#` dyadic - take
- `$` monadic - format \(simple format\)
- `$` dyadic, character vector right argument - form
- `$` dyadic, right argument other than character vector - format \(format with specifier\)
- `%` monadic - invert
- `%` dyadic - divide
- `&` dyadic - min 
- `(` `)` - grouping separator
- `,` monadic - enlist
- `,` dyadic - join
- `-` monadic, followed by space - change sign
- `-` dyadic - subtract
- `.` as last character to variable path - attribute path
- `.` inside variable path - descend into value
- `..` in variable path - descend into attribute
- `.` as index for a dictionary - attribute dictionary
- `.` monadic, list \(type 0\) argument - make dictionary
- `.` monadic, character vector argument - execute
- `.` monadic, dictionary argument - unmake dictionary
- `.` dyadic, first argument is a function - dot apply
- `.` dyadic, first argument is a variable - index at depth
- `.` triadic, third argument is a verb - apply at depth
- `.` triadic, third argument is `:` - error trap at depth
- `.` tetradic - apply at depth with arguments
- `/` with whitespace on the left - comment marker
- `'` as the single item in an expression - Signal
- `'` monadic - Signal
- `:` REPL command - resume
- `:` monadic - return
- `::` with variable name to the left - Global Assign \(statement\)
- `:` with variable name to the left - Assign \(statement\)
- `:` with monadic verb to the left and no arguments - Monadic apply and assign 
- `:` with dyadic verb to the left and arguments - Dyadic apply and assign
- `:[` `]` variadic - conditional execute and assign \(statement\)
- `<` monadic - grade up
- `<` dyadic - less
- `=` monadic - group
- `=` dyadic - equals
- `>` monadic - grade down
- `>` dyadic - more
- `?` monadic - uniques 
- `?` dyadic with list or null \(type 6\) on the left - find
- `?` dyadic with function left argument - inverse function
- `?` triadic - apply inverse function
- `@` monadic - Is Atom
- `@` dyadic, path left argument, character vector right argument - execute at path
- `@` dyadic, function left argument - shallow apply
- `@` dyadic, variable left argument - shallow index 
- `@` triadic, third argument is verb - shallow apply
- `@` triadic, third argument is `:` - shallow error trap
- `@` tetradic - shallow apply with arguments
- `[` `]` with function to the left - Group and dot apply
- `[` `]` with variable to the left and assignment or apply and assign to the right - Amend
- `[` `]` with variable to the left with no assignment to the right - Group and index at depth
- `\` at the start of a line \(whitespace allowed, `^\s*\\`\) - Marker for REPL command
- `^` monadic - Shape
- `^` dyadic - Power
- `_` monadic - Floor
- `_` as a prefix - system reserved verb or variable
- `_` dyadic with integer left argument - drop
- `_` dyadic with integer vector left argument - cut
- `{` `}` - group and make function
- `|` monadic - reverse order
- `|` dyadic - max
- `~` monadic, with numeric argument - not \(is zero, logical negation\)
- `~` monadic, with symbol \(variable path\) argument - attribute handle
- `~` dyadic - match
- `+` monadic - flip \(transpose\)
- `+` dyadic - add
- `*` monadic - first or default
- `*` dyadic - multiply
- ` before a name or a string literal - symbol marker
- `"` `"` (string literal) enclosing a single item - character
- `"` `"` (string literal) enclosing multiple items - character vector
- `;` inside grouping \(except enclosing quotes\) - list separator
- `\n` inside grouping \(except enclosing quotes\) - list separator
- `\n` in the REPL, outside grouping/enclosing - evaluate

### **Core Adverb System \(Iterations\)** ✅
- **Over (`/`)**: `+/ 1 2 3 4 5` → `15` (fold/reduce)
- **Scan (`\`)**: `+\ 1 2 3 4 5` → `(1;3;6;10;15)` (cumulative)
- **Each (`'`)**: `-:' 1 2 3 4` → `(-1;-2;-3;-4)` (element-wise)
- **Each-Left (`\:`)**: `1 2,\: 3 4 5` → `(1 3 4 5;2 3 4 5)` (apply operation for each item in left argument, with entire right argument)
- **Each-Right (`/:`)**: `1 2 3 +/: 4 5` → `(5 6 7;6 7 8)` (apply operation with entire left argument, for each right argument)
- **Each-Pair (`':`)**: `,': 1 2 3 4` → `(2 1;3 2;4 3)` (apply operation to consecutive pairs, reversing left and right)
- **Initialization**: `1 +/ 2 3 4 5` → `15` (with initial value)
- **Adverbs for already modified verbs** 🆕:  `((1 2);(3 4)),/:\:((9 8);(7 6))` → `((1 2 9 8;1 2 7 6);(3 4 9 8;3 4 7 6))` \(adjacent adverbs are nested iterations\)

### **Adverbs for Verbialized Nouns \(iterative indexing\)** ✅
- `'` with matrix/tensor immediately to the left - Scatter Select
- `/` with vector of indices immediately to the left - Transitive Closure (index traversal iteration with convergence)
- `/` with transition matrix immediately to the left - State Transition (2D iterative index)
- `\` with vector of indices immediately to the left - Transitive Closure with trace 
- `\` with transition matrix immediately to the left - State Transition with trace
- `':` with transition matrix immediately to the left - 2D Index each prior

### **Amend, Index, Apply, Assign** ✅
- **Simple assign**: `a:1 2 3 4`
- **Slice extraction**: `m[3 4;1 2]`
- **Sliced assignment**: `m[3 4;1 2]:((8 9);(7 3))`
- **Modify and assign**: `i+:1` (increment), `x-:2` (decrement), `n*:3` (multiply-assign), etc.

### **Core Function System** ✅
- **Anonymous Functions**: `{[x;y] x + y}`
- **Function Assignment**: `func: {[x] x * 2}`
- **Function Application**: `func2 . (4;5)`, `func1 @ 5` or `func2[3;5]`
- **Projections**: `add . 5` creates `{[x] 5 + x}`
- **Multi-statement**: Functions can have semicolon-separated or newline-separated statements

### **Attributes** ✅
- **Dependencies** - Event system for automatic re-calculation
- **Triggers** - Event system for execute on change
- **UI Attributes not implemented** ❌ - .NET FFI is available for UI development, implementation of the K UI is not expected to be implemented.

### **Conditionals** ✅
- `:[]` - Conditional value
- `do[]` - Fixed iteration
- `if[]` - Conditional execution
- `while[]` - Conditional iteration

### **I/O and Communication** ✅
#### **K Serialization System** ✅
- **Binary Serialize (`_db`)**: Convert K data structures to binary format
- **Binary Deserialize (`_bd`)**: Convert binary data back to K data structures
#### **Numbered I/O verbs** ✅
- `0:` monadic - read from file as text
- `0:` dyadic - write to file as text
- `1:` monadic - read from K data file using memory mapped access
- `1:` dyadic - write to file as K data
- `2:` monadic - read whole K data file
- `2:` dyadic - FFI Load Assembly. See [Foreign Function Interface (FFI)](#foreign-function-interface-ffi)
- `3:` monadic, list argument - Open IPC Port
- `3:` monadic, integer argument - Close IPC Port
- `3:` dyadic - IPC set \(asynchronous IPC\)
- `4:` monadic - Type \(get variable type\)
- `4:` dyadic - IPC get \(synchronous IPC\)
- `5:` monadic - String Representation
- `5:` dyadic - Append to file as text
- `6:` monadic - read from file as raw bytes
- `6:` dyadic - write to file as raw bytes

### **System Variables and Functions** ✅
- **Internal Info** `_d` (K dir), `_v` (K vars), `_i` (index), `_f` (self-referent function), `_n` (null singleton)
- **Process Info**`_k` (version), `_p` (port), `_P` (PID), `_w` (who), `_u` (user)
- **System Info** `_s` (space), `_h` (host), `_a` (address), `_o` (os), `_c` (cores), `_r` (RAM), `_m` (mach id)
- **Trigonometric**: `_sin`, `_cos`, `_tan`, `_asin`, `_acos`, `_atan`
- **Hyperbolic**: `_sinh`, `_cosh`, `_tanh`
- **Exponential**: `_exp`, `_log`, `_sqrt`, `_sqr`
- **Arithmetic**: `_abs`, `_floor`, `_ceil`, `_div` (integer division)
- **Bitwise Operations**: `_and`, `_or`, `_xor`, `_rot`, `_shift`
- **Matrix**: `_dot`, `_mul`, `_inv`, `_lsq` (least squares regression)
- **Time Functions**: Complete time and date manipulation functions 
  - **_t**: current K-time in Seconds since 12:00 AM, January 1, 2035 UTC
  - **_T**: current time in Days since 12:00 AM, January 1, 2035 UTC
  - **_gtime**: Converts K-time to date/time vector \(\"yyyyMMdd\";\"hhmmss\"\)
  - **_ltime**: Converts K-time to local time vector with timezone offset
  - **_jd**: Converts date to Julian date \(K Julian Date is days since January 1, 2035\)
  - **_dj**: Converts Julian date back to yyyyMMdd format
  - **_lt**: Adds GMT-to-local-time offset in seconds to a K-time value
- **Search Functions**: `_in` (search), `_bin` (binary search) 
- **String Operations**: `_sm` (string match), `_ss` (string search), `_ssr` (string search and replace), `_ci` (character from integer), `_ic` (integer from character)
- **List Operations**: `_lin` (list intersection indices), `_sv` (scalar from vector), `_vs` (vector from scalar), `_dv` (delete value) `_di` (delete item) 
- **Pattern Matching**: Advanced regex-like pattern matching for `_sm`, `_ss` and `_ssr` based on .NET regex, with 1000 ms timeout, customizable via `.m.regex.timeout`

---

## **📡 IPC Operations (Contributed by Michal Wallace @tangentstorm)**

**Operations 3:, 4:** - Complete k version 3 Inter-Process Communication system
- **3: (IPC Get/Connection)** - Open/close connections and asynchronous messaging
  - `3:(`host;port)` - Open connection, returns handle
  - `3:handle` - Close connection
  - `handle 3:data` - Send asynchronous request
- **4: (IPC Set/Synchronous)** - Synchronous remote execution
  - `handle 4:data` - Send sync request, returns remote reply
  - `(`host;port) 4:data` - Open, send, and close in one step

**IPC-Related System Values:**
- **_i** - Listening port number (0 when inactive)
- **_h** - Preferred host for connection tuples
- **_w** - Current incoming socket handle during .m.g/.m.s/.m.c execution

**K Tree Hooks:**
- **.m.g** - Handles synchronous requests (default: executes K code, returns (status;result))
- **.m.s** - Handles asynchronous requests
- **.m.c** - Runs when connection closes

**Server Startup:**
```bash
ksharp -i PORT              # Start IPC listener in REPL mode
ksharp -i PORT script.k    # Start listener, run script, then serve IPC
```

---

## **⚙ Statement Parsing System**
**K LRS Compliant**
* Left-to-right evaluation of expressions
* Grouped elements (parentheses, brackets, braces) do have precedence
* No verb-specific precedence (e.g., no EMDAS), only positional 
* Adverbs, adnouns and brackets bind to the item on its left
* Long Right Scope parsing: Everything to the right of a verb is its right argument (resulting in right-to-left precedence within an expression)
* **Parse Tree Verbs** 🆕: 
  - **_parse**: Converts character vectors to parse tree representations - `_parse "1 + 2"` → ``(`"+", 1, 2)``
  - **_eval**: Evaluates parse tree representations - ``_eval (`"+", 1, 2)`` → `3`

---

## 🔧 **K version 3 features not available in ksharp**

- **❌ K UI `` `show`` and `` `hide``**
- **❌ Attributes related to UI**
- **❌ Debugging and Tracing**
- **❌ K runtime (.kr) and execution without console**

---

## **🎉 ksharp Enhancements Over K version 3**
- ✅ **Smart Integer Division and exponentiation**: `4 % 2` → `2`, `2 ^ 3` → `8` (integer, not float)
- ✅ **64-bit Long Integers**: `123456789012345j` (modeled on e333j)
- ✅ **Compact Symbol Vectors**: `` `a`b`c `` (no spaces)
- ✅ **Compact List and Dictionary Display**: Semicolon-separated format ``.(`a;1;);(`b;2;))``
- ✅ **Additional system variables and functions inspired on e333j**: `_P` `_o` `_c` `_r` `_m` `_y` `_div` (truncating integer division) `_and` `_or` `_xor` `_not` `_rot` `_shift` (bitwise)
- ✅ **No denorm dictionaries**:   ``.((`a;1);(`a;2)) is .,(`a;2;) and not .((`a;1;);(`a;2;))``
- ✅ **Parse and eval**: `_parse "1 + 2"` ``_eval (`"+";1;2)``
- ✅ **LRS for lists in brackets**: `a:3;+[a+:7;a+4]` evaluates `a+:7` before `a+4` because `a+4` is outside the scope of `a+:7`
- ✅ **Improved Compatibility with k version 2**: `_n?i` → `i` and execute at context `d@s` or `d[s]` (symbol left argument and character vector right argument)
- ✅ **.NET type handling**

### **Foreign Function Interface (FFI)** ✅
- **Method Invocation**: Complete calling of .NET methods from K code with automatic type conversion
- **Type Mapping**: Automatic conversion K data types -> .NET types, .NET objects copied to K dictionaries with hints
- **Static Members**: Loaded into `._dotnet` tree
- **Performance Optimizations**: Type caching and object registry for efficient operations
- **Error Handling**: Comprehensive .NET exception handling and propagation to K

**Assembly Loading**
```k
// Load System.Private.CoreLib assembly
"System.Private.CoreLib" 2: `System.String

// Load custom assembly
"MyAssembly.dll" 2: `MyNamespace.MyClass

// The result is a type dictionary containing metadata
```

**Syntax:** `assembly_name 2: type_name`

- **Left Argument**: Assembly name (file path or assembly name)
- **Right Argument**: Type name (fully qualified .NET type)
- **Result**: Dictionary containing type metadata, methods, properties, constructors

### **Hint System with _gethint and _sethint**

The `_gethint` and `_sethint` verbs provide type marshalling control and object creation hints.

```k
// Create a .NET string object from a k string
s:"hello" 
s _sethint `string

// Get type information
s _gethint
```

**Hint Types:**
- `` `bool`` - System.Boolean, subtype of K int
- `` `byte`` - System.Byte \(unsigned int8\), subtype of K char \(default\)
- `` `sbyte`` - System.SByte \(int8\), subtype of K char
- `` `short`` - System.Int16 \(int16\), subtype of K int
- `` `ushort`` - System.UInt16 \(uint16\), subtype of K int
- `` `int`` - System.Int32 \(int32\), subtype of K int \(default for any value other than 0N\)
- `` `uint`` - System.UInt32 \(uint32\), subtype of K long int
- `` `long`` - System.Int64 \(int64\), subtype of K long int \(default for any value other than 0Nj\)
- `` `ulong`` - System.UInt64 \(uint64\), subtype of K long int
- `` `float`` - System.Single \(float\), subtype of K float
- `` `double`` - System.Double \(double\), subtype of K float \(default\)
- `` `object`` - System.Object \(object\), subtype of K dictionary
- `` `datetime`` - System.DateTime, subtype of K int or K float
- `` `timespan`` - System.TimeSpan, subtype of K int or K float
- `` `dictionary`` - System.Collections.Hashtable, subtype of K dictionary \(default\)
- `` `list`` - System.Collections.Generic.List\<System.Object\>, subtype of K lists and vectors \(default for all except character vectors\) 
- `` `string`` - System.String, subtype of K symbol and K character vector \(default\)
- `` `stringbuilder`` - System.Text.StringBuilder, subtype of K string
- `` `null`` - System.DBNull, subtype of K int and long int \(default conversion for 0 and 0N\)
- `` `method`` - System.Delegate, subtype of K function

### **Object Instantiation and Disposal**

K3CSharp includes automatic object lifecycle management with explicit disposal capabilities.

```k
// Bind .NET dll
complex:`System.Runtime.Numerics.dll 2: `System.Numerics.Complex

// K verb constructor
complex_new:complex[`constructor]

// Create object
c1:complex_new[2;3]

// Dispose object when done
_dispose c1

// Check object status (returns handle information)
c1._this
```

NOTE: When a .NET Object is instantiated, a copy of its data will be mapped onto a K dictionary. This dictionary is an independent copy and changes will not be propagated back to .NET. Changing the .NET object must be done through accessors and methods.

**Object Registry:**
- Thread-safe global object tracking
- Automatic handle generation
- Memory management integration
- IDisposable pattern support

### **The _dotnet Tree**

The `._dotnet` global tree stores loaded assemblies and type information for efficient reuse.

```k
// Access static methods for loaded assemblies
conj_func: ._dotnet.System.Numerics.Complex.Conjugate

// Enumerate metadata
!._dotnet.System.Numerics.Complex

// Type information is cached for performance
```

**Tree Structure:**
- Numeric indices: Assembly references
- Symbol keys: Assembly names
- Nested dictionaries: Type metadata

### **Method Invocation**

Call .NET methods on object instances using dot notation.

```k
// Create object
str: "hello" _hint `object

// Call methods (when method invocation is fully implemented)
str.ToUpper        // Returns "HELLO"
str.Length         // Returns 5
str.Substring(0;2) // Returns "he"

// Access properties
str.Length         // Property access
str.Chars[0]       // Indexer access
```

**Method Calling Features:**
- Instance method invocation
- Static method calls
- Property getter/setter access
- Field access
- Indexer support
- Argument marshalling

### **Error Handling**

The FFI system provides comprehensive error handling:

```k
// Invalid assembly
"NonExistent.dll" 2: `SomeType  // Error: Assembly not found

// Invalid type
"System.Core" 2: `NonExistentType  // Error: Type not found

// Method errors
obj.NonExistentMethod  // Error: Method not found
```

**Error Types:**
- Assembly loading failures
- Type resolution errors
- Method invocation exceptions
- Invalid argument types
- Object disposal errors

### **Performance Considerations**

- **Assembly Caching**: Loaded assemblies are cached in `_dotnet` tree
- **Object Registry**: Efficient handle-based object tracking
- **Type Marshalling**: Optimized for common types
- **Memory Management**: Automatic garbage collection integration

---

## 🏗️ **Architecture**

###  **Project Structure**
```
K3CSharp/
├── ApplyTweaks/                 # Tool MCP Server for applying known_differences.txt to a result
├── K3CSharp/                    # Core interpreter implementation
├── K3CSharp.Comparison/         # k.exe comparison framework
├── K3CSharp.IPC/                # k.exe Inter-Process Communication (Contributed by Michal Wallace @tangentstorm)
├── K3CSharp.IPC.Tests/          # k.exe IPC test framework (Contributed by Michal Wallace @tangentstorm)
├── K3CSharp.MCP/                # Tool MCP Server for running a ksharp session (Contributed by Michal Wallace @tangentstorm)
├── K3CSharp.Comparison/         # k.exe comparison framework
├── K3CSharp.Tests/              # Unit tests 
├── KMCPServer/                  # Tool MCP Server for running an external K/Q interpreter with a command or a script
└── known_differences.txt        # Known differences configuration
```

### **Core Components**
- **Lexer.cs**: Tokenizes input into tokens with underscore ambiguity resolution
- **Parser.cs**: Recursive descent parser building AST with adverb support
- **Evaluator.cs**: AST traversal and evaluation with complete operator system
- **K3Value.cs**: Type system and value operations

---

## 🛠️ **Building and Running**

### **Prerequisites**

**.NET 8.0 SDK** is required to build and run ksharp.

#### **Windows**
```powershell
# Download and install .NET 8.0 SDK
# Visit: https://dotnet.microsoft.com/download/dotnet/8.0
```

#### **Linux (Ubuntu/Debian)**
```bash
# Install .NET 8.0 SDK
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

#### **Linux (Fedora/CentOS)**
```bash
# Install .NET 8.0 SDK
sudo rpm -Uvh https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm
sudo dnf install -y dotnet-sdk-8.0
```

#### **macOS**
```bash
# Install .NET 8.0 SDK using Homebrew
brew install dotnet-sdk

# Or download installer from:
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### **Installation**

#### **Windows**
```powershell
# Clone repository
git clone https://github.com/ERufian/ksharp.git
cd ksharp\K3CSharp

# Restore dependencies
dotnet restore

# Build solution
dotnet build

```

#### **Linux (Ubuntu/Debian)**
```bash
# Clone repository
git clone https://github.com/ERufian/ksharp.git
cd ksharp/K3Csharp

# Restore dependencies
dotnet restore

# Build solution
dotnet build
```

#### **Linux (Fedora/CentOS)**
```bash
# Clone repository
git clone https://github.com/ERufian/ksharp.git
cd ksharp/K3CSharp

# Restore dependencies
dotnet restore

# Build solution
dotnet build

```

#### **macOS**
```bash
# Clone repository
git clone https://github.com/ERufian/ksharp.git
cd ksharp/K3CSharp

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run 
dotnet run
```

### **Build**

```bash
# Build the entire solution
dotnet build

# Build in Release mode
dotnet build -c Release
```

### **Run**

```bash
# Run the interpreter (starting from a bash or zsh prompt at the root directory)
cd K3CSharp
dotnet run

# Run with script file
dotnet run -- script.k

# Run tests (starting from a bash prompt at the root directory)
cd K3CSharp.Tests
dotnet run

# Run comparison (starting from a bash prompt at the root directory)
cd K3CSharp.Comparison
dotnet run
```

### **Troubleshooting**

#### **Common Issues**

**"dotnet: command not found"**
- Ensure .NET 8.0 SDK is installed and in PATH
- Restart terminal after installation
- Verify with `echo $PATH` (Linux/macOS) or `echo %PATH%` (Windows)

**"Cannot find project or solution file"**
- Ensure you're in the correct directory containing `.csproj` or `.sln` files
- Use `ls` (Linux/macOS) or `dir` (Windows) to verify files

**Build errors on Linux/macOS**
- Ensure all required packages are installed
- Try `dotnet clean` followed by `dotnet build`
- Check file permissions: `chmod +x *.sh` (if using shell scripts)

**Performance issues**
- Use release build: `dotnet run -c Release`
- For large datasets, consider increasing memory: `dotnet run --environment DOTNET_GCHeapCount=1`

#### **Platform-Specific Tips**

**Windows PowerShell:**
```powershell
# Set execution policy for scripts (if needed)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**Linux/macOS Shell:**
```bash
# Make script files executable
chmod +x *.sh

# Use bash explicitly if needed
bash script.sh
```

**macOS Specific:**
```bash
# If using zsh (default on modern macOS)
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.zshrc
source ~/.zshrc
```

---

## 🤝 **Contributing**

### Report issues
Please include:
1. Issue Summary 
2. Description of the problem 
3. Expected Result 
4. Steps to reproduce, with a code snippet whenever possible 

Please note: I use a Windows 10 machine for development. OS-specific issues should be very rare, but I will not be able to address issues that can be reproduced only on a different OS

### Contribute code

**VERY IMPORTANT** By submitting a contribution you agree that it will be subject to the terms of the [MIT License](https://mit-license.org/)

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Run comparison framework to verify k.exe compatibility
6. Submit a pull request

---

## 👨‍💻 **Authorship**

This ksharp interpreter implementation was coded originally by **SWE-1.5 and 1.6** with significant contributions from **Kimi K-2.5 and 2.6** and **Claude Opus/Sonnet 4.5, 4.6 and 4.7** based on specifications, direction, prompts, comments and manual fixes provided by **Eusebio Rufian-Zilbermann**.

### **Major Contributors**

- **Michal Wallace** (@tangentstorm) 
  * Complete K version 3 IPC (Inter-Process Communication) system including TCP-based messaging, K tree hooks (.m.g, .m.s, .m.c), and the 3:/4: IPC verbs
  * MCP Server for agent interoperation with a ksharp persistent session
  * Various bugfixes

### **Acknowledgements**

In addition to direct contributors, the following people have been fundamental to the creation and development of this project. I am very thankful for their influence. Without them, it is possible that this interpreter would not exist.

- **Arthur Whitney** - Creator of the K and Q languages
- **Adam Jacobs** - His comments and insight over the years regarding the K interpreter have provided invaluable inspiration and information.
- **Joel Kaplan** - He gave me the chance to learn K. His warning over a decade ago "Once you learn K it will change your mind and you will never think about programming the same way" has proven to be remarkably accurate.
- **Stevan Apter** - His K parser at nsl.com has been a really helpful source of inspiration and reference. Stevan, together with **Sasha Katsman** and **Michael Rosenberg**, greatly helped in my understanding of traditional "idiomatic K".
- **John Earnest** - His oK interpreter was an important inspiration for deciding to develop ksharp. Additionally, his regular questioning of AI assisted development has been an outstanding motivation for pushing the limits and exploring what's possible.

## Note regarding project name

This project is ksharp in all lowercase, because it is an interpreter for the k language written in C sharp. Unfortunately there are other projects with very similar names that differ just in capitalization, like Ksharp kSharp, KSHarp, etc. This project is not related to any of them and the name ksharp is not intended to claim any relationship to any of them, the only implied relationships are the k language (as the model) and C sharp (as the tool)

---

**🚀 Try it out: `dotnet run` and start exploring ksharp!**
