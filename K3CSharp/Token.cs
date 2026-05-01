using System;
using System.Collections.Generic;

namespace K3CSharp
{
    public enum TokenType
    {
        INTEGER,
        LONG,
        FLOAT,
        CHARACTER,
        CHARACTER_VECTOR,
        SYMBOL,
        IDENTIFIER,
        LEFT_PAREN,
        RIGHT_PAREN,
        LEFT_BRACE,
        RIGHT_BRACE,
        LEFT_BRACKET,
        RIGHT_BRACKET,
        PLUS,
        MINUS,
        MULTIPLY,
        DIVIDE,
        MIN,
        MAX,
        LESS,
        GREATER,
        EQUAL,
        POWER,
        MODULUS,
        MATCH,          // ~ match operator
        NEGATE,
        JOIN,
        ASSIGNMENT,
        COLON,          // : conditional evaluation / assignment
        SEMICOLON,
        NEWLINE,
        QUOTE,
        BACKTICK,
        HASH,           // # count operator
        UNDERSCORE,     // _ floor operator
        QUESTION,       // ? unique operator
        APPLY,          // @ apply operator
        ATOM,           // @ atom operator (monadic)
        DOT_APPLY,      // . dot-apply operator (general - parser determines arity)
        ADVERB_SLASH,   // / adverb
        ADVERB_BACKSLASH, // \ adverb  
        ADVERB_TICK,    // ' adverb
        ADVERB_SLASH_COLON,   // /: adverb (each-right)
        ADVERB_BACKSLASH_COLON, // \: adverb (each-left)
        ADVERB_TICK_COLON,    // ': adverb (each-prior)
        
        // Variadic adverbs with brackets
        VARIADIC_ADVERB_OVER,    // /[...] adverb pattern  
        VARIADIC_ADVERB_SCAN,    // \[...] adverb pattern
        VARIADIC_ADVERB_EACH,    // '[...] adverb pattern
        
        // Multi-arity operators
        TRIADIC_OPERATOR,         // For 3-argument operations
        TETRADIC_OPERATOR,        // For 4-argument operations
        ERROR_TRAP_TOKEN,         // For : as error trap marker
        NULL,           // _n null value
        TYPE,           // 4: type operator
        STRING_REPRESENTATION, // 5: string representation operator (monadic)
        IO_VERB_0,      // 0: I/O verb (read/write text)
        IO_VERB_1,      // 1: I/O verb
        IO_VERB_2,      // 2: I/O verb
        IO_VERB_3,      // 3: I/O verb
        IO_VERB_4,      // 5: I/O verb (IPC Get - dyadic)
        IO_VERB_5,      // 5: I/O verb (append data - dyadic)
        IO_VERB_6,      // 6: I/O verb
        IO_VERB_7,      // 7: I/O verb (reserved)
        IO_VERB_8,      // 8: I/O verb (reserved)
        IO_VERB_9,      // 9: I/O verb (reserved)
        GLOBAL_ASSIGNMENT, // :: global assignment operator
        DOLLAR,          // $ format/form operator
        
        // Mathematical floating point operations
        LOG,            // _log logarithm
        EXP,            // _exp exponential
        ABS,            // _abs absolute value
        SQR,            // _sqr square
        SQRT,           // _sqrt square root
        FLOOR_MATH,     // _floor mathematical floor (always returns float)
        SIN,            // _sin sine
        COS,            // _cos cosine
        TAN,            // _tan tangent
        ASIN,           // _asin arcsine
        ACOS,           // _acos arccosine
        ATAN,           // _atan arctangent
        SINH,           // _sinh hyperbolic sine
        COSH,           // _cosh hyperbolic cosine
        TANH,           // _tanh hyperbolic tangent
        
        // Integer and bitwise operations
        DIV,            // _div integer division
        AND,            // _and bitwise-and
        OR,             // _or bitwise-or
        XOR,            // _xor bitwise-xor
        NOT,            // _not bitwise-not
        ROT,            // _rot bitwise-rotation
        SHIFT,          // _shift bitwise-shift
        
        // Linear algebra operations
        DOT_PRODUCT,     // _dot dot product
        MUL,            // _mul matrix multiplication
        INV,            // _inv matrix inverse
        CEIL,           // _ceil ceiling function
        
        // System functions (missing from current implementation)
        TIME,           // _t current time
        DAYS,           // _T current time in days
        DRAW,           // _draw random number generation
        IN,             // _in search/find function
        BIN,            // _bin binary search
        BINL,           // _binl binary search each-left
        LSQ,            // _lsq least squares
        LIN,            // _lin list intersection
        GTIME,          // _gtime GMT time conversion
        LTIME,          // _ltime local time conversion
        LT,             // _lt local time
        JD,             // _jd Julian date
        DJ,             // _dj date from Julian
        VS,             // _vs vector from string
        SV,             // _sv string from vector
        SS,             // _ss string search
        SSR,            // _ssr string search and replace
        SM,             // _sm string match
        CI,             // _ci character from integer
        IC,             // _ic integer from character
        DV,             // _dv delete by value
        DVL,            // _dvl delete by value list
        DI,             // _di delete by index
        GETENV,         // _getenv get environment variable
        SETENV,         // _setenv set environment variable
        SIZE,           // _size file size
        DIRECTORY,      // _d directory operations
        
        // Niladic system variables
        VARIABLE,       // _v script name
        INDEX,          // _i command line args
        FUNCTION,       // _f current function
        SPACE,          // _s memory info
        HOST,           // _h hostname
        PORT,           // _p IPC port
        PID,            // _P process ID
        WHO,            // _w calling port
        USER,           // _u calling username
        ADDRESS,        // _a calling ipv4 address
        VERSION,        // _k version
        OS,             // _o operating system
        CORES,          // _c number of cores
        RAM,            // _r amount of RAM
        MACHID,         // _m machine ID
        STACK,          // _y stack trace
        BD,             // _bd bytes from data
        DB,             // _db data from bytes
        DO,             // _do control flow
        WHILE,          // _while control flow
        IF_FUNC,        // _if control flow
        EXIT,           // _exit control flow
        DISPOSE,        // _dispose object disposal
        GETHINT,        // _gethint get hint
        SETHINT,        // _sethint set hint
        PARSE,          // _parse verb token
        EVAL,           // _eval verb token
        EOF,
        UNKNOWN
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Lexeme { get; }
        public int Position { get; }

        public Token(TokenType type, string lexeme, int position)
        {
            Type = type;
            Lexeme = lexeme;
            Position = position;
        }

        public override string ToString()
        {
            return $"{Type}({Lexeme})";
        }
    }
}
