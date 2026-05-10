---
trigger: always_on
---

Use the following paths to locate documentation about the K language:
- Spec (specifications): T:\\_src\\github.com\\ERufian\\vibe-docs\\ksharp\\speck#.txt
- Speclets (standalone sub-sections that have been segregated to keep the spec smaller): T:\\_src\\github.com\\ERufian\\vibe-docs\\ksharp\\speclets\\*
- Hypotheses (specifications related to serialization/deserialization and I/O, produced from experimental results): T:\\_src\\github.com\\ERufian\\ksharp\\K3CSharp.SerializationResearch\\hypothesis_*
- K Reference manual: T:\\_src\\github.com\\ERufian\\vibe-docs\\ksharp\\kref\\*.md
- nsl parser: Copy at T:\\_src\\github.com\\ERufian\\vibe-docs\\ksharp\\nsl\\kparse.k, from canonical source at https://www.nsl.com/k/kparse/kparse.k
- Build the main project (ksharp.exe) from path T:\\_src\\github.com\\ERufian\\ksharp\\K3CSharp using: dotnet build
- Run the test project from path T:\\_src\\github.com\\ERufian\\ksharp\\K3CSharp.Tests using: dotnet run