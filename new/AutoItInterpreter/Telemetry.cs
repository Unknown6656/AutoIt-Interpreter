using System.Collections.Generic;
using System.Threading.Tasks;
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
        PerformanceMonitor,
        Printing,
        Exceptions,
        InterpreterInitialization,
        LoadPluginFile,
        LoadPlugin,
        ParserInitialization,
        InterpreterRuntime,
        ScanScript,
        ResolveScript,
        ScriptExecution,
        ThreadRun,
        OnAutoItStart,
        OnAutoItExit,
        Au3ScriptExecution,
        NativeScriptExecution,
        ScriptConsoleOut,
        ScriptConsoleIn,
        ProcessLine,
        ProcessDirective,
        ProcessStatement,
        ProcessExpressionStatement,
        ProcessDeclaration,
        ProcessExpression,
        ProcessAssignment,
        EvaluateExpression,
        ExternalProcessor,
        ExpressionCleanup,
        VariableResolution,
        VariableCreation,
    }

    public sealed class Telemetry
    {
        private readonly List<(DateTime timestamp, double cpu_total, double cpu_user, double cpu_kernel, long ram_used)> _performance_measurements = new();
        private readonly List<(TelemetryCategory category, TimeSpan duration)> _recorded_timings = new();
        private volatile bool _run_performancemonitor;


        public (DateTime Timestamp, double TotalCPU, double UserCPU, double KernelCPU, long RAMUsed)[] PerformanceMeasurements => _performance_measurements.ToArray();

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

        // TODO : other stuff, such as exceptions, mem usage, cpu usage, etc.

        public void SubmitTimings(TelemetryCategory category, long ticks) => _recorded_timings.Add((category, new TimeSpan(ticks)));

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

            SubmitTimings(category, sw.ElapsedTicks);

            return result;
        }

        public void Add(Telemetry other) => _recorded_timings.AddRange(other._recorded_timings);

        public async Task StartPerformanceMonitorAsync()
        {
            if (_run_performancemonitor)
                _performance_measurements.Clear();

            _run_performancemonitor = true;

            using Process proc = Process.GetCurrentProcess();
            Stopwatch sw = new Stopwatch();
            TimeSpan ts_total, ts_user, ts_kernel;
            int cores = Environment.ProcessorCount;

            while (_run_performancemonitor)
            {
                sw.Restart();
                ts_total = proc.TotalProcessorTime;
                ts_user = proc.UserProcessorTime;
                ts_kernel = proc.PrivilegedProcessorTime;

                await Task.Delay(_performance_measurements.Count switch
                {
                    < 200 => 20,
                    < 1000 => 30,
                    < 5000 => 50,
                    _ => 100,
                });

                Measure(TelemetryCategory.PerformanceMonitor, delegate
                {
                    ts_total = proc.TotalProcessorTime - ts_total;
                    ts_user = proc.UserProcessorTime - ts_user;
                    ts_kernel = proc.PrivilegedProcessorTime - ts_kernel;
                    sw.Stop();

                    double cpu_total = (double)ts_total.Ticks / sw.ElapsedTicks / cores;
                    double cpu_user = (double)ts_user.Ticks / sw.ElapsedTicks / cores;
                    double cpu_kernel = (double)ts_kernel.Ticks / sw.ElapsedTicks / cores;

                    if (!double.IsFinite(cpu_user))
                        cpu_user = 0;

                    if (!double.IsFinite(cpu_kernel))
                        cpu_kernel = 0;

                    if (!double.IsFinite(cpu_total) || cpu_total < cpu_user + cpu_kernel)
                        cpu_total = cpu_user + cpu_kernel;
                    
                    _performance_measurements.Add((DateTime.Now, cpu_total, cpu_user, cpu_kernel, proc.PrivateMemorySize64));
                });
            }
        }

        public void StopPerformanceMonitor() => _run_performancemonitor = false;
    }

    public sealed class TelemetryTimingsNode
    {
        private readonly List<TelemetryTimingsNode> _children = new();

        public string Name { get; }
        public string Path => IsRoot ? Name : $"{Parent.Path}/{Name}";
        public bool IsRoot => ReferenceEquals(this, Root);
        public bool IsHot => !IsRoot && PercentageOfParent > .1 && PercentageOfTotal > .01 && Siblings.All(s => s.PercentageOfParent < PercentageOfParent);
        public TelemetryTimingsNode Root { get; }
        public TelemetryTimingsNode Parent { get; }
        public TelemetryTimingsNode[] Children => _children.ToArray();
        public TelemetryTimingsNode[] Siblings => IsRoot ? Array.Empty<TelemetryTimingsNode>() : Parent._children.Except(new[] { this }).ToArray();
        public TimeSpan[] Timings { get; }
        public TimeSpan Average { get; }
        public TimeSpan Total { get; }
        public TimeSpan Min { get; }
        public TimeSpan Max { get; }
        // TODO : std dev?

        public double PercentageOfParent => (double)Total.Ticks / Parent.Total.Ticks;

        public double PercentageOfTotal => (double)Total.Ticks / Root.Total.Ticks;


        private TelemetryTimingsNode(TelemetryTimingsNode? parent, string name, TimeSpan[] timings)
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

        public TelemetryTimingsNode AddChild(string name, TimeSpan[] timings)
        {
            TelemetryTimingsNode node = new TelemetryTimingsNode(this, name, timings);

            _children.Add(node);

            return node;
        }

        public static TelemetryTimingsNode FromTelemetry(Telemetry telemetry)
        {
            TimeSpan[] get_timings(params TelemetryCategory[] cat) => cat.SelectMany(c => telemetry.Timings[c]).ToArray();
            TelemetryTimingsNode root = new TelemetryTimingsNode(null, "Total", get_timings(TelemetryCategory.ProgramRuntimeAndPrinting));
            TelemetryTimingsNode nd_interpreter, nd_runtime, nd_codeexec, nd_native, nd_init, nd_thread, nd_au3, nd_proc, nd_vars;

            nd_interpreter = root.AddChild("Interpreter", get_timings(TelemetryCategory.ProgramRuntime));
            root.AddChild("Exceptions", get_timings(TelemetryCategory.Exceptions));
            root.AddChild("Printing", get_timings(TelemetryCategory.Printing));
            root.AddChild("Performance Monitoring", get_timings(TelemetryCategory.PerformanceMonitor));

            nd_init = nd_interpreter.AddChild("Initialization", get_timings(TelemetryCategory.InterpreterInitialization));
            nd_runtime = nd_interpreter.AddChild("Runtime", get_timings(TelemetryCategory.InterpreterRuntime));

            nd_init.AddChild("Load Plugin DLL", get_timings(TelemetryCategory.LoadPluginFile));
            nd_init.AddChild("Plugin Initialization", get_timings(TelemetryCategory.LoadPlugin));
            nd_init.AddChild("Parser Construction", get_timings(TelemetryCategory.ParserInitialization));

            nd_runtime.AddChild("Script Resolution", get_timings(TelemetryCategory.ResolveScript));
            nd_runtime.AddChild("Script Scan", get_timings(TelemetryCategory.ScanScript));
            nd_codeexec = nd_runtime.AddChild("Script Execution", get_timings(TelemetryCategory.ScriptExecution));

            nd_thread = nd_codeexec.AddChild("Run/Start Thread", get_timings(TelemetryCategory.ThreadRun));

            nd_codeexec.AddChild("On Start", get_timings(TelemetryCategory.OnAutoItStart));
            nd_codeexec.AddChild("On Exit", get_timings(TelemetryCategory.OnAutoItExit));
            nd_au3 = nd_thread.AddChild("Au3", get_timings(TelemetryCategory.Au3ScriptExecution));
            nd_native = nd_thread.AddChild("Native", get_timings(TelemetryCategory.NativeScriptExecution));

            nd_native.AddChild("Console Out", get_timings(TelemetryCategory.ScriptConsoleOut));
            nd_native.AddChild("Console In", get_timings(TelemetryCategory.ScriptConsoleIn));

            nd_au3.AddChild("Expression Evaluation", get_timings(TelemetryCategory.EvaluateExpression));
            nd_au3.AddChild("Constant Folding", get_timings(TelemetryCategory.ExpressionCleanup));
            nd_vars = nd_au3.AddChild("Variables", get_timings(TelemetryCategory.VariableResolution, TelemetryCategory.VariableCreation));
            nd_proc = nd_au3.AddChild("Line Processing", get_timings(TelemetryCategory.ProcessLine));

            nd_vars.AddChild("Resolution", get_timings(TelemetryCategory.VariableResolution));
            nd_vars.AddChild("Creation", get_timings(TelemetryCategory.VariableCreation));

            nd_proc.AddChild("Directives", get_timings(TelemetryCategory.ProcessDirective));
            nd_proc.AddChild("Control Statements", get_timings(TelemetryCategory.ProcessStatement));
            nd_proc.AddChild("Expression Statements", get_timings(TelemetryCategory.ProcessExpressionStatement));
            nd_proc.AddChild("Declaration Statements", get_timings(TelemetryCategory.ProcessDeclaration));
            nd_proc.AddChild("Assginment Statements", get_timings(TelemetryCategory.ProcessAssignment));
            nd_proc.AddChild("Expressions", get_timings(TelemetryCategory.ProcessExpression));
            nd_proc.AddChild("External Processing", get_timings(TelemetryCategory.ExternalProcessor));


            return root;
        }
    }
}
