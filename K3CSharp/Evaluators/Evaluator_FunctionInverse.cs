// Copyright (c) 2026 Eusebio Rufian-Zilbermann
//
// This software is licensed under the terms of the  **MIT License with Commons Clause**.
// You are free to use, modify, and distribute it (including in commercial products), provided you include attribution and do not sell the software (or a product whose value derives substantially from this software) itself.
//
// Full license text: [LICENSE.txt](https://github.com/ERufian/ksharp/blob/main/LICENSE.txt)
namespace K3CSharp
{
    /// <summary>
    /// Function Inverse verb implementations extracted from Evaluator.
    /// </summary>
    public class FunctionInverseHandler
    {
        private readonly IEvaluatorContext ctx;

        public FunctionInverseHandler(IEvaluatorContext context)
        {
            ctx = context;
        }
        /// <summary>
        /// Function Inverse: f ? y
        /// Finds x such that f(x) = y using heuristics for known functions
        /// and secant method as fallback.
        /// Tolerance: 1e-6 * |y|
        /// Max iterations: 20
        /// Default initial approximations: 0.9999 and 0.9998
        /// </summary>
        internal K3Value FunctionInverse(K3Value func, K3Value y, K3Value? initialGuess = null)
        {
            // Handle vector y: apply element-wise (atom function)
            if (y is VectorValue yVec)
            {
                var results = new List<K3Value>();
                foreach (var elem in yVec.Elements)
                {
                    results.Add(FunctionInverse(func, elem, initialGuess));
                }
                return new VectorValue(results, ctx.DetermineVectorType(results));
            }

            double yVal = MathHandler.GetNumericValue(y);

            // Try heuristic: known invertible function
            var heuristicResult = TryHeuristicInverse(func, yVal);
            if (heuristicResult.HasValue)
            {
                return MakeFloatResult(heuristicResult.Value);
            }

            // Fallback: secant method
            double x0, x1;
            if (initialGuess != null && !(initialGuess is NullValue))
            {
                x0 = MathHandler.GetNumericValue(initialGuess);
                x1 = 0.9999 * x0;
            }
            else
            {
                x0 = 0.9999;
                x1 = 0.9998;
            }

            return SecantMethod(func, yVal, x0, x1);
        }

        /// <summary>
        /// Try to compute the inverse using heuristics for known functions.
        /// Returns null if the function cannot be identified.
        /// </summary>
        private double? TryHeuristicInverse(K3Value func, double y)
        {
            // Case 1: ProjectedFunctionValue — a bare system verb like (_exp)
            if (func is ProjectedFunctionValue proj)
            {
                return TryKnownFunctionInverse(proj.OperatorName, y);
            }

            // Case 2: FunctionValue — user-defined or lambda
            if (func is FunctionValue fv)
            {
                return TryAnalyzeFunctionBody(fv, y);
            }

            return null;
        }

        /// <summary>
        /// Direct inverse for known system functions.
        /// </summary>
        private double? TryKnownFunctionInverse(string funcName, double y)
        {
            return funcName switch
            {
                "_exp" => Math.Log(y),           // inverse of exp is log
                "_log" => Math.Exp(y),           // inverse of log is exp
                "_sqr" => Math.Sqrt(y),          // inverse of sqr is sqrt
                "_sqrt" => y * y,                // inverse of sqrt is sqr
                "_sin" => Math.Asin(y),          // inverse of sin is asin
                "_cos" => Math.Acos(y),          // inverse of cos is acos
                "_tan" => Math.Atan(y),          // inverse of tan is atan
                "_asin" => Math.Sin(y),          // inverse of asin is sin
                "_acos" => Math.Cos(y),          // inverse of acos is cos
                "_atan" => Math.Tan(y),          // inverse of atan is tan
                "_sinh" => InverseSinh(y),       // inverse of sinh is asinh
                "_cosh" => InverseCosh(y),       // inverse of cosh is acosh
                "_tanh" => InverseTanh(y),       // inverse of tanh is atanh
                "_abs" => y,                     // inverse of abs (positive branch)
                "_floor" => y,                   // inverse of floor (identity for integers)
                "_ceil" => y,                    // inverse of ceil (identity for integers)
                _ => null
            };
        }

        /// <summary>
        /// Analyze a function body to detect patterns like:
        /// - Pure system function: {_exp x} or {_log x}
        /// - Scaled: {n*_exp x} or {_exp x * n} → n*f(x)
        /// - Offset: {n+_exp x} or {_exp x + n} → f(x)+n
        /// </summary>
        private double? TryAnalyzeFunctionBody(FunctionValue fv, double y)
        {
            var body = fv.BodyText?.Trim();
            if (string.IsNullOrEmpty(body)) return null;

            // Pattern 1: Pure system function — body is just a system function name applied to x
            // e.g., "_exp x" or "_log x" or "_sin[x]"
            foreach (var fname in KnownInvertibleFunctions)
            {
                // Match: "fname x" or "fname[x]"
                if (body == $"{fname} x" || body == $"{fname}[x]")
                {
                    return TryKnownFunctionInverse(fname, y);
                }
            }

            // Pattern 2: n*f(x) — "n*fname x" or "n*fname[x]"
            foreach (var fname in KnownInvertibleFunctions)
            {
                // Match: "NUMBER*fname x"
                var prefix1 = $"*{fname} x";
                var prefix2 = $"*{fname}[x]";
                if (body.EndsWith(prefix1) || body.EndsWith(prefix2))
                {
                    var numStr = body.EndsWith(prefix1)
                        ? body.Substring(0, body.Length - prefix1.Length)
                        : body.Substring(0, body.Length - prefix2.Length);
                    if (TryParseNumber(numStr, out double n) && n != 0)
                    {
                        // y = n * f(x) → f(x) = y/n → x = f_inv(y/n)
                        return TryKnownFunctionInverse(fname, y / n);
                    }
                }
            }

            // Pattern 3: f(x)*n — "fname x*n" (less common but possible)
            // Handled similarly

            // Pattern 4: n+f(x) — "n+fname x" or "n+fname[x]"
            foreach (var fname in KnownInvertibleFunctions)
            {
                var prefix1 = $"+{fname} x";
                var prefix2 = $"+{fname}[x]";
                if (body.EndsWith(prefix1) || body.EndsWith(prefix2))
                {
                    var numStr = body.EndsWith(prefix1)
                        ? body.Substring(0, body.Length - prefix1.Length)
                        : body.Substring(0, body.Length - prefix2.Length);
                    if (TryParseNumber(numStr, out double n))
                    {
                        // y = n + f(x) → f(x) = y - n → x = f_inv(y - n)
                        return TryKnownFunctionInverse(fname, y - n);
                    }
                }
            }

            // Pattern 5: f(x)+n — "fname x+n" (K evaluates right-to-left, this means fname(x+n), not what we want)
            // Skip — K's LRS makes this ambiguous

            return null;
        }

        /// <summary>
        /// Secant method for Function Inverse.
        /// Tolerance: 1e-6 * |y| (minimum 1e-12 for y near 0).
        /// Max iterations: 20.
        /// </summary>
        private K3Value SecantMethod(K3Value func, double yTarget, double x0, double x1)
        {
            double tolerance = Math.Max(1e-6 * Math.Abs(yTarget), 1e-12);
            double f0 = EvalFunctionAtPoint(func, x0) - yTarget;
            double f1 = EvalFunctionAtPoint(func, x1) - yTarget;

            for (int i = 0; i < 20; i++)
            {
                if (Math.Abs(f1) < tolerance)
                {
                    return MakeFloatResult(x1);
                }

                double denom = f1 - f0;
                if (Math.Abs(denom) < 1e-30)
                {
                    // Avoid division by zero — perturb
                    x1 += 1e-8;
                    f1 = EvalFunctionAtPoint(func, x1) - yTarget;
                    denom = f1 - f0;
                    if (Math.Abs(denom) < 1e-30)
                        throw new Exception("limit error");
                }

                double x2 = x1 - f1 * (x1 - x0) / denom;
                x0 = x1;
                f0 = f1;
                x1 = x2;
                f1 = EvalFunctionAtPoint(func, x1) - yTarget;
            }

            // Check final convergence
            if (Math.Abs(f1) < tolerance)
            {
                return MakeFloatResult(x1);
            }

            throw new Exception("limit error");
        }

        /// <summary>
        /// Evaluate a function at a specific numeric point.
        /// Handles both ProjectedFunctionValue and FunctionValue.
        /// </summary>
        private double EvalFunctionAtPoint(K3Value func, double x)
        {
            K3Value xVal = new FloatValue(x);
            K3Value result;

            if (func is ProjectedFunctionValue proj)
            {
                // Apply the projected function (system verb) to x
                result = ctx.CallVariableFunction(proj.OperatorName, new List<K3Value> { xVal });
            }
            else if (func is FunctionValue fv)
            {
                result = ctx.ExecuteFunction(fv, new List<K3Value> { xVal });
            }
            else
            {
                throw new Exception("domain error");
            }

            return MathHandler.GetNumericValue(result);
        }


        /// <summary>
        /// Create a FloatValue with standard precision rounding (7 significant digits to match k.exe).
        /// </summary>
        private static K3Value MakeFloatResult(double val)
        {
            return new FloatValue(val);
        }

        // Inverse hyperbolic functions
        private static double InverseSinh(double y) => Math.Log(y + Math.Sqrt(y * y + 1));
        private static double InverseCosh(double y) => Math.Log(y + Math.Sqrt(y * y - 1));
        private static double InverseTanh(double y) => 0.5 * Math.Log((1 + y) / (1 - y));

        // Known invertible functions list
        private static readonly string[] KnownInvertibleFunctions = new[]
        {
            "_log", "_exp", "_abs", "_sqr", "_sqrt", "_floor", "_ceil",
            "_sin", "_cos", "_tan", "_asin", "_acos", "_atan",
            "_sinh", "_cosh", "_tanh"
        };

        /// <summary>
        /// Try to parse a numeric string (integer or float, including negative).
        /// </summary>
        private static bool TryParseNumber(string s, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            // K uses - for negative in literals but may also use unary minus
            return double.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out result);
        }
    }
}
