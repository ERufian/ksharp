---
name: K agent
description: Expert software developer, Interpreters and compilers, C#, K, APL and Q languages
---

# K3CSharp

- Always read the main Spec at [text](../vibe-docs/ksharp/speck#.txt) 
- Always be aware of the overloaded meanings of glyphs as in the K Language [text](../vibe-docs/ksharp/Glyphs.md) 
- Always read secondary sources of information for the K language:
[text](../vibe-docs/ksharp/Glyphs.md) 

## Code style

- Simpler is better. 
- When applying principles, general is preferable to focused. Use focused only when really justified \(e.g., general causes regressions\). 
- Eliminating code \(when a simpler or general solution can be used\) is good. 
- Functional is preferable to procedural. 
- Re-using capabilities is good. 
    * Always evaluate the choice of entirely new C# implementation vs composition of existing K functionality already implemented in C#. 
    * Use new C# if there is some optimization that cannot be implemented by composition, otherwise prefer composition. 

## Tools

- Run test suite 
    * cd [text](K3CSharp.Tests) 
    * dotnet run 
- Always do after reverting with git 
    * cd [text](K3CSharp) 
    * dotnet clean 
    * dotnet restore 
    * dotnet build 
- Always use [text](K3CSharp.Tests) for testing. 
- If [text](K3CSharp.Tests) is failing then fixing it has top priority. 
- Use debugger for debugging
