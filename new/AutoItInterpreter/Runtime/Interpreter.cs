using System.Collections.Concurrent;
using System.Linq;
using System;

using Unknown6656.AutoIt3.ExpressionParser;
using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.Common;
using System.Diagnostics;

namespace Unknown6656.AutoIt3.Runtime
{
    using static Program;
    using static AST;

    public sealed class Interpreter
        : IDisposable
    {
        private readonly ConcurrentDictionary<AU3Thread, __empty> _threads = new ConcurrentDictionary<AU3Thread, __empty>();
        private readonly object _main_thread_mutex = new object();

        public AU3Thread? MainThread { get; private set; }

        public AU3Thread[] Threads => _threads.Keys.ToArray();

        public VariableScope VariableResolver { get; }

        public CommandLineOptions CommandLineOptions { get; }

        public ReadOnlyIndexer<string, Variant?> Macros { get; }

        public ScriptScanner ScriptScanner { get; }

        public PluginLoader PluginLoader { get; }


        public Interpreter(CommandLineOptions opt)
        {
            CommandLineOptions = opt;
            ScriptScanner = new ScriptScanner(this);
            PluginLoader = new PluginLoader(this, PLUGIN_DIR);

            if (!opt.DontLoadPlugins)
                PluginLoader.LoadPlugins();

            PrintInterpreterMessage(PluginLoader.LoadedPlugins.Count switch {
                0 => CurrentLanguage["general.no_plugins_loaded"],
                int i => CurrentLanguage["general.plugins_loaded", i, PluginLoader.PluginDirectory.FullName],
            });

            ScriptScanner.ScanNativeFunctions();

            VariableResolver = VariableScope.CreateGlobalScope(this);
            VariableResolver.CreateVariable(SourceLocation.Unknown, VARIABLE.Discard.Name, false);
            Macros = new ReadOnlyIndexer<string, Variant?>(ResolveMacro);
        }

        public void Dispose()
        {
            foreach (AU3Thread thread in Threads)
            {
                thread.Dispose();
                _threads.TryRemove(thread, out _);
            }
        }

        public AU3Thread CreateNewThread() => new AU3Thread(this);

        internal void AddThread(AU3Thread thread) => _threads.TryAdd(thread, default);

        internal void RemoveThread(AU3Thread thread) => _threads.TryRemove(thread, out _);

        private unsafe Variant? ResolveMacro(string macro)
        {
            switch (macro)
            {
                case "appdatacommondir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                case "appdatadir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                case "autoitexe":
                    return ASM.FullName;
                case "autoitpid":
                    return Process.GetCurrentProcess().Id;
                case "autoitversion":
                    return __module__.InterpreterVersion?.ToString() ?? "0.0.0.0";
                case "autoitx64":
                    return sizeof(void*) > 4;
                case "commonfilesdir":
                    return Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
                case "compiled":
                    return false;

                case "cr":
                    return "\r";
                case "crlf":
                    return "\r\n";
                case "lf":
                    return "\n";



                ///////////////////////////////////// ADDITIONAL MACROS /////////////////////////////////////
                case "esc":
                    return "\x1b";
                case "nul":
                    return "\0";
            }

            /* https://www.autoitscript.com/autoit3/docs/macros.htm

AutoItX64	Returns 1 if the script is running under the native x64 version of AutoIt.
COM_EventObj	Object the COM event is being fired on. Only valid in a COM event function.
ComputerName	Computer's network name.
ComSpec	Value of %COMSPEC%, the SPECified secondary COMmand interpreter; primary for command line uses, e.g. Run(@ComSpec & " /k help | more")
CPUArch	Returns "X86" when the CPU is a 32-bit CPU and "X64" when the CPU is 64-bit.
DesktopCommonDir	Path to Desktop
DesktopDepth	Depth of the primary display in bits per pixel.
DesktopDir	Path to current user's Desktop
DesktopHeight	Height of the primary display in pixels. (Vertical resolution)
DesktopRefresh	Refresh rate of the primary display in hertz.
DesktopWidth	Width of the primary display in pixels. (Horizontal resolution)
DocumentsCommonDir	Path to Documents
error	Status of the error flag. See the function SetError().
exitCode	Exit code as set by Exit statement.
exitMethod	Exit method. See the function OnAutoItExitRegister().
extended	Extended function return - used in certain functions such as StringReplace().
FavoritesCommonDir	Path to Favorites
FavoritesDir	Path to current user's Favorites
GUI_CtrlHandle	Last click GUI Control handle. Only valid in an event Function. See the GUICtrlSetOnEvent() function.
GUI_CtrlId	Last click GUI Control identifier. Only valid in an event Function. See the GUICtrlSetOnEvent() function.
GUI_DragFile	Filename of the file being dropped. Only valid on Drop Event. See the GUISetOnEvent() function.
GUI_DragId	Drag GUI Control identifier. Only valid on Drop Event. See the GUISetOnEvent() function.
GUI_DropId	Drop GUI Control identifier. Only valid on Drop Event. See the GUISetOnEvent() function.
GUI_WinHandle	Last click GUI window handle. Only valid in an event Function. See the GUICtrlSetOnEvent() function.
HomeDrive	Drive letter of drive containing current user's home directory.
HomePath	Directory part of current user's home directory. To get the full path, use in conjunction with @HomeDrive.
HomeShare	Server and share name containing current user's home directory.
HotKeyPressed	Last hotkey pressed. See the HotKeySet() function.
HOUR	Hours value of clock in 24-hour format. Range is 00 to 23
IPAddress1	IP address of first network adapter. Tends to return 127.0.0.1 on some computers.
IPAddress2	IP address of second network adapter. Returns 0.0.0.0 if not applicable.
IPAddress3	IP address of third network adapter. Returns 0.0.0.0 if not applicable.
IPAddress4	IP address of fourth network adapter. Returns 0.0.0.0 if not applicable.
KBLayout	Returns code denoting Keyboard Layout. See Appendix for possible values.
LocalAppDataDir	Path to current user's Local Application Data
LogonDNSDomain	Logon DNS Domain.
LogonDomain	Logon Domain.
LogonServer	Logon server.
MDAY	Current day of month. Range is 01 to 31
MIN	Minutes value of clock. Range is 00 to 59
MON	Current month. Range is 01 to 12
MSEC	Milliseconds value of clock. Range is 000 to 999. The update frequency of this value depends on the timer resolution of the hardware and may not update every millisecond.
MUILang	Returns code denoting Multi Language if available (Vista is OK by default). See Appendix for possible values.
MyDocumentsDir	Path to My Documents target
NumParams	Number of parameters used in calling the user function.
OSArch	Returns one of the following: "X86", "IA64", "X64" - this is the architecture type of the currently running operating system.
OSBuild	Returns the OS build number. For example, Windows 2003 Server returns 3790
OSLang	Returns code denoting OS Language. See Appendix for possible values.
OSServicePack	Service pack info in the form of "Service Pack 3".
OSType	Returns "WIN32_NT" for XP/2003/Vista/2008/Win7/2008R2/Win8/2012/Win8.1/2012R2.
OSVersion	Returns one of the following: "WIN_10", "WIN_81", "WIN_8", "WIN_7", "WIN_VISTA", "WIN_XP", "WIN_XPe",
    for Windows servers: "WIN_2016", "WIN_2012R2", "WIN_2012", "WIN_2008R2", "WIN_2008", "WIN_2003"".
ProgramFilesDir	Path to Program Files folder
ProgramsCommonDir	Path to Start Menu's Programs folder
ProgramsDir	Path to current user's Programs (folder on Start Menu)
ScriptDir	Directory containing the running script. Only includes a trailing backslash when the script is located in the root of a drive.
ScriptFullPath	Equivalent to @ScriptDir & "\" & @ScriptName
ScriptLineNumber	Line number being executed - useful for debug statements (e.g. location of function call). Only significant in uncompiled scripts - note that #include files return their internal line numbering
ScriptName	Filename of the running script.
SEC	Seconds value of clock. Range is 00 to 59
StartMenuCommonDir	Path to Start Menu folder
StartMenuDir	Path to current user's Start Menu
StartupCommonDir	Path to Startup folder
StartupDir	current user's Startup folder
SW_DISABLE	Disables the window.
SW_ENABLE	Enables the window.
SW_HIDE	Hides the window and activates another window.
SW_LOCK	Lock the window to avoid repainting.
SW_MAXIMIZE	Activates the window and displays it as a maximized window.
SW_MINIMIZE	Minimizes the specified window and activates the next top-level window in the Z order.
SW_RESTORE	Activates and displays the window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when restoring a minimized window.
SW_SHOW	Activates the window and displays it in its current size and position.
SW_SHOWDEFAULT	Sets the show state based on the SW_ value specified by the program that started the application.
SW_SHOWMAXIMIZED	Activates the window and displays it as a maximized window.
SW_SHOWMINIMIZED	Activates the window and displays it as a minimized window.
SW_SHOWMINNOACTIVE	Displays the window as a minimized window. This value is similar to @SW_SHOWMINIMIZED, except the window is not activated.
SW_SHOWNA	Displays the window in its current size and position. This value is similar to @SW_SHOW, except the window is not activated.
SW_SHOWNOACTIVATE	Displays a window in its most recent size and position. This value is similar to @SW_SHOWNORMAL, except the window is not activated.
SW_SHOWNORMAL	Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.
SW_UNLOCK	Unlock window to allow painting.
SystemDir	Path to the Windows' System (or System32) folder.
TAB	Tab character, Chr(9)
TempDir	Path to the temporary files folder.
TRAY_ID	Last clicked item identifier during a TraySetOnEvent() or TrayItemSetOnEvent() action.
TrayIconFlashing	Returns 1 if tray icon is flashing; otherwise, returns 0.
TrayIconVisible	Returns 1 if tray icon is visible; otherwise, returns 0.
UserName	ID of the currently logged on user.
UserProfileDir	Path to current user's Profile folder.
WDAY	Numeric day of week. Range is 1 to 7 which corresponds to Sunday through Saturday.
WindowsDir	Path to Windows folder
WorkingDir	Current/active working directory. Only includes a trailing backslash when the script is located in the root of a drive.
YDAY	Current day of year. Range is 001 to 366 (or 001 to 365 if not a leap year)
YEAR	Current four-digit year
             */
        }

        public InterpreterError? Run(ScriptFunction entry_point)
        {
            try
            {
                using AU3Thread thread = CreateNewThread();

                lock (_main_thread_mutex)
                    MainThread = thread;

                return thread.Start(entry_point);
            }
            finally
            {
                lock (_main_thread_mutex)
                    MainThread = null;
            }
        }

        public InterpreterError? Run(ScannedScript script) => Run(script.MainFunction);

        public InterpreterError? Run(string path) => ScriptScanner.ScanScriptFile(SourceLocation.Unknown, path, ScriptScanningOptions.IncludeOnce | ScriptScanningOptions.RelativePath)
                                                                  .Match(Generics.id, Run);

        public static InterpreterError? Run(CommandLineOptions opt)
        {
            using Interpreter interpreter = new Interpreter(opt);

            return interpreter.Run(opt.FilePath);
        }
    }

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
        public SourceLocation? Location { get; }
        public string Message { get; }


        public InterpreterError(SourceLocation? location, string message)
        {
            Location = location;
            Message = message;
        }

        public static InterpreterError WellKnown(SourceLocation? loc, string key, params object?[] args) => new InterpreterError(loc, Program.CurrentLanguage[key, args]);
    }
}

