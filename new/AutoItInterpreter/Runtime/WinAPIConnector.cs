using System;

using Unknown6656.AutoIt3.Runtime.ExternalServices;
using Unknown6656.AutoIt3.Localization;
using Unknown6656.AutoIt3.CLI;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class WinAPIConnector
        : ExternalServiceConnector<WinAPIConnector>
    {
        public override string ChannelName { get; } = "Win32";


        public WinAPIConnector(Interpreter interpreter)
            : base(MainProgram.WINAPI_CONNECTOR, false, interpreter, interpreter.CurrentUILanguage)
        {
        }

        protected override void BeforeShutdown()
        {
        }
    }
}
