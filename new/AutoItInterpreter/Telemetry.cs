using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System;

using Unknown6656.AutoIt3.Localization;
using Unknown6656.AutoIt3.CLI;

using Unknown6656.Mathematics.LinearAlgebra;
using Unknown6656.Common;

namespace Unknown6656.AutoIt3
{
    public enum TelemetryCategory
    {
        ProgramRuntimeAndPrinting,
        ProgramRuntime,
        ParseCommandLine,
        GithubUpdater,
        LoadLanguage,
        PerformanceMonitor,
        Printing,
        Warnings,
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
        TimedFunctions,
        OnAutoItStart,
        OnAutoItExit,
        Au3ScriptExecution,
        Au3CacheWrite,
        Au3CacheRead,
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
        MacroResolving,
        VariableResolving,
        VariableCreation,
        COMConnection,
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

        public void SubmitTimings(TelemetryCategory category, in TimeSpan span) => _recorded_timings.Add((category, span));

        public async Task MeasureAsync(TelemetryCategory category, Func<Task> function) => await MeasureAsync<__empty>(category, async delegate
        {
            await function();

            return default;
        });

        public async Task<T> MeasureAsync<T>(TelemetryCategory category, Func<Task<T>> function) => await Task.Factory.StartNew(delegate
        {
            Stopwatch sw = new();

            sw.Start();

            Task<T> task = function();
            TaskAwaiter<T> awaiter = task.GetAwaiter();
            T result = awaiter.GetResult();

            sw.Stop();

            SubmitTimings(category, sw.Elapsed);

            return result;
        });

        public void Measure(TelemetryCategory category, Action function) => Measure<__empty>(category, delegate
        {
            function();

            return default;
        });

        public T Measure<T>(TelemetryCategory category, Func<T> function)
        {
            Stopwatch sw = new();
            T result;

            sw.Start();
            result = function();
            sw.Stop();

            SubmitTimings(category, sw.Elapsed);

            return result;
        }

        public void Add(Telemetry other) => _recorded_timings.AddRange(other._recorded_timings);

        public async Task StartPerformanceMonitorAsync(Process process)
        {
            Stopwatch sw = new();
            TimeSpan ts_total, ts_user, ts_kernel;
            int cores = Environment.ProcessorCount;

            while (_run_performancemonitor)
            {
                sw.Restart();
                ts_total = process.TotalProcessorTime;
                ts_user = process.UserProcessorTime;
                ts_kernel = process.PrivilegedProcessorTime;

                await Task.Delay(_performance_measurements.Count switch
                {
                    < 200 => 20,
                    < 1000 => 30,
                    < 5000 => 50,
                    _ => 100,
                });

                Measure(TelemetryCategory.PerformanceMonitor, delegate
                {
                    ts_total = process.TotalProcessorTime - ts_total;
                    ts_user = process.UserProcessorTime - ts_user;
                    ts_kernel = process.PrivilegedProcessorTime - ts_kernel;
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

                    _performance_measurements.Add((DateTime.Now, cpu_total, cpu_user, cpu_kernel, process.PrivateMemorySize64));
                });
            }
        }

        public async Task StartPerformanceMonitorAsync()
        {
            if (_run_performancemonitor)
                _performance_measurements.Clear();

            _run_performancemonitor = true;

            using Process proc = Process.GetCurrentProcess();

            await StartPerformanceMonitorAsync(proc);
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
            LanguagePack pack = MainProgram.LanguageLoader.CurrentLanguage!;
            TelemetryTimingsNode root = new TelemetryTimingsNode(null, pack["debug.telemetry.categories.total"], get_timings(TelemetryCategory.ProgramRuntimeAndPrinting));
            TelemetryTimingsNode nd_interpreter, nd_runtime, nd_codeexec, nd_native, nd_init, nd_thread, nd_au3, nd_proc, nd_vars;

            nd_interpreter = root.AddChild(pack["debug.telemetry.categories.interpreter"], get_timings(TelemetryCategory.ProgramRuntime));
            root.AddChild(pack["debug.telemetry.categories.warnings"], get_timings(TelemetryCategory.Warnings));
            root.AddChild(pack["debug.telemetry.categories.exceptions"], get_timings(TelemetryCategory.Exceptions));
            root.AddChild(pack["debug.telemetry.categories.printing"], get_timings(TelemetryCategory.Printing));
            root.AddChild(pack["debug.telemetry.categories.perfmon"], get_timings(TelemetryCategory.PerformanceMonitor));

            nd_interpreter.AddChild(pack["debug.telemetry.categories.argument_parsing"], get_timings(TelemetryCategory.ParseCommandLine));
            nd_interpreter.AddChild(pack["debug.telemetry.categories.github_updater"], get_timings(TelemetryCategory.GithubUpdater));
            nd_interpreter.AddChild(pack["debug.telemetry.categories.load_lang_packs"], get_timings(TelemetryCategory.LoadLanguage));
            nd_init = nd_interpreter.AddChild(pack["debug.telemetry.categories.init"], get_timings(TelemetryCategory.InterpreterInitialization));
            nd_runtime = nd_interpreter.AddChild(pack["debug.telemetry.categories.runtime"], get_timings(TelemetryCategory.InterpreterRuntime));

            nd_init.AddChild(pack["debug.telemetry.categories.load_plugin"], get_timings(TelemetryCategory.LoadPluginFile));
            nd_init.AddChild(pack["debug.telemetry.categories.init_plugin"], get_timings(TelemetryCategory.LoadPlugin));
            nd_init.AddChild(pack["debug.telemetry.categories.build_parser"], get_timings(TelemetryCategory.ParserInitialization));

            nd_runtime.AddChild(pack["debug.telemetry.categories.com_connection"], get_timings(TelemetryCategory.COMConnection));
            nd_runtime.AddChild(pack["debug.telemetry.categories.script_res"], get_timings(TelemetryCategory.ResolveScript));
            nd_runtime.AddChild(pack["debug.telemetry.categories.script_scan"], get_timings(TelemetryCategory.ScanScript));
            nd_codeexec = nd_runtime.AddChild(pack["debug.telemetry.categories.script_exec"], get_timings(TelemetryCategory.ScriptExecution));

            nd_thread = nd_codeexec.AddChild(pack["debug.telemetry.categories.thread"], get_timings(TelemetryCategory.ThreadRun));
            nd_codeexec.AddChild(pack["debug.telemetry.categories.timed"], get_timings(TelemetryCategory.TimedFunctions));
            nd_codeexec.AddChild(pack["debug.telemetry.categories.start"], get_timings(TelemetryCategory.OnAutoItStart));
            nd_codeexec.AddChild(pack["debug.telemetry.categories.exit"], get_timings(TelemetryCategory.OnAutoItExit));

            nd_au3 = nd_thread.AddChild(pack["debug.telemetry.categories.au3"], get_timings(TelemetryCategory.Au3ScriptExecution));
            nd_native = nd_thread.AddChild(pack["debug.telemetry.categories.native"], get_timings(TelemetryCategory.NativeScriptExecution));

            nd_native.AddChild(pack["debug.telemetry.categories.stdout"], get_timings(TelemetryCategory.ScriptConsoleOut));
            nd_native.AddChild(pack["debug.telemetry.categories.stdin"], get_timings(TelemetryCategory.ScriptConsoleIn));

            nd_au3.AddChild(pack["debug.telemetry.categories.cache_read"], get_timings(TelemetryCategory.Au3CacheRead));
            nd_au3.AddChild(pack["debug.telemetry.categories.cache_write"], get_timings(TelemetryCategory.Au3CacheWrite));
            nd_au3.AddChild(pack["debug.telemetry.categories.expr_eval"], get_timings(TelemetryCategory.EvaluateExpression));
            nd_au3.AddChild(pack["debug.telemetry.categories.const_folding"], get_timings(TelemetryCategory.ExpressionCleanup));
            nd_au3.AddChild(pack["debug.telemetry.categories.macros"], get_timings(TelemetryCategory.MacroResolving));
            nd_vars = nd_au3.AddChild(pack["debug.telemetry.categories.variables"], get_timings(TelemetryCategory.VariableResolving, TelemetryCategory.VariableCreation));
            nd_proc = nd_au3.AddChild(pack["debug.telemetry.categories.line_proc"], get_timings(TelemetryCategory.ProcessLine));

            nd_vars.AddChild(pack["debug.telemetry.categories.resolving"], get_timings(TelemetryCategory.VariableResolving));
            nd_vars.AddChild(pack["debug.telemetry.categories.creation"], get_timings(TelemetryCategory.VariableCreation));

            nd_proc.AddChild(pack["debug.telemetry.categories.directives"], get_timings(TelemetryCategory.ProcessDirective));
            nd_proc.AddChild(pack["debug.telemetry.categories.ctrl_statements"], get_timings(TelemetryCategory.ProcessStatement));
            nd_proc.AddChild(pack["debug.telemetry.categories.expr_statements"], get_timings(TelemetryCategory.ProcessExpressionStatement));
            nd_proc.AddChild(pack["debug.telemetry.categories.decl_statements"], get_timings(TelemetryCategory.ProcessDeclaration));
            nd_proc.AddChild(pack["debug.telemetry.categories.assg_statements"], get_timings(TelemetryCategory.ProcessAssignment));
            nd_proc.AddChild(pack["debug.telemetry.categories.expressions"], get_timings(TelemetryCategory.ProcessExpression));
            nd_proc.AddChild(pack["debug.telemetry.categories.external_proc"], get_timings(TelemetryCategory.ExternalProcessor));

            return root;
        }
    }
}
