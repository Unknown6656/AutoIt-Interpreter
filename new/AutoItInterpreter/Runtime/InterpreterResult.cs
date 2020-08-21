using System;
using System.Diagnostics.CodeAnalysis;

using Unknown6656.AutoIt3.CLI;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    // TODO: SOME OF THIS COULD BE OBSOLETE WITH C#10'S DISCRIMINATED UNIONS


    public sealed class InterpreterResult
    {
        public static InterpreterResult OK { get; } = new InterpreterResult(0, null);

        public int ProgramExitCode { get; }

        public InterpreterError? OptionalError { get; }

        public bool IsOK => OptionalError is null && ProgramExitCode == 0;


        public InterpreterResult(int programExitCode, InterpreterError? err = null)
        {
            ProgramExitCode = programExitCode;
            OptionalError = err;
        }

        public static implicit operator InterpreterResult?(InterpreterError? err) => err is null ? null : new InterpreterResult(-1, err);
    }

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

        public static InterpreterError WellKnown(SourceLocation? loc, string key, params object?[] args) => new InterpreterError(loc, MainProgram.LanguageLoader.CurrentLanguage?[key, args] ?? key);
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

        public bool IsSuccess(out Variant value, out Variant? extended) => Is(out value, out int? err, out extended) && err is null;

        public bool IsFatal([MaybeNullWhen(false), NotNullWhen(true)] out InterpreterError? error) => _result.Is(out error);

        public bool IsError(out int error) => IsError(out _, out error, out _);

        public bool IsError(out int error, out Variant? extended) => IsError(out _, out error, out extended);

        public bool IsError(out Variant value, out int error) => IsError(out value, out error, out _);

        public bool IsError(out Variant value, out int error, out Variant? extended)
        {
            bool res = Is(out value, out int? err, out extended);

            if (err is null)
                res = false;

            error = err ?? 0;

            return res;
        }

        public FunctionReturnValue IfNonFatal(FunctionReturnValueDelegate function) => _result.Match(Fatal, t => function(t.@return, t.error, t.extended));

        public FunctionReturnValue IfNonFatal(Func<Variant, FunctionReturnValue> function) => _result.Match(Fatal, t => function(t.@return));

        private bool Is(out Variant @return, out int? error, out Variant? extended)
        {
            bool res = _result.Is(out (Variant @return, int? error, Variant? extended) tuple);

            (@return, error, extended) = tuple;

            return res;
        }

        public static FunctionReturnValue Success(Variant value) => new FunctionReturnValue(value);

        public static FunctionReturnValue Success(Variant value, Variant extended) => new FunctionReturnValue(value, null, extended);

        public static FunctionReturnValue Fatal(InterpreterError error) => new FunctionReturnValue(error);

        public static FunctionReturnValue Error(int error) => new FunctionReturnValue(Variant.False, error);

        public static FunctionReturnValue Error(int error, Variant extended) => new FunctionReturnValue(Variant.False, error, extended);

        public static FunctionReturnValue Error(Variant value, int error, Variant extended) => new FunctionReturnValue(value, error, extended);

        public static implicit operator FunctionReturnValue(Variant v) => Success(v);

        public static implicit operator FunctionReturnValue(InterpreterError err) => Fatal(err);

        // public static implicit operator FunctionReturnValue(Union<InterpreterError, Variant> union) => union.Match(Fatal, Success);
    }
}
