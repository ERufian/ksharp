using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    public enum ASTNodeType
    {
        Literal,
        Vector,
        DyadicOp,
        MonadicOp,               // Monadic operations (for disambiguating colon)
        Assignment,
        GlobalAssignment,
        Variable,
        Function,
        FunctionCall,
        Block,
        ExpressionList,            // Semicolon-separated expressions in parentheses (return all values)
        StatementBlock,            // Semicolon-separated statements in function body (return last value only)
        FormSpecifier,
        ProjectedFunction,
        ApplyAndAssign,
        ConditionalStatement,
        TriadicOp,               // 3-argument operations
        TetradicOp,              // 4-argument operations  
        VariadicOp,              // Variadic operations
        Adnoun,                  // Noun-bound adverb forms (scatter, transitive closure, state transition)
        NotImplemented           // For "not yet implemented" operations
    }

    public class ASTNode
    {
        public ASTNodeType Type { get; }
        public K3Value? Value { get; set; }
        public List<ASTNode> Children { get; }
        public List<string> Parameters { get; set; } = new List<string>();
        public int StartPosition { get; set; } = -1;
        public int EndPosition { get; set; } = -1;
        public bool IsTerminalAssignment { get; set; } = false;

        public ASTNode(ASTNodeType type, K3Value? value = null, List<ASTNode>? children = null)
        {
            Type = type;
            Value = value;
            Children = children ?? new List<ASTNode>();
        }

        public static ASTNode MakeLiteral(K3Value value)
        {
            return new ASTNode(ASTNodeType.Literal, value, null);
        }

        public static ASTNode MakeVector(List<ASTNode> elements)
        {
            return new ASTNode(ASTNodeType.Vector, null, elements);
        }

        public static ASTNode MakeDyadicOp(TokenType op, ASTNode left, ASTNode right)
        {
            var node = new ASTNode(ASTNodeType.DyadicOp);
            if (left != null) node.Children.Add(left);
            if (right != null) node.Children.Add(right);
            
            // Convert token type to traditional operator symbol
            string symbolValue = op switch
            {
                TokenType.PLUS => "+",
                TokenType.MINUS => "-",
                TokenType.MULTIPLY => "*",
                TokenType.DIVIDE => "%",
                TokenType.DIV => "_div",
                TokenType.DOT_PRODUCT => "_dot",

                TokenType.MUL => "_mul",
                TokenType.LSQ => "_lsq",
                TokenType.MIN => "&",
                TokenType.MAX => "|",
                TokenType.LESS => "<",
                TokenType.GREATER => ">",
                TokenType.EQUAL => "=",
                TokenType.IN => "_in",
                TokenType.BIN => "_bin",
                TokenType.BINL => "_binl",
                TokenType.LIN => "_lin",
                TokenType.DV => "_dv",
                TokenType.DVL => "_dvl",
                TokenType.DI => "_di",
                TokenType.VS => "_vs",
                TokenType.SV => "_sv",
                TokenType.SS => "_ss",
                TokenType.SM => "_sm",
                TokenType.CI => "_ci",
                TokenType.IC => "_ic",
                TokenType.DRAW => "_draw",
                TokenType.GETENV => "_getenv",
                TokenType.SETENV => "_setenv",
                TokenType.SIZE => "_size",
                TokenType.BD => "_bd",
                TokenType.DB => "_db",
                TokenType.LT => "_lt",
                TokenType.JD => "_jd",
                TokenType.DJ => "_dj",
                TokenType.GTIME => "_gtime",
                TokenType.LTIME => "_ltime",
                TokenType.MODULUS => "!",
                TokenType.JOIN => ",",
                TokenType.COLON => ":",
                TokenType.HASH => "#",
                TokenType.UNDERSCORE => "_",
                TokenType.QUESTION => "?",
                TokenType.MATCH => "~",
                TokenType.DOLLAR => "$",
                TokenType.APPLY => "@",
                TokenType.DOT_APPLY => ".",
                TokenType.TYPE => "TYPE",
                TokenType.STRING_REPRESENTATION => "STRING_REPRESENTATION",
                TokenType.IO_VERB_0 => "IO_VERB_0",
                TokenType.IO_VERB_1 => "IO_VERB_1",
                TokenType.IO_VERB_2 => "IO_VERB_2",
                TokenType.IO_VERB_3 => "IO_VERB_3",
                TokenType.IO_VERB_4 => "IO_VERB_4",
                TokenType.IO_VERB_5 => "IO_VERB_5",
                TokenType.IO_VERB_6 => "IO_VERB_6",
                TokenType.IO_VERB_7 => "IO_VERB_7",
                TokenType.IO_VERB_8 => "IO_VERB_8",
                TokenType.IO_VERB_9 => "IO_VERB_9",
                TokenType.AND => "_and",
                TokenType.OR => "_or",
                TokenType.XOR => "_xor",
                TokenType.ROT => "_rot",
                TokenType.SHIFT => "_shift",
                TokenType.NOT => "_not",
                TokenType.CEIL => "_ceil",
                TokenType.DO => "do",
                TokenType.WHILE => "while",
                TokenType.IF_FUNC => "if",
                _ => op.ToString()
            };
            
            node.Value = new SymbolValue(symbolValue);
            return node;
        }

        public static ASTNode MakeAssignment(string variableName, ASTNode value)
        {
            var node = new ASTNode(ASTNodeType.Assignment);
            node.Value = new SymbolValue(variableName);
            if (value != null) node.Children.Add(value);
            return node;
        }

        public static ASTNode MakeApplyAndAssign(string variableName, TokenType op, ASTNode rightArgument)
        {
            var node = new ASTNode(ASTNodeType.ApplyAndAssign);
            node.Value = new SymbolValue(variableName);
            node.Children.Add(ASTNode.MakeLiteral(new SymbolValue(op.ToString())));
            node.Children.Add(rightArgument);
            return node;
        }

        public static ASTNode MakeGlobalAssignment(string variableName, ASTNode value)
        {
            var node = new ASTNode(ASTNodeType.GlobalAssignment);
            node.Value = new SymbolValue(variableName);
            if (value != null) node.Children.Add(value);
            return node;
        }

        public static ASTNode MakeVariable(string variableName)
        {
            return new ASTNode(ASTNodeType.Variable, new SymbolValue(variableName));
        }

        public static ASTNode MakeFunction(List<string> parameters, ASTNode body)
        {
            var node = new ASTNode(ASTNodeType.Function);
            node.Parameters = parameters;
            if (body != null) node.Children.Add(body);
            return node;
        }

        public static ASTNode MakeFunctionCall(ASTNode function, List<ASTNode> arguments)
        {
            var node = new ASTNode(ASTNodeType.FunctionCall);
            if (function != null) node.Children.Add(function);
            if (arguments != null) node.Children.AddRange(arguments);
            return node;
        }
    }
}
