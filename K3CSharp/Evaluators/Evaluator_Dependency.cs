// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace K3CSharp
{
    public partial class Evaluator
    {
        private K3Value ResolveDependency(string variablePath, K3Value currentValue)
        {
            var attrDict = kTree.GetAttribute(variablePath);
            if (attrDict == null)
                return currentValue;

            if (!attrDict.Entries.TryGetValue(new SymbolValue("d"), out var depEntry))
                return currentValue;

            if (!(depEntry.Value is VectorValue depExprVec) || depExprVec.Elements.Count == 0)
                return currentValue;

            if (!depExprVec.Elements.All(e => e is CharacterValue))
                return currentValue;

            var depExpr = string.Join("", depExprVec.Elements.Select(e => ((CharacterValue)e).Value));

            if (!_evaluatingDependencies.Add(variablePath))
                return currentValue;

            try
            {
                var referencedVars = ExtractVariableNames(depExpr);
                var dirPath = GetDirectoryPath(variablePath);

                long maxRefVersion = 0;
                foreach (var varName in referencedVars)
                {
                    string refPath;
                    if (varName.StartsWith("."))
                        refPath = varName;
                    else
                        refPath = string.IsNullOrEmpty(dirPath) || dirPath == "." ? varName : dirPath + "." + varName;

                    if (_variableChangeVersion.TryGetValue(refPath, out var version))
                    {
                        if (version > maxRefVersion)
                            maxRefVersion = version;
                    }
                }

                long lastEvaluated = _dependencyLastVersion.GetValueOrDefault(variablePath, 0);

                if (maxRefVersion <= lastEvaluated && lastEvaluated > 0)
                    return currentValue;

                var savedBranch = kTree.CurrentBranch;
                kTree.CurrentBranch = new SymbolValue(dirPath);
                try
                {
                    var newValue = ExecuteStringExpression(depExpr);
                    kTree.SetValue(variablePath, newValue);
                    _dependencyLastVersion[variablePath] = _globalChangeCounter;
                    return newValue;
                }
                finally
                {
                    kTree.CurrentBranch = savedBranch;
                }
            }
            finally
            {
                _evaluatingDependencies.Remove(variablePath);
            }
        }

        private void FireTriggerIfNeeded(string variablePath)
        {
            if (_executingTriggers.Contains(variablePath) || _evaluatingDependencies.Contains(variablePath))
                return;

            var attrDict = kTree.GetAttribute(variablePath);
            if (attrDict == null)
                return;

            if (!attrDict.Entries.TryGetValue(new SymbolValue("t"), out var trigEntry))
                return;

            if (!(trigEntry.Value is VectorValue trigExprVec) || trigExprVec.Elements.Count == 0)
                return;

            if (!trigExprVec.Elements.All(e => e is CharacterValue))
                return;

            var trigExpr = string.Join("", trigExprVec.Elements.Select(e => ((CharacterValue)e).Value));

            if (!_executingTriggers.Add(variablePath))
                return;

            try
            {
                var dirPath = GetDirectoryPath(variablePath);
                var savedBranch = kTree.CurrentBranch;
                kTree.CurrentBranch = new SymbolValue(dirPath);
                try
                {
                    ExecuteStringExpression(trigExpr);
                }
                finally
                {
                    kTree.CurrentBranch = savedBranch;
                }
            }
            finally
            {
                _executingTriggers.Remove(variablePath);
            }
        }

        private static string GetDirectoryPath(string variablePath)
        {
            if (string.IsNullOrEmpty(variablePath) || variablePath == ".")
                return ".";

            int lastDot = variablePath.LastIndexOf('.');
            if (lastDot <= 0)
                return ".";

            return variablePath.Substring(0, lastDot);
        }

        private static HashSet<string> ExtractVariableNames(string expression)
        {
            var names = new HashSet<string>();
            var matches = Regex.Matches(expression, @"[a-zA-Z_][a-zA-Z0-9_.]*");
            foreach (Match match in matches)
            {
                var name = match.Value;
                if (name == "do" || name == "if" || name == "while" ||
                    name == "_in" || name == "_ci" || name == "_val" || name == "_abs" ||
                    name == "_sv" || name == "_vs" || name == "_dv" || name == "_di" ||
                    name == "_bd" || name == "_db" || name == "_bin" || name == "_ts" ||
                    name == "_t" || name == "_d" || name == "_n" || name == "_i" || name == "_f" ||
                    name == "_T" || name == "_D")
                    continue;
                names.Add(name);
            }
            return names;
        }
    }
}
