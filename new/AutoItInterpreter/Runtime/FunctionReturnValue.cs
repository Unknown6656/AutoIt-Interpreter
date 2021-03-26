using System.Diagnostics.CodeAnalysis;
using System;

using Unknown6656.AutoIt3.CLI;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    // TODO: SOME OF THIS COULD BE OBSOLETE WITH C#10'S DISCRIMINATED UNIONS

    public sealed class InterpreterError
    {
        public static InterpreterError Empty { get; } = new InterpreterError(SourceLocation.Unknown, "");

        public SourceLocation? Location { get; }
        public string Message { get; }


        public InterpreterError(SourceLocation? location, string message)
        {
            Location = location;
            Message = message;
        }

        public static InterpreterError WellKnown(SourceLocation? loc, string key, params object?[] args) => new(loc, MainProgram.LanguageLoader.CurrentLanguage?[key, args] ?? key);
    }

    public delegate FunctionReturnValue FunctionReturnValueDelegate(Variant @return, int? error, Variant? extended);

    public sealed class FunctionReturnValue
    {
        private readonly Union<InterpreterError, (Variant @return, int? error, Variant? extended)> _result;


        private FunctionReturnValue(InterpreterError error) => _result = error;

        internal FunctionReturnValue(Variant @return, int? error = null, Variant? extended = null)
        {
            if (extended is int && error is null)
                error = -1;

            _result = (@return, error, extended);
        }

        public override string ToString() => _result.Match(err => $"FATAL: {err}", val => val.error is int err ? $"ERROR: {err}" : $"SUCCESS: {val.@return}");

        public bool IsSuccess(out Variant value, out Variant? extended) => IsNonFatal(out value, out int? err, out extended) && err is null;

        public bool IsFatal([MaybeNullWhen(false), NotNullWhen(true)] out InterpreterError? error) => _result.Is(out error);

        public bool IsError(out int error) => IsError(out _, out error, out _);

        public bool IsError(out int error, out Variant? extended) => IsError(out _, out error, out extended);

        public bool IsError(out Variant value, out int error) => IsError(out value, out error, out _);

        public bool IsError(out Variant value, out int error, out Variant? extended)
        {
            bool res = IsNonFatal(out value, out int? err, out extended);

            if (err is null)
                res = false;

            error = err ?? 0;

            return res;
        }

        public bool IsNonFatal(out Variant @return, out int? error, out Variant? extended)
        {
            bool res = _result.Is(out (Variant @return, int? error, Variant? extended) tuple);

            (@return, error, extended) = tuple;

            return res;
        }

        public FunctionReturnValue IfNonFatal(FunctionReturnValueDelegate function) => _result.Match(Fatal, t => function(t.@return, t.error, t.extended));

        public FunctionReturnValue IfNonFatal(Func<Variant, FunctionReturnValue> function) => _result.Match(Fatal, t => function(t.@return));

        public static FunctionReturnValue Success(Variant value) => new(value);

        public static FunctionReturnValue Success(Variant value, Variant extended) => new(value, null, extended);

        public static FunctionReturnValue Fatal(InterpreterError error) => new(error);

        public static FunctionReturnValue Error(int error) => new(Variant.False, error);

        public static FunctionReturnValue Error(int error, Variant extended) => new(Variant.False, error, extended);

        public static FunctionReturnValue Error(Variant value, int error, Variant extended) => new(value, error, extended);

        public static implicit operator FunctionReturnValue(Variant v) => Success(v);

        public static implicit operator FunctionReturnValue(InterpreterError err) => Fatal(err);

        // public static implicit operator FunctionReturnValue(Union<InterpreterError, Variant> union) => union.Match(Fatal, Success);
    }
}
