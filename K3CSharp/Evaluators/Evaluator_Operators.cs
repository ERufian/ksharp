using System;
using System.Collections.Generic;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private K3Value Plus(K3Value a, K3Value b)
        {
            
            // Handle mixed type promotion
            if (a is IntegerValue && b is LongValue)
            {
                unchecked
                {
                    return new LongValue(((IntegerValue)a).Value + ((LongValue)b).Value);
                }
            }
            if (a is LongValue && b is IntegerValue)
            {
                unchecked
                {
                    return new LongValue(((LongValue)a).Value + ((IntegerValue)b).Value);
                }
            }
            if (a is IntegerValue && b is FloatValue)
                return new FloatValue(((IntegerValue)a).Value + ((FloatValue)b).Value);
            if (a is FloatValue && b is IntegerValue)
                return new FloatValue(((FloatValue)a).Value + ((IntegerValue)b).Value);
            if (a is LongValue && b is FloatValue)
                return new FloatValue(((LongValue)a).Value + ((FloatValue)b).Value);
            if (a is FloatValue && b is LongValue)
                return new FloatValue(((FloatValue)a).Value + ((LongValue)b).Value);
            
            // Handle same type operations - use K3Value Add method for proper overflow handling
            if (a is IntegerValue && b is IntegerValue)
                return ((IntegerValue)a).Add((IntegerValue)b);
            if (a is LongValue && b is LongValue)
                return ((LongValue)a).Add((LongValue)b);
            if (a is FloatValue && b is FloatValue)
                return ((FloatValue)a).Add((FloatValue)b);
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                    return vecA.Add(vecB);
                else
                    return vecA.Add(b);
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vectorB)
                return vectorB.Add(a);
            
            throw new Exception($"Cannot add {a.Type} and {b.Type}");
        }

        private K3Value Minus(K3Value a, K3Value b)
        {
            // Handle mixed type promotion
            if (a is IntegerValue && b is LongValue)
            {
                unchecked
                {
                    return new LongValue(((IntegerValue)a).Value - ((LongValue)b).Value);
                }
            }
            if (a is LongValue && b is IntegerValue)
            {
                unchecked
                {
                    return new LongValue(((LongValue)a).Value - ((IntegerValue)b).Value);
                }
            }
            if (a is IntegerValue && b is FloatValue)
                return new FloatValue(((IntegerValue)a).Value - ((FloatValue)b).Value);
            if (a is FloatValue && b is IntegerValue)
                return new FloatValue(((FloatValue)a).Value - ((IntegerValue)b).Value);
            if (a is LongValue && b is FloatValue)
                return new FloatValue(((LongValue)a).Value - ((FloatValue)b).Value);
            if (a is FloatValue && b is LongValue)
                return new FloatValue(((FloatValue)a).Value - ((LongValue)b).Value);
            
            // Handle same type operations - use the K3Value Subtract method for proper overflow handling
            if (a is IntegerValue && b is IntegerValue)
                return ((IntegerValue)a).Subtract((IntegerValue)b);
            if (a is LongValue && b is LongValue)
                return ((LongValue)a).Subtract((LongValue)b);
            if (a is FloatValue && b is FloatValue)
                return ((FloatValue)a).Subtract((FloatValue)b);
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                    return vecA.Subtract(vecB);
                else
                    return vecA.Subtract(b);
            }
            
            // Handle scalar - vector operations
            if (b is VectorValue vectorB)
            {
                // For scalar - vector, we need to subtract each element from the scalar
                var result = new List<K3Value>();
                foreach (var element in vectorB.Elements)
                {
                    result.Add(Minus(a, element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot subtract {a.Type} and {b.Type}");
        }

        private K3Value Times(K3Value a, K3Value b)
        {
            // Handle mixed type promotion
            if (a is IntegerValue && b is LongValue)
            {
                unchecked
                {
                    return new LongValue(((IntegerValue)a).Value * ((LongValue)b).Value);
                }
            }
            if (a is LongValue && b is IntegerValue)
            {
                unchecked
                {
                    return new LongValue(((LongValue)a).Value * ((IntegerValue)b).Value);
                }
            }
            if (a is IntegerValue && b is FloatValue)
                return new FloatValue(((IntegerValue)a).Value * ((FloatValue)b).Value);
            if (a is FloatValue && b is IntegerValue)
                return new FloatValue(((FloatValue)a).Value * ((IntegerValue)b).Value);
            if (a is LongValue && b is FloatValue)
                return new FloatValue(((LongValue)a).Value * ((FloatValue)b).Value);
            if (a is FloatValue && b is LongValue)
                return new FloatValue(((FloatValue)a).Value * ((LongValue)b).Value);
            
            // Handle same type operations - use the K3Value Multiply method for proper overflow handling
            if (a is IntegerValue && b is IntegerValue)
                return ((IntegerValue)a).Multiply((IntegerValue)b);
            if (a is LongValue && b is LongValue)
                return ((LongValue)a).Multiply((LongValue)b);
            if (a is FloatValue && b is FloatValue)
                return ((FloatValue)a).Multiply((FloatValue)b);
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                    return vecA.Multiply(vecB);
                else
                    return vecA.Multiply(b);
            }
            
            // Handle scalar * vector operations
            if (b is VectorValue vectorB)
            {
                return vectorB.Multiply(a);
            }
            
            throw new Exception($"Cannot multiply {a.Type} and {b.Type}");
        }

        private K3Value Divide(K3Value a, K3Value b)
        {
            // Handle integer division - always promote to float in K
            if (a is IntegerValue && b is IntegerValue)
            {
                int divisor = ((IntegerValue)b).Value;
                int dividend = ((IntegerValue)a).Value;
                
                // Special case: 0%0 returns 0 (per K specification)
                if (divisor == 0 && dividend == 0)
                    return new IntegerValue(0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                
                // In K, division always promotes to float
                return new FloatValue((double)dividend / divisor);
            }
            
            // Handle long division - always promote to float in K
            if (a is LongValue && b is LongValue)
            {
                long divisor = ((LongValue)b).Value;
                long dividend = ((LongValue)a).Value;
                
                // Special case: 0j%0j returns 0j (per K specification)
                if (divisor == 0 && dividend == 0)
                    return new LongValue(0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                
                // In K, division always promotes to float
                return new FloatValue((double)dividend / divisor);
            }
            
            // Handle mixed type promotion
            if (a is IntegerValue && b is LongValue)
            {
                long divisor = ((LongValue)b).Value;
                if (divisor == 0)
                    throw new Exception("Division by zero");
                return new FloatValue((double)((IntegerValue)a).Value / divisor);
            }
            if (a is LongValue && b is IntegerValue)
            {
                int divisor = ((IntegerValue)b).Value;
                if (divisor == 0)
                    throw new Exception("Division by zero");
                return new FloatValue((double)((LongValue)a).Value / divisor);
            }
            if (a is IntegerValue && b is FloatValue)
            {
                double divisor = ((FloatValue)b).Value;
                int dividend = ((IntegerValue)a).Value;
                
                // Special case: 0%0.0 returns 0.0 (per K specification - type promotion case)
                if (divisor == 0.0 && dividend == 0)
                    return new FloatValue(0.0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                return new FloatValue(dividend / divisor);
            }
            if (a is FloatValue && b is IntegerValue)
            {
                int divisor = ((IntegerValue)b).Value;
                double dividend = ((FloatValue)a).Value;
                
                // Special case: 0.0%0 returns 0.0 (per K specification - type promotion case)
                if (divisor == 0 && dividend == 0.0)
                    return new FloatValue(0.0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                return new FloatValue(dividend / divisor);
            }
            if (a is LongValue && b is FloatValue)
            {
                double divisor = ((FloatValue)b).Value;
                long dividend = ((LongValue)a).Value;
                
                // Special case: 0j%0.0 returns 0.0 (per K specification - type promotion case)
                if (divisor == 0.0 && dividend == 0)
                    return new FloatValue(0.0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                return new FloatValue(dividend / divisor);
            }
            if (a is FloatValue && b is LongValue)
            {
                long divisor = ((LongValue)b).Value;
                double dividend = ((FloatValue)a).Value;
                
                // Special case: 0.0%0j returns 0.0 (per K specification - type promotion case)
                if (divisor == 0 && dividend == 0.0)
                    return new FloatValue(0.0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                return new FloatValue(dividend / divisor);
            }
            
            // Handle same type float operations
            if (a is FloatValue && b is FloatValue)
            {
                double divisor = ((FloatValue)b).Value;
                double dividend = ((FloatValue)a).Value;
                
                // Special case: 0.0%0.0 returns 0.0 (per K specification)
                if (divisor == 0.0 && dividend == 0.0)
                    return new FloatValue(0.0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                return new FloatValue(dividend / divisor);
            }
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                    return vecA.Divide(vecB);
                else
                    return vecA.Divide(b);
            }
            
            // Handle scalar / vector operations
            if (b is VectorValue vectorB)
            {
                // For scalar / vector, we need to divide scalar by each element
                var result = new List<K3Value>();
                foreach (var element in vectorB.Elements)
                {
                    result.Add(Divide(a, element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot divide {a.Type} and {b.Type}");
        }

        private K3Value Min(K3Value a, K3Value b)
        {
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(Math.Min(intA.Value, intB.Value));
            if (a is LongValue longA && b is LongValue longB)
                return new LongValue(Math.Min(longA.Value, longB.Value));
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new FloatValue(Math.Min(floatA.Value, floatB.Value));
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                    return vecA.Minimum(vecB);
                else
                    return vecA.Minimum(b);
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vectorB)
            {
                // For scalar + vector, apply min to each element
                var result = new List<K3Value>();
                foreach (var element in vectorB.Elements)
                {
                    result.Add(Min(a, element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot find minimum of {a.Type} and {b.Type}");
        }

        private K3Value Max(K3Value a, K3Value b)
        {
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(Math.Max(intA.Value, intB.Value));
            if (a is LongValue longA && b is LongValue longB)
                return new LongValue(Math.Max(longA.Value, longB.Value));
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new FloatValue(Math.Max(floatA.Value, floatB.Value));
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                    return vecA.Maximum(vecB);
                else
                    return vecA.Maximum(b);
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vectorB)
            {
                // For scalar + vector, apply max to each element
                var result = new List<K3Value>();
                foreach (var element in vectorB.Elements)
                {
                    result.Add(Max(a, element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot find maximum of {a.Type} and {b.Type}");
        }

        private K3Value Less(K3Value a, K3Value b)
        {
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(intA.Value < intB.Value ? 1 : 0);
            if (a is LongValue longA && b is LongValue longB)
                return new IntegerValue(longA.Value < longB.Value ? 1 : 0);
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new IntegerValue(floatA.Value < floatB.Value ? 1 : 0);
            
            // Handle character comparisons based on ASCII values
            if (a is CharacterValue charA && b is CharacterValue charB)
            {
                int asciiA = charA.Value[0];
                int asciiB = charB.Value[0];
                return new IntegerValue(asciiA < asciiB ? 1 : 0);
            }
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                {
                    if (vecA.Elements.Count != vecB.Elements.Count)
                        throw new Exception($"length error: {vecA.Elements.Count} != {vecB.Elements.Count}");
                    
                    var results = new List<K3Value>();
                    for (int i = 0; i < vecA.Elements.Count; i++)
                    {
                        results.Add(Less(vecA.Elements[i], vecB.Elements[i]));
                    }
                    return new VectorValue(results);
                }
                else
                {
                    var results = new List<K3Value>();
                    foreach (var element in vecA.Elements)
                    {
                        results.Add(Less(element, b));
                    }
                    return new VectorValue(results);
                }
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vectorB)
            {
                // For scalar + vector, apply less to each element
                var result = new List<K3Value>();
                foreach (var element in vectorB.Elements)
                {
                    result.Add(Less(a, element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot compare {a.Type} and {b.Type} with <");
        }

        private K3Value More(K3Value a, K3Value b)
        {
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(intA.Value > intB.Value ? 1 : 0);
            if (a is LongValue longA && b is LongValue longB)
                return new IntegerValue(longA.Value > longB.Value ? 1 : 0);
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new IntegerValue(floatA.Value > floatB.Value ? 1 : 0);
            
            // Handle character comparisons based on ASCII values
            if (a is CharacterValue charA && b is CharacterValue charB)
            {
                int asciiA = charA.Value[0];
                int asciiB = charB.Value[0];
                return new IntegerValue(asciiA > asciiB ? 1 : 0);
            }
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                {
                    if (vecA.Elements.Count != vecB.Elements.Count)
                        throw new Exception($"length error: {vecA.Elements.Count} != {vecB.Elements.Count}");
                    
                    var results = new List<K3Value>();
                    for (int i = 0; i < vecA.Elements.Count; i++)
                    {
                        results.Add(More(vecA.Elements[i], vecB.Elements[i]));
                    }
                    return new VectorValue(results);
                }
                else
                {
                    var results = new List<K3Value>();
                    foreach (var element in vecA.Elements)
                    {
                        results.Add(More(element, b));
                    }
                    return new VectorValue(results);
                }
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vectorB)
            {
                // For scalar + vector, apply more to each element
                var result = new List<K3Value>();
                foreach (var element in vectorB.Elements)
                {
                    result.Add(More(a, element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot compare {a.Type} and {b.Type} with >");
        }

        public K3Value Match(K3Value a, K3Value b)
        {
            // For match comparison (~ operator)
            if (a is FloatValue floatA && b is FloatValue floatB)
            {
                // Check for equality first - important for special values like infinities
                if (floatA.Value == floatB.Value)
                    return new IntegerValue(1);
                
                double maxAbs = Math.Max(Math.Abs(floatA.Value), Math.Abs(floatB.Value));
                double threshold = maxAbs * 0.00001; // 0.001 percent
                return new IntegerValue(Math.Abs(floatA.Value - floatB.Value) < threshold ? 1 : 0);
            }
            
            if (a is VectorValue vecAMatch && b is VectorValue vecBMatch)
            {
                if (vecAMatch.Elements.Count != vecBMatch.Elements.Count)
                    return new IntegerValue(0);
                
                for (int i = 0; i < vecAMatch.Elements.Count; i++)
                {
                    var result = Match(vecAMatch.Elements[i], vecBMatch.Elements[i]);
                    if (result is IntegerValue intResult && intResult.Value == 0)
                        return new IntegerValue(0);
                }
                return new IntegerValue(1);
            }
            
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(intA.Value == intB.Value ? 1 : 0);
            if (a is LongValue longA && b is LongValue longB)
                return new IntegerValue(longA.Value == longB.Value ? 1 : 0);
            if (a is CharacterValue charA && b is CharacterValue charB)
                return new IntegerValue(charA.Value == charB.Value ? 1 : 0);
            if (a is SymbolValue symA && b is SymbolValue symB)
                return new IntegerValue(symA.Value == symB.Value ? 1 : 0);
            
            throw new Exception($"Cannot compare {a.Type} and {b.Type} with =");
        }

        public K3Value Equal(K3Value a, K3Value b)
        {
            // For element-wise equality comparison (= operator)
            if (a is VectorValue vecA && b is VectorValue vecB)
            {
                if (vecA.Elements.Count != vecB.Elements.Count)
                    throw new Exception($"length error: {vecA.Elements.Count} != {vecB.Elements.Count}");
                
                var results = new List<K3Value>();
                for (int i = 0; i < vecA.Elements.Count; i++)
                {
                    results.Add(Equal(vecA.Elements[i], vecB.Elements[i]));
                }
                return new VectorValue(results);
            }
            
            // Handle vector + scalar operations
            if (a is VectorValue vecAEqual)
            {
                var results = new List<K3Value>();
                foreach (var element in vecAEqual.Elements)
                {
                    results.Add(Equal(element, b));
                }
                return new VectorValue(results);
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vecBEqual)
            {
                var results = new List<K3Value>();
                foreach (var element in vecBEqual.Elements)
                {
                    results.Add(Equal(a, element));
                }
                return new VectorValue(results);
            }
            
            // For scalar comparison, delegate to Match
            return Match(a, b);
        }

        private K3Value Power(K3Value a, K3Value b)
        {
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue((int)Math.Pow(intA.Value, intB.Value));
            if (a is LongValue longA && b is LongValue longB)
                return new LongValue((long)Math.Pow(longA.Value, longB.Value));
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new FloatValue(Math.Pow(floatA.Value, floatB.Value));
            
            // Handle mixed types - convert to float
            if (a is IntegerValue intA2 && b is FloatValue floatB2)
                return new FloatValue(Math.Pow(intA2.Value, floatB2.Value));
            if (a is FloatValue floatA2 && b is IntegerValue intB2)
                return new FloatValue(Math.Pow(floatA2.Value, intB2.Value));
            if (a is LongValue longA2 && b is FloatValue floatB3)
                return new FloatValue(Math.Pow(longA2.Value, floatB3.Value));
            if (a is FloatValue floatA3 && b is LongValue longB2)
                return new FloatValue(Math.Pow(floatA3.Value, longB2.Value));
            if (a is IntegerValue intA3 && b is LongValue longB3)
                return new LongValue((long)Math.Pow(intA3.Value, longB3.Value));
            if (a is LongValue longA3 && b is IntegerValue intB3)
                return new LongValue((long)Math.Pow(longA3.Value, intB3.Value));
            
            // Handle vector operations
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                {
                    if (vecA.Elements.Count != vecB.Elements.Count)
                        throw new Exception($"length error: {vecA.Elements.Count} != {vecB.Elements.Count}");
                    
                    var results = new List<K3Value>();
                    for (int i = 0; i < vecA.Elements.Count; i++)
                    {
                        results.Add(Power(vecA.Elements[i], vecB.Elements[i]));
                    }
                    return new VectorValue(results);
                }
                else
                {
                    var results = new List<K3Value>();
                    foreach (var element in vecA.Elements)
                    {
                        results.Add(Power(element, b));
                    }
                    return new VectorValue(results);
                }
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vectorB)
            {
                // For scalar + vector, apply power to each element
                var result = new List<K3Value>();
                foreach (var element in vectorB.Elements)
                {
                    result.Add(Power(a, element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot raise {a.Type} to power of {b.Type}");
        }

        private K3Value ModRotate(K3Value left, K3Value right)
        {
            // Enhanced ! operator with multiple behaviors
            if (left is IntegerValue leftInt && right is IntegerValue rightInt)
            {
                // Integer mod: remainder of division
                return new IntegerValue(leftInt.Value % rightInt.Value);
            }
            else if (left is VectorValue leftVec && right is IntegerValue rightIntVal)
            {
                // Vector mod: remainder for each element
                var result = new List<K3Value>();
                foreach (var element in leftVec.Elements)
                {
                    if (element is IntegerValue intVal)
                    {
                        result.Add(new IntegerValue(intVal.Value % rightIntVal.Value));
                    }
                    else
                    {
                        throw new Exception("Vector mod requires all elements to be integers");
                    }
                }
                // Preserve input vector type, use GetVectorType to handle null case
                int vectorType = GetVectorType(leftVec);
                return new VectorValue(result, vectorType);
            }
            else if (left is IntegerValue leftIntVal && right is VectorValue rightVec)
            {
                // Vector rotation: rotate vector by integer
                int rotation = leftIntVal.Value;
                int size = rightVec.Elements.Count;
                
                if (size == 0)
                    return new VectorValue(new List<K3Value>());
                
                // Normalize rotation to be within vector bounds
                rotation = ((rotation % size) + size) % size;
                
                var result = new List<K3Value>();
                for (int i = 0; i < size; i++)
                {
                    result.Add(rightVec.Elements[(i + rotation) % size]);
                }
                // Preserve input vector type
                int vectorType = GetVectorType(rightVec);
                return new VectorValue(result, vectorType);
            }
            else if (left is VectorValue leftVec2 && right is VectorValue rightVec2)
            {
                // Vector + Vector: apply element-wise (for 'each' adverb)
                // Each element of left rotates the corresponding element of right
                var result = new List<K3Value>();
                int minLength = Math.Min(leftVec2.Elements.Count, rightVec2.Elements.Count);

                for (int i = 0; i < minLength; i++)
                {
                    var leftElement = leftVec2.Elements[i];
                    var rightElement = rightVec2.Elements[i];

                    // Recursively apply ModRotate to each pair
                    var pairResult = ModRotate(leftElement, rightElement);
                    result.Add(pairResult);
                }

                return new VectorValue(result);
            }
            else
            {
                throw new Exception("Modulus operator requires integer arguments or vector+integer combinations");
            }
        }

        private K3Value Join(K3Value a, K3Value b)
        {
            // Handle joining two values into a vector
            var elements = new List<K3Value>();
            
            if (a is VectorValue vecA)
            {
                elements.AddRange(vecA.Elements);
            }
            else
            {
                elements.Add(a);
            }
            
            if (b is VectorValue vecB)
            {
                elements.AddRange(vecB.Elements);
            }
            else
            {
                elements.Add(b);
            }
            
            // Determine vector type based on result elements
            int vectorType = GetVectorType(new VectorValue(elements));
            return new VectorValue(elements, vectorType);
        }

        private K3Value Take(K3Value count, K3Value data)
        {
            if (count is IntegerValue intCount)
            {
                // n#f where f is a function/projection: create deferred projection (n#,:) style
                // When applied to x, computes n#(f x)
                if (data is FunctionValue || data is AdverbProjectedFunctionValue || data is ProjectedFunctionValue)
                {
                    var capturedCount = intCount;
                    var capturedFunc = data;
                    return new DeferredTakeProjection(capturedCount, capturedFunc, this);
                }

                if (data is VectorValue dataVec)
                {
                    // Take from vector
                    var actualCount = Math.Max(0, intCount.Value);
                    var result = new List<K3Value>();
                    
                    if (dataVec.Elements.Count == 0)
                    {
                        // Empty source vector - return empty result with same type
                        return new VectorValue(result, GetVectorType(dataVec));
                    }
                    
                    // Repeat the source vector periodically to fill the requested count
                    for (int i = 0; i < actualCount; i++)
                    {
                        var sourceIndex = i % dataVec.Elements.Count;
                        result.Add(dataVec.Elements[sourceIndex]);
                    }
                    
                    // Preserve vector type in the result
                    return new VectorValue(result, GetVectorType(dataVec));
                }
                else
                {
                    // Take from scalar - create vector with scalar repeated
                    var actualCount = Math.Max(0, intCount.Value);
                    var result = new List<K3Value>();
                    
                    for (int i = 0; i < actualCount; i++)
                    {
                        result.Add(data!);
                    }
                    
                    // Determine vector type from scalar type
                    int vectorType = GetScalarVectorType(data!);
                    return new VectorValue(result, vectorType);
                }
            }
            else if (count is LongValue longCount)
            {
                // Handle long count by converting to integer
                return Take(new IntegerValue((int)longCount.Value), data!);
            }
            else if (count is VectorValue shapeVec)
            {
                // Reshape: vector left arg specifies shape dimensions
                // e.g., 3 4 # !12 → 3 rows of 4 elements
                var shape = shapeVec.Elements.Select(e =>
                {
                    if (e is IntegerValue iv) return iv.Value;
                    if (e is LongValue lv) return (int)lv.Value;
                    throw new Exception("Reshape dimensions must be integers");
                }).ToList();

                // Flatten the data source into a list of elements
                var flatData = new List<K3Value>();
                if (data is VectorValue dataVec2)
                    flatData.AddRange(dataVec2.Elements);
                else
                    flatData.Add(data!);

                // Build the reshaped result from innermost dimension outward
                // For 2+ dimensions, recursively build nested vectors
                return ReshapeBuild(shape, 0, flatData, new int[] { 0 });
            }
            else
            {
                throw new Exception("Take count must be an integer");
            }
        }

        private K3Value ReshapeBuild(List<int> shape, int dim, List<K3Value> flatData, int[] index)
        {
            int size = shape[dim];
            if (dim == shape.Count - 1)
            {
                // Innermost dimension: take individual elements with cycling
                var elements = new List<K3Value>();
                for (int i = 0; i < size; i++)
                {
                    elements.Add(flatData[index[0] % flatData.Count]);
                    index[0]++;
                }
                return new VectorValue(elements);
            }
            else
            {
                // Outer dimension: build sub-vectors
                var rows = new List<K3Value>();
                for (int i = 0; i < size; i++)
                {
                    rows.Add(ReshapeBuild(shape, dim + 1, flatData, index));
                }
                return new VectorValue(rows);
            }
        }

        private K3Value FloorBinary(K3Value left, K3Value right)
        {
            // Enhanced _ operator with multiple behaviors
            if (left is VectorValue leftVec && right is VectorValue rightVec)
            {
                // Cut operation: cut vector at specified indices
                var result = new List<K3Value>();
                int prevIndex = 0;
                
                foreach (var element in leftVec.Elements)
                {
                    if (element is IntegerValue cutPoint)
                    {
                        if (cutPoint.Value < 0)
                            throw new Exception("Cut operation cannot contain negative indices");
                        
                        if (cutPoint.Value > prevIndex && cutPoint.Value <= rightVec.Elements.Count)
                        {
                            var subVector = new List<K3Value>();
                            for (int i = prevIndex; i < cutPoint.Value && i < rightVec.Elements.Count; i++)
                            {
                                subVector.Add(rightVec.Elements[i]);
                            }
                            int vectorType = GetVectorType(leftVec);
                            result.Add(new VectorValue(subVector, vectorType)); // Preserve input vector type
                        }
                        prevIndex = cutPoint.Value;
                    }
                    else
                    {
                        throw new Exception("Cut operation requires integer indices");
                    }
                }
                
                // Add remaining elements
                if (prevIndex < rightVec.Elements.Count)
                {
                    var subVector = new List<K3Value>();
                    for (int i = prevIndex; i < rightVec.Elements.Count; i++)
                    {
                        subVector.Add(rightVec.Elements[i]);
                    }
                    int vectorType = GetVectorType(leftVec);
                    result.Add(new VectorValue(subVector, vectorType)); // Preserve input vector type
                }
                
                return new VectorValue(result);
            }
            else if (left is IntegerValue leftInt && right is VectorValue dropVec)
            {
                // Drop operation: drop N elements from start or end
                int dropCount = leftInt.Value;
                int size = dropVec.Elements.Count;
                
                if (dropCount >= 0)
                {
                    // Drop from start
                    if (dropCount >= size)
                        return new VectorValue(new List<K3Value>());
                    
                    var result = new List<K3Value>();
                    for (int i = dropCount; i < size; i++)
                    {
                        result.Add(dropVec.Elements[i]);
                    }
                    return new VectorValue(result);
                }
                else
                {
                    // Drop from end (negative count)
                    int dropFromEnd = -dropCount;
                    if (dropFromEnd >= size)
                        return new VectorValue(new List<K3Value>());
                    
                    var result = new List<K3Value>();
                    for (int i = 0; i < size - dropFromEnd; i++)
                    {
                        result.Add(dropVec.Elements[i]);
                    }
                    return new VectorValue(result);
                }
            }
            else
            {
                throw new Exception("Drop/Cut operation requires vector arguments or integer+vector");
            }
        }

        private bool IsNonZeroInteger(K3Value value)
        {
            if (value is IntegerValue intValue)
            {
                return intValue.Value != 0;
            }
            else if (value is LongValue longValue)
            {
                return longValue.Value != 0;
            }
            else
            {
                throw new Exception("Condition must be an integer atom");
            }
        }

        private K3Value LessThan(K3Value a, K3Value b)
        {
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(intA.Value < intB.Value ? 1 : 0);
            if (a is LongValue longA && b is LongValue longB)
                return new IntegerValue(longA.Value < longB.Value ? 1 : 0);
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new IntegerValue(floatA.Value < floatB.Value ? 1 : 0);
            if (a is CharacterValue charA && b is CharacterValue charB)
                return new IntegerValue(charA.Value[0] < charB.Value[0] ? 1 : 0);
            
            // Handle string comparison (character vectors)
            if (a is VectorValue vecA && b is VectorValue vecB && 
                vecA.Elements.All(e => e is CharacterValue) && vecB.Elements.All(e => e is CharacterValue))
            {
                var strA = string.Join("", vecA.Elements.Select(e => ((CharacterValue)e).Value));
                var strB = string.Join("", vecB.Elements.Select(e => ((CharacterValue)e).Value));
                return new IntegerValue(string.Compare(strA, strB) < 0 ? 1 : 0);
            }
            
            throw new Exception($"Cannot compare {a.Type} and {b.Type} with <");
        }

        private K3Value GreaterThan(K3Value a, K3Value b)
        {
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(intA.Value > intB.Value ? 1 : 0);
            if (a is LongValue longA && b is LongValue longB)
                return new IntegerValue(longA.Value > longB.Value ? 1 : 0);
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new IntegerValue(floatA.Value > floatB.Value ? 1 : 0);
            if (a is CharacterValue charA && b is CharacterValue charB)
                return new IntegerValue(charA.Value[0] > charB.Value[0] ? 1 : 0);
            
            // Handle string comparison (character vectors)
            if (a is VectorValue vecA && b is VectorValue vecB && 
                vecA.Elements.All(e => e is CharacterValue) && vecB.Elements.All(e => e is CharacterValue))
            {
                var strA = string.Join("", vecA.Elements.Select(e => ((CharacterValue)e).Value));
                var strB = string.Join("", vecB.Elements.Select(e => ((CharacterValue)e).Value));
                return new IntegerValue(string.Compare(strA, strB) > 0 ? 1 : 0);
            }
            
            throw new Exception($"Cannot compare {a.Type} and {b.Type} with >");
        }

        private K3Value ArithmeticNegate(K3Value a)
        {
            if (a is IntegerValue intA)
                return new IntegerValue(-intA.Value);
            if (a is LongValue longA)
                return new LongValue(-longA.Value);
            if (a is FloatValue floatA)
                return new FloatValue(-floatA.Value);
            
            throw new Exception($"Cannot negate {a.Type}");
        }

        private K3Value LogicalNegate(K3Value a)
        {
            if (a is IntegerValue intA)
                return new IntegerValue(intA.Value == 0 ? 1 : 0);
            if (a is LongValue longA)
                return new IntegerValue(longA.Value == 0 ? 1 : 0);
            if (a is FloatValue floatA)
                return new IntegerValue(floatA.Value == 0 ? 1 : 0);
            
            if (a is VectorValue vec)
            {
                var result = new List<K3Value>();
                foreach (var element in vec.Elements)
                {
                    result.Add(LogicalNegate(element));
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot logically negate {a.Type}");
        }

        private K3Value MonadicMinus(K3Value a)
        {
            if (a is IntegerValue intA)
                return new IntegerValue(-intA.Value);
            if (a is LongValue longA)
                return new LongValue(-longA.Value);
            if (a is FloatValue floatA)
                return new FloatValue(-floatA.Value);
            if (a is VectorValue vecA)
            {
                var result = new List<K3Value>();
                foreach (var element in vecA.Elements)
                    result.Add(MonadicMinus(element));
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot apply monadic minus to {a.Type}");
        }

        private K3Value Transpose(K3Value a)
        {
            // Flip/transpose operation: +(`a`b`c;1 2 3) -> ((`a;1);(`b;2);(`c;3))
            if (a is VectorValue vec && vec.Elements.Count == 2)
            {
                var first = vec.Elements[0];
                var second = vec.Elements[1];
                
                if (first is VectorValue firstVec && second is VectorValue secondVec)
                {
                    // Check if both vectors have the same length
                    if (firstVec.Elements.Count != secondVec.Elements.Count)
                    {
                        throw new Exception("Flip requires vectors of equal length");
                    }
                    
                    // Create the flipped structure: ((first[i];second[i]);...)
                    var result = new List<K3Value>();
                    for (int i = 0; i < firstVec.Elements.Count; i++)
                    {
                        var pair = new List<K3Value> { firstVec.Elements[i], secondVec.Elements[i] };
                        result.Add(new VectorValue(pair));
                    }
                    
                    return new VectorValue(result);
                }
            }
            
            // For other cases, return as-is (matrix transpose not implemented)
            return a;
        }

        private K3Value First(K3Value a)
        {
            if (a is VectorValue vecA && vecA.Elements.Count > 0)
                return vecA.Elements[0];
            
            return a; // For scalars, return the value itself
        }

        private K3Value Reciprocal(K3Value a)
        {
            if (a is IntegerValue intA)
                return new FloatValue(1.0 / intA.Value);
            if (a is LongValue longA)
                return new FloatValue(1.0 / longA.Value);
            if (a is FloatValue floatA)
                return new FloatValue(1.0 / floatA.Value);
            
            throw new Exception($"Cannot find reciprocal of {a.Type}");
        }

        private K3Value Where(K3Value a)
        {
            // Convert scalar to single-element vector for consistent processing
            VectorValue vecA;
            if (a is IntegerValue intA)
            {
                vecA = new VectorValue(new List<K3Value> { intA });
            }
            else if (a is VectorValue vectorA)
            {
                vecA = vectorA;
            }
            else
            {
                throw new Exception($"Cannot apply where to {a.Type}");
            }
            
            // Generate indices repeated according to count values
            var elements = new List<K3Value>();
            for (int i = 0; i < vecA.Elements.Count; i++)
            {
                var element = vecA.Elements[i];
                int count = 0;
                
                // Get count value from element
                if (element is IntegerValue intVal)
                {
                    count = intVal.Value;
                }
                else if (element is FloatValue floatVal)
                {
                    count = (int)floatVal.Value;
                }
                
                // Add index repeated 'count' times
                for (int j = 0; j < count; j++)
                {
                    elements.Add(new IntegerValue(i));
                }
            }
            return new VectorValue(elements);
        }

        private K3Value Reverse(K3Value a)
        {
            if (a is VectorValue vecA)
            {
                var reversed = new List<K3Value>(vecA.Elements);
                reversed.Reverse();
                // Preserve input vector type
                int vectorType = GetVectorType(vecA);
                return new VectorValue(reversed, vectorType);
            }
            
            return a; // For scalars, return the value itself
        }

        private K3Value GradeUp(K3Value a)
        {
            if (a is VectorValue vecA)
            {
                var indices = new List<int>();
                for (int i = 0; i < vecA.Elements.Count; i++)
                {
                    indices.Add(i);
                }
                
                // Simple stable sort implementation
                indices.Sort((i, j) => CompareValues(vecA.Elements[i], vecA.Elements[j]));
                
                var result = new List<K3Value>();
                foreach (var index in indices)
                {
                    result.Add(new IntegerValue(index));
                }
                return new VectorValue(result, -1); // Integer vector
            }
            
            throw new Exception("Rank error: grade-up operator '<' requires a vector argument");
        }

        private K3Value GradeDown(K3Value a)
        {
            if (a is VectorValue vecA)
            {
                var indices = new List<int>();
                for (int i = 0; i < vecA.Elements.Count; i++)
                {
                    indices.Add(i);
                }
                
                // Simple stable sort implementation (descending)
                indices.Sort((i, j) => CompareValues(vecA.Elements[j], vecA.Elements[i]));
                
                var result = new List<K3Value>();
                foreach (var index in indices)
                {
                    result.Add(new IntegerValue(index));
                }
                return new VectorValue(result, -1); // Integer vector
            }
            
            throw new Exception("Rank error: grade-down operator '>' requires a vector argument");
        }

        private int CompareValues(K3Value a, K3Value b)
        {
            // Handle all K3Value types
            if (a is IntegerValue intA && b is IntegerValue intB)
                return intA.Value.CompareTo(intB.Value);
            if (a is LongValue longA && b is LongValue longB)
                return longA.Value.CompareTo(longB.Value);
            if (a is FloatValue floatA && b is FloatValue floatB)
                return floatA.Value.CompareTo(floatB.Value);
            if (a is CharacterValue charA && b is CharacterValue charB)
                return charA.Value.CompareTo(charB.Value);
            if (a is SymbolValue symA && b is SymbolValue symB)
                return string.Compare(symA.Value, symB.Value, StringComparison.Ordinal);
            
            // For vectors and other types, use ToString comparison
            int comparison = string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
            
            // For stable sorting: if equal, preserve original order
            if (comparison == 0)
                return -1; // a comes before b in original order
            
            return comparison;
        }

        private K3Value Shape(K3Value a)
        {
            if (a is VectorValue vecA)
            {
                // Check if this is a simple vector (no nested vectors)
                var hasNestedVectors = vecA.Elements.Any(e => e is VectorValue);
                
                if (!hasNestedVectors)
                {
                    // Simple vector - return its length as a 1-element vector
                    return new VectorValue(new List<K3Value> { new IntegerValue(vecA.Elements.Count) }, -1); // Integer vector
                }
                else
                {
                    // Matrix or tensor - compute dimensions
                    var dimensions = new List<int>();
                    var current = vecA;
                    
                    // First dimension is number of top-level elements
                    dimensions.Add(current.Elements.Count);
                    
                    // Check if we have a regular matrix/tensor
                    if (current.Elements.Count > 0 && current.Elements[0] is VectorValue)
                    {
                        var firstElement = (VectorValue)current.Elements[0];
                        var isUniform = true;
                        var uniformLength = firstElement.Elements.Count;
                        
                        // Check if all elements have the same structure
                        foreach (var element in current.Elements)
                        {
                            if (element is VectorValue vec)
                            {
                                if (vec.Elements.Count != uniformLength)
                                {
                                    isUniform = false;
                                    break;
                                }
                            }
                            else
                            {
                                isUniform = false;
                                break;
                            }
                        }
                        
                        if (isUniform)
                        {
                            // Check if we have nested vectors (3D tensor) - only if uniformLength > 0
                            if (uniformLength > 0 && firstElement.Elements[0] is VectorValue)
                            {
                                // 3D tensor - check uniformity of third dimension
                                var thirdDimUniform = true;
                                var thirdDimLength = ((VectorValue)firstElement.Elements[0]).Elements.Count;
                                
                                foreach (var element in current.Elements)
                                {
                                    var vec = (VectorValue)element;
                                    foreach (var subElement in vec.Elements)
                                    {
                                        if (subElement is VectorValue subVec)
                                        {
                                            if (subVec.Elements.Count != thirdDimLength)
                                            {
                                                thirdDimUniform = false;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            thirdDimUniform = false;
                                            break;
                                        }
                                    }
                                    if (!thirdDimUniform) break;
                                }
                                
                                if (thirdDimUniform)
                                {
                                    dimensions.Add(uniformLength);
                                    dimensions.Add(thirdDimLength);
                                }
                                else
                                {
                                    // Jagged in third dimension - only add dimensions that are uniform
                                    dimensions.Add(uniformLength);
                                }
                            }
                            else
                            {
                                // 2D matrix - add second dimension
                                dimensions.Add(uniformLength);
                            }
                        }
                        else
                        {
                            // Jagged matrix - only add first dimension (rows)
                            // According to spec: "shape will be a vector of the lengths of the dimensions that do have uniform length"
                        }
                    }
                    
                    return new VectorValue(dimensions.Select(d => (K3Value)new IntegerValue(d)).ToList(), -1); // Integer vector
                }
            }
            else if (a is VectorValue list)
            {
                // Handle VectorValue (empty list) according to updated spec
                // "If the input is an empty list result will be handled like a jagged list representing 1 dimension of length 0: ,0"
                return new VectorValue(new List<K3Value> { new IntegerValue(0) }, -1); // Integer vector
            }
            
            return new VectorValue(new List<K3Value>(), -1); // Empty integer vector
        }

        private K3Value Enumerate(K3Value a)
        {
            if (a is IntegerValue intA)
            {
                if (intA.Value == 0)
                {
                    return new VectorValue(new List<K3Value>(), -1); // Empty integer vector
                }
                
                var elements = new List<K3Value>();
                for (int i = 0; i < intA.Value; i++)
                {
                    elements.Add(new IntegerValue(i));
                }
                return new VectorValue(elements, -1); // Integer vector
            }
            else if (a is LongValue longA)
            {
                if (longA.Value == 0)
                {
                    return new VectorValue(new List<K3Value>(), -1); // Empty integer vector
                }
                
                var elements = new List<K3Value>();
                for (long i = 0; i < longA.Value; i++)
                {
                    elements.Add(new LongValue(i));
                }
                return new VectorValue(elements, -64); // Long vector
            }
            else if (a is SymbolValue sym)
            {
                // Handle empty symbol as root K-tree enumeration
                if (sym.Value == "")
                {
                    return kTree.GetRootKeys();
                }
                
                // Handle symbol as a path to a dictionary
                var dictValue = GetVariableValue(sym.Value);
                if (dictValue is DictionaryValue dict)
                {
                    var keys = new List<K3Value>();
                    foreach (var key in dict.Entries.Keys)
                    {
                        keys.Add(key);
                    }
                    return new VectorValue(keys, -4); // Symbol vector
                }
                throw new Exception($"Cannot enumerate {sym.Value}: not a dictionary");
            }
            else if (a is DictionaryValue dict)
            {
                // Enumerate operator on dictionary returns list of keys
                var keys = new List<K3Value>();
                foreach (var key in dict.Entries.Keys)
                {
                    keys.Add(key);
                }
                return new VectorValue(keys, -4); // Symbol vector
            }
            else if (a is FunctionValue func)
            {
                // For FFI functions, return function information
                var info = new List<K3Value>();
                
                // Add function name
                info.Add(new SymbolValue(func.BodyText));
                
                // Add parameters
                if (func.Parameters.Count > 0)
                {
                    var paramList = new List<K3Value>();
                    foreach (var param in func.Parameters)
                    {
                        paramList.Add(new SymbolValue(param));
                    }
                    info.Add(new VectorValue(paramList, -4)); // Symbol vector of parameters
                }
                
                // Add hint if available
                if (func.Hint != null)
                {
                    info.Add(func.Hint);
                }
                
                return new VectorValue(info, 0); // Mixed list
            }
            
            throw new Exception($"Cannot enumerate {a.Type}");
        }

        public K3Value Enlist(K3Value a)
        {
            var elements = new List<K3Value> { a };
            
            // Set vector type based on argument type
            int vectorType = a.Type switch
            {
                ValueType.Integer => -1,    // Integer vector
                ValueType.Float => -2,      // Float vector  
                ValueType.Character => -3,  // Character vector
                ValueType.Symbol => -4,     // Symbol vector
                ValueType.Long => -64,     // Long vector
                _ => 0                  // Generic list for other types
            };
            
            return new VectorValue(elements, vectorType);
        }

        private K3Value Count(K3Value a)
        {
            if (a is VectorValue vecA)
                return new IntegerValue(vecA.Elements.Count);
            
            return new IntegerValue(1); // For scalars
        }

        private K3Value Floor(K3Value a)
        {
            if (a is IntegerValue intA)
                return intA;
            if (a is LongValue longA)
                return longA;
            if (a is FloatValue floatA)
            {
                // Handle special values according to speclet
                if (double.IsPositiveInfinity(floatA.Value))
                    return new IntegerValue("0I");
                else if (double.IsNegativeInfinity(floatA.Value))
                    return new IntegerValue("-0I");
                else if (double.IsNaN(floatA.Value))
                    return new IntegerValue("0N");
                else
                    return new IntegerValue((int)Math.Floor(floatA.Value));
            }
            
            throw new Exception($"Cannot floor {a.Type}");
        }

        // Binary versions for operators that can be both monadic and binary
        private bool IsScalar(K3Value value)
        {
            return value is IntegerValue || value is LongValue || value is FloatValue || 
                   value is CharacterValue || value is SymbolValue || value is NullValue;
        }
        
        private int GetScalarVectorType(K3Value scalar)
        {
            if (scalar is IntegerValue || scalar is LongValue)
                return -1; // Integer vector
            else if (scalar is FloatValue)
                return -2; // Float vector  
            else if (scalar is CharacterValue)
                return -3; // Character vector
            else if (scalar is SymbolValue)
                return -4; // Symbol vector
            else
                return 0; // Default to mixed list
        }
        
        private int GetVectorType(VectorValue vec)
        {
            if (vec.Elements.Count == 0)
                return 0; // Default to mixed list for empty vectors without explicit type
                
            // For non-empty vectors, determine from element types
            var firstElement = vec.Elements[0];
            if (vec.Elements.All(e => e is IntegerValue || e is LongValue))
                return -1; // Integer vector
            else if (vec.Elements.All(e => e is FloatValue))
                return -2; // Float vector  
            else if (vec.Elements.All(e => e is CharacterValue))
                return -3; // Character vector
            else if (vec.Elements.All(e => e is SymbolValue))
                return -4; // Symbol vector
            else
                return 0; // Default to mixed list
        }
    }
}
