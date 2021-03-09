// #define STANDALONE

using System;

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging.Serilog;
using Avalonia.Logging;
using Avalonia;

using Unknown6656.AutoIt3.Runtime.ExternalServices;

namespace Unknown6656.AutoIt3.GUI
{
    public sealed class GUIServer
        : ExternalServiceProvider<GUIServer>
    {
        private App? _app = null;


        protected override void OnStartup(string[] args)
        {
            AppBuilder builder = AppBuilder.Configure<App>();

            builder.UsePlatformDetect();
            builder.LogToDebug(LogEventLevel.Debug);
            // builder.StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);

            _app = builder.Instance as App;
        }

        protected override void MainLoop(ref bool shutdown)
        {
            // shutdown = true;
        }

        protected override void OnShutdown(bool external_request)
        {
        }
#if STANDALONE
        public static void Main(string[] args)
        {
            GUIServer server = new();
            bool exit = false;

            server.OnStartup(args);

            while (!exit)
                server.MainLoop(ref exit);

            server.OnShutdown(false);
        }
#else
        public static int Main(string[] args) => Run<GUIServer>(args);
#endif
        // public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
    }
}
