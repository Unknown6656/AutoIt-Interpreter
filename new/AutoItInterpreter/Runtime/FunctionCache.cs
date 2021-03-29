using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System;

using Unknown6656.AutoIt3.CLI;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class FunctionCache
    {
        private readonly Dictionary<AU3Function, List<(Variant[] Arguments, FunctionReturnValue ReturnValue)>> _cache = new();

        public Interpreter Interpreter { get; }


        internal FunctionCache(Interpreter interpreter) => Interpreter = interpreter;

        public void SetOrUpdate(AU3Function function, Variant[] arguments, FunctionReturnValue return_value) => Interpreter.Telemetry.Measure(TelemetryCategory.Au3CacheWrite, delegate
        {
            if (!_cache.TryGetValue(function, out List<(Variant[] args, FunctionReturnValue ret)>? entries))
                _cache[function] = entries = new();

            for (int i = 0; i < entries.Count; ++i)
                if (entries[i] is { args: { } args } entry && args.SequenceEqual(arguments))
                {
                    entry.ret = return_value;
                    entries[i] = entry;

                    MainProgram.PrintDebugMessage($"cache update: {function.Name}({arguments.StringJoin(", ")}) -> {return_value}");

                    return;
                }

            MainProgram.PrintDebugMessage($"new cache entry: {function.Name}({arguments.StringJoin(", ")}) -> {return_value}");

            entries.Add((arguments, return_value));
        });

        public bool TryFetch(AU3Function function, Variant[] arguments, [NotNullWhen(true)] out FunctionReturnValue? return_value) =>
            (return_value = Interpreter.Telemetry.Measure(TelemetryCategory.Au3CacheRead, delegate
            {
                if (_cache.TryGetValue(function, out List<(Variant[] args, FunctionReturnValue ret)>? entries))
                    for (int i = 0; i < entries.Count; ++i)
                        if (entries[i].args.SequenceEqual(arguments))
                        {
                            MainProgram.PrintDebugMessage($"cache hit: {function.Name}({arguments.StringJoin(", ")}) -> {entries[i].ret}");

                            return entries[i].ret;
                        }

                MainProgram.PrintDebugMessage($"cache miss: {function.Name}({arguments.StringJoin(", ")})");

                return null;
            })) is { };

        public override string ToString() => $"{_cache} function(s), {_cache.Values.Sum(e => e.Count)} total entries";
    }
}
