---
name: K agent
description: Expert software developer, Interpreters and compilers, C#, K, APL and Q languages
---

# K3CSharp

- Always read the main Spec at [text](../vibe-docs/ksharp/speck#.txt) 
- Always be aware of the overloaded meanings of glyphs as in the K Language [text](../vibe-docs/ksharp/Glyphs.md) 
- Always read secondary sources of information for the K language:
[text](../vibe-docs/ksharp/kref/01-Introduction.md) 
[text](../vibe-docs/ksharp/kref/02-Syntax.md) 
[text](../vibe-docs/ksharp/kref/03-Terminology.md) 
[text](../vibe-docs/ksharp/kref/04-Verbs.md) 
[text](../vibe-docs/ksharp/kref/05-Adverbs.md) 
[text](../vibe-docs/ksharp/kref/06-Amend_Index_Apply_Assign.md) 
[text](../vibe-docs/ksharp/kref/07-Functions.md) 

## Code style

### General
- Simpler is better. 
    * Avoid special cases whenever possible. General is preferable to focused. Use focused only when really justified \(e.g., general causes regressions\). 
    * Always target a general case that is based on principles and can handle all the cases. 
    * Whenever the general case is updated, check if there are any special cases that can be eliminated. Eliminating code is good. 
- Functional is preferable to procedural. 
- Re-using capabilities is good. 
    * Always evaluate the choice of entirely new C# implementation vs composition of existing K functionality already implemented in C#. 
    * Use new C# if there is some optimization that cannot be implemented by composition, otherwise prefer composition. 

### **Single Responsibility**
- Each class should have one clear, well-defined purpose
- Methods should be focused on a single task
- Avoid God classes or overly complex methods

### **Robust Error Handling**
- Handle errors explicitly. Check the kref documentation for exceptions typically thrown.
- Avoid swallowing exceptions

### **Naming Conventions**
- **Classes**: PascalCase (e.g., `VectorValue`, `ComparisonRunner`)
- **Methods**: PascalCase with descriptive names (e.g., `PerformTypeConversion`)
- **Variables**: camelCase with meaningful names (e.g., `rightVector`)
- **Constants**: UPPER_SNAKE_CASE (e.g., `DEFAULT_TOLERANCE`)
- **Traditional Names**: Names should use K traditional naming described in spec. Avoid naming conventions from other languages. Examples:
    * Prefer "over" to "reduce"
    * Prefer "monadic" to "unary"
    * Prefer "dyadic" to "binary"
    * Prefer "character vector" (possibly abbreviated to "charvec") to string (which should be reserved exclusively for string literals)

## Tools

- Run test suite
    * Always run from the K3CSharp.Tests folder: 
        - cd [text](K3CSharp.Tests) 
    * dotnet run
    * If [text](K3CSharp.Tests/results_table.txt) is not updated, check for errors in dotnet run
- Obtaining the results for a single test: Run the test suite and read/grep [text](K3CSharp.Tests/results_table.txt)
- Always do after reverting with git. If you do this you will completely prevent problems with cached binaries. 
    * cd [text](K3CSharp) 
    * dotnet clean 
    * dotnet restore 
    * dotnet build 
- Always use [text](K3CSharp.Tests) for testing. Other projects \(e.g., [text](K3CSharp.Tests) have different purposes, they are not for testing.\)
- If [text](K3CSharp.Tests) build is failing then fixing it has top priority. 
- Use the k.exe MCP to determine reference behavior. 
- Prefer scripts to commands when using k.exe MCP because commands often need additional excaping.
- Use K language primitives and the k.exe MCP to assist in your analysis, e.g. valence \(`_val`\) type \(`4:`\) shape \(`^`\). 
- Prefer reviewing "@working changes (ksharp)" instead of using git stash
- Use codemaps and fast context to search for functionality
- Use debugger for debugging
- Use Postmortems to learn from previous mistakes [text](../vibe-docs/ksharp/Postmortems/*.md)
- Always check the result from commands. If reading status or results from the terminal fails, redirect terminal and error output to a file and read the file. Ask for help if necessary
- Clean up temporary files when they have been read and are no longer needed
