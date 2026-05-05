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
            
            // Handle same type operations
            if (a is IntegerValue intA && b is IntegerValue intB)
                return new IntegerValue(intA.Value + intB.Value);
            if (a is LongValue longA && b is LongValue longB)
                return new LongValue(longA.Value + longB.Value);
            if (a is FloatValue floatA && b is FloatValue floatB)
                return new FloatValue(floatA.Value + floatB.Value);
            
            // Handle vector operations - element-wise
            if (a is VectorValue vecA)
            {
                if (b is VectorValue vecB)
                {
                    if (vecA.Elements.Count != vecB.Elements.Count)
                        throw new InvalidOperationException("Vector size mismatch for addition");
                    var result = new List<K3Value>();
                    for (int i = 0; i < vecA.Elements.Count; i++)
                        result.Add(Plus(vecA.Elements[i], vecB.Elements[i]));
                    return new VectorValue(result);
                }
                else
                {
                    var result = new List<K3Value>();
                    foreach (var elem in vecA.Elements)
                        result.Add(Plus(elem, b));
                    return new VectorValue(result);
                }
            }
            
            // Handle scalar + vector operations
            if (b is VectorValue vectorB)
            {
                var result = new List<K3Value>();
                foreach (var elem in vectorB.Elements)
                    result.Add(Plus(a, elem));
                return new VectorValue(result);
            }
            
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
            
            // Handle same type operations
            if (a is IntegerValue intA2 && b is IntegerValue intB2)
                return new IntegerValue(intA2.Value - intB2.Value);
            if (a is LongValue longA2 && b is LongValue longB2)
                return new LongValue(longA2.Value - longB2.Value);
            if (a is FloatValue floatA2 && b is FloatValue floatB2)
                return new FloatValue(floatA2.Value - floatB2.Value);
            
            // Handle vector operations - element-wise
            if (a is VectorValue vecA2)
            {
                if (b is VectorValue vecB2)
                {
                    if (vecA2.Elements.Count != vecB2.Elements.Count)
                        throw new InvalidOperationException("Vector size mismatch for subtraction");
                    var result = new List<K3Value>();
                    for (int i = 0; i < vecA2.Elements.Count; i++)
                        result.Add(Minus(vecA2.Elements[i], vecB2.Elements[i]));
                    return new VectorValue(result);
                }
                else
                {
                    var result = new List<K3Value>();
                    foreach (var elem in vecA2.Elements)
                        result.Add(Minus(elem, b));
                    return new VectorValue(result);
                }
            }
            
            // Handle scalar - vector operations
            if (b is VectorValue vectorB2)
            {
                var result = new List<K3Value>();
                foreach (var elem in vectorB2.Elements)
                    result.Add(Minus(a, elem));
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
            
            // Handle same type operations
            if (a is IntegerValue intA3 && b is IntegerValue intB3)
                return new IntegerValue(intA3.Value * intB3.Value);
            if (a is LongValue longA3 && b is LongValue longB3)
                return new LongValue(longA3.Value * longB3.Value);
            if (a is FloatValue floatA3 && b is FloatValue floatB3)
                return new FloatValue(floatA3.Value * floatB3.Value);
            
            // Handle vector operations - element-wise
            if (a is VectorValue vecA3)
            {
                if (b is VectorValue vecB3)
                {
                    if (vecA3.Elements.Count != vecB3.Elements.Count)
                        throw new InvalidOperationException("Vector size mismatch for multiplication");
                    var result = new List<K3Value>();
                    for (int i = 0; i < vecA3.Elements.Count; i++)
                        result.Add(Times(vecA3.Elements[i], vecB3.Elements[i]));
                    return new VectorValue(result);
                }
                else
                {
                    var result = new List<K3Value>();
                    foreach (var elem in vecA3.Elements)
                        result.Add(Times(elem, b));
                    return new VectorValue(result);
                }
            }
            
            // Handle scalar * vector operations
            if (b is VectorValue vectorB3)
            {
                var result = new List<K3Value>();
                foreach (var elem in vectorB3.Elements)
                    result.Add(Times(a, elem));
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot multiply {a.Type} and {b.Type}");
        }

        private K3Value Divide(K3Value a, K3Value b)
        {
            // Handle integer division - scalar division always promotes to float
            if (a is IntegerValue && b is IntegerValue)
            {
                int divisor = ((IntegerValue)b).Value;
                int dividend = ((IntegerValue)a).Value;
                
                // Special case: 0%0 returns 0 (per K specification)
                if (divisor == 0 && dividend == 0)
                    return new IntegerValue(0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                
                // Scalar division always promotes to float in K
                return new FloatValue((double)dividend / divisor);
            }
            
            // Handle long division - scalar division always promotes to float
            if (a is LongValue && b is LongValue)
            {
                long divisor = ((LongValue)b).Value;
                long dividend = ((LongValue)a).Value;
                
                // Special case: 0j%0j returns 0j (per K specification)
                if (divisor == 0 && dividend == 0)
                    return new LongValue(0);
                
                if (divisor == 0)
                    throw new Exception("Division by zero");
                
                // Scalar division always promotes to float
                return new FloatValue((double)dividend / divisor);
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
            if (a is IntegerValue && b is FloatValue)
            {
                double divisor = ((FloatValue)b).Value;
                int dividend = ((IntegerValue)a).Value;
                
                // Special case: 0%0.0 returns 0.0 (per K specification - type promotion case)
                if (divisor == 0.0 && dividend == 0)
                    return new FloatValue(0.0);
                
                if (divisor == 0.0)
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
            
            // Handle vector operations - element-wise
            if (a is VectorValue vecA4)
            {
                if (b is VectorValue vecB4)
                {
                    if (vecA4.Elements.Count != vecB4.Elements.Count)
                        throw new InvalidOperationException("Vector size mismatch for division");
                    
                    // Check if all elements are integers and if any division has remainder
                    bool allIntegers = vecA4.Elements.All(e => e is IntegerValue or LongValue) && 
                                       vecB4.Elements.All(e => e is IntegerValue or LongValue);
                    
                    bool anyHasRemainder = false;
                    if (allIntegers)
                    {
                        for (int i = 0; i < vecA4.Elements.Count; i++)
                        {
                            var left = vecA4.Elements[i];
                            var right = vecB4.Elements[i];
                            long dividend = left is IntegerValue iv ? iv.Value : ((LongValue)left).Value;
                            long divisor = right is IntegerValue iv2 ? iv2.Value : ((LongValue)right).Value;
                            
                            // Skip 0%0 and division by zero checks for remainder detection
                            if (divisor != 0 && dividend % divisor != 0)
                            {
                                anyHasRemainder = true;
                                break;
                            }
                        }
                    }
                    
                    var result = new List<K3Value>();
                    for (int i = 0; i < vecA4.Elements.Count; i++)
                    {
                        var left = vecA4.Elements[i];
                        var right = vecB4.Elements[i];
                        K3Value divResult;
                        
                        if (allIntegers && anyHasRemainder)
                        {
                            // Cast to float and do float division
                            double dividend = left is IntegerValue iv ? iv.Value : ((LongValue)left).Value;
                            double divisor = right is IntegerValue iv2 ? iv2.Value : ((LongValue)right).Value;
                            
                            if (divisor == 0.0 && dividend == 0.0)
                                divResult = new FloatValue(0.0);
                            else if (divisor == 0)
                                throw new Exception("Division by zero");
                            else
                                divResult = new FloatValue(dividend / divisor);
                        }
                        else if (allIntegers)
                        {
                            // All integers and no remainder - do integer division directly
                            long dividend = left is IntegerValue iv ? iv.Value : ((LongValue)left).Value;
                            long divisor = right is IntegerValue iv2 ? iv2.Value : ((LongValue)right).Value;
                            
                            if (divisor == 0 && dividend == 0)
                                divResult = new IntegerValue(0);
                            else if (divisor == 0)
                                throw new Exception("Division by zero");
                            else
                                divResult = divisor > int.MaxValue || divisor < int.MinValue || 
                                           dividend > int.MaxValue || dividend < int.MinValue
                                    ? new LongValue(dividend / divisor)
                                    : new IntegerValue((int)(dividend / divisor));
                        }
                        else
                        {
                            divResult = Divide(left, right);
                        }
                        
                        // Check if this is 0%0 with adjacent very large numbers
                        divResult = CheckZeroDivisionSpecialCase(left, right, divResult, i, vecA4.Elements, vecB4.Elements);
                        
                        result.Add(divResult);
                    }
                    return new VectorValue(result);
                }
                else
                {
                    // Check if all elements are integers and scalar is integer, and if any division has remainder
                    bool allIntegers = vecA4.Elements.All(e => e is IntegerValue or LongValue) && 
                                       (b is IntegerValue or LongValue);
                    
                    long scalarDivisor = 0;
                    if (b is IntegerValue biv) scalarDivisor = biv.Value;
                    else if (b is LongValue blv) scalarDivisor = blv.Value;
                    
                    bool anyHasRemainder = false;
                    if (allIntegers && scalarDivisor != 0)
                    {
                        anyHasRemainder = vecA4.Elements.Any(e => {
                            long dividend = e is IntegerValue iv ? iv.Value : ((LongValue)e).Value;
                            return dividend % scalarDivisor != 0;
                        });
                    }
                    
                    var result = new List<K3Value>();
                    for (int i = 0; i < vecA4.Elements.Count; i++)
                    {
                        var elem = vecA4.Elements[i];
                        K3Value divResult;
                        
                        if (allIntegers && anyHasRemainder)
                        {
                            // Cast to float and do float division
                            double dividend = elem is IntegerValue iv ? iv.Value : ((LongValue)elem).Value;
                            double divisor = scalarDivisor;
                            
                            if (divisor == 0.0 && dividend == 0.0)
                                divResult = new FloatValue(0.0);
                            else if (divisor == 0)
                                throw new Exception("Division by zero");
                            else
                                divResult = new FloatValue(dividend / divisor);
                        }
                        else if (allIntegers)
                        {
                            // All integers and no remainder - do integer division directly
                            long dividend = elem is IntegerValue iv ? iv.Value : ((LongValue)elem).Value;
                            
                            if (scalarDivisor == 0 && dividend == 0)
                                divResult = new IntegerValue(0);
                            else if (scalarDivisor == 0)
                                throw new Exception("Division by zero");
                            else
                                divResult = scalarDivisor > int.MaxValue || scalarDivisor < int.MinValue || 
                                           dividend > int.MaxValue || dividend < int.MinValue
                                    ? new LongValue(dividend / scalarDivisor)
                                    : new IntegerValue((int)(dividend / scalarDivisor));
                        }
                        else
                        {
                            divResult = Divide(elem, b);
                        }
                        
                        // Check for special 0%0 case with adjacent large numbers
                        divResult = CheckZeroDivisionSpecialCase(elem, b, divResult, i, vecA4.Elements, Enumerable.Repeat(b, vecA4.Elements.Count).ToList());
                        
                        result.Add(divResult);
                    }
                    return new VectorValue(result);
                }
            }
            
            // Handle scalar / vector operations
            if (b is VectorValue vectorB4)
            {
                // Check if scalar is integer and all vector elements are integers, and if any division has remainder
                bool allIntegers = (a is IntegerValue or LongValue) && 
                                   vectorB4.Elements.All(e => e is IntegerValue or LongValue);
                
                long scalarDividend = 0;
                if (a is IntegerValue aiv) scalarDividend = aiv.Value;
                else if (a is LongValue alv) scalarDividend = alv.Value;
                
                bool anyHasRemainder = false;
                if (allIntegers)
                {
                    anyHasRemainder = vectorB4.Elements.Any(e => {
                        long divisor = e is IntegerValue iv ? iv.Value : ((LongValue)e).Value;
                        return divisor != 0 && scalarDividend % divisor != 0;
                    });
                }
                
                var result = new List<K3Value>();
                for (int i = 0; i < vectorB4.Elements.Count; i++)
                {
                    var elem = vectorB4.Elements[i];
                    K3Value divResult;
                    
                    if (allIntegers && anyHasRemainder)
                    {
                        // Cast to float and do float division
                        double dividend = scalarDividend;
                        double divisor = elem is IntegerValue iv ? iv.Value : ((LongValue)elem).Value;
                        
                        if (divisor == 0.0 && dividend == 0.0)
                            divResult = new FloatValue(0.0);
                        else if (divisor == 0)
                            throw new Exception("Division by zero");
                        else
                            divResult = new FloatValue(dividend / divisor);
                    }
                    else if (allIntegers)
                    {
                        // All integers and no remainder - do integer division directly
                        long divisor = elem is IntegerValue iv ? iv.Value : ((LongValue)elem).Value;
                        
                        if (divisor == 0 && scalarDividend == 0)
                            divResult = new IntegerValue(0);
                        else if (divisor == 0)
                            throw new Exception("Division by zero");
                        else
                            divResult = divisor > int.MaxValue || divisor < int.MinValue || 
                                       scalarDividend > int.MaxValue || scalarDividend < int.MinValue
                                ? new LongValue(scalarDividend / divisor)
                                : new IntegerValue((int)(scalarDividend / divisor));
                    }
                    else
                    {
                        divResult = Divide(a, elem);
                    }
                    
                    // Check for special 0%0 case with adjacent large numbers
                    divResult = CheckZeroDivisionSpecialCase(a, elem, divResult, i, Enumerable.Repeat(a, vectorB4.Elements.Count).ToList(), vectorB4.Elements);
                    
                    result.Add(divResult);
                }
                return new VectorValue(result);
            }
            
            throw new Exception($"Cannot divide {a.Type} and {b.Type}");
        }
        
        /// <summary>
        /// Check if this is the special 0%0 case in a vector where adjacent elements have very large values.
        /// Per K spec: 0.0%0.0 returns 0.0 unless in a vector with very large adjacent numbers, then returns 0i or -0i.
        /// </summary>
        private K3Value CheckZeroDivisionSpecialCase(K3Value left, K3Value right, K3Value result, int index, List<K3Value> leftElements, List<K3Value> rightElements)
        {
            // Only apply to the 0%0 case
            if (!IsZero(left) || !IsZero(right))
                return result;
            
            // Check if result is currently 0, 0j, or 0.0 (not already special 0i/-0i)
            bool isCurrentlyZero = (result is IntegerValue iv && iv.Value == 0) ||
                                   (result is LongValue lv && lv.Value == 0) ||
                                   (result is FloatValue fv && fv.Value == 0.0);
            
            if (!isCurrentlyZero)
                return result;
            
            // Check adjacent elements for very large values
            bool hasLargeNeighbor = false;
            bool neighborIsPositive = true;
            
            // Check previous element
            if (index > 0)
            {
                var prevLeft = leftElements[index - 1];
                var prevRight = rightElements[index - 1];
                var neighborValue = GetDivisionResultValue(prevLeft, prevRight);
                if (IsVeryLarge(neighborValue))
                {
                    hasLargeNeighbor = true;
                    neighborIsPositive = neighborValue > 0;
                }
            }
            
            // Check next element
            if (!hasLargeNeighbor && index < leftElements.Count - 1)
            {
                var nextLeft = leftElements[index + 1];
                var nextRight = rightElements[index + 1];
                var neighborValue = GetDivisionResultValue(nextLeft, nextRight);
                if (IsVeryLarge(neighborValue))
                {
                    hasLargeNeighbor = true;
                    neighborIsPositive = neighborValue > 0;
                }
            }
            
            if (hasLargeNeighbor)
            {
                // Return 0i or -0i based on neighbor's sign
                return neighborIsPositive ? new FloatValue(float.PositiveInfinity) : new FloatValue(float.NegativeInfinity);
            }
            
            return result;
        }
        
        /// <summary>
        /// Check if a value is zero (for any numeric type)
        /// </summary>
        private bool IsZero(K3Value value)
        {
            return (value is IntegerValue iv && iv.Value == 0) ||
                   (value is LongValue lv && lv.Value == 0) ||
                   (value is FloatValue fv && fv.Value == 0.0);
        }
        
        /// <summary>
        /// Get the result value of dividing two numbers (for checking if neighbor is large)
        /// </summary>
        private double GetDivisionResultValue(K3Value left, K3Value right)
        {
            try
            {
                double GetNumericValue(K3Value v)
                {
                    if (v is IntegerValue iv) return iv.Value;
                    if (v is LongValue lv) return lv.Value;
                    if (v is FloatValue fv) return fv.Value;
                    return 0;
                }
                
                double l = GetNumericValue(left);
                double r = GetNumericValue(right);
                
                if (r == 0)
                    return l >= 0 ? double.PositiveInfinity : double.NegativeInfinity;
                
                return l / r;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Check if a value is "very large" (approaching infinity threshold)
        /// </summary>
        private bool IsVeryLarge(double value)
        {
            // Very large threshold - using a high value that would indicate
            // the neighbor operation produces an extremely large result
            const double veryLargeThreshold = 1e100;
            return Math.Abs(value) > veryLargeThreshold || double.IsInfinity(value);
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
            if (a is IntegerValue intA2 && b is FloatValue floatB2)
                return new IntegerValue(intA2.Value < floatB2.Value ? 1 : 0);
            if (a is FloatValue floatA2 && b is IntegerValue intB2)
                return new IntegerValue(floatA2.Value < intB2.Value ? 1 : 0);
            
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
            if (a is IntegerValue intA2 && b is FloatValue floatB2)
                return new IntegerValue(intA2.Value > floatB2.Value ? 1 : 0);
            if (a is FloatValue floatA2 && b is IntegerValue intB2)
                return new IntegerValue(floatA2.Value > intB2.Value ? 1 : 0);
            
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

            // Handle null values
            if (a is NullValue && b is NullValue)
                return new IntegerValue(1);
            if (a is NullValue || b is NullValue)
                return new IntegerValue(0);

            // A vector can never match an atom (and vice versa)
            bool aIsVec = a is VectorValue;
            bool bIsVec = b is VectorValue;
            if (aIsVec && !bIsVec && b is not NullValue)
                return new IntegerValue(0);
            if (bIsVec && !aIsVec && a is not NullValue)
                return new IntegerValue(0);

            // Handle vector comparison
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

            // Handle cross-type numeric comparisons
            // Check if both are numeric types
            bool aIsNumeric = IsNumericValue(a);
            bool bIsNumeric = IsNumericValue(b);

            if (aIsNumeric && bIsNumeric)
            {
                // Convert both to double for tolerant comparison
                double numA = GetNumericValue(a);
                double numB = GetNumericValue(b);

                // Tolerant equality for numeric types
                if (numA == numB)
                    return new IntegerValue(1);

                double maxAbs = Math.Max(Math.Abs(numA), Math.Abs(numB));
                double threshold = maxAbs * 0.00001; // 0.001 percent
                return new IntegerValue(Math.Abs(numA - numB) < threshold ? 1 : 0);
            }

            // Handle same-type comparisons for non-numeric types
            if (a is CharacterValue charA && b is CharacterValue charB)
                return new IntegerValue(charA.Value == charB.Value ? 1 : 0);
            if (a is SymbolValue symA && b is SymbolValue symB)
                return new IntegerValue(symA.Value == symB.Value ? 1 : 0);

            // Different types that are not both numeric - not equal
            if (aIsNumeric != bIsNumeric)
                return new IntegerValue(0);

            throw new Exception($"Cannot compare {a.Type} and {b.Type} with ~");
        }

        /// <summary>
        /// Check if a value is numeric (Integer, Long, or Float)
        /// </summary>
        private bool IsNumericValue(K3Value value)
        {
            return value is IntegerValue or LongValue or FloatValue;
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
                // Vector mod: remainder for each element (recursively handles nested vectors)
                var result = new List<K3Value>();
                foreach (var element in leftVec.Elements)
                {
                    result.Add(ModRotate(element, rightIntVal));
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
                    var takeCount = intCount.Value;
                    var result = new List<K3Value>();
                    
                    if (dataVec.Elements.Count == 0 || takeCount == 0)
                    {
                        // Empty source vector or zero count - return empty result with same type
                        return new VectorValue(result, GetVectorType(dataVec));
                    }
                    
                    int n = dataVec.Elements.Count;
                    int absCount = Math.Abs(takeCount);
                    
                    // Calculate starting index: positive count starts at 0, negative starts from end
                    int startIndex = takeCount > 0 
                        ? 0 
                        : (n - (absCount % n)) % n;
                    
                    // Collect elements cycling through the source vector
                    for (int i = 0; i < absCount; i++)
                    {
                        var sourceIndex = (startIndex + i) % n;
                        result.Add(dataVec.Elements[sourceIndex]);
                    }
                    
                    // Preserve vector type in the result
                    return new VectorValue(result, GetVectorType(dataVec));
                }
                else
                {
                    // Take from scalar - create vector with scalar repeated
                    var absCount = Math.Abs(intCount.Value);
                    var result = new List<K3Value>();
                    
                    for (int i = 0; i < absCount; i++)
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
            if (a is IntegerValue intA2 && b is FloatValue floatB2)
                return new IntegerValue(intA2.Value < floatB2.Value ? 1 : 0);
            if (a is FloatValue floatA2 && b is IntegerValue intB2)
                return new IntegerValue(floatA2.Value < intB2.Value ? 1 : 0);
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
            if (a is IntegerValue intA2 && b is FloatValue floatB2)
                return new IntegerValue(intA2.Value > floatB2.Value ? 1 : 0);
            if (a is FloatValue floatA2 && b is IntegerValue intB2)
                return new IntegerValue(floatA2.Value > intB2.Value ? 1 : 0);
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
            if (a is CharacterValue charA)
                return new IntegerValue(charA.Value == "\0" ? 1 : 0);
            
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
            // +((`a`b`c);(1 2 3)) -> same as above
            // +("abc") -> "abc" (atom/vector identity)
            // +(,"abcde") -> (,"a";,"b";,"c";,"d";,"e")  // 1-element vector of 5-char string -> 5-element vector of 1-char strings
            // +1 2 3 -> 1 2 3 (vector of atoms is identity)
            
            if (a is not VectorValue vec || vec.Elements.Count == 0)
                return a; // Atoms and empty vectors are identity
            
            // Check if all elements are atoms (not vectors) - if so, return as identity
            // Per K spec: "If all items of x are atoms then +x is identical to x"
            bool allAtoms = true;
            foreach (var elem in vec.Elements)
            {
                if (elem is VectorValue)
                {
                    allAtoms = false;
                    break;
                }
            }
            if (allAtoms)
                return a;
            
            // Check if all elements are vectors (or atoms that can be treated as 1-element vectors)
            var rows = new List<VectorValue>();
            int innerLength = -1;
            foreach (var elem in vec.Elements)
            {
                if (elem is VectorValue v)
                {
                    if (innerLength == -1)
                        innerLength = v.Elements.Count;
                    else if (v.Elements.Count != innerLength)
                        throw new Exception($"Flip length error: vector has {v.Elements.Count} items, expected {innerLength}");
                    rows.Add(v);
                }
                else
                {
                    // Atom element: treat as 1-element vector
                    if (innerLength == -1)
                        innerLength = 1;
                    else if (innerLength != 1)
                        throw new Exception($"Flip length error: atom treated as 1-element vector, expected {innerLength}");
                    rows.Add(new VectorValue(new List<K3Value> { elem }));
                }
            }
            
            if (innerLength <= 0)
                return a;
            
            int outerLength = rows.Count;
            
            // Transpose: create innerLength new rows, each with outerLength elements
            var result = new List<K3Value>();
            for (int col = 0; col < innerLength; col++)
            {
                var newRow = new List<K3Value>();
                for (int row = 0; row < outerLength; row++)
                {
                    newRow.Add(rows[row].Elements[col]);
                }
                result.Add(new VectorValue(newRow));
            }
            
            return new VectorValue(result);
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

                // Stable sort: compare values, and if equal, preserve original order by comparing indices
                indices.Sort((i, j) =>
                {
                    int cmp = CompareValues(vecA.Elements[i], vecA.Elements[j]);
                    return cmp != 0 ? cmp : i.CompareTo(j);
                });

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

                // Stable sort (descending): compare values, and if equal, preserve original order by comparing indices
                indices.Sort((i, j) =>
                {
                    int cmp = CompareValues(vecA.Elements[j], vecA.Elements[i]);
                    return cmp != 0 ? cmp : i.CompareTo(j);
                });

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
            
            // For single-element vectors, compare the inner values directly
            if (a is VectorValue vecA && b is VectorValue vecB &&
                vecA.Elements.Count == 1 && vecB.Elements.Count == 1)
            {
                return CompareValues(vecA.Elements[0], vecB.Elements[0]);
            }

            // For vectors and other types, use ToString comparison
            return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }

        private K3Value Shape(K3Value a)
        {
            if (a is VectorValue vecA)
            {
                var dimensions = ComputeShapeDimensions(vecA);
                return new VectorValue(dimensions.Select(d => (K3Value)new IntegerValue(d)).ToList(), -1);
            }

            // Scalar - return empty integer vector
            return new VectorValue(new List<K3Value>(), -1);
        }

        private List<int> ComputeShapeDimensions(VectorValue vec)
        {
            // Simple vector (no nested vectors) -> length is the only dimension
            bool hasNestedVectors = vec.Elements.Any(e => e is VectorValue);
            if (!hasNestedVectors)
            {
                return new List<int> { vec.Elements.Count };
            }

            // Jagged: not every element is a vector -> only first dimension is uniform
            if (!vec.Elements.All(e => e is VectorValue))
            {
                return new List<int> { vec.Elements.Count };
            }

            // All elements are vectors - get shape of first element
            var firstElement = (VectorValue)vec.Elements[0];
            var subDimensions = ComputeShapeDimensions(firstElement);

            // Check all other elements have the same shape
            for (int i = 1; i < vec.Elements.Count; i++)
            {
                var element = (VectorValue)vec.Elements[i];
                var elementSubDimensions = ComputeShapeDimensions(element);
                if (!DimensionsEqual(subDimensions, elementSubDimensions))
                {
                    return new List<int> { vec.Elements.Count };
                }
            }

            // All same shape - prepend our dimension
            var result = new List<int> { vec.Elements.Count };
            result.AddRange(subDimensions);
            return result;
        }

        private bool DimensionsEqual(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
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
