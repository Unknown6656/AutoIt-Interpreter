using System;

using Unknown6656.AutoIt3.Runtime.ExternalServices;

namespace Unknown6656.AutoIt3.WinAPI.Server
{
    public sealed class WinAPIServer
        : ExternalServiceProvider<WinAPIServer>
    {
        protected override void OnStartup(string[] argv)
        {
        }

        protected override void MainLoop(ref bool shutdown)
        {
        }

        protected override void OnShutdown(bool external_request)
        {
        }

        public static int Main(string[] argv) => Run<WinAPIServer>(argv);
    }
}
