using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    /// <summary>
    /// Custom equality comparer for K3Value that uses the Match function for proper value comparison.
    /// This ensures that values are compared by their content and type, not by reference or ToString().
    /// </summary>
    public class K3ValueEqualityComparer : IEqualityComparer<K3Value>
    {
        private readonly Evaluator _evaluator;

        public K3ValueEqualityComparer(Evaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public bool Equals(K3Value? x, K3Value? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            // Use the Match function for proper K3 value comparison
            var result = _evaluator.Match(x, y);
            return result is IntegerValue intVal && intVal.Value == 1;
        }

        public int GetHashCode(K3Value obj)
        {
            if (obj is null) return 0;

            // Generate hash code based on value type and content
            return obj.Type switch
            {
                ValueType.Integer => obj is IntegerValue iv ? iv.Value.GetHashCode() : 0,
                ValueType.Long => obj is LongValue lv ? lv.Value.GetHashCode() : 0,
                ValueType.Float => obj is FloatValue fv ? fv.Value.GetHashCode() : 0,
                ValueType.Character => obj is CharacterValue cv ? cv.Value.GetHashCode() : 0,
                ValueType.Symbol => obj is SymbolValue sv ? sv.Value.GetHashCode() : 0,
                ValueType.Null => 0,
                _ => obj.ToString()?.GetHashCode() ?? 0
            };
        }
    }

    public partial class Evaluator
    {
        private K3Value Unique(K3Value a)
        {
            if (a is VectorValue vecA)
            {
                var uniqueElements = new List<K3Value>();
                var comparer = new K3ValueEqualityComparer(this);
                var seen = new HashSet<K3Value>(comparer);

                foreach (var element in vecA.Elements)
                {
                    if (seen.Add(element))
                    {
                        uniqueElements.Add(element);
                    }
                }

                return new VectorValue(uniqueElements);
            }

            return a; // For scalars, return value itself
        }

        private K3Value Group(K3Value a)
        {
            if (a is VectorValue vecA)
            {
                var comparer = new K3ValueEqualityComparer(this);
                var groups = new Dictionary<K3Value, List<int>>(comparer);

                // First pass: collect indices for each unique value
                for (int i = 0; i < vecA.Elements.Count; i++)
                {
                    var element = vecA.Elements[i];

                    if (!groups.ContainsKey(element))
                    {
                        groups[element] = new List<int>();
                    }
                    groups[element].Add(i);
                }

                // Second pass: create group vectors in order of first appearance
                var result = new List<K3Value>();
                var seenKeys = new HashSet<K3Value>(comparer);

                for (int i = 0; i < vecA.Elements.Count; i++)
                {
                    var element = vecA.Elements[i];

                    if (seenKeys.Add(element)) // First time seeing this value
                    {
                        // Create vector of indices for this group
                        var indices = groups[element].Select(idx => (K3Value)new IntegerValue(idx)).ToList();
                        result.Add(new VectorValue(indices));
                    }
                }

                return new VectorValue(result);
            }

            return a; // For scalars, return value itself
        }
    }
}
