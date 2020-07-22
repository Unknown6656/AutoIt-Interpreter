using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Text;
using System;

using Unknown6656.AutoIt3.Localization;

using EnvDTE;

using DTEProcess = EnvDTE.Process;
using Process = System.Diagnostics.Process;

namespace Unknown6656.AutoIt3.Runtime.ExternalServices
{
    /*
        ARGUMENTS:
            <data-pipe> <debugging-pipe> <lang-path> [...]
     */
    public abstract class ExternalServiceProvider <@this>
        where @this : ExternalServiceProvider<@this>, new()
    {
        private BinaryWriter? _debug_writer = null;
        private volatile bool _running = true;

#pragma warning disable CS8618 // Non-nullable field is uninitialized
        protected BinaryReader DataReader { get; private set; }

        protected BinaryWriter DataWriter { get; private set; }
#pragma warning restore CS8618
        public LanguagePack? UILanguage { get; init; }

        public abstract string ChannelName { get; }


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

            string pipe_data = argv[0];
            string pipe_debug = argv[1];

            LanguageLoader loader = new LanguageLoader();

            loader.LoadLanguagePackFromYAMLFile(new FileInfo(argv[2]));

            T provider = new T { UILanguage = loader.CurrentLanguage };
            int exitcode;

            try
            {
                using Task debug_task = Task.Factory.StartNew(async delegate
                {
                    using NamedPipeClientStream debug = new NamedPipeClientStream(argv[1]);

                    await debug.ConnectAsync();

                    provider._debug_writer = new BinaryWriter(debug);

                    while (provider._running)
                        await Task.Delay(100);

                    provider._debug_writer.Close();
                    provider._debug_writer.Dispose();
                    provider._debug_writer = null;
                    debug.Close();
                    debug.Dispose();
                });
                using NamedPipeServerStream server = new NamedPipeServerStream(argv[0]);

                server.WaitForConnection();

                using BinaryReader reader = new BinaryReader(server);
                using BinaryWriter writer = new BinaryWriter(server);

                provider.DataWriter = writer;
                provider.DataReader = reader;
                provider.DebugPrint("debug.external.server.started");
                provider.OnStartup(argv.Skip(3).ToArray());

                bool external = true;

                while (provider._running && server.IsConnected)
                {
                    bool shutdown = false;

                    provider.MainLoop(ref shutdown);

                    if (shutdown)
                    {
                        external = false;
                        provider._running = false;
                    }
                }

                provider.DebugPrint("debug.external.server.stopped");
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

            provider._debug_writer?.Flush();
            provider._debug_writer?.Close();
            provider._debug_writer?.Dispose();
            provider._debug_writer = null;

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
        private readonly NamedPipeClientStream _client;
        private readonly Task _debug_monitor;

        protected BinaryReader DataReader { get; }

        protected BinaryWriter DataWriter { get; }

        public abstract string ChannelName { get; }

        protected IDebugPrintingService Printer { get; }

        protected LanguagePack UILanguage { get; }

        public Process ServerProcess { get; }

        public string PipeName { get; }


        protected ExternalServiceConnector(FileInfo server, IDebugPrintingService printer, LanguagePack ui_language)
        {
            Printer = printer;
            UILanguage = ui_language;

            DebugPrint("debug.external.starting");

            PipeName = $"__autoit3__{Guid.NewGuid():N}";
            ServerProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Arguments = $"{PipeName} {PipeName}D \"{ui_language.FilePath}\"",
                    FileName = Path.GetFullPath(server.FullName),
                }
            };
            ServerProcess.Start();
            _debug_monitor = Task.Factory.StartNew(async () => await DebugMonitorTask(PipeName + 'D'));
            _client = new NamedPipeClientStream(PipeName);
            _client.Connect();
            DataReader = new BinaryReader(_client);
            DataWriter = new BinaryWriter(_client);

            DebugPrint("debug.external.started", PipeName, ServerProcess.Id);
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached && Environment.OSVersion.Platform is PlatformID.Win32NT)
                AttachVSDebugger();
        }

        private void AttachVSDebugger()
        {
            using Process vs_current = Process.GetCurrentProcess();

            try
            {
                if (VisualStudioAttacher.GetAttachedVisualStudio(vs_current) is (Process vs_proc, _DTE vs_inst))
                    using (vs_proc)
                    {
                        VisualStudioAttacher.AttachVisualStudioToProcess(vs_inst, ServerProcess);

                        DebugPrint("debug.external.vsdbg_attached");

                        return;
                    }
            }
            catch
            {
                DebugPrint("debug.external.vsdbg_error");
            }
#endif
        }

        private async Task DebugMonitorTask(string debug_channel_name)
        {
            using NamedPipeServerStream _debug_server = new NamedPipeServerStream(debug_channel_name);

            await _debug_server.WaitForConnectionAsync();

            using BinaryReader _debug_reader = new BinaryReader(_debug_server);

            while (!ServerProcess.HasExited)
                Printer.Print(ChannelName + "-Provider", _debug_reader.ReadString());
        }

        public void DebugPrint(string key, params object?[] args) => Printer.Print(ChannelName + "-Connector", UILanguage[key, args]);

        protected abstract void BeforeShutdown();

        public void Stop(bool force)
        {
            try
            {
                if (!ServerProcess.HasExited)
                {
                    try
                    {
                        BeforeShutdown();
                    }
                    catch
                    {
                    }

                    DataWriter.Flush();
                    _client.Flush();

                    if (force)
                        ServerProcess.WaitForExit(1000);
                    else
                        ServerProcess.WaitForExit();

                    DataWriter.Close();
                    DataReader.Close();
                    _client.Close();

                    if (force)
                        ServerProcess.Kill();

                    _debug_monitor.Wait();
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            DebugPrint("debug.external.disposed", PipeName, ServerProcess.Id);

            Stop(false);

            ServerProcess.Dispose();
            DataWriter.Dispose();
            DataReader.Dispose();
            _client.Dispose();
            _debug_monitor.Dispose();
        }
    }

#if DEBUG
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
