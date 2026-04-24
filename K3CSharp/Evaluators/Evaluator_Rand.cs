using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private static int _randomSeed = -314159; // Default seed value
        
        private K3Value Draw(K3Value left, K3Value right)
        {
            // _draw function - dyadic implementation
            // It has 3 different cases depending on input types: Select, Deal and Probability
            // The left argument must be either a nonnegative integer or a vector of nonnegative integers
            // The right argument must be an integer
            
            // Handle scalar left operand
            if (left is IntegerValue leftInt && right is IntegerValue rightInt)
            {
                if (rightInt.Value > 0)
                {
                    // Select case: right argument is positive
                    return DrawSelect(leftInt.Value, rightInt.Value);
                }
                else if (rightInt.Value == 0)
                {
                    // Probability case: right argument is 0
                    return DrawProbability(leftInt, rightInt);
                }
                else
                {
                    // Deal case: right argument is negative
                    return DrawDeal(leftInt.Value, -rightInt.Value);
                }
            }
            // Handle vector left operand
            else if (left is VectorValue leftVec && right is IntegerValue rightVal)
            {
                if (leftVec.Elements.All(e => e is IntegerValue))
                {
                    var dims = leftVec.Elements.Select(e => ((IntegerValue)e).Value).ToList();
                    
                    if (rightVal.Value < 0)
                    {
                        // Deal case with vector left: creates a matrix with ALL unique values
                        // e.g., 2 3 _draw -10 creates 2 rows of 3 unique values from 0-9
                        // ALL 6 values must be unique across the entire matrix
                        var totalElements = dims.Aggregate(1, (acc, val) => acc * val);
                        var range = -rightVal.Value;
                        
                        if (totalElements > range)
                        {
                            throw new Exception($"_draw Deal case: product of dimensions ({totalElements}) must be <= range ({range}) for unique values");
                        }
                        
                        // Generate all unique values at once
                        var allUniqueValues = DrawDeal(totalElements, range);
                        
                        // Reshape into matrix/tensor
                        return ReshapeDrawResult(allUniqueValues, dims);
                    }
                    else
                    {
                        // Select/Probability with vector left: create matrix
                        // e.g., 2 3 _draw 4 creates 2 rows of 3 random selections from 0-3
                        // e.g., 2 3 _draw 0 creates 2 rows of 3 random floats between 0-1
                        var totalElements = dims.Aggregate(1, (acc, val) => acc * val);
                        
                        K3Value allValues;
                        if (rightVal.Value > 0)
                            allValues = DrawSelect(totalElements, rightVal.Value);
                        else
                            allValues = DrawProbability(new IntegerValue(totalElements), rightVal);
                        
                        // Reshape into matrix/tensor
                        return ReshapeDrawResult(allValues, dims);
                    }
                }
                throw new Exception("_draw requires integer or vector of integers");
            }
            else
            {
                throw new Exception("_draw requires integer or vector of integers");
            }
        }
        
        private K3Value DrawSelect(int count, int right)
        {
            // Select case: generate random integers between 0 and right (exclusive)
            if (count < 0)
            {
                throw new Exception("_draw requires nonnegative integer");
            }
            
            if (count == 0)
            {
                return new VectorValue(new List<K3Value>());
            }
            
            var random = new Random(_randomSeed);
            var results = new List<K3Value>();
            
            for (int i = 0; i < count; i++)
            {
                var randomValue = random.Next(0, right);
                results.Add(new IntegerValue(randomValue));
                
                // Update seed for next random number generation
                _randomSeed = Math.Abs(random.Next()) % 1000000;
            }
            
            return new VectorValue(results);
        }
        
        private K3Value DrawDeal(int count, int range)
        {
            // Deal case: right argument is a negative integer
            if (count < 0)
            {
                throw new Exception("_draw requires nonnegative integer");
            }
            
            if (count > range)
            {
                throw new Exception("_draw Deal case: count must be <= range for unique values");
            }
            
            var random = new Random(_randomSeed);
            var results = new List<K3Value>();
            
            // Generate unique random integers between 0 and range (exclusive)
            var possibleValues = new HashSet<int>();
            while (possibleValues.Count < count)
            {
                var randomValue = random.Next(0, range);
                if (!possibleValues.Contains(randomValue))
                {
                    possibleValues.Add(randomValue);
                }
            }
            
            // Use of generated unique values
            var possibleValuesList = possibleValues.ToList();
            for (int i = 0; i < count; i++)
            {
                if (i < possibleValuesList.Count)
                {
                    results.Add(new IntegerValue(possibleValuesList[i]));
                }
                else
                {
                    // Fallback: generate any random value
                    results.Add(new IntegerValue(random.Next(0, range)));
                }
                
                // Update seed for next random number generation
                _randomSeed = Math.Abs(random.Next()) % 1000000;
            }
            
            return new VectorValue(results);
        }
        
        private K3Value ReshapeDrawResult(K3Value flatValues, List<int> dims)
        {
            // Reshape flat vector into matrix/tensor with given dimensions
            if (flatValues is not VectorValue flatVec)
            {
                throw new Exception("_draw reshape requires vector input");
            }
            
            if (dims.Count == 1)
            {
                // Single dimension - just return the vector
                return flatValues;
            }
            
            // Build nested structure from dimensions
            // e.g., dims = [2, 3] means 2 rows of 3 elements each
            return BuildNestedStructure(flatVec.Elements, dims, 0);
        }
        
        private K3Value BuildNestedStructure(List<K3Value> elements, List<int> dims, int dimIndex)
        {
            if (dimIndex == dims.Count - 1)
            {
                // Last dimension - return a vector of elements
                return new VectorValue(elements);
            }
            
            // Build structure for current dimension
            var currentDim = dims[dimIndex];
            var result = new List<K3Value>();
            var elementsPerSubStructure = dims.Skip(dimIndex + 1).Aggregate(1, (acc, val) => acc * val);
            
            for (int i = 0; i < currentDim; i++)
            {
                var startIndex = i * elementsPerSubStructure;
                var subElements = elements.Skip(startIndex).Take(elementsPerSubStructure).ToList();
                result.Add(BuildNestedStructure(subElements, dims, dimIndex + 1));
            }
            
            return new VectorValue(result);
        }
        
        private K3Value DrawProbability(K3Value left, K3Value right)
        {
            // Probability case: right argument is 0
            if (left is IntegerValue leftVal && right is IntegerValue rightVal && rightVal.Value == 0)
            {
                // Generate random floating point numbers between 0 and 1
                var random = new Random(_randomSeed);
                var results = new List<K3Value>();
                
                for (int i = 0; i < leftVal.Value; i++)
                {
                    var randomValue = random.NextDouble();
                    results.Add(new FloatValue(randomValue));
                    
                    // Update seed for next random number generation
                    _randomSeed = Math.Abs(random.Next()) % 1000000;
                }
                
                return new VectorValue(results);
            }
            
            throw new Exception("_draw Probability case requires left integer and right argument 0");
        }
        
        // Method to get/set random seed (for \r command)
        public static int RandomSeed
        {
            get { return _randomSeed; }
            set { _randomSeed = value; }
        }
    }
}
