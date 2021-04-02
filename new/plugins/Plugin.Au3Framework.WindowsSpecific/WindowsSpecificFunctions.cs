using System.Windows.Forms;
using System.IO;
using System;

using Unknown6656.Mathematics;
using Unknown6656.Common;

using Unknown6656.AutoIt3.Extensibility;
using Unknown6656.AutoIt3.Runtime.Native;
using Unknown6656.AutoIt3.Runtime;

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

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
        }

        private static FunctionReturnValue GUI(CallFrame frame, Variant[] args)
        {
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
