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
        protected override void OnStartup(string[] args)
        {
            AppBuilder builder = BuildAvaloniaApp();

            builder.StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnLastWindowClose);
        }

        protected override void MainLoop(ref bool shutdown)
        {

        }

        protected override void OnShutdown(bool external_request)
        {

        }

        public static int Main(string[] args) => Run<GUIServer>(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
                                                                 .UsePlatformDetect()
                                                                 .LogToDebug(LogEventLevel.Debug);

    }
}
