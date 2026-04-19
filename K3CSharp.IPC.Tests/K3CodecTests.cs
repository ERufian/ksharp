using System;
using System.Collections.Generic;
using System.Linq;
using K3CSharp.IPC;

namespace K3CSharp.IPC.Tests
{
    /// <summary>
    /// Wire-format tests, mirroring the <c>k3ipc-go</c> test suite
    /// (<c>k3ipc/k3ipc_test.go</c>). Every vector below was taken verbatim
    /// from the reference implementation so a passing run proves bit-for-bit
    /// compatibility with the Go codec.
    /// </summary>
    public static class K3CodecTests
    {
        /// <summary>Run all codec tests and return true if all passed.</summary>
        public static bool RunAll()
        {
            var results = new List<(string Name, bool Pass, string? Info)>
            {
                Test("K3INT",   TestK3Int),
                Test("K3FLT",   TestK3Flt),
                Test("K3CHR",   TestK3Chr),
                Test("K3CHRs",  TestK3Chrs),
                Test("K3SYM",   TestK3Sym),
                Test("K3SYMs",  TestK3Syms),
                Test("K3NUL",   TestK3Nul),
                Test("K3LST",   TestK3Lst),
                Test("K3DCT",   TestK3Dct),
                Test("NumStr",  TestNumStrHelpers),
            };

            int passed = results.Count(r => r.Pass);
            Console.WriteLine();
            Console.WriteLine($"K3Codec: {passed}/{results.Count} tests passed");
            foreach (var (name, pass, info) in results)
            {
                string mark = pass ? "PASS" : "FAIL";
                string suffix = string.IsNullOrEmpty(info) ? "" : "  -- " + info;
                Console.WriteLine($"  [{mark}] {name}{suffix}");
            }
            return passed == results.Count;
        }

        private static (string Name, bool Pass, string? Info) Test(
            string name, Func<string?> body)
        {
            try
            {
                string? err = body();
                return (name, err == null, err);
            }
            catch (Exception ex)
            {
                return (name, false, ex.Message);
            }
        }

        // --- Vectors ---------------------------------------------------

        private static string? TestK3Int()
        {
            return CheckAll(
                (0,         "1 0 0 0 8 0 0 0 1 0 0 0 0 0 0 0"),
                (1,         "1 0 0 0 8 0 0 0 1 0 0 0 1 0 0 0"),
                (0x7ffffffe,"1 0 0 0 8 0 0 0 1 0 0 0 254 255 255 127"),
                (1234,      "1 0 0 0 8 0 0 0 1 0 0 0 210 4 0 0"));
        }

        private static string? TestK3Flt()
        {
            return Check(1.1,
                "1 0 0 0 16 0 0 0 2 0 0 0 1 0 0 0 154 153 153 153 153 153 241 63");
        }

        private static string? TestK3Chr()
        {
            return Check((byte)'x', "1 0 0 0 8 0 0 0 3 0 0 0 120 0 0 0");
        }

        private static string? TestK3Chrs()
        {
            string? e1 = Check("hello",
                "1 0 0 0 14 0 0 0 253 255 255 255 5 0 0 0 104 101 108 108 111 0");
            if (e1 != null) return e1;
            return Check("hi",
                "1 0 0 0 11 0 0 0 253 255 255 255 2 0 0 0 104 105 0");
        }

        private static string? TestK3Sym()
        {
            return Check(new KSym("abc"),
                "1 0 0 0 8 0 0 0 4 0 0 0 97 98 99 0");
        }

        private static string? TestK3Syms()
        {
            return Check(
                new[] { new KSym("abc"), new KSym("xyz") },
                "1 0 0 0 16 0 0 0 252 255 255 255 2 0 0 0 97 98 99 0 120 121 122 0");
        }

        private static string? TestK3Nul()
        {
            return Check((object?)null,
                "1 0 0 0 8 0 0 0 6 0 0 0 0 0 0 0");
        }

        private static string? TestK3Lst()
        {
            string? e;
            e = Check(Array.Empty<object?>(),
                "1 0 0 0 8 0 0 0 0 0 0 0 0 0 0 0");
            if (e != null) return e;

            e = Check(new object?[] { "abc", 1 },
                "1 0 0 0 32 0 0 0 0 0 0 0 2 0 0 0 253 255 255 255 3 0 0 0 97 98 99 0 0 0 0 0 1 0 0 0 1 0 0 0");
            if (e != null) return e;

            e = Check(new object?[] { (byte)'a', (byte)'b', (byte)'c', 1 },
                "1 0 0 0 40 0 0 0 0 0 0 0 4 0 0 0 3 0 0 0 97 0 0 0 3 0 0 0 98 0 0 0 3 0 0 0 99 0 0 0 1 0 0 0 1 0 0 0");
            if (e != null) return e;

            return Check(
                new object?[] { new object?[] { Array.Empty<object?>(), Array.Empty<object?>() } },
                "1 0 0 0 32 0 0 0 0 0 0 0 1 0 0 0 0 0 0 0 2 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0");
        }

        private static string? TestK3Dct()
        {
            string? e;
            e = Check(new Dictionary<string, object?>(),
                "1 0 0 0 8 0 0 0 5 0 0 0 0 0 0 0");
            if (e != null) return e;

            return Check(new Dictionary<string, object?> { ["k"] = 123 },
                "1 0 0 0 40 0 0 0 5 0 0 0 1 0 0 0 0 0 0 0 3 0 0 0 4 0 0 0 107 0 0 0 1 0 0 0 123 0 0 0 6 0 0 0 0 0 0 0");
        }

        private static string? TestNumStrHelpers()
        {
            byte[] bs = K3Codec.NumStrToBytes("1 2 255 0");
            if (bs.Length != 4 || bs[0] != 1 || bs[1] != 2 || bs[2] != 255 || bs[3] != 0)
                return "NumStrToBytes produced unexpected output";
            if (K3Codec.BytesToNumStr(bs) != "1 2 255 0")
                return "BytesToNumStr round-trip failed";
            return null;
        }

        // --- Helpers ---------------------------------------------------

        private static string? CheckAll(params (object? Value, string Bytes)[] cases)
        {
            foreach (var (v, b) in cases)
            {
                string? e = Check(v, b);
                if (e != null) return e;
            }
            return null;
        }

        private static string? Check(object? expectedValue, string expectedBytes)
        {
            // Decode
            byte[] bytes = K3Codec.NumStrToBytes(expectedBytes);
            object? actualValue = K3Codec.Db(bytes);
            if (!ValuesEqual(expectedValue, actualValue))
            {
                return $"Db() mismatch: expected {Format(expectedValue)} but got {Format(actualValue)}";
            }

            // Encode
            byte[] actualBytes = K3Codec.Bd(expectedValue);
            string actualStr = K3Codec.BytesToNumStr(actualBytes);
            if (actualStr != expectedBytes)
            {
                return $"Bd() mismatch:\n    expect: {expectedBytes}\n    actual: {actualStr}";
            }
            return null;
        }

        private static bool ValuesEqual(object? a, object? b)
        {
            if (a == null) return b == null;
            if (b == null) return false;

            if (a is object?[] la && b is object?[] lb)
            {
                if (la.Length != lb.Length) return false;
                for (int i = 0; i < la.Length; i++)
                {
                    if (!ValuesEqual(la[i], lb[i])) return false;
                }
                return true;
            }

            if (a is KSym[] sa && b is KSym[] sb)
            {
                if (sa.Length != sb.Length) return false;
                for (int i = 0; i < sa.Length; i++)
                {
                    if (sa[i] != sb[i]) return false;
                }
                return true;
            }

            if (a is IDictionary<string, object?> da && b is IDictionary<string, object?> db)
            {
                if (da.Count != db.Count) return false;
                foreach (var kvp in da)
                {
                    if (!db.TryGetValue(kvp.Key, out object? bv)) return false;
                    if (!ValuesEqual(kvp.Value, bv)) return false;
                }
                return true;
            }

            return a.Equals(b);
        }

        private static string Format(object? v)
        {
            if (v == null) return "null";
            if (v is string s) return "\"" + s + "\"";
            if (v is KSym sym) return sym.ToString();
            if (v is object?[] arr) return "[" + string.Join(", ", arr.Select(Format)) + "]";
            if (v is KSym[] syms) return "[" + string.Join(", ", syms.Select(x => x.ToString())) + "]";
            if (v is IDictionary<string, object?> d)
            {
                return "{" + string.Join(", ", d.Select(kv => kv.Key + ":" + Format(kv.Value))) + "}";
            }
            return v.ToString() ?? "?";
        }
    }
}
