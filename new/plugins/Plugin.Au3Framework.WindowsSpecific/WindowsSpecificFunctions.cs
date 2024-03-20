using System.Windows.Forms;
using System.IO;
using System;
using System.Threading;

using Unknown6656.Mathematics;
using Unknown6656.Generics;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;
using System.Drawing;
using static Microsoft.FSharp.Core.ByRefKinds;
using Microsoft.FSharp.Core;
using System.Diagnostics.Eventing.Reader;
//using Plugin.Au3Framework.WindowsSpecific;

[assembly: AutoIt3Plugin]


namespace Unknown6656.AutoIt3.Extensibility.Plugins.Au3Framework
{
    public sealed class WindowsSpecificFunctions
        : AbstractFunctionProvider
    {
        public WindowsSpecificFunctions(Interpreter interpreter)
            : base(interpreter)
        {
            RegisterFunction(nameof(ClipGet), 0, ClipGet, OS.Windows);
            RegisterFunction(nameof(ClipPut), 1, ClipPut);
            RegisterFunction(nameof(FileOpenDialog), 3, 6, FileOpenDialog, Variant.Zero, Variant.Default, Variant.Default);
            RegisterFunction(nameof(FileSaveDialog), 3, 6, FileSaveDialog, Variant.Zero, Variant.Default, Variant.Default);
            RegisterFunction(nameof(FileSelectFolder), 2, 5, FileSelectFolder, Variant.Zero, Variant.Default, Variant.Default);



            RegisterFunction(nameof(GUICreate), 1, 8, GUICreate, Variant.Default, Variant.Default, -1, -1, -1, -1, Variant.Zero);

            RegisterFunction(nameof(MouseGetPos), 0, 1, MouseGetPos, OS.Windows);
            RegisterFunction(nameof(MouseMove), 2, 3, MouseMove, OS.Windows);
            RegisterFunction(nameof(MouseClick), 1, 5, MouseClick, OS.Windows);
            RegisterFunction(nameof(MouseDown), 1, MouseDown, OS.Windows);
            RegisterFunction(nameof(MouseUp), 1, MouseUp, OS.Windows);
        }

        private static FunctionReturnValue ClipGet(CallFrame frame, Variant[] args)
        {
            try
            {
                string value = Clipboard.GetText();

                if (string.IsNullOrEmpty(value))
                    return FunctionReturnValue.Error(1);
                else
                    return (Variant)value;
            }
            catch
            {
                return FunctionReturnValue.Error(Clipboard.ContainsText() ? 3 : 2);
            }
        }

        private static FunctionReturnValue ClipPut(CallFrame frame, Variant[] args)
        {
            try
            {
                return NativeInterop.DoPlatformDependent(delegate
                {
                    string value = args[0].ToString();

                    if (string.IsNullOrEmpty(value))
                        Clipboard.Clear();
                    else
                        Clipboard.SetText(value);

                    return Variant.True;
                }, () => Variant.FromBoolean(NativeInterop.Exec($"echo \"{args[0]}\" | pbcopy").code == 0));
            }
            catch
            {
                return Variant.False;
            }
        }

        private static FunctionReturnValue FileSelectFolder(CallFrame frame, Variant[] args)
        {
            try
            {
                int options = (int)args[2];
                using FolderBrowserDialog ofd = new()
                {
                    Description = args[0].ToString(),
                    SelectedPath = args[3].ToString(),
                    RootFolder = Environment.SpecialFolder.MyComputer,
                    ShowNewFolderButton = options.HasFlag(1),
                    AutoUpgradeEnabled = true,
                };
                DialogResult result;

                if (args[5].IsDefault)
                    result = ofd.ShowDialog();
                else
                    result = ofd.ShowDialog(WindowWrapper.FromHWND(args[5]));

                if (result is DialogResult.OK)
                    return Variant.FromString(ofd.SelectedPath);
            }
            catch
            {
            }

            return FunctionReturnValue.Error("", 1, Variant.Null);
        }

        private static FunctionReturnValue FileSaveDialog(CallFrame frame, Variant[] args)
        {
            try
            {
                int options = (int)args[3];
                using SaveFileDialog ofd = new()
                {
                    Title = args[0].ToString(),
                    InitialDirectory = args[1].ToString(),
                    Filter = args[2].ToString(),
                    FileName = args[4].ToString(),
                    CheckFileExists = options.HasFlag(1),
                    CheckPathExists = options.HasFlag(2),
                    OverwritePrompt = options.HasFlag(16),
                };
                DialogResult result;

                if (args[5].IsDefault)
                    result = ofd.ShowDialog();
                else
                    result = ofd.ShowDialog(WindowWrapper.FromHWND(args[5]));

                return FunctionReturnValue.Success(
                    ofd.FileNames is { Length: > 1 } names ? names.StringJoin("|") : ofd.FileName,
                    (int)result
                );
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        private static FunctionReturnValue FileOpenDialog(CallFrame frame, Variant[] args)
        {
            try
            {
                int options = (int)args[3];
                using OpenFileDialog ofd = new()
                {
                    Title = args[0].ToString(),
                    InitialDirectory = args[1].ToString(),
                    Filter = args[2].ToString(),
                    FileName = args[4].ToString(),
                    CheckFileExists = options.HasFlag(1),
                    CheckPathExists = options.HasFlag(2),
                    Multiselect = options.HasFlag(4),
                };
                DialogResult result;

                if (args[5].IsDefault)
                    result = ofd.ShowDialog();
                else
                    result = ofd.ShowDialog(WindowWrapper.FromHWND(args[5]));

                if (result is DialogResult.OK && options.HasFlag(8) && !File.Exists(ofd.FileName))
                    using (FileStream fs = File.Create(ofd.FileName))
                        fs.Close();

                return FunctionReturnValue.Success(
                    ofd.FileNames is { Length: > 1 } names ? names.StringJoin("|") : ofd.FileName,
                    (int)result
                );
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        private static FunctionReturnValue GUICreate(CallFrame frame, Variant[] args)
        {
            try
            {
                Form window = new()
                {
                    Text = args[0].ToString(),
                };

                if ((int)args[1] is int width and > 0)
                    window.Width = width;

                if ((int)args[2] is int height and > 0)
                    window.Height = height;

                if ((int)args[3] is int left and > 0)
                    window.Width = left;

                if ((int)args[4] is int top and > 0)
                    window.Height = top;

                if ((int)args[5] >= 0)
                    NativeInterop.SetWindowLongPtr(window.Handle, -16, (nint)args[5]); // style

                if ((int)args[6] >= 0)
                    NativeInterop.SetWindowLongPtr(window.Handle, -20, (nint)args[6]); // ex-style

                if ((int)args[7] >= 0)
                    NativeInterop.SetParent(window.Handle, (nint)args[7]);

                return Variant.FromHandle(window.Handle);
            }
            catch
            {
                return FunctionReturnValue.Error(1);
            }
        }

        //private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        //{
        //}

        // TODO relative coords
        private static FunctionReturnValue MouseGetPos(CallFrame frame, Variant[] args)
        {
            Point pos = new Point(Cursor.Position.X, Cursor.Position.Y);
            if (args[0].IsNull)
                return Variant.FromArray(frame.Interpreter, pos.X, pos.Y);
            else if (args[0] == 0)
                return Variant.FromNumber(pos.X);
            else if (args[0] == 1)
                return Variant.FromNumber(pos.Y);
            else
                return FunctionReturnValue.Error(1);
        }

        private static FunctionReturnValue MouseMove(CallFrame frame, Variant[] args)
        {
            // TODO if args.length is 3, move mouse at the given speed.  Create thread?
            if (args[0].Type is VariantType.Number && args[1].Type is VariantType.Number)
            {
                Cursor.Position = new Point((int)args[0].ToNumber(), (int)args[1].ToNumber());
                /*
                NativeInterop.POINT p = new NativeInterop.POINT((int)args[0].ToNumber(), (int)args[1].ToNumber());
                IntPtr desktopWinHandle = NativeInterop.GetDesktopWindow();
                NativeInterop.ClientToScreen(desktopWinHandle, ref p);
                NativeInterop.SetCursorPos(p.x, p.y);
                */
                return FunctionReturnValue.Success(1);
            }
            return FunctionReturnValue.Error(1);
        }

        private const UInt32 MOUSEEVENTF_MOVE = 0x0001;
        private const UInt32 MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const UInt32 MOUSEEVENTF_LEFTUP = 0x0004;
        private const UInt32 MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const UInt32 MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const UInt32 MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const UInt32 MOUSEEVENTF_RIGHTUP = 0x0010;
        private const UInt32 MOUSEEVENTF_ABSOLUTE = 0x8000;

        // TODO implement MouseClickDownDelay (Option) to set time between down and up
        private static FunctionReturnValue MouseClick(CallFrame frame, Variant[] args)
        {
            // TODO if args.length is 5, specify speed?  Of movement?
            UInt32 button;// = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP;
            uint x;
            uint y;
            int repetitions;

            if (string.Equals(args[0].ToString(), "middle", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP;
            }
            else if (string.Equals(args[0].ToString(), "right", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP;
            }
            else if (string.Equals(args[0].ToString(), "left", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP;
            }
            else {
                return FunctionReturnValue.Error(1);
            }


            if (args[1].IsNull)
                x = (uint)Cursor.Position.X;
            else
                x = (uint)(args[1].ToNumber());

            if (args[2].IsNull)
                y = (uint)Cursor.Position.Y;
            else
                y = (uint)(args[2].ToNumber());

            if (args[3].IsNull)
                repetitions = 1;
            else
                repetitions = (int)(args[3].ToNumber());

            Cursor.Position = new Point((int)x, (int)y);
            for (int t = 0; t < repetitions; t++)
            {
                // MOUSEEVENTF_ABSOLUTE needs to do the 65560/screenwidth * x thing
                // without, MOUSEEVENT_MOVE seems to do in pixels (relative)
                NativeInterop.mouse_event(button, x, y, 0, 0);
                Thread.Sleep(100);
            }
            return FunctionReturnValue.Success(1);
        }

        private static FunctionReturnValue MouseDown(CallFrame frame, Variant[] args)
        {
            UInt32 button;
            uint x = (uint)Cursor.Position.X;
            uint y = (uint)Cursor.Position.Y;

            if (string.Equals(args[0].ToString(), "middle", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_MIDDLEDOWN;
            }
            else if (string.Equals(args[0].ToString(), "right", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_RIGHTDOWN;
            }
            else if (string.Equals(args[0].ToString(), "left", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_LEFTDOWN;
            }
            else
            {
                return FunctionReturnValue.Error(1);
            }


            NativeInterop.mouse_event(button, x, y, 0, 0);
            return FunctionReturnValue.Success(1);
        }

        private static FunctionReturnValue MouseUp(CallFrame frame, Variant[] args)
        {
            UInt32 button;
            uint x = (uint)Cursor.Position.X;
            uint y = (uint)Cursor.Position.Y;

            if (string.Equals(args[0].ToString(), "middle", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_MIDDLEUP;
            }
            else if (string.Equals(args[0].ToString(), "right", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_RIGHTUP;
            }
            else if (string.Equals(args[0].ToString(), "left", StringComparison.InvariantCultureIgnoreCase))
            {
                button = MOUSEEVENTF_LEFTUP;
            }
            else
            {
                return FunctionReturnValue.Error(1);
            }


            NativeInterop.mouse_event(button, x, y, 0, 0);
            return FunctionReturnValue.Success(1);
        }

        public sealed class WindowWrapper
            : IWin32Window
        {
            public nint Handle { get; }


            public WindowWrapper(nint handle) => Handle = handle;

            public static WindowWrapper FromHWND(Variant hwnd) => new((nint)hwnd);
        }
    }
}
