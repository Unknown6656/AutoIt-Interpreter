using System;

using Unknown6656.AutoIt3.Runtime.ExternalServices;
using Unknown6656.AutoIt3.Localization;
using Unknown6656.AutoIt3.CLI;

namespace Unknown6656.AutoIt3.Runtime
{
    public sealed class GUIConnector
        : ExternalServiceConnector<GUIConnector>
    {
        public override string ChannelName { get; } = "GUI";


        public GUIConnector(Interpreter interpreter)
            : base(MainProgram.GUI_CONNECTOR, true, interpreter, interpreter.CurrentUILanguage)
        {
        }

        protected override void BeforeShutdown()
        {
        }
    }
}
