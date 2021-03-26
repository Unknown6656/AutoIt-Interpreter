using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System;

using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class TimerManager
        : IDisposable
    {
        private readonly ConcurrentDictionary<ScriptFunction, (int Interval, AU3Thread Thread, Task<FunctionReturnValue> Task)> _timers = new();
        private volatile bool _active = false;
        private ScriptFunction? _last = null;


        public Interpreter Interpreter { get; }

        public bool IsActive => _active;

        public ScriptFunction? MostRecentRegistration => _last;

        public int ActiveTimerCount => _timers.Count;

        public ImmutableDictionary<ScriptFunction, int> ActiveTimers => _timers.ToImmutableDictionary(t => t.Key, t => t.Value.Interval);


        public TimerManager(Interpreter interpreter) => Interpreter = interpreter;

        public void Dispose()
        {
            StopAllTimers();
            UnregisterAllTimers();
        }

        public override string ToString() => $"({(_active ? "Active" : "Paused")}) {_timers.Count} timers.";

        public void StartAllTimers() => _active = true;

        public void StopAllTimers() => _active = false;

        public void UnregisterAllTimers() => _timers.Keys.ToArray().Select(UnregisterTimer);

        public void RegisterOrUpdateTimer(ScriptFunction function, int interval)
        {
            interval = Math.Max(interval, 1);

            if (_timers.TryGetValue(function, out (int, AU3Thread thread, Task<FunctionReturnValue> task) value))
                _timers[function] = (interval, value.thread, value.task);
            else
            {
                AU3Thread thread = Interpreter.CreateNewThread();
                NativeFunction loop = NativeFunction.FromDelegate(Interpreter, frame =>
                {
                    FunctionReturnValue result = Variant.Zero;

                    while (_timers.TryGetValue(function, out (int Interval, AU3Thread, Task<FunctionReturnValue>) value))
                    {
                        if (_active)
                            result = frame.Interpreter.Telemetry.Measure(TelemetryCategory.TimedFunctions, () => frame.Call(function, Array.Empty<Variant>()));

                        if (result.IsFatal(out _))
                            break;

                        Thread.Sleep(value.Interval);
                    }

                    return result;
                });

                var tuple = _timers[function] = (interval, thread, null)!;

                tuple.Task = thread.RunAsync(loop, Array.Empty<Variant>(), InterpreterRunContext.Interactive);
                _timers[function] = tuple;
            }

            _last = function;
        }

        public bool UnregisterTimer(ScriptFunction function)
        {
            if (_timers.TryRemove(function, out (int, AU3Thread thread, Task<FunctionReturnValue> task) value))
            {
                value.thread.Stop();
                value.task?.Wait();

                return true;
            }
            else
                return false;
        }

        public bool HasTimer(ScriptFunction function, out int interval)
        {
            bool result = _timers.TryGetValue(function, out (int ival, AU3Thread, Task<FunctionReturnValue>) tuple);

            interval = tuple.ival;

            return result;
        }
    }
}
