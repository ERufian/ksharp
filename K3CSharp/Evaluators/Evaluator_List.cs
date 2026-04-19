using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using K3CSharp;

namespace K3CSharp
{
    public partial class Evaluator
    {
        // List and system-related functions
        
        // Helper method to extract string content from VectorValue
        private string ExtractStringFromVector(VectorValue vecVal)
        {
            // Check if this is a character vector (string)
            if (vecVal.Elements.All(e => e is CharacterValue))
            {
                // Extract the actual string content from character values
                var chars = vecVal.Elements.Select(e => ((CharacterValue)e).Value);
                return string.Concat(chars);
            }
            else
            {
                // For non-character vectors, use standard ToString
                return string.Concat(vecVal.Elements.Select(e => e.ToString()));
            }
        }
        
        // Dyadic implementations
        private K3Value In(K3Value left, K3Value right)
        {
            // _in (Find) function - searches for left argument in right argument
            // Returns 1 if found, 0 if not found (per K3 spec)
            // OPTIMIZED: Use early exit with Match for tolerant comparison
            
            if (right is VectorValue rightVec)
            {
                // Search for left in right vector with early exit
                for (int i = 0; i < rightVec.Elements.Count; i++)
                {
                    var element = rightVec.Elements[i];
                    var matchResult = Match(left, element);
                    
                    if (matchResult is IntegerValue intVal && intVal.Value == 1)
                    {
                        return new IntegerValue(1); // Found - return 1
                    }
                }
                return new IntegerValue(0); // Not found
            }
            else
            {
                // Search for left in right scalar
                var matchResult = Match(left, right);
                return (matchResult is IntegerValue intVal && intVal.Value == 1) 
                    ? new IntegerValue(1) 
                    : new IntegerValue(0);
            }
        }

        private K3Value Bin(K3Value left, K3Value right)
        {
            // _bin (Binary Search) function - performs binary search on sorted list
            // According to spec: x _bin y where x is ascending list, y is atom
            // Returns index of first element in x that is >= y
            // If first element > y, returns 0
            // If last element < y, returns length of x
            
            if (left is VectorValue leftVec)
            {
                if (leftVec.Elements.Count == 0)
                {
                    return new IntegerValue(0);
                }
                
                // Check if first element is already >= right
                var firstComparison = CompareValues(leftVec.Elements[0], right);
                if (firstComparison >= 0)
                {
                    return new IntegerValue(0);
                }
                
                // Check if last element is < right
                var lastComparison = CompareValues(leftVec.Elements[leftVec.Elements.Count - 1], right);
                if (lastComparison < 0)
                {
                    return new IntegerValue(leftVec.Elements.Count);
                }
                
                // Binary search for first element >= right
                int low = 0;
                int high = leftVec.Elements.Count - 1;
                int result = leftVec.Elements.Count; // Default to length if not found
                
                while (low <= high)
                {
                    int mid = (low + high) / 2;
                    var midValue = leftVec.Elements[mid];
                    var comparison = CompareValues(midValue, right);
                    
                    if (comparison >= 0)
                    {
                        result = mid; // Potential answer, continue searching left
                        high = mid - 1;
                    }
                    else
                    {
                        low = mid + 1;
                    }
                }
                
                return new IntegerValue((int)result);
            }
            else
            {
                // For non-vector left, return 0 if left >= right, 1 otherwise
                var comparison = CompareValues(left, right);
                return new IntegerValue(comparison >= 0 ? 0 : 1);
            }
        }

        private K3Value Binl(K3Value left, K3Value right)
        {
            // _binl (Binary Search Each-Left) function
            // According to spec: x _binl y where x is ascending list, y is list
            // Returns vector of indices where each element of y would be inserted in x
            // x _binl y is equivalent to x _bin: y
            
            if (right is VectorValue rightVec)
            {
                var results = new List<K3Value>();
                
                // For each element in right, find insertion position in left
                foreach (var rightElement in rightVec.Elements)
                {
                    var result = Bin(left, rightElement);
                    results.Add(result);
                }
                
                return new VectorValue(results);
            }
            else
            {
                // Single element case
                return Bin(left, right);
            }
        }

        private K3Value Lin(K3Value left, K3Value right)
        {
            // _lin (List Intersection) function
            // Returns 1 for each element of left that is in right, 0 otherwise
            // Equivalent to left _in\: right but optimized using HashSet
            
            if (left is VectorValue leftVec)
            {
                var results = new List<K3Value>();
                
                // Create a HashSet for efficient O(1) lookups of right argument elements
                var rightSet = CreateHashSet(right);
                
                foreach (var leftElement in leftVec.Elements)
                {
                    bool found = false;
                    
                    // Check if leftElement exists in rightSet
                    if (rightSet != null)
                    {
                        found = rightSet.Contains(leftElement);
                    }
                    else
                    {
                        // Fallback to linear search if HashSet creation failed
                        if (right is VectorValue rightVec)
                        {
                            foreach (var rightElement in rightVec.Elements)
                            {
                                var matchResult = Match(leftElement, rightElement);
                                if (matchResult is IntegerValue intVal && intVal.Value == 1)
                                {
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                    
                    results.Add(new IntegerValue(found ? 1 : 0));
                }
                
                return new VectorValue(results);
            }
            else
            {
                // Single element case - return 1 if found, 0 otherwise
                if (right is VectorValue rightVec)
                {
                    foreach (var rightElement in rightVec.Elements)
                    {
                        var matchResult = Match(left, rightElement);
                        if (matchResult is IntegerValue intVal && intVal.Value == 1)
                        {
                            return new IntegerValue(1);
                        }
                    }
                    return new IntegerValue(0);
                }
                else
                {
                    // Scalar case
                    var matchResult = Match(left, right);
                    return (matchResult is IntegerValue intVal && intVal.Value == 1) 
                        ? new IntegerValue(1) 
                        : new IntegerValue(0);
                }
            }
        }

        // Monadic placeholder functions
        private K3Value InFunction(K3Value operand)
        {
            // _in function should be handled as dyadic in binary operations
            // This monadic case should not be reached in normal operation
            throw new Exception("_in (Find) function requires two arguments - use infix notation: x _in y");
        }

        private K3Value BinFunction(K3Value operand)
        {
            throw new Exception("_bin (binary search) operation reserved for future use");
        }

        private K3Value BinlFunction(K3Value operand)
        {
            // _binl function should be handled as dyadic in binary operations
            // This monadic case should not be reached in normal operation
            throw new Exception("_binl (binary search each-left) function requires two arguments - use infix notation: x _binl y");
        }

        private K3Value LinFunction(K3Value operand)
        {
            // _lin function should be handled as dyadic in binary operations
            // This monadic case should not be reached in normal operation
            throw new Exception("_lin (list intersection) function requires two arguments - use infix notation: x _lin y");
        }

        // Database and system functions (placeholders)
        private K3Value Dv(K3Value left, K3Value right)
        {
            // _dv (Delete by Value) function
            // Returns a copy of left with all occurrences of right removed
            // For dictionaries, returns left as is (they are atomic)
            
            // Handle dictionary case - dictionaries are atomic, so return as is
            if (left is DictionaryValue)
            {
                return left;
            }
            
            // Handle vector case
            if (left is VectorValue leftVec)
            {
                var results = new List<K3Value>();
                
                foreach (var element in leftVec.Elements)
                {
                    var matchResult = Match(element, right);
                    if (matchResult is IntegerValue intVal && intVal.Value != 1)
                    {
                        // Element doesn't match right, keep it
                        results.Add(element);
                    }
                }
                
                return new VectorValue(results);
            }
            else
            {
                // For scalar left, return left if it doesn't match right, empty vector otherwise
                var matchResult = Match(left, right);
                if (matchResult is IntegerValue intVal && intVal.Value != 1)
                {
                    return left;
                }
                else
                {
                    return new VectorValue(new List<K3Value>()); // Empty vector
                }
            }
        }

        private K3Value Dvl(K3Value left, K3Value right)
        {
            // _dvl (Delete by Value List) function
            // Equivalent to _dv/: - applies _dv with each-right adverb
            // Returns a copy of left with all occurrences of elements in right removed
            // For dictionaries, returns left as is (they are atomic)
            
            // Handle dictionary case - dictionaries are atomic, so return as is
            if (left is DictionaryValue)
            {
                return left;
            }
            
            // Handle right as a list/vector - apply Dv for each element
            if (right is VectorValue rightVec)
            {
                K3Value result = left;
                foreach (var element in rightVec.Elements)
                {
                    result = Dv(result, element);
                }
                return result;
            }
            else
            {
                // If right is not a list, just use Dv
                return Dv(left, right);
            }
        }

        private K3Value Di(K3Value left, K3Value right)
        {
            // _di (Delete by Index) function
            // Returns a copy of left with items removed at indices specified in right
            // Works with both vectors and dictionaries
            
            // Handle dictionary case
            if (left is DictionaryValue leftDict)
            {
                var newEntries = new List<KeyValuePair<SymbolValue, (K3Value Value, DictionaryValue?)>>();
                
                if (right is SymbolValue rightSymbol)
                {
                    // Remove key from dictionary
                    foreach (var entry in leftDict.Entries)
                    {
                        var key = entry.Key;
                        if (!Match(new SymbolValue(key.Value), rightSymbol).Equals(new IntegerValue(1)))
                        {
                            newEntries.Add(entry);
                        }
                    }
                }
                else if (right is VectorValue rightVec)
                {
                    // Remove multiple keys from dictionary
                    var symbolsToRemove = new HashSet<string>();
                    foreach (var rightElement in rightVec.Elements)
                    {
                        if (rightElement is SymbolValue rightSym)
                        {
                            symbolsToRemove.Add(rightSym.Value);
                        }
                    }
                    
                    foreach (var entry in leftDict.Entries)
                    {
                        if (!symbolsToRemove.Contains(entry.Key.Value))
                        {
                            newEntries.Add(entry);
                        }
                    }
                }
                else
                {
                    throw new Exception("_di: right argument must be a symbol or symbol vector when left is a dictionary");
                }
                
                return new DictionaryValue(new Dictionary<SymbolValue, (K3Value Value, DictionaryValue?)>(newEntries));
            }
            
            // Handle vector case
            if (left is VectorValue leftVec)
            {
                var results = new List<K3Value>();
                
                // If right is a scalar, treat it as a single index
                if (!(right is VectorValue))
                {
                    var index = GetIndexValue(right);
                    if (index >= 0 && index < leftVec.Elements.Count)
                    {
                        // Skip the element at this index
                        for (int i = 0; i < leftVec.Elements.Count; i++)
                        {
                            if (i != index)
                            {
                                results.Add(leftVec.Elements[i]);
                            }
                        }
                    }
                    else
                    {
                        // Invalid index, return original vector
                        return left;
                    }
                }
                else
                {
                    // Right is a vector of indices
                    var rightVec = (VectorValue)right;
                    var indicesToRemove = new HashSet<int>();
                    
                    foreach (var indexValue in rightVec.Elements)
                    {
                        var index = GetIndexValue(indexValue);
                        if (index >= 0 && index < leftVec.Elements.Count)
                        {
                            indicesToRemove.Add(index);
                        }
                    }
                    
                    for (int i = 0; i < leftVec.Elements.Count; i++)
                    {
                        if (!indicesToRemove.Contains(i))
                        {
                            results.Add(leftVec.Elements[i]);
                        }
                    }
                }
                
                return new VectorValue(results);
            }
            else
            {
                throw new Exception("_di: left argument must be a vector or dictionary");
            }
        }
        
        private int GetIndexValue(K3Value value)
        {
            if (value is IntegerValue intValue)
            {
                return intValue.Value;
            }
            else
            {
                throw new Exception("_di: index must be an integer");
            }
        }

        private K3Value Sv(K3Value left, K3Value right)
        {
            // _sv (Scalar from Vector) function
            // Performs numeric base or radix conversion
            // Left argument is the base or radices, right argument is integer vector
            
            if (left is IntegerValue leftInt && right is VectorValue rightVec)
            {
                // Single base case
                return SvSingleBase(leftInt.Value, rightVec);
            }
            else if (left is VectorValue leftVec && right is VectorValue rightVec2)
            {
                // Multiple radices case
                return SvMultipleRadices(leftVec, rightVec2);
            }
            else
            {
                throw new Exception("_sv: left argument must be integer (single base) or vector (multiple radies), right argument must be integer vector");
            }
        }
        
        private K3Value SvSingleBase(long baseValue, VectorValue digits)
        {
            // Convert digits from given base to base 10
            long result = 0;
            long multiplier = 1;
            
            // Process digits from right to left (least significant to most significant)
            for (int i = digits.Elements.Count - 1; i >= 0; i--)
            {
                if (digits.Elements[i] is IntegerValue digit)
                {
                    var digitValue = ((IntegerValue)digits.Elements[i]).Value;
                    if (digitValue < 0 || digitValue >= baseValue)
                    {
                        throw new Exception($"_sv: digit {digitValue} is out of range for base {baseValue}");
                    }
                    result += digitValue * multiplier;
                    multiplier *= baseValue;
                }
                else
                {
                    throw new Exception("_sv: all elements in right argument must be integers");
                }
            }
            
            return new IntegerValue((int)result);
        }
        
        private K3Value SvMultipleRadices(VectorValue radices, VectorValue digitVec)
        {
            // Convert digits from mixed radices to base 10
            if (radices.Elements.Count != digitVec.Elements.Count)
            {
                throw new Exception("_sv: number of radices must match number of digits");
            }
            
            long result = 0;
            
            // Process from left to right (most significant to least significant)
            for (int i = 0; i < digitVec.Elements.Count; i++)
            {
                if (i < radices.Elements.Count && radices.Elements[i] is IntegerValue radix && digitVec.Elements[i] is IntegerValue digit)
                {
                    var radixValue = ((IntegerValue)radices.Elements[i]).Value;
                    var digitValue = ((IntegerValue)digitVec.Elements[i]).Value;
                    
                    if (radixValue <= 0)
                    {
                        throw new Exception($"_sv: radix {radixValue} must be positive");
                    }
                    if (digitValue < 0 || digitValue >= radixValue)
                    {
                        throw new Exception($"_sv: digit {digitValue} is out of range for radix {radixValue}");
                    }
                    
                    result = result * radixValue + digitValue;
                }
                else
                {
                    throw new Exception("_sv: all elements must be integers and radices must be positive integers");
                }
            }
            
            return new IntegerValue((int)result);
        }

        private K3Value Vs(K3Value left, K3Value right)
        {
            // _vs (vector from scalar) function
            // Dyadic verb: x _vs y
            // Converts scalar to vector representation using base/radices
            
            if (right is IntegerValue rightInt)
            {
                // Single integer case
                return VsSingle(left, (int)rightInt.Value);
            }
            else if (right is VectorValue rightVec)
            {
                // Vector case - convert each integer to vector
                // Result is a matrix (list of lists of equal length) where each column
                // is the corresponding digits from conversion of all items in right argument
                // When a is an integer and V is a vector of integers, a _vs V is the same as a _vs\: V
                var results = new List<List<K3Value>>();
                var maxLength = 0;
                
                // First, convert each integer to its digit vector
                foreach (var element in rightVec.Elements)
                {
                    if (element is IntegerValue intVal)
                    {
                        var result = VsSingle(left, (int)intVal.Value);
                        if (result is VectorValue vec)
                        {
                            var digitList = vec.Elements.ToList();
                            results.Add(digitList);
                            if (digitList.Count > maxLength)
                            {
                                maxLength = digitList.Count;
                            }
                        }
                        else
                        {
                            results.Add(new List<K3Value> { result });
                            if (maxLength < 1)
                            {
                                maxLength = 1;
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("_vs: all elements in right argument must be integers");
                    }
                }
                
                // Pad shorter vectors with leading zeros to make them equal length
                for (int i = 0; i < results.Count; i++)
                {
                    while (results[i].Count < maxLength)
                    {
                        results[i].Insert(0, new IntegerValue(0));
                    }
                }
                
                // Transpose the matrix so each column becomes a row
                var transposed = new List<K3Value>();
                for (int col = 0; col < maxLength; col++)
                {
                    var column = new List<K3Value>();
                    for (int row = 0; row < results.Count; row++)
                    {
                        column.Add(results[row][col]);
                    }
                    transposed.Add(new VectorValue(column));
                }
                
                return new VectorValue(transposed);
            }
            else
            {
                throw new Exception("_vs: right argument must be integer or integer vector");
            }
        }
        
        private K3Value VsSingle(K3Value left, int value)
        {
            // Convert single integer to vector representation
            
            if (left is IntegerValue baseVal)
            {
                // Single base case
                int baseNum = (int)baseVal.Value;
                return ConvertToBase(value, baseNum);
            }
            else if (left is VectorValue radices)
            {
                // Multiple radices case
                var radicesList = new List<int>();
                foreach (var element in radices.Elements)
                {
                    if (element is IntegerValue intVal)
                    {
                        radicesList.Add((int)intVal.Value);
                    }
                    else
                    {
                        throw new Exception("_vs: all radices must be integers");
                    }
                }
                return ConvertToRadices(value, radicesList);
            }
            else
            {
                throw new Exception("_vs: left argument must be integer or integer vector");
            }
        }
        
        private K3Value ConvertToBase(int value, int baseNum)
        {
            if (baseNum <= 0)
                throw new Exception("_vs: base must be positive");
            
            var digits = new List<int>();
            int remaining = value;
            
            if (remaining == 0)
            {
                return new VectorValue(new List<K3Value> { new IntegerValue(0) });
            }
            
            while (remaining != 0)
            {
                digits.Add(Math.Abs(remaining % baseNum));
                remaining = remaining / baseNum;
            }
            
            digits.Reverse();
            return new VectorValue(digits.Select(d => (K3Value)new IntegerValue(d)).ToList());
        }
        
        private K3Value ConvertToRadices(int value, List<int> radices)
        {
            var digits = new List<int>();
            int remaining = value;
            
            // Process radices from right to left (least significant to most)
            for (int i = radices.Count - 1; i >= 0; i--)
            {
                int radix = radices[i];
                if (radix <= 0)
                    throw new Exception("_vs: all radices must be positive");
                
                digits.Add(Math.Abs(remaining % radix));
                remaining = remaining / radix;
            }
            
            digits.Reverse();
            return new VectorValue(digits.Select(d => (K3Value)new IntegerValue(d)).ToList());
        }

        private K3Value Ci(K3Value left)
        {
            // _ci (character from integer) function - monadic version
            
            if (left is IntegerValue leftInt)
            {
                // Single integer case
                return CiSingle(leftInt.Value);
            }
            else if (left is VectorValue leftVec)
            {
                // Vector case - convert each integer to character
                var results = new List<K3Value>();
                foreach (var element in leftVec.Elements)
                {
                    if (element is IntegerValue innerIntVal)
                    {
                        results.Add(CiSingle((int)innerIntVal.Value));
                    }
                    else
                    {
                        throw new Exception("_ci: all elements must be integers");
                    }
                }
                return new VectorValue(results);
            }
            else
            {
                throw new Exception("_ci: operand must be integer or integer vector");
            }
        }
        
        private K3Value CiSingle(int intValue)
        {
            // Convert integer to ASCII character
            // Handle negative values and values > 255 by allowing unchecked overflow
            // Convert to unsigned byte to get proper ASCII behavior
            var charValue = (char)(intValue & 0xFF);
            return new CharacterValue(charValue.ToString());
        }

        private K3Value Ic(K3Value left)
        {
            // _ic (integer from character) function - monadic version
            
            if (left is CharacterValue leftChar)
            {
                // Single character case
                if (leftChar.Value.Length == 1)
                {
                    char c = (char)leftChar.Value[0];  // Get first character with explicit cast
                    return IcSingle(c);
                }
                else
                    throw new Exception("_ic: left argument must be a single character");
            }
            else if (left is VectorValue leftVec)
            {
                // Vector case - convert each character to integer
                var results = new List<K3Value>();
                foreach (var element in leftVec.Elements)
                {
                    if (element is CharacterValue charVal)
                    {
                        if (charVal.Value.Length == 1)
                        {
                            char c = charVal.Value[0];
                            results.Add(IcSingle(c));
                        }
                        else
                        {
                            throw new Exception("_ic: all elements must be single characters");
                        }
                    }
                }
                return new VectorValue(results);
            }
            else
            {
                throw new Exception("_ic: left argument must be character or character vector");
            }
        }
        
        private K3Value IcSingle(char charValue)
        {
            // Convert character to integer (ASCII value)
            return new IntegerValue((int)charValue);
        }

        private K3Value Sm(K3Value left, K3Value right)
        {
            // _sm (string match) function
            // Dyadic verb: x _sm y
            // Returns 1 if left argument matches right argument pattern, 0 otherwise
            
            // Convert both arguments to strings for comparison
            string leftStr = left switch
            {
                CharacterValue charVal => charVal.Value,
                SymbolValue symVal => symVal.Value,
                _ => throw new Exception("_sm: left argument must be character or symbol")
            };
            
            string rightStr = right switch
            {
                CharacterValue charVal => charVal.Value,
                SymbolValue symVal => symVal.Value,
                _ => throw new Exception("_sm: right argument must be character or symbol")
            };
            
            // Check if right argument contains regex wildcards
            bool useRegex = rightStr.Contains('*') || rightStr.Contains('?') || rightStr.Contains('[');
            
            if (useRegex)
            {
                try
                {
                    // Use C# regex for pattern matching
                    var regex = new System.Text.RegularExpressions.Regex(rightStr);
                    return new IntegerValue(regex.IsMatch(leftStr) ? 1 : 0);
                }
                catch
                {
                    // If regex fails, fall back to exact match
                    return new IntegerValue(leftStr == rightStr ? 1 : 0);
                }
            }
            else
            {
                // Simple string comparison
                return new IntegerValue(leftStr == rightStr ? 1 : 0);
            }
        }

        private K3Value SsFunction(K3Value left, K3Value right)
        {
            // _ss (string search) function
            // Dyadic verb: x _ss y
            // Returns start indices where pattern occurs in text (0-based)
            
            string leftStr = left switch
            {
                CharacterValue charVal => charVal.Value,
                SymbolValue symVal => symVal.Value,
                VectorValue vecVal => ExtractStringFromVector(vecVal),
                _ => throw new Exception("_ss: left argument must be character or symbol")
            };
            
            string rightStr = right switch
            {
                CharacterValue charVal => charVal.Value,
                SymbolValue symVal => symVal.Value,
                VectorValue vecVal => ExtractStringFromVector(vecVal),
                _ => throw new Exception("_ss: right argument must be character or symbol")
            };
            
            List<int> indices = new List<int>();
            int index = 0;
            
            while (true)
            {
                int foundIndex = leftStr.IndexOf(rightStr, index);
                if (foundIndex == -1)
                    break;
                indices.Add(foundIndex); // Keep 0-based indexing
                index = foundIndex + 1; // Move to next character after found pattern
            }
            
            // Always return integer vector, even for 0 or 1 items
            if (indices.Count == 0)
                return new VectorValue(new List<K3Value>(), -1); // Empty integer vector
            else
                return new VectorValue(indices.Select(i => new IntegerValue(i)).Cast<K3Value>().ToList());
        }

        private K3Value GetenvFunction(K3Value operand)
        {
            string varName = operand switch
            {
                SymbolValue sym => sym.Value,
                VectorValue vec when vec.Elements.All(e => e is CharacterValue) => string.Concat(vec.Elements.Cast<CharacterValue>().Select(e => e.Value)),
                CharacterValue ch => ch.Value.ToString(),
                _ => throw new Exception("_getenv: argument must be a symbol or character vector")
            };

            string? value = Environment.GetEnvironmentVariable(varName);
            if (value == null)
                return new VectorValue(new List<K3Value>()); // Empty vector if not found
            
            return new VectorValue(value.Select(c => new CharacterValue(c.ToString())).Cast<K3Value>().ToList());
        }

        private K3Value SetenvFunction(K3Value varNameArg, K3Value valueArg)
        {            
            string varName = varNameArg switch
            {
                SymbolValue sym => sym.Value,
                VectorValue nameVec when nameVec.Elements.All(e => e is CharacterValue) => string.Concat(nameVec.Elements.Cast<CharacterValue>().Select(e => e.Value)),
                CharacterValue ch => ch.Value.ToString(),
                _ => throw new Exception("_setenv: first argument must be a symbol or character vector")
            };
            
            string value = valueArg switch
            {
                VectorValue valVec when valVec.Elements.All(e => e is CharacterValue) => string.Concat(valVec.Elements.Cast<CharacterValue>().Select(e => e.Value)),
                CharacterValue ch => ch.Value.ToString(),
                _ => throw new Exception("_setenv: second argument must be a character vector")
            };
            
            Environment.SetEnvironmentVariable(varName, value);
            return new NullValue(); // _setenv returns null (nothing) per spec
        }

        private K3Value SizeFunction(K3Value operand)
        {
            string fileName = operand switch
            {
                SymbolValue sym => sym.Value,
                VectorValue vec when vec.Elements.All(e => e is CharacterValue) => string.Concat(vec.Elements.Cast<CharacterValue>().Select(e => e.Value)),
                CharacterValue ch => ch.Value.ToString(),
                _ => throw new Exception("_size: argument must be a symbol or character vector")
            };

            try
            {
                if (File.Exists(fileName))
                {
                    var fileInfo = new FileInfo(fileName);
                    return new FloatValue((float)fileInfo.Length);
                }
                else
                {
                    throw new Exception($"_size: file '{fileName}' not found");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"_size: error accessing file '{fileName}': {ex.Message}");
            }
        }

        private K3Value ExitFunction(K3Value operand)
        {
            // _exit is a monadic verb, but can also be used niladically
            // According to speclet: if argument is _n (no argument provided, niladic usage), exit code will be 0
            // If an integer argument is provided, use it as exit code
            
            int exitCode = 0; // Default for niladic case
            
            bool isNiladic = operand is null
                || operand is NullValue
                || operand is VectorValue vec && vec.Elements.Count == 0;

            if (!isNiladic)
            {
                exitCode = operand switch
                {
                    IntegerValue iv => iv.Value,
                    LongValue lv => (int)lv.Value,
                    FloatValue fv => (int)fv.Value,
                    _ => throw new Exception("_exit: argument must be an integer (or niladic for exit code 0)")
                };
            }
            
            RequestExit(exitCode);
            throw new K3ExitException(exitCode);
        }

        // Helper methods for list operations
        private HashSet<K3Value>? CreateHashSet(K3Value value)
        {
            // Create a HashSet from a K3Value for efficient lookups
            try
            {
                var set = new HashSet<K3Value>(new K3ValueComparer());
                
                if (value is VectorValue vec)
                {
                    foreach (var element in vec.Elements)
                    {
                        set.Add(element);
                    }
                }
                else
                {
                    set.Add(value);
                }
                
                return set;
            }
            catch
            {
                return null; // Return null if HashSet creation fails
            }
        }
    }
}
