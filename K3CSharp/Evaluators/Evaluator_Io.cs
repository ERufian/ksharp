using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp;

public partial class Evaluator
{
    // I/O Verbs - digit-colon operators (0: through 9:)
    // Based on io_verbs.txt speclet
    
    // Monadic I/O verbs (single argument)
    private K3Value IoVerbMonadic(K3Value operand, int digit)
    {
        return digit switch
        {
            0 => ReadText(operand),           // READ TEXT
            1 => ReadMemoryMappedKData(operand), // READ MEMORY MAPPED K DATA
            2 => ReadRawKData(operand),       // READ RAW K DATA
            3 => OpenClosePort(operand),       // OPEN/CLOSE IPC CONNECTION
            4 => GetTypeCode(operand),         // TYPE (existing implementation)
            5 => StringRepresentation(operand), // STRING REPRESENTATION (existing implementation)
            6 => ReadBytes(operand),          // READ BYTES
            7 => throw new NotImplementedException("7: reserved for future use (direct memory access and P/Invoke)"),
            8 => throw new NotImplementedException("8: reserved for future use (shared memory, fork and create process)"),
            9 => throw new NotImplementedException("9: reserved for future use (threads and fibers)"),
            _ => throw new ArgumentException($"Invalid I/O verb digit: {digit}")
        };
    }
    
    // Dyadic I/O verbs (two arguments)
    private K3Value IoVerbDyadic(K3Value left, K3Value right, int digit)
    {
        return digit switch
        {
            0 => left is VectorValue { Elements.Count: 2, VectorType: 0 }
                ? LoadTextFileAsFields(left, right)
                : WriteText(left, right),
            1 => left switch
            {
                VectorValue { Elements.Count: 2, VectorType: 0 } => LoadBinaryFileAsFields(left, right),
                CharacterValue { Value: "c" or "i" or "d" } => LoadBinaryFileSpecial(left, right),
                _ => WriteData(left, right)
            },
            2 => WriteMemoryMappedKData(left, right), // WRITE MEMORY MAPPED K DATA (and FFI dynamic load)
            3 => IpcSet(left, right),            // IPC SET
            4 => IpcGet(left, right),            // IPC GET
            5 => AppendData(left, right),          // APPEND DATA
            6 => WriteBytes(left, right),          // WRITE BYTES
            7 => throw new NotImplementedException("7: reserved for future use (direct memory access and P/Invoke)"),
            8 => throw new NotImplementedException("8: reserved for future use (shared memory, fork and create process)"),
            9 => throw new NotImplementedException("9: reserved for future use (threads and fibers)"),
            _ => throw new ArgumentException($"Invalid I/O verb digit: {digit}")
        };
    }
    
    // Existing implementations moved from Evaluator.cs
    private K3Value GetTypeCode(K3Value value)
    {
        if (value is IntegerValue)
            return new IntegerValue(1);
        if (value is LongValue)
            return new IntegerValue(64);
        if (value is FloatValue)
            return new IntegerValue(2);
        if (value is CharacterValue)
            return new IntegerValue(3);
        if (value is SymbolValue)
            return new IntegerValue(4);
        if (value is DictionaryValue)
            return new IntegerValue(5);
        if (value is NullValue)
            return new IntegerValue(6);
        if (value is VectorValue vector)
        {
            if (vector.Elements.Count == 0)
                return new IntegerValue(-1); // Empty vector (assume integer vector by default)
            if (vector.Elements.All(x => x is IntegerValue))
                return new IntegerValue(-1); // Integer vector
            if (vector.Elements.All(x => x is LongValue))
                return new IntegerValue(-64); // Long vector
            if (vector.Elements.All(x => x is FloatValue))
                return new IntegerValue(-2); // Float vector
            if (vector.Elements.All(x => x is CharacterValue))
                return new IntegerValue(-3); // Character vector
            if (vector.Elements.All(x => x is SymbolValue))
                return new IntegerValue(-4); // Symbol vector
            if (vector.Elements.All(x => x is DictionaryValue))
                return new IntegerValue(-5); // Dictionary vector
            if (vector.Elements.All(x => x is VectorValue))
                return new IntegerValue(0); // Nested vector (generic list)
        }
        
        return new IntegerValue(0); // Default to generic list
    }
    
    private K3Value StringRepresentation(K3Value value)
    {
        // 5: verb - produce string representation of argument with proper escaping
        // Use raw ToString() without additional escaping to avoid double-escaping
        string representation = value.ToString();
        
        // Create character vector directly - each character as separate CharacterValue
        var charElements = new List<K3Value>();
        foreach (char c in representation)
        {
            // Create CharacterValue for each character without additional processing
            charElements.Add(new CharacterValue(c.ToString()));
        }
        return new VectorValue(charElements, -3);
    }
    
    // Stub implementations for new I/O verbs
    private K3Value ReadText(K3Value operand)
    {
        try
        {
            string path;
            string separator = "\n"; // Default line separator
            
            // Handle operand types: symbol, character vector, or list with path and separator
            if (operand is SymbolValue sym)
            {
                path = sym.Value;
            }
            else if (operand is CharacterValue charVal)
            {
                path = charVal.Value.ToString();
            }
            else if (operand is VectorValue vec && vec.Elements.Count >= 2)
            {
                // First element is path, second is separator
                var pathElement = vec.Elements[0];
                var separatorElement = vec.Elements[1];
                
                path = pathElement switch
                {
                    SymbolValue s => s.Value,
                    CharacterValue c => c.Value.ToString(),
                    _ => throw new Exception("0: path must be symbol or character vector")
                };
                
                separator = separatorElement switch
                {
                    SymbolValue s => s.Value,
                    CharacterValue c => c.Value.ToString(),
                    VectorValue sepVec when sepVec.Elements.Count > 0 => string.Join("", sepVec.Elements.OfType<CharacterValue>().Select(cv => cv.Value)),
                    _ => throw new Exception("0: separator must be symbol or character vector")
                };
            }
            else
            {
                throw new Exception("0: argument must be symbol, character vector, or list with path and separator");
            }
            
            // Handle standard input
            if (string.IsNullOrEmpty(path))
            {
                return ReadFromStandardInput();
            }
            
            // Read file with UTF-8 encoding
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var streamReader = new StreamReader(fileStream, System.Text.Encoding.UTF8);
            
            var lines = new List<K3Value>();
            string? line;
            
            while ((line = streamReader.ReadLine()) != null)
            {
                // Convert line to character vector (each character as separate CharacterValue)
                var charElements = new List<K3Value>();
                foreach (char c in line)
                {
                    charElements.Add(new CharacterValue(c.ToString()));
                }
                lines.Add(new VectorValue(charElements, -3)); // -3 indicates character vector type
            }
            
            return new VectorValue(lines, 0); // 0 indicates generic list (list of character vectors)
        }
        catch (Exception ex)
        {
            // Convert exceptions to K signals
            throw new Exception($"0: {ex.Message}");
        }
    }
    
    private K3Value ReadFromStandardInput()
    {
        var lines = new List<K3Value>();
        
        try
        {
            while (true)
            {
                string? line = Console.ReadLine();
                if (line == null) break; // EOF reached
                
                // Convert line to character vector (each character as separate CharacterValue)
                var charElements = new List<K3Value>();
                foreach (char c in line)
                {
                    charElements.Add(new CharacterValue(c.ToString()));
                }
                lines.Add(new VectorValue(charElements, -3)); // -3 indicates character vector type
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl-C pressed - terminate gracefully
        }
        
        return new VectorValue(lines, 0); // 0 indicates generic list (list of character vectors)
    }
    
    private K3Value ReadMemoryMappedKData(K3Value operand)
    {
        try
        {
            // Get file path from operand (symbol or character vector)
            string path = GetPathFromValue(operand);
            
            // Ensure .l extension
            path = EnsureLExtension(path);
            
            // Try to validate file and get vector type information
            var (isValid, vectorType, length) = MemoryMappedFileUtils.ValidateKDataFile(path);
            
            if (isValid && MemoryMappedFileUtils.IsOptimizableType(vectorType))
            {
                // Create optimized memory-mapped vector
                return new MemoryMappedKVector(path, vectorType, length);
            }
            else
            {
                // For non-optimizable types or validation failures, fall back to regular ReadRawKData
                // This ensures identical behavior to 2: for all data types
                return ReadRawKData(operand);
            }
        }
        catch (Exception ex)
        {
            // Re-throw as K signal with same format as ReadRawKData
            throw new Exception(ex.Message);
        }
    }
    
    private K3Value ReadRawKData(K3Value operand)
    {
        try
        {
            // Get file path from operand (symbol or character vector)
            string path = GetPathFromValue(operand);
            
            // Ensure .l extension
            path = EnsureLExtension(path);
            
            // Read entire file into memory
            if (!File.Exists(path))
            {
                throw new Exception($"The system cannot find the file specified: {path}");
            }
            
            var fileBytes = File.ReadAllBytes(path);
            
            // Validate file header (first 8 bytes should be: FD FF FF FF 01 00 00 00)
            byte[] expectedHeader = new byte[] { 0xFD, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00 };
            if (fileBytes.Length < expectedHeader.Length)
            {
                throw new Exception("Invalid K data file");
            }
            
            for (int i = 0; i < expectedHeader.Length; i++)
            {
                if (fileBytes[i] != expectedHeader[i])
                {
                    throw new Exception("Invalid K data file");
                }
            }
            
            // Discard file header, get data portion
            var dataBytes = fileBytes.Skip(expectedHeader.Length).ToArray();
            
            // Construct _bd message with standard header: 01 00 00 00 + 4-byte length + data
            var bdMessage = new List<byte>();
            
            // Standard _bd header (octal: \001\000\000\000)
            bdMessage.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 });
            
            // 4-byte length in little-endian format
            byte[] lengthBytes = BitConverter.GetBytes(dataBytes.Length);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            bdMessage.AddRange(lengthBytes);
            
            // Add the data
            bdMessage.AddRange(dataBytes);
            
            // Convert to character vector for _db function
            var charElements = new List<K3Value>();
            for (int i = 0; i < bdMessage.Count; i++)
            {
                charElements.Add(new CharacterValue(((char)bdMessage[i]).ToString()));
            }
            
            var bdVector = new VectorValue(charElements, -3);
            
            // Use existing _db function to deserialize
            return DbFunction(bdVector);
        }
        catch (Exception ex)
        {
            // Re-throw as K signal
            throw new Exception(ex.Message);
        }
    }
    
    private K3Value OpenClosePort(K3Value operand)
    {
        if (TryGetHandle(operand, out int handle))
        {
            CloseIpcConnection(handle);
            return new NullValue();
        }

        return new IntegerValue(OpenIpcConnection(operand));
    }
    
    private K3Value ReadBytes(K3Value operand)
    {
        try
        {
            string path;
            
            // Handle operand: symbol, character vector, or list with path and separator
            if (operand is SymbolValue || (operand is CharacterValue))
            {
                path = GetPathFromValue(operand);
            }
            else if (operand is VectorValue vec && vec.Elements.Count >= 2)
            {
                // List format: [path, separator] - ignore separator for 6: (raw bytes)
                path = GetPathFromValue(vec.Elements[0]);
            }
            else
            {
                throw new Exception("6: argument must be symbol, character vector, or list with path and separator");
            }
            
            // Handle standard input (not applicable for raw bytes - return empty)
            if (string.IsNullOrEmpty(path))
            {
                return new VectorValue(new List<K3Value>(), -3); // Empty character vector
            }
            
            // Read all bytes from file using raw byte access
            byte[] allBytes;
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var memoryStream = new MemoryStream())
                {
                    fileStream.CopyTo(memoryStream);
                    allBytes = memoryStream.ToArray();
                }
            }
            
            // Convert each byte to a character (raw byte-to-char mapping)
            var charElements = new List<K3Value>();
            foreach (byte b in allBytes)
            {
                // Direct byte-to-char mapping without encoding interpretation
                charElements.Add(new CharacterValue(((char)b).ToString()));
            }
            
            return new VectorValue(charElements, -3); // -3 indicates character vector type
        }
        catch (Exception ex)
        {
            // Convert exceptions to K signals with same format as other I/O verbs
            throw new Exception($"6: {ex.Message}");
        }
    }
    
    private K3Value WriteText(K3Value left, K3Value right)
    {
        try
        {
            string path;
            
            // Handle left argument (path): symbol or character vector
            path = left switch
            {
                SymbolValue sym => sym.Value,
                CharacterValue charVal => charVal.Value.ToString(),
                _ => throw new Exception("0: output path must be symbol or character vector")
            };
            
            // Handle standard output
            if (string.IsNullOrEmpty(path))
            {
                WriteToStandardOutput(right);
                return new NullValue(); // Return null as specified
            }
            
            // Write to file with UTF-8 encoding and platform-specific line endings
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8);
            
            string lineEnding = Environment.NewLine; // Platform-specific line endings
            
            // Handle right argument (data to write)
            if (right is VectorValue vec)
            {
                // Check if this is a list of lists (structured data with separator)
                // Only treat as structured data if elements are actual nested vectors (not character vectors)
                if (vec.Elements.Count > 0 && vec.Elements[0] is VectorValue nestedVec && nestedVec.VectorType != -3)
                {
                    // This is structured data - write as fields with separators
                    WriteStructuredData(streamWriter, vec, lineEnding);
                }
                else
                {
                    // This is a simple list - write each item on its own line
                    WriteSimpleList(streamWriter, vec, lineEnding);
                }
            }
            else
            {
                // Single item - write it and add line ending
                streamWriter.Write(right.ToString());
                streamWriter.Write(lineEnding);
            }
            
            streamWriter.Flush();
            return new NullValue(); // Return null as specified
        }
        catch (Exception ex)
        {
            // Convert exceptions to K signals
            throw new Exception($"0: {ex.Message}");
        }
    }
    
    private void WriteStructuredData(TextWriter writer, VectorValue data, string lineEnding)
    {
        // Default separator is comma (CSV)
        string separator = ",";
        
        // Check if first element is a list with separator specification
        if (data.Elements.Count >= 2 && data.Elements[0] is VectorValue firstVec && firstVec.Elements.Count == 1)
        {
            // Check if this is a separator specification (path, separator) pattern
            // For now, assume comma separator for CSV
            separator = ",";
        }
        
        foreach (var element in data.Elements)
        {
            if (element is VectorValue lineVec)
            {
                // Write each field in the line
                var fields = new List<string>();
                foreach (var field in lineVec.Elements)
                {
                    string fieldText = field.ToString();
                    
                    // Apply CSV escaping if separator is comma
                    if (separator == ",")
                    {
                        fieldText = EscapeCsvField(fieldText);
                    }
                    
                    fields.Add(fieldText);
                }
                
                writer.WriteLine(string.Join(separator, fields));
            }
            else
            {
                // Single item in line
                writer.WriteLine(element.ToString());
            }
        }
    }
    
    private void WriteSimpleList(TextWriter writer, VectorValue data, string lineEnding)
    {
        foreach (var item in data.Elements)
        {
            // For character vectors, use ToString() result but remove outermost enclosing quotes
            if (item is VectorValue charVec && charVec.VectorType == -3)
            {
                string toStringResult = item.ToString();
                if (toStringResult.StartsWith("\"") && toStringResult.EndsWith("\"") && toStringResult.Length > 2)
                {
                    writer.Write(toStringResult.Substring(1, toStringResult.Length - 2));
                }
                else
                {
                    writer.Write(toStringResult);
                }
            }
            else
            {
                writer.Write(item.ToString());
            }
            writer.Write(lineEnding);
        }
    }
    
    private string EscapeCsvField(string field)
    {
        // RFC 4180 CSV escaping
        if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
        {
            // Escape quotes by doubling them and wrap in quotes
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
    
    private void WriteToStandardOutput(K3Value data)
    {
        try
        {
            string lineEnding = Environment.NewLine;
            
            if (data is VectorValue vec)
            {
                // Check if this is a list of lists (structured data)
                if (vec.Elements.Count > 0 && vec.Elements[0] is VectorValue)
                {
                    WriteStructuredData(Console.Out, vec, lineEnding);
                }
                else
                {
                    WriteSimpleList(Console.Out, vec, lineEnding);
                }
            }
            else
            {
                Console.WriteLine(data.ToString());
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"0: stdout write failed: {ex.Message}");
        }
    }
    
    private K3Value WriteMemoryMappedKData(K3Value left, K3Value right)
    {
        // Implement dyadic 2: for .NET assembly loading
        // Syntax: "assembly.dll" 2: `System.TypeName`
        
        if (left is CharacterValue charValue && right is SymbolValue symValue)
        {
            return LoadDotNetAssembly(charValue.Value, symValue.Value);
        }
        else if (left is VectorValue vector && vector.VectorType == -3 && right is SymbolValue symbolType) // -3 = character vector
        {
            // Extract string from character vector
            var chars = vector.Elements.Select(e => e.ToString().Trim('"')).ToArray();
            var charVectorPath = string.Join("", chars);
            return LoadDotNetAssembly(charVectorPath, symbolType.Value);
        }
        else if (left is SymbolValue assemblyName && right is SymbolValue typeNameSymbol)
        {
            // Try to load by assembly name (e.g., "System.Core" 2: `System.Math)
            return LoadDotNetAssembly(assemblyName.Value, typeNameSymbol.Value);
        }
        else
        {
            throw new Exception("2: assembly loading requires character vector (assembly path/name) and symbol (type name)");
        }
    }
    
    private K3Value LoadDotNetAssembly(string assemblyPath, string typeName)
    {
        try
        {
            // Load the assembly
            System.Reflection.Assembly assembly;
            
            if (assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
                assemblyPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // Load from file path
                string fullPath = System.IO.Path.GetFullPath(assemblyPath);
                if (!System.IO.File.Exists(fullPath))
                {
                    throw new Exception($"Assembly file not found: {fullPath}");
                }
                assembly = System.Reflection.Assembly.LoadFrom(fullPath);
            }
            else
            {
                // Load by assembly name
                assembly = System.Reflection.Assembly.Load(assemblyPath);
            }
            
            // Find the specified type
            var type = assembly.GetTypes()
                .FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
                
            if (type == null)
            {
                throw new Exception($"Type '{typeName}' not found in assembly '{assemblyPath}'");
            }
            
            // Store the assembly in the _dotnet tree
            ForeignFunctionInterface.StoreAssemblyInDotNetTree(assembly);
            
            // Create the type dictionary
            var typeDict = ForeignFunctionInterface.CreateNetTypeDictionary(type);
            
            // Extract the simple type name (without namespace) for dotnet branch
            var dotnetPath = $"_dotnet.{type.Namespace}.{type.Name}";
            var absolutePath = $".{dotnetPath}";
            
            // Store the type dictionary directly in the KTree using absolute path
            kTree.SetValue(absolutePath, typeDict);
            
            // Return the absolute path symbol as specified in the spec
            return new SymbolValue(absolutePath);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load .NET assembly '{assemblyPath}' type '{typeName}': {ex.Message}", ex);
        }
    }
    
    private K3Value WriteData(K3Value left, K3Value right)
    {
        try
        {
            // Get file path from left argument (symbol or character vector)
            string path = GetPathFromValue(left);
            
            // Ensure .l extension
            path = EnsureLExtension(path);
            
            // Write K data file using shared helper
            WriteKDataFile(path, right);
            
            // Return null per specification
            return new NullValue();
        }
        catch (Exception ex)
        {
            // Re-throw as K signal
            throw new Exception(ex.Message);
        }
    }
    
    private K3Value IpcGet(K3Value left, K3Value right)
    {
        if (TryGetHandle(left, out int handle))
        {
            return SendSyncIpc(handle, right);
        }

        if (IsSelfConnectionSpec(left))
        {
            return SendSyncSelfIpc(right);
        }

        int transientHandle = OpenIpcConnection(left);
        try
        {
            return SendSyncIpc(transientHandle, right);
        }
        finally
        {
            CloseIpcConnection(transientHandle);
        }
    }
    
    private K3Value IpcSet(K3Value left, K3Value right)
    {
        if (TryGetHandle(left, out int handle))
        {
            SendAsyncIpc(handle, right);
            return new NullValue();
        }

        if (IsSelfConnectionSpec(left))
        {
            SendAsyncSelfIpc(right);
            return new NullValue();
        }

        int transientHandle = OpenIpcConnection(left);
        try
        {
            SendAsyncIpc(transientHandle, right);
            return new NullValue();
        }
        finally
        {
            CloseIpcConnection(transientHandle);
        }
    }
    
    private K3Value AppendData(K3Value left, K3Value right)
    {
        try
        {
            string path = GetPathFromValue(left);
            path = EnsureLExtension(path);

            // Ensure right argument is treated as a general list of elements to append.
            // Always extract elements from VectorValue to handle typed vectors that
            // may have been auto-detected (e.g., (1;2) becoming an integer vector).
            VectorValue newList;
            if (right is VectorValue rightVec)
            {
                newList = new VectorValue(new List<K3Value>(rightVec.Elements), 0);
            }
            else
            {
                newList = new VectorValue(new List<K3Value> { right }, 0);
            }

            // If file does not exist, behave like dyadic 1: but return the count
            if (!File.Exists(path))
            {
                WriteKDataFile(path, newList);
                return new IntegerValue(newList.Elements.Count);
            }

            // Try optimized in-place append first; fall back to full re-serialization on failure
            if (TryOptimizedAppend(path, newList, out int newCount))
            {
                return new IntegerValue(newCount);
            }

            return FullAppend(path, newList);
        }
        catch (Exception ex)
        {
            throw new Exception($"5: {ex.Message}");
        }
    }

    /// <summary>
    /// Optimized append that updates the list count and appends element data
    /// without reading/writing the entire file. Only works for general lists.
    /// </summary>
    private bool TryOptimizedAppend(string path, VectorValue newList, out int newCount)
    {
        newCount = 0;

        try
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            // Read and verify file header (8 bytes)
            byte[] fileHeader = new byte[8];
            if (fileStream.Read(fileHeader, 0, 8) != 8)
                return false;

            byte[] expectedHeader = new byte[] { 0xFD, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00 };
            for (int i = 0; i < 8; i++)
            {
                if (fileHeader[i] != expectedHeader[i])
                    return false;
            }

            // Read list type and count (8 bytes at offset 8)
            byte[] listHeader = new byte[8];
            if (fileStream.Read(listHeader, 0, 8) != 8)
                return false;

            int listType = BitConverter.ToInt32(listHeader, 0);
            int listCount = BitConverter.ToInt32(listHeader, 4);

            // Only optimize for general lists (type 0)
            if (listType != 0)
                return false;

            // Serialize new data using _bd
            var bdResult = BdFunction(newList);
            if (!(bdResult is VectorValue bdVector && bdVector.VectorType == -3))
                return false;

            var bdBytes = new List<byte>();
            foreach (var element in bdVector.Elements.OfType<CharacterValue>())
            {
                bdBytes.Add((byte)element.Value[0]);
            }

            // Need at least 16 bytes (8-byte message header + 8-byte list header)
            if (bdBytes.Count < 16)
                return false;

            // Extract element data: skip _bd message header (8) + list header (8)
            var elementData = bdBytes.Skip(16).ToArray();

            // Append element data at end of file
            fileStream.Seek(0, SeekOrigin.End);
            fileStream.Write(elementData, 0, elementData.Length);

            // Update count in place at offset 12 (file header 8 + type 4)
            newCount = listCount + newList.Elements.Count;
            byte[] newCountBytes = BitConverter.GetBytes(newCount);
            fileStream.Seek(12, SeekOrigin.Begin);
            fileStream.Write(newCountBytes, 0, 4);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Full append by reading, deserializing, combining, re-serializing, and writing.
    /// Used as fallback when optimized append cannot be used.
    /// </summary>
    private K3Value FullAppend(string path, VectorValue newList)
    {
        // Read existing file
        var fileBytes = File.ReadAllBytes(path);

        // Validate file header
        byte[] expectedHeader = new byte[] { 0xFD, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00 };
        if (fileBytes.Length < expectedHeader.Length)
        {
            throw new Exception("Invalid K data file");
        }

        for (int i = 0; i < expectedHeader.Length; i++)
        {
            if (fileBytes[i] != expectedHeader[i])
            {
                throw new Exception("Invalid K data file");
            }
        }

        // Construct _bd message from data portion
        var dataBytes = fileBytes.Skip(expectedHeader.Length).ToArray();
        var bdMessage = new List<byte>();
        bdMessage.AddRange(new byte[] { 0x01, 0x00, 0x00, 0x00 });
        byte[] lengthBytes = BitConverter.GetBytes(dataBytes.Length);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }
        bdMessage.AddRange(lengthBytes);
        bdMessage.AddRange(dataBytes);

        // Convert to character vector for _db
        var charElements = new List<K3Value>();
        for (int i = 0; i < bdMessage.Count; i++)
        {
            charElements.Add(new CharacterValue(((char)bdMessage[i]).ToString()));
        }

        // Deserialize existing data
        var existingValue = DbFunction(new VectorValue(charElements, -3));

        // Combine existing and new data as a general list
        var combinedElements = new List<K3Value>();

        if (existingValue is VectorValue existingVec && existingVec.VectorType == 0)
        {
            combinedElements.AddRange(existingVec.Elements);
        }
        else
        {
            combinedElements.Add(existingValue);
        }

        combinedElements.AddRange(newList.Elements);

        var combinedList = new VectorValue(combinedElements, 0);

        // Write combined list to file
        WriteKDataFile(path, combinedList);

        return new IntegerValue(combinedElements.Count);
    }

    /// <summary>
    /// Writes a K value as a K data file (used by both dyadic 1: and 5:)
    /// </summary>
    private void WriteKDataFile(string path, K3Value value)
    {
        var bdResult = BdFunction(value);

        if (bdResult is VectorValue bdVector && bdVector.VectorType == -3)
        {
            var bdBytes = new List<byte>();
            foreach (var element in bdVector.Elements.OfType<CharacterValue>())
            {
                bdBytes.Add((byte)element.Value[0]);
            }

            if (bdBytes.Count < 8)
            {
                throw new Exception("Serialized data too short");
            }

            var dataBytes = bdBytes.Skip(8).ToArray();

            byte[] fileHeader = new byte[] { 0xFD, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00 };
            var fileBytes = new List<byte>();
            fileBytes.AddRange(fileHeader);
            fileBytes.AddRange(dataBytes);

            File.WriteAllBytes(path, fileBytes.ToArray());
        }
        else
        {
            throw new Exception("_bd function did not return character vector");
        }
    }
    
    private K3Value WriteBytes(K3Value left, K3Value right)
    {
        try
        {
            string path;
            
            // Handle left argument (path): symbol or character vector
            path = left switch
            {
                SymbolValue sym => sym.Value,
                CharacterValue charVal => charVal.Value.ToString(),
                _ => throw new Exception("6: output path must be symbol or character vector")
            };
            
            // Handle standard output (not applicable for raw bytes - write to console as characters)
            if (string.IsNullOrEmpty(path))
            {
                // For raw bytes to standard output, write characters directly
                if (right is VectorValue && ((VectorValue)right).VectorType == -3)
                {
                    // Character vector - write each character
                    var charVector = (VectorValue)right;
                    foreach (var element in charVector.Elements.OfType<CharacterValue>())
                    {
                        Console.Write(element.Value);
                    }
                }
                else
                {
                    // Convert to string and write
                    Console.Write(right.ToString());
                }
                return new NullValue(); // Return null as specified
            }
            
            // Write raw bytes to file without encoding
            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // Handle right argument (data to write as raw bytes)
                if (right is VectorValue && ((VectorValue)right).VectorType == -3)
                {
                    // Character vector - convert each character to byte and write
                    var charVector = (VectorValue)right;
                    foreach (var element in charVector.Elements.OfType<CharacterValue>())
                    {
                        if (element.Value.Length > 0)
                        {
                            byte byteValue = (byte)element.Value[0]; // Raw char-to-byte mapping
                            fileStream.WriteByte(byteValue);
                        }
                    }
                }
                else if (right is CharacterValue charVal)
                {
                    // Single character - write as byte
                    if (charVal.Value.Length > 0)
                    {
                        byte byteValue = (byte)charVal.Value[0];
                        fileStream.WriteByte(byteValue);
                    }
                }
                else
                {
                    // Convert to string and write each character as byte
                    string text = right.ToString();
                    foreach (char c in text)
                    {
                        byte byteValue = (byte)c;
                        fileStream.WriteByte(byteValue);
                    }
                }
                
                fileStream.Flush();
            }
            return new NullValue(); // Return null as specified
        }
        catch (Exception ex)
        {
            // Convert exceptions to K signals with same format as other I/O verbs
            throw new Exception($"6: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Extract file path from a K value (symbol or character vector)
    /// </summary>
    private string GetPathFromValue(K3Value value)
    {
        if (value is SymbolValue symbol)
        {
            return symbol.Value;
        }
        else if (value is VectorValue vec && vec.VectorType == -3)
        {
            // Character vector - concatenate characters
            return string.Concat(vec.Elements.OfType<CharacterValue>().Select(cv => cv.Value));
        }
        else
        {
            throw new Exception("Path must be a symbol or character vector");
        }
    }
    
    /// <summary>
    /// Ensure file path has .l extension according to specification
    /// </summary>
    private string EnsureLExtension(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return ".l";
        }
        
        string extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension) || extension.ToLower() != ".l")
        {
            return path + ".l";
        }
        
        return path;
    }

    // ===================== Load Text File as Fields =====================

    /// <summary>
    /// (s;w) 0: f  or  (s;w) 0: (f;b;n)
    /// Load a text file with fixed-width fields.
    /// s = type codes (I=integer, F=float, C=char vector, S=symbol, space=skip)
    /// w = field widths
    /// </summary>
    private K3Value LoadTextFileAsFields(K3Value left, K3Value right)
    {
        try
        {
            // Parse left argument (s;w)
            var (typeCodes, widths) = ParseTextFieldSpec(left);

            // Parse right argument (f or (f;b;n))
            var (path, offset, length) = GetFilePathAndRange(right);

            // Read file content
            byte[] fileBytes;
            if (!File.Exists(path))
                throw new Exception($"The system cannot find the file specified: {path}");

            if (offset.HasValue && length.HasValue)
            {
                fileBytes = new byte[length.Value];
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                fs.Seek(offset.Value, SeekOrigin.Begin);
                int read = fs.Read(fileBytes, 0, length.Value);
                if (read < length.Value)
                    Array.Resize(ref fileBytes, read);
            }
            else
            {
                fileBytes = File.ReadAllBytes(path);
            }

            // Convert bytes to text (assume UTF-8, but K uses raw bytes for text I/O)
            string text = System.Text.Encoding.UTF8.GetString(fileBytes);

            // Split into lines (handle both \n and \r\n)
            var rawLines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var lines = rawLines.Where(l => !string.IsNullOrEmpty(l)).ToList();

            int totalWidth = widths.Sum();
            int fieldCount = typeCodes.Length;

            // Validate line lengths (all lines must equal total width)
            foreach (var line in lines)
            {
                if (line.Length != totalWidth)
                    throw new Exception($"length error: file line length {line.Length} != total field width {totalWidth}");
            }

            // Parse each line into fields and collect by column
            var columns = new List<List<K3Value>>();
            for (int f = 0; f < fieldCount; f++)
                columns.Add(new List<K3Value>());

            foreach (var line in lines)
            {
                int pos = 0;
                for (int f = 0; f < fieldCount; f++)
                {
                    string fieldText = line.Substring(pos, widths[f]);
                    columns[f].Add(ParseTextField(fieldText, typeCodes[f]));
                    pos += widths[f];
                }
            }

            // Build result: list of field vectors
            var resultElements = new List<K3Value>();
            for (int f = 0; f < fieldCount; f++)
            {
                int vectorType = typeCodes[f] switch
                {
                    'I' => -1,
                    'F' => -2,
                    'C' => 0,  // list of character vectors (one per row)
                    'S' => -4,
                    ' ' => 0,
                    _ => 0
                };
                resultElements.Add(new VectorValue(columns[f], vectorType));
            }

            return new VectorValue(resultElements, 0);
        }
        catch (Exception ex)
        {
            throw new Exception($"0: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse the left argument (s;w) for text file loading.
    /// Returns type codes string and widths array.
    /// </summary>
    private (char[] typeCodes, int[] widths) ParseTextFieldSpec(K3Value left)
    {
        if (left is not VectorValue vec || vec.Elements.Count != 2)
            throw new Exception("left argument must be a 2-item list (s;w)");

        // Extract s (type codes)
        var sElement = vec.Elements[0];
        string typeCodesStr;
        if (sElement is CharacterValue charVal)
        {
            typeCodesStr = charVal.Value;
        }
        else if (sElement is VectorValue charVec && charVec.VectorType == -3)
        {
            typeCodesStr = string.Concat(charVec.Elements.OfType<CharacterValue>().Select(c => c.Value));
        }
        else
        {
            throw new Exception("s must be a character vector");
        }

        // Extract w (widths)
        var wElement = vec.Elements[1];
        int[] widths;
        if (wElement is IntegerValue intVal)
        {
            widths = new[] { intVal.Value };
        }
        else if (wElement is VectorValue intVec && intVec.VectorType == -1)
        {
            widths = intVec.Elements.OfType<IntegerValue>().Select(iv => iv.Value).ToArray();
        }
        else if (wElement is VectorValue mixedVec)
        {
            widths = mixedVec.Elements.Select(e => e switch
            {
                IntegerValue iv => iv.Value,
                LongValue lv => (int)lv.Value,
                _ => throw new Exception("widths must be integers")
            }).ToArray();
        }
        else
        {
            throw new Exception("w must be an integer vector");
        }

        if (typeCodesStr.Length != widths.Length)
            throw new Exception($"length error: type codes count {typeCodesStr.Length} != widths count {widths.Length}");

        return (typeCodesStr.ToCharArray(), widths);
    }

    /// <summary>
    /// Parse a single text field according to its type code.
    /// </summary>
    private K3Value ParseTextField(string fieldText, char typeCode)
    {
        // Trim trailing whitespace for most types (except character which keeps padding)
        string trimmed = fieldText.TrimEnd();

        return typeCode switch
        {
            'I' => ParseIntegerField(trimmed),
            'F' => ParseFloatField(trimmed),
            'C' => new VectorValue(fieldText.Select(c => new CharacterValue(c.ToString())).Cast<K3Value>().ToList(), -3),
            'S' => new SymbolValue(trimmed.Trim()),
            ' ' => new NullValue(), // skip field
            _ => throw new Exception($"unknown type code: {typeCode}")
        };
    }

    private K3Value ParseIntegerField(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new IntegerValue(0);
        if (int.TryParse(text, out int intVal))
            return new IntegerValue(intVal);
        throw new Exception($"domain error: invalid integer '{text}'");
    }

    private K3Value ParseFloatField(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new FloatValue(0.0);
        // K format: 1e+50, 1.234, etc.
        string normalized = text.Replace("e+", "E").Replace("e-", "E-");
        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
            return new FloatValue(dblVal);
        throw new Exception($"domain error: invalid float '{text}'");
    }

    // ===================== Load Binary File as Fields =====================

    /// <summary>
    /// (s;w) 1: f  or  (s;w) 1: (f;b;n)
    /// Load a binary file with fixed-width fields.
    /// s = C type codes (c=char, b=byte, s=short, i=int, f=float, d=double, C=string, S=symbol, space=skip)
    /// w = field widths in bytes
    /// </summary>
    private K3Value LoadBinaryFileAsFields(K3Value left, K3Value right)
    {
        try
        {
            var (typeCodes, widths) = ParseBinaryFieldSpec(left);
            var (path, offset, length) = GetFilePathAndRange(right);

            if (!File.Exists(path))
                throw new Exception($"The system cannot find the file specified: {path}");

            byte[] fileBytes;
            if (offset.HasValue && length.HasValue)
            {
                fileBytes = new byte[length.Value];
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                fs.Seek(offset.Value, SeekOrigin.Begin);
                int read = fs.Read(fileBytes, 0, length.Value);
                if (read < length.Value)
                    Array.Resize(ref fileBytes, read);
            }
            else
            {
                fileBytes = File.ReadAllBytes(path);
            }

            int totalWidth = widths.Sum();
            if (fileBytes.Length % totalWidth != 0)
                throw new Exception($"length error: file length {fileBytes.Length} is not a multiple of field width {totalWidth}");

            int recordCount = fileBytes.Length / totalWidth;
            int fieldCount = typeCodes.Length;

            var columns = new List<List<K3Value>>();
            for (int f = 0; f < fieldCount; f++)
                columns.Add(new List<K3Value>());

            for (int r = 0; r < recordCount; r++)
            {
                int pos = r * totalWidth;
                for (int f = 0; f < fieldCount; f++)
                {
                    columns[f].Add(ParseBinaryField(fileBytes, pos, widths[f], typeCodes[f]));
                    pos += widths[f];
                }
            }

            var resultElements = new List<K3Value>();
            for (int f = 0; f < fieldCount; f++)
            {
                int vectorType = typeCodes[f] switch
                {
                    'c' or 'b' or 's' or 'i' => -1,
                    'f' or 'd' => -2,
                    'C' => 0,  // list of character vectors (one per row)
                    'S' => -4,
                    ' ' => 0,
                    _ => 0
                };
                resultElements.Add(new VectorValue(columns[f], vectorType));
            }

            return new VectorValue(resultElements, 0);
        }
        catch (Exception ex)
        {
            throw new Exception($"1: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse the left argument (s;w) for binary file loading.
    /// </summary>
    private (char[] typeCodes, int[] widths) ParseBinaryFieldSpec(K3Value left)
    {
        if (left is not VectorValue vec || vec.Elements.Count != 2)
            throw new Exception("left argument must be a 2-item list (s;w)");

        var sElement = vec.Elements[0];
        string typeCodesStr;
        if (sElement is CharacterValue charVal)
        {
            typeCodesStr = charVal.Value;
        }
        else if (sElement is VectorValue charVec && charVec.VectorType == -3)
        {
            typeCodesStr = string.Concat(charVec.Elements.OfType<CharacterValue>().Select(c => c.Value));
        }
        else
        {
            throw new Exception("s must be a character vector");
        }

        var wElement = vec.Elements[1];
        int[] widths;
        if (wElement is IntegerValue intVal)
        {
            widths = new[] { intVal.Value };
        }
        else if (wElement is VectorValue intVec && intVec.VectorType == -1)
        {
            widths = intVec.Elements.OfType<IntegerValue>().Select(iv => iv.Value).ToArray();
        }
        else if (wElement is VectorValue mixedVec)
        {
            widths = mixedVec.Elements.Select(e => e switch
            {
                IntegerValue iv => iv.Value,
                LongValue lv => (int)lv.Value,
                _ => throw new Exception("widths must be integers")
            }).ToArray();
        }
        else
        {
            throw new Exception("w must be an integer vector");
        }

        if (typeCodesStr.Length != widths.Length)
            throw new Exception($"length error: type codes count {typeCodesStr.Length} != widths count {widths.Length}");

        return (typeCodesStr.ToCharArray(), widths);
    }

    /// <summary>
    /// Parse a single binary field from a byte array.
    /// </summary>
    private K3Value ParseBinaryField(byte[] data, int offset, int width, char typeCode)
    {
        return typeCode switch
        {
            'c' => new CharacterValue(((char)data[offset]).ToString()),
            'b' => new IntegerValue((sbyte)data[offset]),
            's' => new IntegerValue(BitConverter.ToInt16(data, offset)),
            'i' => new IntegerValue(BitConverter.ToInt32(data, offset)),
            'f' => new FloatValue(BitConverter.ToSingle(data, offset)),
            'd' => new FloatValue(BitConverter.ToDouble(data, offset)),
            'C' => new VectorValue(Enumerable.Range(offset, width).Select(i => new CharacterValue(((char)data[i]).ToString())).Cast<K3Value>().ToList(), -3),
            'S' => new SymbolValue(System.Text.Encoding.UTF8.GetString(data, offset, width).TrimEnd('\0')),
            ' ' => new NullValue(),
            _ => throw new Exception($"unknown binary type code: {typeCode}")
        };
    }

    // ===================== Load Binary File Special =====================

    /// <summary>
    /// c 1: f  or  c 1: (f;b;n)
    /// Load entire binary file as a single vector.
    /// c = "c" (character string), "i" (4-byte int vector), "d" (8-byte float vector)
    /// </summary>
    private K3Value LoadBinaryFileSpecial(K3Value left, K3Value right)
    {
        try
        {
            string mode = left is CharacterValue cv ? cv.Value
                : throw new Exception("left argument must be 'c', 'i', or 'd'");

            var (path, offset, length) = GetFilePathAndRange(right);

            if (!File.Exists(path))
                throw new Exception($"The system cannot find the file specified: {path}");

            byte[] fileBytes;
            if (offset.HasValue && length.HasValue)
            {
                fileBytes = new byte[length.Value];
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                fs.Seek(offset.Value, SeekOrigin.Begin);
                int read = fs.Read(fileBytes, 0, length.Value);
                if (read < length.Value)
                    Array.Resize(ref fileBytes, read);
            }
            else
            {
                fileBytes = File.ReadAllBytes(path);
            }

            return mode switch
            {
                "c" => new VectorValue(fileBytes.Select(b => new CharacterValue(((char)b).ToString())).Cast<K3Value>().ToList(), -3),
                "i" => LoadBinaryAsIntVector(fileBytes),
                "d" => LoadBinaryAsFloatVector(fileBytes),
                _ => throw new Exception($"unknown mode: {mode}")
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"1: {ex.Message}");
        }
    }

    private K3Value LoadBinaryAsIntVector(byte[] data)
    {
        if (data.Length % 4 != 0)
            throw new Exception($"length error: file length {data.Length} is not a multiple of 4");
        var elements = new List<K3Value>();
        for (int i = 0; i < data.Length; i += 4)
        {
            elements.Add(new IntegerValue(BitConverter.ToInt32(data, i)));
        }
        return new VectorValue(elements, -1);
    }

    private K3Value LoadBinaryAsFloatVector(byte[] data)
    {
        if (data.Length % 8 != 0)
            throw new Exception($"length error: file length {data.Length} is not a multiple of 8");
        var elements = new List<K3Value>();
        for (int i = 0; i < data.Length; i += 8)
        {
            elements.Add(new FloatValue(BitConverter.ToDouble(data, i)));
        }
        return new VectorValue(elements, -2);
    }

    // ===================== Helper: Extract path/offset/length from right argument =====================

    /// <summary>
    /// Parse right argument which can be f (path) or (f;b;n) (path, offset, length).
    /// Returns (path, optional offset, optional length).
    /// </summary>
    private (string path, int? offset, int? length) GetFilePathAndRange(K3Value right)
    {
        if (right is SymbolValue sym)
            return (sym.Value, null, null);

        if (right is CharacterValue charVal)
            return (charVal.Value, null, null);

        if (right is VectorValue vec && vec.VectorType == -3)
        {
            // Character vector path
            string path = string.Concat(vec.Elements.OfType<CharacterValue>().Select(c => c.Value));
            return (path, null, null);
        }

        if (right is VectorValue list && list.Elements.Count >= 3)
        {
            // (f;b;n) form
            var pathElement = list.Elements[0];
            string path = pathElement switch
            {
                SymbolValue s => s.Value,
                CharacterValue c => c.Value,
                VectorValue cv when cv.VectorType == -3 => string.Concat(cv.Elements.OfType<CharacterValue>().Select(c => c.Value)),
                _ => throw new Exception("path must be symbol or character vector")
            };

            int offset = list.Elements[1] switch
            {
                IntegerValue iv => iv.Value,
                LongValue lv => (int)lv.Value,
                _ => throw new Exception("offset must be an integer")
            };

            int length = list.Elements[2] switch
            {
                IntegerValue iv => iv.Value,
                LongValue lv => (int)lv.Value,
                _ => throw new Exception("length must be an integer")
            };

            return (path, offset, length);
        }

        throw new Exception("right argument must be a path or a list (path;offset;length)");
    }
}
