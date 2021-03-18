using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class TimerManager
        : IDisposable
    {
        private readonly ConcurrentDictionary<ScriptFunction, int> _timers = new();


        public Interpreter Interpreter { get; }

        public bool IsActive { get; private set; }

        public ImmutableDictionary<ScriptFunction, int> ActiveTimers => _timers.ToImmutableDictionary();


        public TimerManager(Interpreter interpreter) => Interpreter = interpreter;

        public void Dispose()
        {
            StopAllTimers();
            UnregisterAllTimers();

            .// TODO
        }

        public override string ToString() => $"({(IsActive ? "Active" : "Paused")}) {_timers.Count} timers.";

        public void StartAllTimers()
        {
            IsActive = true;
        }

        public void StopAllTimers()
        {
            IsActive = false;
        }

        public void UnregisterAllTimers() => _timers.Keys.ToArray().Select(UnregisterTimer);

        public void RegisterOrUpdateTimer(ScriptFunction function, int interval);

        public bool UnregisterTimer(ScriptFunction function);

        public bool HasTimer(ScriptFunction function, out int interval);
    }
}
