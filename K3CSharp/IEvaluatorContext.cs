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
    /// Provides the minimal surface that extracted verb-handler classes need
    /// from the Evaluator.  This replaces the partial-class coupling and
    /// enables each handler to be a standalone, testable class.
    /// </summary>
    public interface IEvaluatorContext
    {
        // ── Verb dispatch (covers all cross-handler verb calls) ──────────
        K3Value? DispatchMonadic(string verbName, K3Value operand);
        K3Value? DispatchDyadic(string verbName, K3Value left, K3Value right);
        K3Value EvaluateDyadicOp(string opName, K3Value left, K3Value right);

        // ── Core evaluation ──────────────────────────────────────────────
        K3Value Evaluate(ASTNode node);
        K3Value CallDirectFunction(ASTNode functionNode, List<K3Value> arguments);
        K3Value ExecuteStringExpression(string expression);
        K3Value CallVariableFunction(string name, List<K3Value> arguments);
        K3Value ExecuteFunction(FunctionValue function, List<K3Value> arguments);

        // ── State access ─────────────────────────────────────────────────
        K3Value GetVariable(string name);
        K3Value? GetVariableValuePublic(string name);
        K3Value SetVariable(string name, K3Value value);
        K3Value SetGlobalVariable(string name, K3Value value);
        KTree KTree { get; }
        string ScriptName { get; }
        List<string> CommandLineArgs { get; }
        FunctionValue? CurrentFunctionValue { get; }
        Evaluator? ParentEvaluator { get; }

        // ── Adverb operations ────────────────────────────────────────────
        K3Value ApplyOver(K3Value verb, K3Value init, K3Value data);

        // ── Format helpers ───────────────────────────────────────────────
        K3Value EvaluateStringExpression(K3Value value);
        bool IsTypeConversionSpecifier(K3Value left);
        bool IsCharacterVectorOrList(K3Value value);
        K3Value PerformTypeConversion(K3Value left, K3Value right);

        // ── IPC helpers ──────────────────────────────────────────────────
        string GetIpcHost();
        int GetListeningPort();
        int GetCurrentIncomingHandle();
        System.Net.IPAddress? GetCurrentIncomingAddress();

        // ── Helpers ──────────────────────────────────────────────────────
        int DetermineVectorType(List<K3Value> elements);
        K3Value Match(K3Value left, K3Value right);
        K3Value? GetVariableValue(string name);
        int CompareValues(K3Value a, K3Value b);
        void RequestExit(int exitCode);
        void SetValue(string path, K3Value value);
    }
}
