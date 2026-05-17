// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System.Collections.Generic;

namespace K3CSharp
{
    /// <summary>
    /// Explicit IEvaluatorContext implementation — bridges private Evaluator
    /// members to the public interface that extracted handler classes consume.
    /// This file will shrink as partials are converted to standalone classes.
    /// </summary>
    public partial class Evaluator
    {
        // ── IEvaluatorContext explicit implementations ────────────────────

        K3Value? IEvaluatorContext.DispatchMonadic(string verbName, K3Value operand)
            => DispatchMonadic(verbName, operand);

        K3Value? IEvaluatorContext.DispatchDyadic(string verbName, K3Value left, K3Value right)
            => DispatchDyadic(verbName, left, right);

        K3Value IEvaluatorContext.EvaluateDyadicOp(string opName, K3Value left, K3Value right)
            => EvaluateDyadicOperatorWithRegistry(opName, left, right);

        K3Value IEvaluatorContext.Evaluate(ASTNode node) => Evaluate(node);

        K3Value IEvaluatorContext.CallDirectFunction(ASTNode functionNode, List<K3Value> arguments)
            => CallDirectFunction(functionNode, arguments);

        K3Value IEvaluatorContext.ExecuteStringExpression(string expression)
            => ExecuteStringExpression(expression);

        K3Value IEvaluatorContext.GetVariable(string name) => GetVariable(name);
        K3Value IEvaluatorContext.SetVariable(string name, K3Value value) => SetVariable(name, value);
        K3Value IEvaluatorContext.SetGlobalVariable(string name, K3Value value) => SetGlobalVariable(name, value);

        KTree IEvaluatorContext.KTree => kTree;
        string IEvaluatorContext.ScriptName => ScriptName;
        List<string> IEvaluatorContext.CommandLineArgs => CommandLineArgs;
        FunctionValue? IEvaluatorContext.CurrentFunctionValue => currentFunctionValue;
        Evaluator? IEvaluatorContext.ParentEvaluator => parentEvaluator;

        K3Value IEvaluatorContext.CallVariableFunction(string name, List<K3Value> arguments) => CallVariableFunction(name, arguments);
        K3Value IEvaluatorContext.ExecuteFunction(FunctionValue function, List<K3Value> arguments) => ExecuteFunction(function, arguments);

        K3Value IEvaluatorContext.ApplyOver(K3Value verb, K3Value init, K3Value data) => Over(verb, init, data);

        K3Value IEvaluatorContext.EvaluateStringExpression(K3Value value) => EvaluateStringExpression(value);
        bool IEvaluatorContext.IsTypeConversionSpecifier(K3Value left) => IsTypeConversionSpecifier(left);
        bool IEvaluatorContext.IsCharacterVectorOrList(K3Value value) => IsCharacterVectorOrList(value);
        K3Value IEvaluatorContext.PerformTypeConversion(K3Value left, K3Value right) => PerformTypeConversion(left, right);

        string IEvaluatorContext.GetIpcHost() => GetIpcHost();
        int IEvaluatorContext.GetListeningPort() => GetListeningPort();
        int IEvaluatorContext.GetCurrentIncomingHandle() => GetCurrentIncomingHandle();
        System.Net.IPAddress? IEvaluatorContext.GetCurrentIncomingAddress() => GetCurrentIncomingAddress();

        int IEvaluatorContext.DetermineVectorType(List<K3Value> elements) => DetermineVectorType(elements);
        K3Value IEvaluatorContext.Match(K3Value left, K3Value right) => Match(left, right);
        K3Value? IEvaluatorContext.GetVariableValue(string name) => GetVariableValue(name);
        int IEvaluatorContext.CompareValues(K3Value a, K3Value b) => CompareValues(a, b);
        void IEvaluatorContext.RequestExit(int exitCode) => RequestExit(exitCode);
        void IEvaluatorContext.SetValue(string path, K3Value value) => kTree.SetValue(path, value);
    }
}
