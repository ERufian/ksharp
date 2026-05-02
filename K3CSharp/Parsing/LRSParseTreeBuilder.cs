using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp.Parsing
{
    /// <summary>
    /// Parse tree construction for LRS parser
    /// Builds comprehensive AST nodes for _parse verb functionality
    /// Supports tree inspection and serialization/deserialization
    /// </summary>
    public class LRSParseTreeBuilder
    {
        private readonly List<Token> tokens;
        
        public LRSParseTreeBuilder(List<Token> tokens)
        {
            this.tokens = tokens;
        }

        /// <summary>
        /// Build detailed parse tree from tokens
        /// </summary>
        /// <param name="tokens">Tokens to build tree from</param>
        /// <returns>Root AST node of detailed parse tree</returns>
        public ASTNode? BuildDetailedParseTree(List<Token> tokens)
        {
            if (tokens.Count == 0)
                return null;
                
            if (tokens.Count == 1)
                return CreateDetailedNodeFromToken(tokens[0]);
            
            // Use LRS parser with parse tree mode enabled
            var lrsParser = new LRSParser(tokens, buildParseTree: true);
            var position = 0;
            return lrsParser.ParseExpression(ref position);
        }

        /// <summary>
        /// Create detailed AST node from token with full metadata
        /// </summary>
        private ASTNode CreateDetailedNodeFromToken(Token token)
        {
            var node = LRSAtomicParser.ParseAtomicToken(token);
            
            // Enhance node with position information for debugging
            node.StartPosition = token.Position;
            node.EndPosition = token.Position + token.Lexeme.Length;
            
            return node;
        }

        /// <summary>
        /// Build parse tree with operator precedence information
        /// </summary>
        public ASTNode? BuildPrecedenceAwareTree(List<Token> tokens)
        {
            if (tokens.Count == 0)
                return null;
                
            if (tokens.Count == 1)
                return CreatePrecedenceNode(tokens[0]);
            
            // Find the rightmost dyadic operator (LRS strategy)
            var rightmostOpIndex = FindRightmostDyadicOperator(tokens);
            if (rightmostOpIndex == -1)
            {
                // No dyadic operators - check for monadic operators
                return BuildMonadicTree(tokens);
            }
            
            // Split at rightmost operator
            var leftTokens = tokens.GetRange(0, rightmostOpIndex);
            var rightTokens = tokens.GetRange(rightmostOpIndex + 1, tokens.Count - rightmostOpIndex - 1);
            var opToken = tokens[rightmostOpIndex];
            
            // Recursively build left and right subtrees
            var leftNode = BuildPrecedenceAwareTree(leftTokens);
            var rightNode = BuildPrecedenceAwareTree(rightTokens);
            
            // Create dyadic operation node with precedence info
            return CreateDyadicNodeWithPrecedence(opToken, leftNode, rightNode);
        }

        /// <summary>
        /// Find rightmost dyadic operator in token list
        /// </summary>
        private int FindRightmostDyadicOperator(List<Token> tokens)
        {
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (OperatorDetector.IsDyadicOperator(tokens[i].Type))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Build monadic operation tree
        /// </summary>
        private ASTNode? BuildMonadicTree(List<Token> tokens)
        {
            if (tokens.Count == 0)
                return null;
                
            var firstToken = tokens[0];
            
            // Check if first token is a monadic operator
            if (OperatorDetector.SupportsMonadic(firstToken.Type))
            {
                var operandTokens = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1) : new List<Token>();
                var operand = BuildPrecedenceAwareTree(operandTokens);
                
                return CreateMonadicNodeWithPrecedence(firstToken, operand);
            }
            
            // Default to atomic parsing
            return CreatePrecedenceNode(tokens[0]);
        }

        /// <summary>
        /// Create precedence-aware dyadic node
        /// </summary>
        private ASTNode CreateDyadicNodeWithPrecedence(Token opToken, ASTNode? left, ASTNode? right)
        {
            var node = ASTNode.MakeDyadicOp(opToken.Type, 
                left ?? ASTNode.MakeLiteral(new NullValue()), 
                right ?? ASTNode.MakeLiteral(new NullValue()));
            
            // Keep the operator symbol clean - evaluator expects plain symbols like ","
            // Precedence metadata should not modify the node value
            
            return node;
        }

        /// <summary>
        /// Create precedence-aware monadic node
        /// </summary>
        private ASTNode CreateMonadicNodeWithPrecedence(Token opToken, ASTNode? operand)
        {
            var node = ASTNode.MakeDyadicOp(opToken.Type, 
                operand ?? ASTNode.MakeLiteral(new NullValue()), 
                ASTNode.MakeLiteral(new NullValue()));
            
            // Add precedence metadata
            var precedenceInfo = new Dictionary<string, object>
            {
                ["precedence"] = "monadic",
                ["associativity"] = "right",
                ["operator"] = VerbRegistry.GetDyadicOperatorSymbol(opToken.Type)
            };
            
            // Store precedence info in node value
            if (node.Value is SymbolValue symbol)
            {
                var enhancedValue = $"{symbol.Value}[precedence:monadic,associativity:right]";
                node.Value = new SymbolValue(enhancedValue);
            }
            
            return node;
        }

        /// <summary>
        /// Create precedence-aware atomic node
        /// </summary>
        private ASTNode CreatePrecedenceNode(Token token)
        {
            var node = LRSAtomicParser.ParseAtomicToken(token);
            
            // Add atomic precedence info
            if (node.Value is SymbolValue symbol)
            {
                var enhancedValue = $"{symbol.Value}[precedence:atomic]";
                node.Value = new SymbolValue(enhancedValue);
            }
            
            return node;
        }

        /// <summary>
        /// Serialize parse tree to string representation
        /// </summary>
        public string SerializeTree(ASTNode? node, int indentLevel = 0)
        {
            if (node == null)
                return "null";
                
            var indent = new string(' ', indentLevel * 2);
            var result = $"{indent}{GetNodeDescription(node)}\n";
            
            foreach (var child in node.Children)
            {
                result += SerializeTree(child, indentLevel + 1);
            }
            
            return result;
        }

        /// <summary>
        /// Get description of node for serialization
        /// </summary>
        private string GetNodeDescription(ASTNode node)
        {
            return node.Type switch
            {
                ASTNodeType.Literal => $"Literal({node.Value})",
                ASTNodeType.Variable => $"Variable({node.Value})",
                ASTNodeType.DyadicOp => $"DyadicOp({node.Value})",
                ASTNodeType.Vector => $"Vector({node.Children.Count} items)",
                ASTNodeType.Function => $"Function({node.Value})",
                ASTNodeType.FunctionCall => $"FunctionCall({(node.Children.Count > 0 ? node.Children[0].Value : node.Value)})",
                ASTNodeType.ProjectedFunction => $"ProjectedFunction({node.Value})",
                _ => $"{node.Type}({node.Value})"
            };
        }

        /// <summary>
        /// Validate parse tree structure
        /// </summary>
        public bool ValidateTree(ASTNode? node)
        {
            if (node == null)
                return true;
                
            // Check for circular references
            var visited = new HashSet<ASTNode>();
            return ValidateNodeRecursive(node, visited);
        }

        /// <summary>
        /// Recursively validate node structure
        /// </summary>
        private bool ValidateNodeRecursive(ASTNode node, HashSet<ASTNode> visited)
        {
            if (visited.Contains(node))
                return false; // Circular reference
                
            visited.Add(node);
            
            foreach (var child in node.Children)
            {
                if (!ValidateNodeRecursive(child, visited))
                    return false;
            }
            
            visited.Remove(node);
            return true;
        }

        /// <summary>
        /// Get tree statistics for debugging
        /// </summary>
        public Dictionary<string, object> GetTreeStatistics(ASTNode? root)
        {
            var stats = new Dictionary<string, object>
            {
                ["nodeCount"] = 0,
                ["maxDepth"] = 0,
                ["dyadicOpCount"] = 0,
                ["monadicOpCount"] = 0,
                ["vectorCount"] = 0
            };
            
            if (root == null)
                return stats;
                
            CalculateTreeStats(root, 0, stats);
            return stats;
        }

        /// <summary>
        /// Calculate tree statistics recursively
        /// </summary>
        private void CalculateTreeStats(ASTNode node, int depth, Dictionary<string, object> stats)
        {
            stats["nodeCount"] = (int)stats["nodeCount"] + 1;
            stats["maxDepth"] = Math.Max((int)stats["maxDepth"], depth);
            
            switch (node.Type)
            {
                case ASTNodeType.DyadicOp:
                    stats["dyadicOpCount"] = (int)stats["dyadicOpCount"] + 1;
                    break;
                case ASTNodeType.Vector:
                    stats["vectorCount"] = (int)stats["vectorCount"] + 1;
                    break;
            }
            
            foreach (var child in node.Children)
            {
                CalculateTreeStats(child, depth + 1, stats);
            }
        }
    }
}
