// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System.Collections.Generic;

namespace K3CSharp
{
    // Custom comparer for K3Value to use in HashSet operations
    public class K3ValueComparer : IEqualityComparer<K3Value>
    {
        public bool Equals(K3Value? x, K3Value? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            
            // Use Match function for comparison (same as _in uses)
            var evaluator = new Evaluator();
            var matchResult = evaluator.Match(x, y);
            
            if (matchResult is IntegerValue intVal)
            {
                return intVal.Value == 1;
            }
            
            return false;
        }

        public int GetHashCode(K3Value obj)
        {
            if (obj is null) return 0;
            
            // Generate hash code based on type and value
            return obj.Type switch
            {
                ValueType.Integer => obj is IntegerValue iv ? iv.Value.GetHashCode() : 0,
                ValueType.Long => obj is LongValue lv ? lv.Value.GetHashCode() : 0,
                ValueType.Float => obj is FloatValue fv ? fv.Value.GetHashCode() : 0,
                ValueType.Character => obj is CharacterValue cv ? cv.Value.GetHashCode() : 0,
                ValueType.Symbol => obj is SymbolValue sv ? sv.Value.GetHashCode() : 0,
                ValueType.Vector => obj is VectorValue vv ? vv.Elements.Count.GetHashCode() : 0,
                _ => obj.ToString().GetHashCode()
            };
        }
    }
}
