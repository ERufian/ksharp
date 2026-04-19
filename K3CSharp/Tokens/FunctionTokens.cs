using System;

namespace K3CSharp
{
    /// <summary>
    /// Mathematical function tokens (_sin, _cos, _log, etc.)
    /// </summary>
    public class MathFunctionToken : FunctionToken
    {
        public MathFunctionType MathType { get; }

        public MathFunctionToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            MathType = tokenType switch
            {
                TokenType.SIN => MathFunctionType.Sin,
                TokenType.COS => MathFunctionType.Cos,
                TokenType.TAN => MathFunctionType.Tan,
                TokenType.ASIN => MathFunctionType.Asin,
                TokenType.ACOS => MathFunctionType.Acos,
                TokenType.ATAN => MathFunctionType.Atan,
                TokenType.SINH => MathFunctionType.Sinh,
                TokenType.COSH => MathFunctionType.Cosh,
                TokenType.TANH => MathFunctionType.Tanh,
                TokenType.LOG => MathFunctionType.Log,
                TokenType.EXP => MathFunctionType.Exp,
                TokenType.ABS => MathFunctionType.Abs,
                TokenType.SQR => MathFunctionType.Sqr,
                TokenType.SQRT => MathFunctionType.Sqrt,
                TokenType.FLOOR_MATH => MathFunctionType.Floor,
                TokenType.CEIL => MathFunctionType.Ceil,
                _ => throw new ArgumentException($"Invalid math function: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.MathFunction;
        public override string Symbol => MathType switch
        {
            MathFunctionType.Sin => "_sin",
            MathFunctionType.Cos => "_cos",
            MathFunctionType.Tan => "_tan",
            MathFunctionType.Asin => "_asin",
            MathFunctionType.Acos => "_acos",
            MathFunctionType.Atan => "_atan",
            MathFunctionType.Sinh => "_sinh",
            MathFunctionType.Cosh => "_cosh",
            MathFunctionType.Tanh => "_tanh",
            MathFunctionType.Log => "_log",
            MathFunctionType.Exp => "_exp",
            MathFunctionType.Abs => "_abs",
            MathFunctionType.Sqr => "_sqr",
            MathFunctionType.Sqrt => "_sqrt",
            MathFunctionType.Floor => "_floor",
            MathFunctionType.Ceil => "_ceil",
            _ => throw new ArgumentOutOfRangeException(nameof(MathType))
        };

        public override int[] SupportedArities => new[] { 1 }; // All math functions are monadic
        public override bool IsSystemVariable => false;
    }

    /// <summary>
    /// System function tokens (_bd, _db, _getenv, etc.)
    /// </summary>
    public class SystemFunctionToken : FunctionToken
    {
        public SystemFunctionType SystemType { get; }

        public SystemFunctionToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            SystemType = tokenType switch
            {
                TokenType.BD => SystemFunctionType.Bd,
                TokenType.DB => SystemFunctionType.Db,
                TokenType.GETENV => SystemFunctionType.Getenv,
                TokenType.SETENV => SystemFunctionType.Setenv,
                TokenType.SIZE => SystemFunctionType.Size,
                TokenType.DRAW => SystemFunctionType.Draw,
                TokenType.DIV => SystemFunctionType.Div,
                TokenType.AND => SystemFunctionType.And,
                TokenType.OR => SystemFunctionType.Or,
                TokenType.XOR => SystemFunctionType.Xor,
                TokenType.NOT => SystemFunctionType.Not,
                TokenType.ROT => SystemFunctionType.Rot,
                TokenType.SHIFT => SystemFunctionType.Shift,
                TokenType.DOT_PRODUCT => SystemFunctionType.Dot,
                TokenType.MUL => SystemFunctionType.Mul,
                TokenType.INV => SystemFunctionType.Inv,
                TokenType.LSQ => SystemFunctionType.Lsq,
                TokenType.IN => SystemFunctionType.In,
                TokenType.BIN => SystemFunctionType.Bin,
                TokenType.BINL => SystemFunctionType.Binl,
                TokenType.LIN => SystemFunctionType.Lin,
                TokenType.DV => SystemFunctionType.Dv,
                TokenType.DVL => SystemFunctionType.Dvl,
                TokenType.DI => SystemFunctionType.Di,
                TokenType.VS => SystemFunctionType.Vs,
                TokenType.SV => SystemFunctionType.Sv,
                TokenType.SS => SystemFunctionType.Ss,
                TokenType.SM => SystemFunctionType.Sm,
                TokenType.CI => SystemFunctionType.Ci,
                TokenType.IC => SystemFunctionType.Ic,
                TokenType.GTIME => SystemFunctionType.Gtime,
                TokenType.LTIME => SystemFunctionType.Ltime,
                TokenType.LT => SystemFunctionType.Lt,
                TokenType.JD => SystemFunctionType.Jd,
                TokenType.DJ => SystemFunctionType.Dj,
                _ => throw new ArgumentException($"Invalid system function: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.SystemFunction;
        public override string Symbol => SystemType switch
        {
            SystemFunctionType.Bd => "_bd",
            SystemFunctionType.Db => "_db",
            SystemFunctionType.Getenv => "_getenv",
            SystemFunctionType.Setenv => "_setenv",
            SystemFunctionType.Size => "_size",
            SystemFunctionType.Draw => "_draw",
            SystemFunctionType.Div => "_div",
            SystemFunctionType.And => "_and",
            SystemFunctionType.Or => "_or",
            SystemFunctionType.Xor => "_xor",
            SystemFunctionType.Not => "_not",
            SystemFunctionType.Rot => "_rot",
            SystemFunctionType.Shift => "_shift",
            SystemFunctionType.Dot => "_dot",
            SystemFunctionType.Mul => "_mul",
            SystemFunctionType.Inv => "_inv",
            SystemFunctionType.Lsq => "_lsq",
            SystemFunctionType.In => "_in",
            SystemFunctionType.Bin => "_bin",
            SystemFunctionType.Binl => "_binl",
            SystemFunctionType.Lin => "_lin",
            SystemFunctionType.Dv => "_dv",
            SystemFunctionType.Dvl => "_dvl",
            SystemFunctionType.Di => "_di",
            SystemFunctionType.Vs => "_vs",
            SystemFunctionType.Sv => "_sv",
            SystemFunctionType.Ss => "_ss",
            SystemFunctionType.Sm => "_sm",
            SystemFunctionType.Ci => "_ci",
            SystemFunctionType.Ic => "_ic",
            SystemFunctionType.Gtime => "_gtime",
            SystemFunctionType.Ltime => "_ltime",
            SystemFunctionType.Lt => "_lt",
            SystemFunctionType.Jd => "_jd",
            SystemFunctionType.Dj => "_dj",
            _ => throw new ArgumentOutOfRangeException(nameof(SystemType))
        };

        public override int[] SupportedArities => SystemType switch
        {
            SystemFunctionType.Bd => new[] { 1 },
            SystemFunctionType.Db => new[] { 1 },
            SystemFunctionType.Getenv => new[] { 1 },
            SystemFunctionType.Setenv => new[] { 2 },
            SystemFunctionType.Size => new[] { 1 },
            SystemFunctionType.Draw => new[] { 1 },
            SystemFunctionType.Div => new[] { 2 },
            SystemFunctionType.And => new[] { 2 },
            SystemFunctionType.Or => new[] { 2 },
            SystemFunctionType.Xor => new[] { 2 },
            SystemFunctionType.Not => new[] { 1 },
            SystemFunctionType.Rot => new[] { 2 },
            SystemFunctionType.Shift => new[] { 2 },
            SystemFunctionType.Dot => new[] { 2 },
            SystemFunctionType.Mul => new[] { 2 },
            SystemFunctionType.Inv => new[] { 1 },
            SystemFunctionType.Lsq => new[] { 2 },
            SystemFunctionType.In => new[] { 2 },
            SystemFunctionType.Bin => new[] { 2 },
            SystemFunctionType.Binl => new[] { 2 },
            SystemFunctionType.Lin => new[] { 2 },
            SystemFunctionType.Dv => new[] { 2 },
            SystemFunctionType.Dvl => new[] { 2 },
            SystemFunctionType.Di => new[] { 2 },
            SystemFunctionType.Vs => new[] { 1 },
            SystemFunctionType.Sv => new[] { 2 },
            SystemFunctionType.Ss => new[] { 3 },
            SystemFunctionType.Sm => new[] { 2 },
            SystemFunctionType.Ci => new[] { 1 },
            SystemFunctionType.Ic => new[] { 1 },
            SystemFunctionType.Gtime => new[] { 0 },
            SystemFunctionType.Ltime => new[] { 0 },
            SystemFunctionType.Lt => new[] { 1 },
            SystemFunctionType.Jd => new[] { 1 },
            SystemFunctionType.Dj => new[] { 1 },
            _ => new[] { 1 }
        };

        public override bool IsSystemVariable => SystemType switch
        {
            SystemFunctionType.Gtime => true,
            SystemFunctionType.Ltime => true,
            _ => false
        };
    }

    /// <summary>
    /// I/O operator tokens (0:, 1:, 2:, etc.)
    /// </summary>
    public class IOOperatorToken : FunctionToken
    {
        public IOOperatorType IOType { get; }

        public IOOperatorToken(TokenType tokenType, string lexeme, int position, int line = 0, int column = 0)
            : base(tokenType, lexeme, position, line, column)
        {
            IOType = tokenType switch
            {
                TokenType.IO_VERB_0 => IOOperatorType.ReadText,
                TokenType.IO_VERB_1 => IOOperatorType.WriteData,
                TokenType.IO_VERB_2 => IOOperatorType.ReadData,
                TokenType.IO_VERB_3 => IOOperatorType.IPC,
                TokenType.IO_VERB_6 => IOOperatorType.GetHandle,
                TokenType.TYPE => IOOperatorType.Type,
                TokenType.STRING_REPRESENTATION => IOOperatorType.StringRepresentation,
                _ => throw new ArgumentException($"Invalid I/O operator: {tokenType}")
            };
        }

        public override TokenCategory Category => TokenCategory.IOOperator;
        public override string Symbol => IOType switch
        {
            IOOperatorType.ReadText => "0:",
            IOOperatorType.WriteData => "1:",
            IOOperatorType.ReadData => "2:",
            IOOperatorType.IPC => "3:",
            IOOperatorType.GetHandle => "6:",
            IOOperatorType.Type => "4:",
            IOOperatorType.StringRepresentation => "5:",
            _ => throw new ArgumentOutOfRangeException(nameof(IOType))
        };

        public override int[] SupportedArities => IOType switch
        {
            IOOperatorType.ReadText => new[] { 0, 1 }, // Can be monadic (read from stdin) or dyadic (read from file)
            IOOperatorType.WriteData => new[] { 2 },
            IOOperatorType.ReadData => new[] { 1 },
            IOOperatorType.IPC => new[] { 1, 2 },
            IOOperatorType.GetHandle => new[] { 1 },
            IOOperatorType.Type => new[] { 1, 2 },
            IOOperatorType.StringRepresentation => new[] { 1 },
            _ => new[] { 1 }
        };

        public override bool IsSystemVariable => false;
    }

    /// <summary>
    /// Enumerations for different function types
    /// </summary>
    public enum MathFunctionType
    {
        Sin, Cos, Tan, Asin, Acos, Atan, Sinh, Cosh, Tanh,
        Log, Exp, Abs, Sqr, Sqrt, Floor, Ceil
    }

    public enum SystemFunctionType
    {
        Bd, Db, Getenv, Setenv, Size, Draw, Div, And, Or, Xor, Not,
        Rot, Shift, Dot, Mul, Inv, Lsq, In, Bin, Binl, Lin,
        Dv, Dvl, Di, Vs, Sv, Ss, Sm, Ci, Ic, Gtime, Ltime, Lt, Jd, Dj
    }

    public enum IOOperatorType
    {
        ReadText, WriteData, ReadData, IPC, GetHandle, Type, StringRepresentation
    }
}
