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
            0 => WriteText(left, right),          // WRITE TEXT
            1 => WriteData(left, right),          // WRITE K DATA
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
            
            // Serialize the right argument using _bd function
            var bdResult = BdFunction(right);
            
            if (bdResult is VectorValue bdVector && bdVector.VectorType == -3)
            {
                // Convert character vector back to bytes
                var bdBytes = new List<byte>();
                foreach (var element in bdVector.Elements.OfType<CharacterValue>())
                {
                    bdBytes.Add((byte)element.Value[0]);
                }
                
                // Skip first 8 bytes (header and length) to get data portion
                if (bdBytes.Count < 8)
                {
                    throw new Exception("Serialized data too short");
                }
                
                var dataBytes = bdBytes.Skip(8).ToArray();
                
                // Create file with standard header: FD FF FF FF 01 00 00 00
                byte[] fileHeader = new byte[] { 0xFD, 0xFF, 0xFF, 0xFF, 0x01, 0x00, 0x00, 0x00 };
                
                // Write file: header + data
                var fileBytes = new List<byte>();
                fileBytes.AddRange(fileHeader);
                fileBytes.AddRange(dataBytes);
                
                File.WriteAllBytes(path, fileBytes.ToArray());
                
                // Return null per specification
                return new NullValue();
            }
            else
            {
                throw new Exception("_bd function did not return character vector");
            }
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
            string path;
            
            // Handle left argument (path): symbol or character vector
            path = left switch
            {
                SymbolValue sym => sym.Value,
                CharacterValue charVal => charVal.Value.ToString(),
                _ => throw new Exception("5: output path must be symbol or character vector")
            };
            
            // Handle standard output (not applicable for append - redirect to WriteText)
            if (string.IsNullOrEmpty(path))
            {
                // For append to standard output, just write to standard output
                WriteToStandardOutput(right);
                return new NullValue(); // Return null as specified
            }
            
            // Append to file with UTF-8 encoding and platform-specific line endings
            using var fileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None);
            using var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8);
            
            string lineEnding = Environment.NewLine; // Platform-specific line endings
            
            // Handle right argument (data to append) - same logic as WriteText
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
            // Convert exceptions to K signals with same format as other I/O verbs
            throw new Exception($"5: {ex.Message}");
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
}
