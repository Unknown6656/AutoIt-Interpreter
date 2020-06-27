﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

using Unknown6656.Mathematics.LinearAlgebra;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3
{
    public enum TelemetryCategory
    {
        ProgramRuntimeAndPrinting,
        ProgramRuntime,
        Printing,
        Exceptions,
        InterpreterInitialization,
        InterpreterRuntime,
        ResolveScript,
        ScanScript,
    }

    public sealed class Telemetry
    {
        private readonly List<(TelemetryCategory category, TimeSpan duration)> _recorded_timings = new();


        public ReadOnlyIndexer<TelemetryCategory, IEnumerable<TimeSpan>> Timings { get; }

        public ReadOnlyIndexer<TelemetryCategory, TimeSpan> TotalTime { get; }

        public ReadOnlyIndexer<TelemetryCategory, TimeSpan> AverageTime { get; }


        public Telemetry()
        {
            Timings = new(c => _recorded_timings.SelectWhere(t => t.category == c, t => t.duration));
            TotalTime = new(c => new TimeSpan(Timings[c].Sum(t => t.Ticks)));
            AverageTime = new(c => new TimeSpan((long)Timings[c].Average(t => t.Ticks)));
        }

        public void Reset() => _recorded_timings.Clear();

        public void Submit(TelemetryCategory category, long ticks) => _recorded_timings.Add((category, new TimeSpan(ticks)));

        public void Measure(TelemetryCategory category, Action function) => Measure<__empty>(category, delegate
        {
            function();

            return default;
        });

        public T Measure<T>(TelemetryCategory category, Func<T> function)
        {
            Stopwatch sw = new Stopwatch();
            T result;

            sw.Start();
            result = function();
            sw.Stop();

            Submit(category, sw.ElapsedTicks);

            return result;
        }

        public void Add(Telemetry other) => _recorded_timings.AddRange(other._recorded_timings);

        public void Print() => Program.PrintTelemetry(this);
    }

    public sealed class TelemetryNode
    {
        private readonly List<TelemetryNode> _children = new();

        public string Name { get; }
        public string Path => IsRoot ? Name : $"{Parent.Path}/{Name}";
        public bool IsRoot => ReferenceEquals(this, Root);
        public TelemetryNode Root { get; }
        public TelemetryNode Parent { get; }
        public TelemetryNode[] Children => _children.ToArray();

        public TimeSpan[] Timings { get; }
        public TimeSpan Average { get; }
        public TimeSpan Total { get; }
        public TimeSpan Min { get; }
        public TimeSpan Max { get; }
        // TODO : std dev?

        public double PercentageOfParent => (double)Total.Ticks / Parent.Total.Ticks;

        public double PercentageOfTotal => (double)Total.Ticks / Root.Total.Ticks;


        private TelemetryNode(TelemetryNode? parent, string name, TimeSpan[] timings)
        {
            Name = name;
            Parent = parent ?? this;
            Root = ReferenceEquals(this, Parent) ? this : Parent.Root;
            Timings = timings;

            if (timings.Length == 0)
                Total = Average = Min = Max = default;
            else
            {
                Min = timings.Min();
                Max = timings.Max();
                Total = new TimeSpan(timings.Sum(t => t.Ticks));
                Average = new TimeSpan((long)timings.Average(t => t.Ticks));
            }
        }

        public override string ToString() => $"{Path}: {Total}";

        public TelemetryNode AddChild(string name, TimeSpan[] timings)
        {
            TelemetryNode node = new TelemetryNode(this, name, timings);

            _children.Add(node);

            return node;
        }

        public static TelemetryNode FromTelemetry(Telemetry telemetry)
        {
            TimeSpan[] get_timings(TelemetryCategory cat) => telemetry.Timings[cat].ToArray();
            TelemetryNode root = new TelemetryNode(null, "Total", get_timings(TelemetryCategory.ProgramRuntimeAndPrinting));
            TelemetryNode nd_interpreter, nd_runtime;

            nd_interpreter = root.AddChild("Interpreter", get_timings(TelemetryCategory.ProgramRuntime));
            root.AddChild("Exceptions", get_timings(TelemetryCategory.Exceptions));
            root.AddChild("Printing", get_timings(TelemetryCategory.Printing));

            nd_interpreter.AddChild("Initialization", get_timings(TelemetryCategory.InterpreterInitialization));
            nd_runtime = nd_interpreter.AddChild("Runtime", get_timings(TelemetryCategory.InterpreterInitialization));
            
            nd_runtime.AddChild("Script Resolution", get_timings(TelemetryCategory.ResolveScript));
            nd_runtime.AddChild("Script Scan", get_timings(TelemetryCategory.ScanScript));
            // TODO

            return root;
        }
    }
}
