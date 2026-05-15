// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
using System;
using System.Collections.Generic;

namespace K3CSharp
{
    /// <summary>
    /// Parser pipeline that coordinates between multiple parser modules
    /// This provides a clean way to orchestrate parsing without replacing existing functionality
    /// </summary>
    public class ParserPipeline
    {
        private readonly List<IParserModule> modules;
        private readonly ParseContext context;

        public ParserPipeline(List<Token> tokens, string sourceText)
        {
            modules = new List<IParserModule>();
            context = new ParseContext(tokens, sourceText);
            InitializeModules();
        }

        private void InitializeModules()
        {
            modules.Add(new ExpressionParser());
            modules.Add(new PrimaryParser());
            modules.Add(new AdverbParser());
            modules.Add(new BracketParser());
            modules.Add(new FunctionParser());
            modules.Add(new AssignmentParser());
        }

        /// <summary>
        /// Try to parse using the modular pipeline
        /// Returns null if no module can handle the current token
        /// </summary>
        public ASTNode? TryParseWithModules()
        {
            if (context.IsAtEnd())
            {
                return null;
            }

            var currentToken = context.CurrentToken();
            
            // Try each module in order to see which can handle the current token
            foreach (var module in modules)
            {
                if (module.CanHandle(currentToken.Type))
                {
                    try
                    {
                        var result = module.Parse(context);
                        return result;
                    }
                    catch
                    {
                        // If module fails, continue to next module
                        continue;
                    }
                }
            }
            
            // No module could handle this token
            return null;
        }

        /// <summary>
        /// Get current position for synchronization with main parser
        /// </summary>
        public int GetCurrentPosition() => context.Current;

        /// <summary>
        /// Set current position for synchronization with main parser
        /// </summary>
        public void SetCurrentPosition(int position) => context.Current = position;

        /// <summary>
        /// Check if pipeline can handle the current token
        /// </summary>
        public bool CanHandleCurrentToken()
        {
            if (context.IsAtEnd())
            {
                return false;
            }

            var currentToken = context.CurrentToken();
            return modules.Any(module => module.CanHandle(currentToken.Type));
        }

        /// <summary>
        /// Test specific module functionality
        /// </summary>
        public ASTNode? TestModule<T>() where T : IParserModule, new()
        {
            var module = new T();
            var currentToken = context.CurrentToken();
            
            if (module.CanHandle(currentToken.Type))
            {
                try
                {
                    return module.Parse(context);
                }
                catch
                {
                    return null;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Get module statistics for debugging
        /// </summary>
        public Dictionary<string, bool> GetModuleCapabilities()
        {
            var capabilities = new Dictionary<string, bool>();
            
            if (context.IsAtEnd())
            {
                return capabilities;
            }

            var currentToken = context.CurrentToken();
            
            foreach (var module in modules)
            {
                var moduleName = module.GetType().Name;
                capabilities[moduleName] = module.CanHandle(currentToken.Type);
            }
            
            return capabilities;
        }
    }
}