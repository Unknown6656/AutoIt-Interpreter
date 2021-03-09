//#define USE_VS_DEBUGGER

using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.Localization;
#if USE_VS_DEBUGGER
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using EnvDTE;

using DTEProcess = EnvDTE.Process;
#endif
using Process = System.Diagnostics.Process;

namespace Unknown6656.AutoIt3.Runtime.ExternalServices
{
    /*
        ARGUMENTS:
            <data-pipe> <debugging-pipe> <lang-path> [...]
     */
    public abstract class ExternalServiceProvider<@this>
        where @this : ExternalServiceProvider<@this>, new()
    {
        private StreamWriter? _debug_writer = null;
        private volatile bool _running = true;

#pragma warning disable CS8618 // Non-nullable field is uninitialized
        protected BinaryReader DataReader { get; private set; }

        protected BinaryWriter DataWriter { get; private set; }
#pragma warning restore CS8618
        public LanguagePack? UILanguage { get; init; }


        protected abstract void OnStartup(string[] argv);

        protected abstract void MainLoop(ref bool shutdown);

        protected abstract void OnShutdown(bool external_request);

        public void DebugPrint(string key, params object?[] args)
        {
            if (_debug_writer is { } wr)
                try
                {
                    string message = UILanguage?[key, args].Trim() ?? key;

                    wr.Write(message);
                    wr.Flush();
                }
                catch
                {
                }
        }

        protected static int Run<T>(string[] argv)
            where T : ExternalServiceProvider<T>, new()
        {
            if (argv.Length < 3)
                return -1;

            string pipe_idata = argv[0];
            string pipe_odata = argv[1];
            string pipe_debug = argv[2];

            LanguageLoader loader = new LanguageLoader();

            loader.LoadLanguagePackFromYAMLFile(new FileInfo(argv[3]));

            T provider = new T { UILanguage = loader.CurrentLanguage };
            int exitcode;

            try
            {
                using Task debug_task = Task.Factory.StartNew(async delegate
                {
                    using AnonymousPipeClientStream debug = new AnonymousPipeClientStream(PipeDirection.Out, pipe_debug);

                    provider._debug_writer = new StreamWriter(debug) { AutoFlush = true };

                    while (provider._running)
                        await Task.Delay(100);
                });
                using AnonymousPipeClientStream pipe_in = new AnonymousPipeClientStream(PipeDirection.In, pipe_idata);
                using AnonymousPipeClientStream pipe_out = new AnonymousPipeClientStream(PipeDirection.Out, pipe_odata);
                using BinaryReader reader = new BinaryReader(pipe_in);
                using BinaryWriter writer = new BinaryWriter(pipe_out);

                provider.DataWriter = writer;
                provider.DataReader = reader;
                provider.DebugPrint("debug.external.server.started", pipe_idata, pipe_odata, pipe_debug);
                provider.OnStartup(argv.Skip(3).ToArray());

                bool external = true;
                bool shutdown = false;

                while (provider._running && pipe_in.IsConnected)
                {
                    provider.MainLoop(ref shutdown);

                    if (shutdown)
                    {
                        external = false;
                        provider._running = false;
                    }
                }

                provider.DebugPrint("debug.external.server.stopped", pipe_idata, pipe_odata, pipe_debug);
                provider.OnShutdown(external);

                exitcode = 0;
            }
            catch (Exception ex)
            {
                exitcode = ex.HResult;

                StringBuilder sb = new StringBuilder();

                while (ex is { })
                {
                    sb.Insert(0, $"[{ex.GetType()}] \"{ex.Message}\":\n{ex.StackTrace}\n");
                    ex = ex.InnerException;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(sb.ToString());
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (exitcode != 0)
                try
                {
                    provider.OnShutdown(true);
                }
                catch
                {
                }

            Environment.Exit(exitcode);

            return exitcode;
        }
    }

    public interface IDebugPrintingService
    {
        void Print(string channel, string message);
    }

    public abstract class ExternalServiceConnector<@this>
        : IDisposable
        where @this : ExternalServiceConnector<@this>
    {
        private readonly AnonymousPipeServerStream _pipe_debug;
        private readonly AnonymousPipeServerStream _pipe_idata;
        private readonly AnonymousPipeServerStream _pipe_odata;
        private readonly Task? _process_monitor;
        private readonly Task? _debug_monitor;

        protected BinaryReader DataReader { get; }

        protected BinaryWriter DataWriter { get; }

        public abstract string ChannelName { get; }

        protected IDebugPrintingService Printer { get; }

        protected LanguagePack UILanguage { get; }

        public Process? ServerProcess { get; }


        protected ExternalServiceConnector(FileInfo server, bool use_dotnet, IDebugPrintingService printer, LanguagePack ui_language)
        {
            Printer = printer;
            UILanguage = ui_language;

            DebugPrint("debug.external.starting", server.FullName);

            _pipe_debug = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            _pipe_idata = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            _pipe_odata = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);

            string name_debug = _pipe_debug.GetClientHandleAsString();
            string name_idata = _pipe_idata.GetClientHandleAsString();
            string name_odata = _pipe_odata.GetClientHandleAsString();

            try
            {
                string args = $"{name_odata} {name_idata} {name_debug} \"{ui_language.FilePath}\"";
                string server_path = Path.GetFullPath(server.FullName);
                ProcessStartInfo psi = use_dotnet ? new ProcessStartInfo("dotnet", $"\"{server_path}\" {args}") : new ProcessStartInfo(server_path, args);

                ServerProcess = Process.Start(psi);

                _process_monitor = Task.Factory.StartNew(ProcessMonitorTask);
                _debug_monitor = Task.Factory.StartNew(delegate
                {
                    using StreamReader _debug_reader = new StreamReader(_pipe_debug);

                    while (ServerProcess?.HasExited is false)
                        Printer.Print(ChannelName + "-Provider", _debug_reader.ReadLine().Trim());

                    _debug_reader.Close();
                });

                _pipe_debug.DisposeLocalCopyOfClientHandle();
                _pipe_idata.DisposeLocalCopyOfClientHandle();
                _pipe_odata.DisposeLocalCopyOfClientHandle();

                DataReader = new BinaryReader(_pipe_idata);
                DataWriter = new BinaryWriter(_pipe_odata);

                DebugPrint("debug.external.started", server.FullName, name_odata, name_idata, name_debug, ServerProcess.Id);
#if USE_VS_DEBUGGER
                if (System.Diagnostics.Debugger.IsAttached && Environment.OSVersion.Platform is PlatformID.Win32NT)
                    AttachVSDebugger();
#endif
            }
            catch
            {
                Stop(true);
            }
        }

        ~ExternalServiceConnector() => Dispose(false);
#if USE_VS_DEBUGGER
        private void AttachVSDebugger()
        {
            using Process vs_current = Process.GetCurrentProcess();

            try
            {
                if (VisualStudioAttacher.GetAttachedVisualStudio(vs_current) is (Process vs_proc, _DTE vs_inst))
                    using (vs_proc)
                    {
                        VisualStudioAttacher.AttachVisualStudioToProcess(vs_inst, ServerProcess!);

                        DebugPrint("debug.external.vsdbg_attached");

                        return;
                    }
            }
            catch
            {
                DebugPrint("debug.external.vsdbg_error");
            }
        }
#endif
        private async Task ProcessMonitorTask()
        {
            while (ServerProcess?.HasExited is false)
                await Task.Delay(100);

            Stop(true);
        }

        public void DebugPrint(string key, params object?[] args) => Printer.Print(ChannelName + "-Connector", UILanguage[key, args]);

        protected abstract void BeforeShutdown();

        public void Stop(bool force)
        {
            try
            {
                if (force || !(ServerProcess?.HasExited is true))
                {
                    DebugPrint("debug.external.stopping", ServerProcess?.Id);

                    Try(BeforeShutdown);
                    Try(delegate
                    {
                        DataWriter?.Flush();
                        _pipe_odata?.Flush();
                    });
                    Try(() => ServerProcess?.WaitForExit(1000));
                    Try(delegate
                    {
                        DataWriter?.Close();
                        DataReader?.Close();
                    });

                    if (_pipe_odata is { })
                        Try(_pipe_odata.Close);

                    if (_pipe_idata is { })
                        Try(_pipe_idata.Close);

                    if (force)
                    {
                        Try(() => ServerProcess?.Kill());
                        // Try(delegate
                        // {
                        //     _debug_monitor?.Wait();
                        //     _process_monitor?.Wait();
                        // });
                    }

                    DebugPrint("debug.external.stopped");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void Dispose(bool disposing)
        {
            Stop(!disposing);

            DebugPrint("debug.external.disposed");

            Try(() => ServerProcess?.Kill());
            Try(() => ServerProcess?.Dispose());
            DataWriter?.Dispose();
            DataReader?.Dispose();
            _pipe_idata?.Dispose();
            _pipe_odata?.Dispose();
            _pipe_debug?.Dispose();

            Try(delegate
            {
                _debug_monitor?.Wait();
                _debug_monitor?.Dispose();
            });
            Try(delegate
            {
                _process_monitor?.Wait();
                _process_monitor?.Dispose();
            });
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        private static void Try(Action a)
        {
            try
            {
                a();
            }
            catch
            {
            }
        }
    }

#if USE_VS_DEBUGGER
    public static class VisualStudioAttacher
    {
        private const string DEVENV_NAME = "devenv";
        private const string DEVENV_RNAME = "!VisualStudio";

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        public static string? GetSolutionForVisualStudio(Process vs_process)
        {
            if (TryGetVsInstance(vs_process.Id, out _DTE? instance))
                try
                {
                    return instance?.Solution.FullName;
                }
                catch
                {
                }

            return null;
        }

        public static (Process VisualStudioProcess, _DTE VisualStudioInstance)? GetAttachedVisualStudio(Process app_process)
        {
            foreach (Process vs_process in GetVisualStudioProcesses())
                if (TryGetVsInstance(vs_process.Id, out _DTE? instance) && instance?.Debugger.DebuggedProcesses is Processes processes)
                    foreach (DTEProcess? dbg_process in processes)
                        if (dbg_process?.ProcessID == app_process.Id)
                            return (vs_process, instance);

            return null;
        }

        public static void AttachVisualStudioToProcess(_DTE vs_instance, Process target_process)
        {
            DTEProcess? proc = vs_instance.Debugger.LocalProcesses.Cast<DTEProcess>().FirstOrDefault(p => p.ProcessID == target_process.Id);

            if (proc is { })
                proc.Attach();
            else
                throw new InvalidOperationException($"Visual Studio process cannot find specified application '{target_process.Id}'");
        }

        public static Process? GetVisualStudioForSolutions(List<string> solutionNames)
        {
            foreach (string solution in solutionNames)
                if (GetVisualStudioForSolution(solution) is Process proc)
                    return proc;

            return null;
        }

        public static Process? GetVisualStudioForSolution(string solutionName)
        {
            foreach (Process proc in GetVisualStudioProcesses())
                if (TryGetVsInstance(proc.Id, out _DTE? instance))
                    try
                    {
                        string name = Path.GetFileName(instance?.Solution.FullName);

                        if (string.Equals(name, solutionName, StringComparison.InvariantCultureIgnoreCase))
                            return proc;
                    }
                    catch
                    {
                    }

            return null;
        }

        private static IEnumerable<Process> GetVisualStudioProcesses() => Process.GetProcesses().Where(o => o.ProcessName.Contains(DEVENV_NAME));

        private static bool TryGetVsInstance(int processId, out _DTE? instance)
        {
            IMoniker[] monikers = new IMoniker[1];

            GetRunningObjectTable(0, out IRunningObjectTable runningObjectTable);

            runningObjectTable.EnumRunning(out IEnumMoniker monikerEnumerator);
            monikerEnumerator.Reset();

            while (monikerEnumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                CreateBindCtx(0, out IBindCtx ctx);

                monikers[0].GetDisplayName(ctx, null, out string running_name);
                runningObjectTable.GetObject(monikers[0], out object? running_obj);

                if (running_obj is _DTE && running_name.StartsWith(DEVENV_RNAME))
                    if (int.Parse(running_name.Split(':')[1]) == processId)
                    {
                        instance = (_DTE)running_obj;

                        return true;
                    }
            }

            instance = null;

            return false;
        }
    }
#endif
}
