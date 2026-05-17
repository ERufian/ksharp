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
    public partial class Evaluator
    {
        private Dictionary<string, Func<K3Value, K3Value>>? _monadicDispatch;
        private Dictionary<string, Func<K3Value, K3Value, K3Value>>? _dyadicDispatch;

        /// <summary>
        /// Centralized monadic verb dispatch table. Lazy-initialized on first use.
        /// Replaces multiple hardcoded switch statements with a single dictionary lookup.
        /// </summary>
        private Dictionary<string, Func<K3Value, K3Value>> MonadicDispatch
        {
            get
            {
                _monadicDispatch ??= BuildMonadicDispatch();
                return _monadicDispatch;
            }
        }

        /// <summary>
        /// Centralized dyadic verb dispatch table. Lazy-initialized on first use.
        /// Replaces the local dictionary in EvaluateDyadicOperatorWithRegistry and
        /// other hardcoded switch statements.
        /// </summary>
        private Dictionary<string, Func<K3Value, K3Value, K3Value>> DyadicDispatch
        {
            get
            {
                _dyadicDispatch ??= BuildDyadicDispatch();
                return _dyadicDispatch;
            }
        }

        /// <summary>
        /// Dispatch a monadic verb by name. Returns null if not found in the dispatch table.
        /// </summary>
        internal K3Value? DispatchMonadic(string verbName, K3Value operand)
        {
            if (MonadicDispatch.TryGetValue(verbName, out var handler))
                return handler(operand);
            return null;
        }

        /// <summary>
        /// Dispatch a dyadic verb by name. Returns null if not found in the dispatch table.
        /// </summary>
        internal K3Value? DispatchDyadic(string verbName, K3Value left, K3Value right)
        {
            if (DyadicDispatch.TryGetValue(verbName, out var handler))
                return handler(left, right);
            return null;
        }

        private Dictionary<string, Func<K3Value, K3Value>> BuildMonadicDispatch()
        {
            var d = new Dictionary<string, Func<K3Value, K3Value>>();

            // Primitive glyphs — monadic sense
            d["-"]  = MonadicMinus;
            d["+"]  = Transpose;
            d["*"]  = FirstElement;
            d["%"]  = Reciprocal;
            d["&"]  = Where;
            d["|"]  = op => Reverse(op);
            d["<"]  = GradeUp;
            d[">"]  = GradeDown;
            d["^"]  = Shape;
            d["!"]  = Enumerate;
            d[","]  = Enlist;
            d["#"]  = op => Count(op);
            d["_"]  = Floor;
            d["?"]  = hashgroupHandler.Unique;
            d["="]  = hashgroupHandler.Group;
            d["$"]  = formatHandler.Format;
            d["@"]  = Atom;
            d["."]  = MakeFunction;
            d["~"]  = Negate;

            // Forced-monadic colon variants (verb:)
            d["-:"] = ArithmeticNegate;
            d["+:"] = Transpose;
            d["*:"] = FirstElement;
            d["%:"] = Reciprocal;
            d["&:"] = Where;
            d["|:"] = op => Reverse(op);
            d["<:"] = GradeUp;
            d[">:"] = GradeDown;
            d["^:"] = Shape;
            d["!:"] = Enumerate;
            d[",:"] = Enlist;
            d["#:"] = op => Count(op);
            d["_:"] = Floor;
            d["?:"] = hashgroupHandler.Unique;
            d["=:"] = hashgroupHandler.Group;
            d["$:"] = formatHandler.Format;
            d["@:"] = Atom;
            d[".:"] = MakeFunction;
            d["~:"] = Negate;

            // Math functions
            d["_log"]   = mathHandler.MathLog;
            d["_exp"]   = mathHandler.MathExp;
            d["_abs"]   = mathHandler.MathAbs;
            d["_sqr"]   = mathHandler.MathSqr;
            d["_sqrt"]  = mathHandler.MathSqrt;
            d["_floor"] = mathHandler.MathFloor;
            d["_ceil"]  = mathHandler.MathCeil;
            d["_sin"]   = mathHandler.MathSin;
            d["_cos"]   = mathHandler.MathCos;
            d["_tan"]   = mathHandler.MathTan;
            d["_asin"]  = mathHandler.MathAsin;
            d["_acos"]  = mathHandler.MathAcos;
            d["_atan"]  = mathHandler.MathAtan;
            d["_sinh"]  = mathHandler.MathSinh;
            d["_cosh"]  = mathHandler.MathCosh;
            d["_tanh"]  = mathHandler.MathTanh;
            d["_inv"]   = op => mathHandler.MathInv(op);
            d["_not"]   = mathHandler.MathNot;

            // TokenType-name aliases (parser may emit these instead of the canonical names)
            d["ABS"]        = mathHandler.MathAbs;
            d["SQR"]        = mathHandler.MathSqr;
            d["SQRT"]       = mathHandler.MathSqrt;
            d["FLOOR_MATH"] = mathHandler.MathFloor;
            d["CEIL"]       = mathHandler.MathCeil;
            d["SIN"]        = mathHandler.MathSin;
            d["COS"]        = mathHandler.MathCos;
            d["TAN"]        = mathHandler.MathTan;
            d["ASIN"]       = mathHandler.MathAsin;
            d["ACOS"]       = mathHandler.MathAcos;
            d["ATAN"]       = mathHandler.MathAtan;
            d["SINH"]       = mathHandler.MathSinh;
            d["COSH"]       = mathHandler.MathCosh;
            d["TANH"]       = mathHandler.MathTanh;
            d["INV"]        = op => mathHandler.MathInv(op);

            // Time / date functions
            d["_lt"]    = timeHandler.LtFunction;
            d["_jd"]    = timeHandler.JdFunction;
            d["_dj"]    = timeHandler.DjFunction;
            d["_gtime"] = timeHandler.GtimeFunction;
            d["_ltime"] = timeHandler.LtimeFunction;

            // List / conversion functions
            d["_in"]   = op => listHandler.InFunction(op);
            d["_bin"]  = listHandler.BinFunction;
            d["_binl"] = listHandler.BinlFunction;
            d["_lin"]  = listHandler.LinFunction;
            d["_ci"]   = listHandler.Ci;
            d["_ic"]   = listHandler.Ic;
            d["_bd"]   = BdFunction;
            d["_db"]   = DbFunction;

            // System variables (monadic form)
            d["_v"] = varsHandler.VarFunction;
            d["_i"] = varsHandler.IndexFunction;
            d["_f"] = varsHandler.FunctionFunction;
            d["_n"] = varsHandler.NullFunction;
            d["_s"] = varsHandler.SpaceFunction;
            d["_h"] = varsHandler.HostFunction;
            d["_p"] = varsHandler.PortFunction;
            d["_P"] = varsHandler.ProcessIdFunction;
            d["_w"] = varsHandler.WhoFunction;
            d["_u"] = varsHandler.UserFunction;
            d["_a"] = varsHandler.AddressFunction;
            d["_k"] = varsHandler.VersionFunction;
            d["_o"] = varsHandler.OsFunction;
            d["_c"] = varsHandler.CoresFunction;
            d["_r"] = varsHandler.RamFunction;
            d["_m"] = varsHandler.MachineIdFunction;
            d["_y"] = varsHandler.StackFunction;
            d["_T"] = op => timeHandler.TFunction(op);

            // System functions
            d["_d"]      = varsHandler.DirFunction;
            d["_getenv"] = listHandler.GetenvFunction;
            d["_size"]   = listHandler.SizeFunction;
            d["_host"]   = listHandler.HostDnsFunction;
            d["_exit"]   = listHandler.ExitFunction;

            // Control flow
            d["_while"] = WhileFunction;
            d["_if"]    = IfFunction;
            d["do"]     = DoFunction;
            d["while"]  = WhileFunction;
            d["if"]     = IfFunction;

            // ksharp verbs
            d["_parse"]   = op => Verbs.ParseVerbHandler.Parse(new[] { op });
            d["_eval"]    = EvaluateEvalVerb;
            d["_gethint"] = op => GetHintFunction(new List<K3Value> { op });
            d["GETHINT"]  = op => GetHintFunction(new List<K3Value> { op });
            d["_dispose"] = op => DisposeFunction(new List<K3Value> { op });
            d["DISPOSE"]  = op => DisposeFunction(new List<K3Value> { op });

            // I/O verbs — monadic form
            d["IO_VERB_0"] = op => IoVerbMonadic(op, 0);
            d["IO_VERB_1"] = op => IoVerbMonadic(op, 1);
            d["IO_VERB_2"] = op => IoVerbMonadic(op, 2);
            d["IO_VERB_3"] = op => IoVerbMonadic(op, 3);
            d["IO_VERB_4"] = op => IoVerbMonadic(op, 4);
            d["IO_VERB_5"] = op => IoVerbMonadic(op, 5);
            d["IO_VERB_6"] = op => IoVerbMonadic(op, 6);
            d["IO_VERB_7"] = op => IoVerbMonadic(op, 7);
            d["IO_VERB_8"] = op => IoVerbMonadic(op, 8);
            d["IO_VERB_9"] = op => IoVerbMonadic(op, 9);
            d["TYPE"]                  = op => IoVerbMonadic(op, 4);
            d["STRING_REPRESENTATION"] = op => IoVerbMonadic(op, 5);

            // Identity for monadic min/max
            d["MIN"] = op => op;
            d["MAX"] = op => op;

            // Adverb names (when appearing as monadic operator in DyadicOp context)
            d["over"]       = op => ApplyAdverbSlash(op, new NullValue(), new NullValue());
            d["scan"]       = op => ApplyAdverbBackslash(op, new NullValue(), new NullValue());
            d["each"]       = op => ApplyAdverbTick(op, new NullValue(), new NullValue());
            d["each-right"] = op => ApplyAdverbSlashColon(op, new NullValue(), new NullValue());
            d["each-left"]  = op => ApplyAdverbBackslashColon(op, new NullValue(), new NullValue());
            d["each-prior"] = op => ApplyAdverbTickColon(op, new NullValue(), new NullValue());

            return d;
        }

        private Dictionary<string, Func<K3Value, K3Value, K3Value>> BuildDyadicDispatch()
        {
            var d = new Dictionary<string, Func<K3Value, K3Value, K3Value>>();

            // Primitive glyphs — dyadic sense
            d["+"] = Plus;
            d["-"] = Minus;
            d["*"] = Times;
            d["%"] = Divide;
            d["^"] = Power;
            d["!"] = ModRotate;
            d["&"] = Min;
            d["|"] = Max;
            d["<"] = Less;
            d[">"] = More;
            d["="] = Equal;
            d["~"] = Match;
            d[","] = (l, r) => Join(l, r);
            d["#"] = (l, r) => Take(l, r);
            d["_"] = DropOrCut;
            d["@"] = AtIndex;
            d["."] = DotApply;
            d["$"] = formatHandler.Format;
            d["?"] = Find;
            d["::"] = GlobalAssignment;
            d["POWER"] = Power;

            // System functions — dyadic
            d["_in"]     = listHandler.In;
            d["_draw"]   = randHandler.Draw;
            d["_bin"]    = listHandler.Bin;
            d["_div"]    = mathHandler.MathDiv;
            d["_dot"]    = mathHandler.MathDot;
            d["_mul"]    = mathHandler.MathMul;
            d["_inv"]    = mathHandler.MathInv;
            d["_lsq"]    = mathHandler.MathLsq;
            d["_and"]    = mathHandler.MathAnd;
            d["_or"]     = mathHandler.MathOr;
            d["_xor"]    = mathHandler.MathXor;
            d["_rot"]    = mathHandler.MathRot;
            d["_shift"]  = mathHandler.MathShift;
            d["_binl"]   = listHandler.Binl;
            d["_lin"]    = listHandler.Lin;
            d["_dv"]     = listHandler.Dv;
            d["_dvl"]    = listHandler.Dvl;
            d["_di"]     = listHandler.Di;
            d["_sm"]     = listHandler.Sm;
            d["_sv"]     = listHandler.Sv;
            d["_vs"]     = listHandler.Vs;
            d["_ss"]     = listHandler.SsFunction;
            d["_setenv"] = listHandler.SetenvFunction;
            d["_bd"]     = (l, r) => BdFunction(r);
            d["_db"]     = (l, r) => DbFunction(r);

            // ksharp verbs — dyadic
            d["_sethint"] = (l, r) => SetHintFunction(new List<K3Value> { l, r });
            d["SETHINT"]  = (l, r) => SetHintFunction(new List<K3Value> { l, r });

            // I/O verbs — dyadic form
            d["IO_VERB_0"] = (l, r) => IoVerbDyadic(l, r, 0);
            d["IO_VERB_1"] = (l, r) => IoVerbDyadic(l, r, 1);
            d["IO_VERB_2"] = (l, r) => IoVerbDyadic(l, r, 2);
            d["IO_VERB_3"] = (l, r) => IoVerbDyadic(l, r, 3);
            d["IO_VERB_4"] = (l, r) => IoVerbDyadic(l, r, 4);
            d["IO_VERB_5"] = (l, r) => IoVerbDyadic(l, r, 5);
            d["IO_VERB_6"] = (l, r) => IoVerbDyadic(l, r, 6);
            d["IO_VERB_7"] = (l, r) => IoVerbDyadic(l, r, 7);
            d["IO_VERB_8"] = (l, r) => IoVerbDyadic(l, r, 8);
            d["IO_VERB_9"] = (l, r) => IoVerbDyadic(l, r, 9);
            d["TYPE"]                  = (l, r) => IoVerbDyadic(l, r, 4);
            d["STRING_REPRESENTATION"] = (l, r) => IoVerbMonadic(r, 5);

            // Adverb names — dyadic context (left is init/verb, right is data)
            d["over"]  = (l, r) => Over(l, l, r);
            d["scan"]  = (l, r) => Scan(l, l, r);
            d["each"]  = (l, r) => HandleAdverbTick(l, new NullValue(), r);
            d["/:"]    = (l, r) => EachRight(l, new NullValue(), r);
            d["\\:"]   = (l, r) => EachLeft(l, new NullValue(), r);

            return d;
        }

        // Wrapper to resolve naming conflict with LINQ's First/Count
        private K3Value FirstElement(K3Value operand) => First(operand);
    }
}
